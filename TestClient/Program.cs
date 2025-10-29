using System;
using System.Diagnostics;
using System.Net.Sockets;
using System.Numerics;
using System.Runtime.Intrinsics.Arm;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace FluffyByte.TestClient;

class Program
{
    private static TcpClient? _tcpClient;
    private static UdpClient? _udpClient;
    private static NetworkStream? _tcpStream;
    private static string _myGuid = "none";

    // Must match server's secret
    private const string CLIENT_SECRET = "FluffyByte_OPUL_SecretKey_2025";

    static async Task Main()
    {
        Console.WriteLine("=== FluffyByte.OPUL Test Client ===");
        Console.WriteLine("Connecting to server...\n");

        try
        {
            await ConnectToServer();

            // Keep alive
            Console.WriteLine("\nPress any key to disconnect...");
            Console.ReadKey();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: {ex.Message}");
        }
        finally
        {
            _tcpClient?.Close();
            _udpClient?.Close();
        }
    }

    static async Task ConnectToServer()
    {
        // Connect TCP
        _tcpClient = new TcpClient();
        await _tcpClient.ConnectAsync("10.0.0.84", 9997);
        _tcpStream = _tcpClient.GetStream();

        Console.WriteLine("✓ TCP Connected");

        // Initialize UDP
        _udpClient = new UdpClient();

        // Wait for handshake
        await WaitForHandshake();

        // Wait for authentication challenge
        await HandleAuthentication();

        // Start listening tasks
        _ = Task.Run(ReceiveTcpMessages);
        _ = Task.Run(ReceiveUdpPackets);
    }

    static async Task WaitForHandshake()
    {
        if (_tcpStream == null) return;
        if (_udpClient == null || _tcpClient == null) return;

        byte[] buffer = new byte[1024];
        int bytesRead = await _tcpStream.ReadAsync(buffer);
        string message = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();

        Console.WriteLine($"← TCP: {message}");

        // Parse: "HANDSHAKE|<Guid>|<IP>|<Port>"
        if (message.StartsWith("HANDSHAKE|"))
        {
            string[] parts = message.Split('|');
            _myGuid = parts[1];
            string serverIp = parts[2];
            int udpPort = int.Parse(parts[3]);

            Console.WriteLine($"  My GUID: {_myGuid}");

            // Send UDP handshake response
            string udpHandshake = $"HANDSHAKE|{_myGuid}";
            byte[] data = Encoding.UTF8.GetBytes(udpHandshake);
            await _udpClient.SendAsync(data, data.Length, serverIp, udpPort);

            Console.WriteLine($"→ UDP: Sent handshake to {serverIp}:{udpPort}");

            // Wait for acknowledgment
            var ackResult = await _udpClient.ReceiveAsync();
            string ack = Encoding.UTF8.GetString(ackResult.Buffer);
            Console.WriteLine($"← UDP: {ack}");

            if (ack == "HANDSHAKE_ACK")
            {
                Console.WriteLine("✓ UDP Handshake Complete\n");
            }
        }
    }
    static async Task HandleAuthentication()
    {
        if (_tcpStream == null) return;

        Console.WriteLine("Waiting for authentication challenge...");

        byte[] buffer = new byte[1024];

        // Keep reading until we get the AUTH_CHALLENGE
        while (true)
        {
            int bytesRead = await _tcpStream.ReadAsync(buffer);
            if (bytesRead == 0)
            {
                Console.WriteLine("❌ Connection closed before authentication");
                return;
            }

            string message = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();

            // Skip empty or junk messages
            if (string.IsNullOrWhiteSpace(message))
                continue;

            Console.WriteLine($"← TCP: {message}");

            if (message.StartsWith("AUTH_CHALLENGE|"))
            {
                string challenge = message.Split('|')[1];
                Console.WriteLine($"  Challenge: {challenge}");

                // Compute response using HMACSHA256
                string response = ComputeAuthResponse(challenge);
                Console.WriteLine($"  Computed Response: {response}");

                // Send response as TEXT (not binary)
                string responseMessage = $"AUTH_RESPONSE|{response}\n";
                byte[] data = Encoding.UTF8.GetBytes(responseMessage);
                await _tcpStream.WriteAsync(data);
                await _tcpStream.FlushAsync();

                Console.WriteLine($"→ TCP: Sent auth response");

                // Wait for auth result
                bytesRead = await _tcpStream.ReadAsync(buffer);
                string result = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();

                Console.WriteLine($"← TCP: {result}");

                if (result == "AUTH_SUCCESS")
                {
                    Console.WriteLine("✓ Authentication Successful!\n");
                }
                else
                {
                    Console.WriteLine("❌ Authentication Failed\n");
                }

                break; // Exit the loop after handling auth
            }
            else
            {
                Console.WriteLine($"  (Ignoring non-challenge message)");
            }
        }
    }


    static string ComputeAuthResponse(string challenge)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(CLIENT_SECRET));
        byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(challenge));
        return Convert.ToBase64String(hash);
    }

    /// <summary>
    /// Reads a binary packet from TCP stream (length-prefixed).
    /// Matches server's WriteBinary format: [4 bytes length][data]
    /// </summary>
    static async Task<byte[]?> ReadBinaryAsync()
    {
        if (_tcpStream == null) return null;

        try
        {
            // Read length prefix (4 bytes)
            byte[] lengthBuffer = new byte[4];
            int bytesRead = await _tcpStream.ReadAsync(lengthBuffer.AsMemory(0, 4));

            if (bytesRead != 4)
            {
                Console.WriteLine("Failed to read length prefix");
                return null;
            }

            int length = BitConverter.ToInt32(lengthBuffer, 0);

            if (length <= 0 || length > 10 * 1024 * 1024) // 10 MB max
            {
                Console.WriteLine($"Invalid binary length: {length}");
                return null;
            }

            // Read actual data
            byte[] data = new byte[length];
            int totalRead = 0;

            while (totalRead < length)
            {
                int read = await _tcpStream.ReadAsync(data.AsMemory(totalRead, length - totalRead));
                if (read == 0)
                {
                    Console.WriteLine("Connection closed while reading binary data");
                    return null;
                }
                totalRead += read;
            }

            return data;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ReadBinary Error: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Writes binary data to TCP stream (length-prefixed).
    /// Matches server's ReadyBinary format: [4 bytes length][data]
    /// </summary>
    static async Task WriteBinaryAsync(byte[] data)
    {
        if (_tcpStream == null) return;

        try
        {
            // Write length prefix (4 bytes)
            byte[] lengthBytes = BitConverter.GetBytes(data.Length);
            await _tcpStream.WriteAsync(lengthBytes.AsMemory(0, 4));

            // Write actual data
            await _tcpStream.WriteAsync(data);
            await _tcpStream.FlushAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"WriteBinary Error: {ex.Message}");
        }
    }

    static async Task ReceiveTcpMessages()
    {
        byte[] buffer = new byte[1024];

        if (_tcpStream == null) return;
        if (_udpClient == null || _tcpClient == null) return;

        try
        {
            while (_tcpClient.Connected)
            {
                int bytesRead = await _tcpStream.ReadAsync(buffer);
                if (bytesRead == 0) break;

                string message = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();
                Console.WriteLine($"← TCP: {message}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"TCP Error: {ex.Message}");
        }
    }

    static async Task ReceiveUdpPackets()
    {
        if (_tcpStream == null) return;
        if (_udpClient == null || _tcpClient == null) return;

        try
        {
            while (true)
            {
                var result = await _udpClient.ReceiveAsync();

                // Extract sequence number
                uint seqNum = BitConverter.ToUInt32(result.Buffer, 0);

                // Extract payload
                byte[] payload = new byte[result.Buffer.Length - 4];
                Array.Copy(result.Buffer, 4, payload, 0, payload.Length);

                string message = Encoding.UTF8.GetString(payload);
                Console.WriteLine($"← UDP [seq:{seqNum}]: {message}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"UDP Error: {ex.Message}");
        }
    }
}