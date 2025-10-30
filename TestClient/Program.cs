using System;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Net;

namespace FluffyByte.TestClient;

class Program
{
    private static TcpClient? _tcpClient;
    private static UdpClient? _udpClient;
    private static NetworkStream? _tcpStream;
    private static string _myGuid = "none";

    private const string CLIENT_SECRET = "FluffyByte_OPUL_SecretKey_2025";
    private static readonly ConcurrentQueue<long> _tickTimes = new();

    static async Task Main()
    {
        Console.WriteLine("=== FluffyByte.OPUL Test Client ===");
        Console.WriteLine("Connecting to server...\n");

        try
        {
            await ConnectToServer();

            // Background listeners
            _ = Task.Run(ReceiveTcpMessages);
            _ = Task.Run(ReceiveUdpPackets);
            _ = Task.Run(ReportTickRate);

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
        _tcpClient = new TcpClient();
        await _tcpClient.ConnectAsync("10.0.0.84", 9997);
        _tcpStream = _tcpClient.GetStream();
        Console.WriteLine("✓ TCP Connected");

        _udpClient = new UdpClient();
        await WaitForHandshake();
        await HandleAuthentication();
    }

    static async Task WaitForHandshake()
    {
        if (_tcpStream == null || _udpClient == null) return;

        byte[] buffer = new byte[1024];
        int bytesRead = await _tcpStream.ReadAsync(buffer);
        string message = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();

        Console.WriteLine($"← TCP: {message}");

        if (!message.StartsWith("HANDSHAKE|")) return;

        string[] parts = message.Split('|');
        _myGuid = parts[1];
        string serverIp = parts[2];
        int udpPort = int.Parse(parts[3]);

        Console.WriteLine($"  My GUID: {_myGuid}");

        // Send UDP handshake
        string udpHandshake = $"HANDSHAKE|{_myGuid}";
        byte[] data = Encoding.UTF8.GetBytes(udpHandshake);
        await _udpClient.SendAsync(data, data.Length, serverIp, udpPort);
        Console.WriteLine($"→ UDP: Sent handshake to {serverIp}:{udpPort}");

        // Wait for acknowledgment
        var ackResult = await _udpClient.ReceiveAsync();
        string ack = Encoding.UTF8.GetString(ackResult.Buffer);
        Console.WriteLine($"← UDP: {ack}");

        if (ack == "HANDSHAKE_ACK")
            Console.WriteLine("✓ UDP Handshake Complete\n");
        else
            Console.WriteLine("❌ UDP Handshake Failed\n");
    }

    static async Task HandleAuthentication()
    {
        if (_tcpStream == null) return;

        Console.WriteLine("Waiting for authentication challenge...");
        byte[] buffer = new byte[1024];

        while (true)
        {
            int bytesRead = await _tcpStream.ReadAsync(buffer);
            if (bytesRead == 0)
            {
                Console.WriteLine("❌ Connection closed before authentication");
                return;
            }

            string message = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();
            if (string.IsNullOrWhiteSpace(message)) continue;

            Console.WriteLine($"← TCP: {message}");

            if (message.StartsWith("AUTH_CHALLENGE|"))
            {
                string challenge = message["AUTH_CHALLENGE|".Length..];
                Console.WriteLine($"  Challenge: {challenge}");

                string response = ComputeAuthResponse(challenge);
                Console.WriteLine($"  HMAC: {response}");

                string responseMessage = $"AUTH_RESPONSE|{response}\n";
                byte[] data = Encoding.UTF8.GetBytes(responseMessage);
                await _tcpStream.WriteAsync(data);
                await _tcpStream.FlushAsync();
                Console.WriteLine("→ TCP: Sent auth response");

                bytesRead = await _tcpStream.ReadAsync(buffer);
                string result = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();
                Console.WriteLine($"← TCP: {result}");

                if (result == "AUTH_SUCCESS")
                    Console.WriteLine("✓ Authentication Successful!\n");
                else
                    Console.WriteLine("❌ Authentication Failed\n");

                break;
            }
        }
    }

    static string ComputeAuthResponse(string challenge)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(CLIENT_SECRET));
        byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(challenge));
        return Convert.ToBase64String(hash);
    }

    static async Task ReceiveTcpMessages()
    {
        if (_tcpStream == null) return;
        byte[] buffer = new byte[1024];

        try
        {
            while (_tcpClient?.Connected == true)
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
        if (_udpClient == null) return;

        try
        {
            while (true)
            {
                var result = await _udpClient.ReceiveAsync();
                var buffer = result.Buffer;

                if (buffer.Length < 21)
                    continue;

                byte packetType = buffer[0];
                if (packetType != 1)
                    continue; // Not a tick packet

                // Ensure consistent little-endian decoding
                if (!BitConverter.IsLittleEndian)
                {
                    Array.Reverse(buffer, 1, 4);
                    Array.Reverse(buffer, 5, 8);
                    Array.Reverse(buffer, 13, 8);
                }

                int tickType = BitConverter.ToInt32(buffer, 1);
                ulong tickCount = BitConverter.ToUInt64(buffer, 5);
                long timestamp = BitConverter.ToInt64(buffer, 13);

                // Guard against bad timestamps
                if (timestamp < 0 || timestamp > DateTimeOffset.MaxValue.ToUnixTimeMilliseconds())
                    continue;

                DateTime tickTime = DateTimeOffset.FromUnixTimeMilliseconds(timestamp).LocalDateTime;

                _tickTimes.Enqueue(timestamp);
                Console.WriteLine($"← UDP TICK | Type:{tickType} | Count:{tickCount} | Time:{tickTime:T}");
            }
        }
        catch (SocketException ex)
        {
            Console.WriteLine($"UDP Socket closed: {ex.SocketErrorCode}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"UDP Error: {ex.Message}");
        }
    }

    static async Task ReportTickRate()
    {
        while (true)
        {
            await Task.Delay(5000);
            if (_tickTimes.IsEmpty) continue;

            long[] ticks = [.. _tickTimes];
            _tickTimes.Clear();

            Console.WriteLine($"[Tick Summary] Received {ticks.Length} ticks in last 5s");
        }
    }
}
