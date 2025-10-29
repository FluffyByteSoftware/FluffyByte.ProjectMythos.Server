using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using FluffyByte.ProjectMythos.Server.Core.IO.Debug;
using FluffyByte.ProjectMythos.Server.Core.IO.Networking.FluffyClient;

namespace FluffyByte.ProjectMythos.Server.Core.IO.Networking;

/// <summary>
/// Sentinel handles monitoring for new network connections and manages the TCP/UDP handshake process.
/// </summary>
public class Sentinel : CoreProcessBase
{
    /// <summary>
    /// Sentinel process name.
    /// </summary>
    public override string Name => "Sentinel";

    /// <summary>
    /// The default TCP port number used by the host for network communication.
    /// </summary>
    public const int HOST_TCP_PORT = 9997;

    /// <summary>
    /// The default UDP port number used by the host for communication.
    /// </summary>
    public const int HOST_UDP_PORT = 9998;

    /// <summary>
    /// Represents the default TCP address of the host.
    /// </summary>
    public const string HOST_TCP_ADDRESS = "10.0.0.84";

    /// <summary>
    /// Represents the maximum number of clients that can be connected simultaneously.
    /// </summary>
    public const int MAX_CLIENTS = 9;

    /// <summary>
    /// Gets a value indicating whether the system is currently accepting new client connections.
    /// </summary>
    public bool AcceptingNewClients { get; private set; } = true;

    /// <summary>
    /// Gets the instance of the <see cref="Watcher"/> associated with this object.
    /// </summary>
    public Watcher Watcher { get; private set; }

    private TcpListener _listener = new(IPAddress.Any, HOST_TCP_PORT);
    private readonly UdpClient _udpSocket; // Shared UDP socket for handshakes

    /// <summary>
    /// Tracks pending connections waiting for UDP handshake completion.
    /// Key: Handshake GUID, Value: (TcpClient, TaskCompletionSource for UDP endpoint)
    /// </summary>
    private readonly Dictionary<Guid, (TcpClient TcpClient, 
        TaskCompletionSource<IPEndPoint> UdpEndpointSource)> 
        _pendingHandshakes = [];


    /// <summary>
    /// Initializes a new instance of the <see cref="Sentinel"/> class with the specified shutdown token.
    /// </summary>
    /// <remarks>The <see cref="Sentinel"/> class is responsible for monitoring and managing specific
    /// operations.  Upon initialization, it creates a watcher instance and clears any existing lists associated with
    /// it.</remarks>
    /// <param name="shutdownToken">A <see cref="CancellationToken"/> that is used to signal when the sentinel should shut down.</param>
    public Sentinel(CancellationToken shutdownToken) : base(shutdownToken)
    {
        Watcher = new(this);
        Watcher.ClearLists();

        _udpSocket = new(HOST_UDP_PORT);
    }

    /// <summary>
    /// Starts the Sentinel's TCP and UDP listeners.
    /// </summary>
    public override async Task StartAsync()
    {
        AcceptingNewClients = true;

        _listener = new(IPAddress.Parse(HOST_TCP_ADDRESS), HOST_TCP_PORT);
        _listener.Start();
        Scribe.Info($"[{Name}] Listening for TCP connections on {HOST_TCP_ADDRESS}:{HOST_TCP_PORT}");

        Scribe.Info($"[{Name}] UDP socket bound to port {HOST_UDP_PORT}");

        try
        {
            _ = ListenForNewConnection();
            _ = ListenForUdpHandshakes();
        }
        catch (Exception ex)
        {
            Scribe.Error(ex);
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Stops the Sentinel and cleans up resources.
    /// </summary>
    public override async Task StopAsync()
    {
        try
        {
            _listener.Stop();
            _udpSocket?.Close();

            AcceptingNewClients = false;
        }
        catch (Exception ex)
        {
            Scribe.Error(ex);
        }

        await Task.CompletedTask;
    }

    private async Task ListenForNewConnection()
    {
        try
        {
            while (!_shutdownToken.IsCancellationRequested && Watcher.RawTcpClients.Count < MAX_CLIENTS)
            {
                TcpClient tcpClient = await _listener.AcceptTcpClientAsync(_shutdownToken);

                Scribe.Info($"[{Name}] New TCP client connected from {tcpClient.Client.RemoteEndPoint}.");

                Watcher.RegisterRawTcpClient(tcpClient);

                // Perform handshake and create vessel once complete
                _ = Task.Run(() => PerformHandshakeAndCreateVessel(tcpClient), _shutdownToken);
            }
        }
        catch (OperationCanceledException)
        {
            Scribe.Debug($"[{Name}] TCP Listener canceled (shutdown requested)");
        }
        catch (Exception ex)
        {
            Scribe.Error(ex);
        }
        finally
        {
            Scribe.Debug($"[{Name}] No longer accepting new clients.");

            List<Vessel> vessels = [.. Watcher.Vessels];

            foreach (Vessel v in vessels)
            {
                await v.DisconnectAsync();
            }
        }
    }
    /// <summary>
    /// Performs the TCP/UDP handshake and creates a fully-connected Vessel once complete.
    /// </summary>
    private async Task PerformHandshakeAndCreateVessel(TcpClient tcpClient)
    {
        Guid handshakeGuid = Guid.NewGuid();
        TaskCompletionSource<IPEndPoint> udpEndpointSource = new();

        try
        {
            // Register this pending handshake
            _pendingHandshakes[handshakeGuid] = (tcpClient, udpEndpointSource);

            // Send handshake request via TCP
            await SendTcpHandshakeRequest(tcpClient, handshakeGuid);

            // Wait for UDP handshake (with 10-second timeout)
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(10), _shutdownToken);
            var completedTask = await Task.WhenAny(udpEndpointSource.Task, timeoutTask);

            if (completedTask == timeoutTask)
            {
                // Timeout occurred
                Scribe.Warn($"[{Name}] Handshake {handshakeGuid} timed out waiting for UDP response.");
                _pendingHandshakes.Remove(handshakeGuid);
                tcpClient.Close();
                Watcher.UnregisterRawTcpClient(tcpClient);
                return;
            }

            // UDP handshake completed successfully!
            IPEndPoint udpEndpoint = await udpEndpointSource.Task;
            _pendingHandshakes.Remove(handshakeGuid);

            // NOW create the vessel with both TCP and UDP information
            Vessel newVessel = new(tcpClient, udpEndpoint, _udpSocket);

            Watcher.RegisterVessel(newVessel);

            Scribe.Info($"[{Name}] Vessel {newVessel.Id} fully connected (TCP + UDP)");

            bool authenticated = await GateKeeper.AuthenticateVesselAsync(newVessel);

            if (!authenticated)
            {
                Scribe.Warn($"[{Name}] Vessel {newVessel.Id} failed authentication. Disconnecting.");
                Watcher.UnregisterVessel(newVessel);
                return;
            }

            // ✅ NOW send welcome message (after authentication succeeds)
            await newVessel.TcpIO.WriteTextAsync("Connection established. Welcome to FluffyByte.OPUL!");

            // Start handling TCP communication
            _ = Task.Run(() => HandleClientTcpCommunication(newVessel), _shutdownToken);
        }
        catch (Exception ex)
        {
            Scribe.Error(ex);
            _pendingHandshakes.Remove(handshakeGuid);
            tcpClient.Close();
            Watcher.UnregisterRawTcpClient(tcpClient);
        }
    }

    /// <summary>
    /// Sends the TCP handshake request to a newly connected client.
    /// </summary>
    private async Task SendTcpHandshakeRequest(TcpClient tcpClient, Guid handshakeGuid)
    {
        try
        {
            NetworkStream stream = tcpClient.GetStream();

            // Format: "HANDSHAKE|<Guid>|<ServerIP>|<UdpPort>"
            string handshakeMessage = $"HANDSHAKE|{handshakeGuid}|{HOST_TCP_ADDRESS}|{HOST_UDP_PORT}\n";
            byte[] data = System.Text.Encoding.UTF8.GetBytes(handshakeMessage);

            await stream.WriteAsync(data, _shutdownToken);
            await stream.FlushAsync(_shutdownToken);

            Scribe.Info($"[{Name}] Sent handshake request with Guid: {handshakeGuid}");
        }
        catch (Exception ex)
        {
            Scribe.Error(ex);
        }
    }

    /// <summary>
    /// Listens for UDP handshake responses from clients.
    /// </summary>
    private async Task ListenForUdpHandshakes()
    {
        try
        {
            while (!_shutdownToken.IsCancellationRequested)
            {
                var result = await _udpSocket.ReceiveAsync(_shutdownToken);
                string message = System.Text.Encoding.UTF8.GetString(result.Buffer);

                // Expected format: "HANDSHAKE|<Guid>"
                if (message.StartsWith("HANDSHAKE|"))
                {
                    string guidString = message.Split('|')[1];

                    if (Guid.TryParse(guidString, out Guid handshakeGuid))
                    {
                        // Check if this is a pending handshake
                        if (_pendingHandshakes.TryGetValue(handshakeGuid, out var pendingData))
                        {
                            Scribe.Info($"[{Name}] Received UDP handshake for Guid: {handshakeGuid}");

                            // Send acknowledgment
                            byte[] ack = System.Text.Encoding.UTF8.GetBytes("HANDSHAKE_ACK");
                            await _udpSocket.SendAsync(ack, ack.Length, result.RemoteEndPoint);

                            // Complete the handshake (triggers vessel creation)
                            pendingData.UdpEndpointSource.SetResult(result.RemoteEndPoint);
                        }
                        else
                        {
                            Scribe.Warn($"[{Name}] Received UDP handshake for unknown Guid: {handshakeGuid}");
                        }
                    }
                }
                else
                {
                    // Not a handshake - route to existing vessel
                    RouteUdpPacket(result.Buffer, result.RemoteEndPoint);
                }
            }
        }
        catch (OperationCanceledException)
        {
            Scribe.Warn($"[{Name}] UDP Listener canceled (shutdown requested)");
        }
        catch (Exception ex)
        {
            Scribe.Error(ex);
        }
    }

    /// <summary>
    /// Routes incoming UDP packets to the correct vessel based on endpoint.
    /// </summary>
    private void RouteUdpPacket(byte[] data, IPEndPoint remoteEndPoint)
    {
        var vessel = Watcher.Vessels.FirstOrDefault(
            v => v.UdpEndPoint?.Equals(remoteEndPoint) ?? false
        );

        if (vessel != null)
        {
            vessel.UdpIO.OnPacketReceived(data);
        }
        else
        {
            Scribe.Warn($"[{Name}] Received UDP packet from unregistered endpoint: {remoteEndPoint}");
        }
    }

    private async Task HandleClientTcpCommunication(Vessel vessel)
    {
        try
        {
            while (!_shutdownToken.IsCancellationRequested && !vessel.Disconnecting)
            {
                string message = await vessel.TcpIO.ReadTextAsync();

                if (string.IsNullOrEmpty(message))
                    break;

                ProcessTcpMessage(vessel, message);
            }
        }
        catch (Exception ex)
        {
            Scribe.Error(ex);
        }
        finally
        {
            await vessel.DisconnectAsync();
            Watcher.UnregisterVessel(vessel);
        }
    }

    private void ProcessTcpMessage(Vessel vessel, string message)
    {
        Scribe.Debug($"[{Name}] Received TCP from Vessel {vessel.Id} : {message}");
    }
}