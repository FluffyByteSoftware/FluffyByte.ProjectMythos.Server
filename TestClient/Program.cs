using System;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace FluffyByte.TestClient;

class Program
{
    private static TcpClient? _tcpClient;
    private static UdpClient? _udpClient;
    private static NetworkStream? _tcpStream;
    private static string _myGuid = "none";

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