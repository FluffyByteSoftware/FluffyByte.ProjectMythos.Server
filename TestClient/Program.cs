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
    private static IPEndPoint? _serverUdpEndpoint;

    private const string CLIENT_SECRET = "FluffyByte_OPUL_SecretKey_2025";
    private static readonly ConcurrentQueue<long> _tickTimes = new();

    static async Task Main()
    {
        Console.WriteLine("=== FluffyByte.ProjectMythos Test Client (Debug Version) ===");
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
            Console.WriteLine($"Stack: {ex.StackTrace}");
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

        // FIX: Explicitly bind UDP client to a local port
        // This ensures the socket is ready to receive
        _udpClient = new UdpClient(0); // 0 = bind to any available port

        // Log the local endpoint we're bound to
        if(_udpClient.Client.LocalEndPoint == null)
        {
            throw new Exception("Failed to bind UDP client to a local endpoint.");
        }

        var localEp = (IPEndPoint)_udpClient.Client.LocalEndPoint;
        Console.WriteLine($"✓ UDP Client bound to local port: {localEp.Port}");

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
        Console.WriteLine($"  Server UDP: {serverIp}:{udpPort}");

        // Store server endpoint for future reference
        _serverUdpEndpoint = new IPEndPoint(IPAddress.Parse(serverIp), udpPort);

        // Send UDP handshake
        string udpHandshake = $"HANDSHAKE|{_myGuid}";
        byte[] data = Encoding.UTF8.GetBytes(udpHandshake);
        await _udpClient.SendAsync(data, data.Length, serverIp, udpPort);

        if(_udpClient.Client.LocalEndPoint == null)
        {
            throw new Exception("UDP client local endpoint is null after sending handshake.");
        }

        var localEp = (IPEndPoint)_udpClient.Client.LocalEndPoint;
        Console.WriteLine($"→ UDP: Sent handshake from {localEp.Address}:{localEp.Port} to {serverIp}:{udpPort}");

        // Wait for acknowledgment with timeout
        var receiveTask = _udpClient.ReceiveAsync();
        var timeoutTask = Task.Delay(5000);

        var completedTask = await Task.WhenAny(receiveTask, timeoutTask);

        if (completedTask == timeoutTask)
        {
            Console.WriteLine("❌ UDP Handshake timeout - no ACK received after 5 seconds");
            Console.WriteLine("   Server may not have our UDP endpoint or firewall is blocking");
            return;
        }

        var ackResult = receiveTask.Result;
        string ack = Encoding.UTF8.GetString(ackResult.Buffer);
        Console.WriteLine($"← UDP: {ack} from {ackResult.RemoteEndPoint}");

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

        Console.WriteLine($"[UDP Receiver] Listening for packets...");

        try
        {
            int packetCount = 0;
            while (true)
            {
                var result = await _udpClient.ReceiveAsync();
                var buffer = result.Buffer;
                packetCount++;

                Console.WriteLine($"[UDP] Received packet #{packetCount} ({buffer.Length} bytes) from {result.RemoteEndPoint}");

                // Handle handshake ACK separately (if it arrives late)
                if (buffer.Length < 25) // Tick packets are always 25 bytes (4 seq + 21 data)
                {
                    string shortMessage = Encoding.UTF8.GetString(buffer);
                    Console.WriteLine($"  Short message: {shortMessage}");
                    continue;
                }

                // Extract sequence number (first 4 bytes) - part of server's reliability layer
                uint sequenceNumber = BitConverter.ToUInt32(buffer, 0);
                Console.WriteLine($"  Sequence number: {sequenceNumber}");

                // Extract payload (skip first 4 bytes)
                byte[] payload = new byte[buffer.Length - 4];
                Array.Copy(buffer, 4, payload, 0, payload.Length);

                // Now decode the actual tick packet from the payload
                if (payload.Length != 21)
                {
                    Console.WriteLine($"  ⚠ Unexpected payload size: {payload.Length} bytes (expected 21)");
                    continue;
                }

                byte packetType = payload[0];
                Console.WriteLine($"  Packet type byte: {packetType}");

                if (packetType != 1)
                {
                    Console.WriteLine($"  ⚠ Not a tick packet (expected type 1, got {packetType})");
                    continue;
                }

                // Decode the actual tick data from the payload
                try
                {
                    int tickType = BitConverter.ToInt32(payload, 1);
                    ulong tickCount = BitConverter.ToUInt64(payload, 5);
                    long timestamp = BitConverter.ToInt64(payload, 13);

                    Console.WriteLine($"  Decoded: Type={tickType}, Count={tickCount}, Timestamp={timestamp}");


                    // Guard against bad timestamps
                    if (timestamp < 0 || timestamp > DateTimeOffset.MaxValue.ToUnixTimeMilliseconds())
                    {
                        Console.WriteLine($"  ⚠ Invalid timestamp: {timestamp}");
                        continue;
                    }

                    DateTime tickTime = DateTimeOffset.FromUnixTimeMilliseconds(timestamp).LocalDateTime;

                    _tickTimes.Enqueue(timestamp);
                    Console.WriteLine($"← UDP TICK | Type:{tickType} | Count:{tickCount} | Time:{tickTime:T}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  ⚠ Decode error: {ex.Message}");
                }
            }
        }
        catch (SocketException ex)
        {
            Console.WriteLine($"UDP Socket closed: {ex.SocketErrorCode}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"UDP Error: {ex.Message}");
            Console.WriteLine($"Stack: {ex.StackTrace}");
        }
    }

    static async Task ReportTickRate()
    {
        while (true)
        {
            await Task.Delay(5000);
            if (_tickTimes.IsEmpty)
            {
                Console.WriteLine($"[Tick Summary] No ticks received in last 5s ⚠");
                continue;
            }

            long[] ticks = [.. _tickTimes];
            _tickTimes.Clear();

            Console.WriteLine($"[Tick Summary] Received {ticks.Length} ticks in last 5s ✓");
        }
    }
}