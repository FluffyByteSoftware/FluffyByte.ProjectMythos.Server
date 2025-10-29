using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using FluffyByte.ProjectMythos.Server.Core.IO.Debug;
using FluffyByte.ProjectMythos.Server.Core.IO.Networking.FluffyClient;

namespace FluffyByte.ProjectMythos.Server.Core.IO.Networking;

/// <summary>
/// Represents a server process that manages TCP and UDP communication for client connections.
/// </summary>
/// <remarks>The <see cref="Sentinel"/> class is responsible for handling incoming client connections over TCP and
/// UDP, managing handshakes, and maintaining a list of connected clients (vessels). It provides functionality to start
/// and stop the server, as well as to process client communication. This class is designed to be used in scenarios
/// where a server needs to manage multiple client connections with both TCP and UDP protocols.</remarks>
public class Sentinel : CoreProcessBase
{
    #region Public Variables and Constants
    /// <summary>
    /// Gets the name of the current instance.
    /// </summary>
    public override string Name => "Sentinel";

    /// <summary>
    /// Specifies the default TCP port number used for host communication.
    /// </summary>
    public const int HOST_TCP_PORT = 9997;
    
    /// <summary>
    /// The default UDP port number used for host communication.
    /// </summary>
    /// <remarks>This constant represents the port number 9998, which is typically used for UDP-based
    /// communication between hosts. Ensure that this port is not blocked by firewalls or other network restrictions
    /// when using it in your application.</remarks>
    public const int HOST_UDP_PORT = 9998;

    /// <summary>
    /// Represents the default TCP address of the host.
    /// </summary>
    /// <remarks>This constant specifies the IP address "10.0.0.84" as the default TCP address for the host.
    /// It is intended to be used in scenarios where a predefined host address is required.
    /// Can be accessed by the public IP and not 10.0.0.84</remarks>
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
    /// Gets the <see cref="Watcher"/> instance associated with this object.
    /// </summary>
    public Watcher Watcher { get; private set; }
    #endregion

    #region Local Variables and Constants
    private TcpListener _listener = new(IPAddress.Any, HOST_TCP_PORT);
    private readonly UdpClient _udpSocket;
    private static readonly IPEndPoint DummyEndPoint = new(IPAddress.None, 0);

    private readonly Dictionary<Guid, (TcpClient TcpClient, TaskCompletionSource<IPEndPoint> UdpEndpointSource)> _pendingHandshakes = [];
    #endregion

    #region Public Methods
    /// <summary>
    /// Initializes a new instance of the <see cref="Sentinel"/> class with the specified shutdown token.
    /// </summary>
    /// <param name="shutdownToken">A <see cref="CancellationToken"/> that signals when the sentinel should shut down.</param>
    public Sentinel(CancellationToken shutdownToken) : base(shutdownToken)
    {
        Watcher = new(this);
        Watcher.ClearLists();
        _udpSocket = new(HOST_UDP_PORT);
    }

    /// <summary>
    /// Starts the server and begins listening for incoming TCP and UDP connections.
    /// </summary>
    /// <remarks>This method initializes the server's TCP listener and UDP socket, enabling it to accept new
    /// client connections. It sets the server to an active state and logs the listening status. Any exceptions
    /// encountered during the initialization of connection listeners are logged.</remarks>
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
    /// Stops the listener and closes the associated UDP socket, preventing the acceptance of new clients.
    /// </summary>
    /// <remarks>This method ensures that the listener is stopped and any associated resources, such as the
    /// UDP socket, are released.  It sets <see cref="AcceptingNewClients"/> to <see langword="false"/> to indicate that
    /// no new clients will be accepted.</remarks>
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

    #endregion

    #region Local Methods
    /// <summary>
    /// Listens for and accepts new TCP client connections until the maximum client limit is reached or a shutdown is
    /// requested.
    /// </summary>
    /// <remarks>This method continuously waits for incoming TCP client connections and registers them with
    /// the <see cref="Watcher"/>. Each accepted client undergoes a handshake process and is associated with a new
    /// vessel. The method stops listening when the shutdown token is triggered or the maximum number of clients is
    /// reached. <para> Exceptions such as <see cref="OperationCanceledException"/> and <see cref="SocketException"/>
    /// are handled internally, and appropriate debug or error messages are logged. </para> <para> Upon shutdown, all
    /// registered vessels and their associated TCP clients are unregistered and disconnected. </para></remarks>
    private async Task ListenForNewConnection()
    {
        try
        {
            while (!_shutdownToken.IsCancellationRequested && Watcher.RawTcpClients.Count < MAX_CLIENTS)
            {
                TcpClient tcpClient = await _listener.AcceptTcpClientAsync(_shutdownToken);
                Scribe.Info($"[{Name}] New TCP client connected from {tcpClient.Client.RemoteEndPoint}.");
                Watcher.RegisterRawTcpClient(tcpClient);
                _ = Task.Run(() => PerformHandshakeAndCreateVessel(tcpClient), _shutdownToken);
            }
        }
        catch (OperationCanceledException)
        {
            Scribe.Debug($"[{Name}] TCP Listener canceled (shutdown requested)");
        }
        catch (SocketException ex) when (ex.SocketErrorCode == SocketError.OperationAborted)
        {
            Scribe.Debug($"[{Name}] TCP Listener socket operation aborted (likely due to shutdown)");
        }
        catch (SocketException ex)
        {
            Scribe.Debug($"[{Name}] SocketException in TCP Listener: {ex.Message}");
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
                Watcher.UnregisterVessel(v);
                Watcher.UnregisterRawTcpClient(v.tcpClient);
                await v.DisconnectAsync();
            }
        }
    }

    /// <summary>
    /// Performs a handshake with a TCP client, establishes a UDP connection, and creates a new vessel.
    /// </summary>
    /// <remarks>This method initiates a handshake with the specified TCP client, waits for a corresponding
    /// UDP endpoint to be provided, and creates a new vessel to manage the connection. If the handshake or
    /// authentication fails, the client is disconnected. The method also handles timeouts and exceptions during the
    /// handshake process.</remarks>
    /// <param name="tcpClient">The TCP client to perform the handshake with.</param>

    private async Task PerformHandshakeAndCreateVessel(TcpClient tcpClient)
    {
        Guid handshakeGuid = Guid.NewGuid();
        TaskCompletionSource<IPEndPoint> udpEndpointSource = new();

        try
        {
            _pendingHandshakes[handshakeGuid] = (tcpClient, udpEndpointSource);
            await SendTcpHandshakeRequest(tcpClient, handshakeGuid);
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(10), _shutdownToken);
            var completedTask = await Task.WhenAny(udpEndpointSource.Task, timeoutTask);

            if (completedTask == timeoutTask)
            {
                Scribe.Warn($"[{Name}] Handshake {handshakeGuid} timed out waiting for UDP response.");
                _pendingHandshakes.Remove(handshakeGuid);
                tcpClient.Close();
                Watcher.UnregisterRawTcpClient(tcpClient);
                return;
            }

            IPEndPoint udpEndpoint = await udpEndpointSource.Task;
            _pendingHandshakes.Remove(handshakeGuid);
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

            await newVessel.TcpIO.WriteTextAsync("Connection established. Welcome to FluffyByte.OPUL!");
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
    /// Sends a TCP handshake request to the specified client using the provided handshake identifier.
    /// </summary>
    /// <remarks>This method sends a formatted handshake message containing the handshake identifier, host TCP
    /// address, and host UDP port to the connected client over the provided <see cref="TcpClient"/>. The operation is
    /// asynchronous and respects the shutdown token to handle cancellation requests gracefully.</remarks>
    /// <param name="tcpClient">The <see cref="TcpClient"/> instance representing the connection to the remote client.</param>
    /// <param name="handshakeGuid">A unique identifier for the handshake operation, used to correlate the request.</param>
    private async Task SendTcpHandshakeRequest(TcpClient tcpClient, Guid handshakeGuid)
    {
        try
        {
            NetworkStream stream = tcpClient.GetStream();
            string handshakeMessage = $"HANDSHAKE|{handshakeGuid}|{HOST_TCP_ADDRESS}|{HOST_UDP_PORT}\n";
            byte[] data = System.Text.Encoding.UTF8.GetBytes(handshakeMessage);

            await stream.WriteAsync(data, _shutdownToken);
            await stream.FlushAsync(_shutdownToken);
            Scribe.Info($"[{Name}] Sent handshake request with Guid: {handshakeGuid}");
        }
        catch (OperationCanceledException)
        {
            Scribe.Debug($"[{Name}] TCP Handshake sending canceled (shutdown requested)");
        }
        catch (SocketException ex) when (ex.SocketErrorCode == SocketError.OperationAborted)
        {
            Scribe.Debug($"[{Name}] TCP Listener socket operation aborted (likely due to shutdown)");
        }
        catch (SocketException ex)
        {
            Scribe.Debug($"[{Name}] SocketException in TCP Listener: {ex.Message}");
        }
        catch (Exception ex)
        {
            Scribe.Error(ex);
        }
    }

    /// <summary>
    /// Listens for incoming UDP handshake messages and processes them asynchronously.
    /// </summary>
    /// <remarks>This method continuously listens for UDP packets on the configured socket until a shutdown is
    /// requested. If a packet contains a valid handshake message, it acknowledges the handshake and resolves the
    /// associated pending handshake task. Non-handshake packets are routed for further processing.</remarks>
    private async Task ListenForUdpHandshakes()
    {
        try
        {
            while (!_shutdownToken.IsCancellationRequested)
            {
                var result = await SafeReceiveAsync(_udpSocket, _shutdownToken);
                string message = System.Text.Encoding.UTF8.GetString(result.Buffer);

                if (message.StartsWith("HANDSHAKE|"))
                {
                    string guidString = message.Split('|')[1];
                    if (Guid.TryParse(guidString, out Guid handshakeGuid))
                    {
                        if (_pendingHandshakes.TryGetValue(handshakeGuid, out var pendingData))
                        {
                            Scribe.Info($"[{Name}] Received UDP handshake for Guid: {handshakeGuid}");
                            byte[] ack = System.Text.Encoding.UTF8.GetBytes("HANDSHAKE_ACK");
                            await _udpSocket.SendAsync(ack, ack.Length, result.RemoteEndPoint);
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
    /// Routes an incoming UDP packet to the appropriate vessel based on the sender's endpoint.
    /// </summary>
    /// <remarks>If the sender's endpoint matches a registered vessel, the packet is forwarded to the vessel's
    /// UDP handler. Otherwise, a warning is logged indicating that the packet was received from an unregistered
    /// endpoint.</remarks>
    /// <param name="data">The byte array containing the UDP packet data.</param>
    /// <param name="remoteEndPoint">The <see cref="IPEndPoint"/> representing the sender's endpoint.</param>
    private void RouteUdpPacket(byte[] data, IPEndPoint remoteEndPoint)
    {
        var vessel = Watcher.Vessels.FirstOrDefault(v => v.UdpEndPoint?.Equals(remoteEndPoint) ?? false);
        if (vessel != null)
        {
            vessel.UdpIO.OnPacketReceived(data);
        }
        else
        {
            Scribe.Warn($"[{Name}] Received UDP packet from unregistered endpoint: {remoteEndPoint}");
        }
    }

    /// <summary>
    /// Handles the TCP communication for the specified vessel, processing incoming messages until the vessel
    /// disconnects or a shutdown is requested.
    /// </summary>
    /// <remarks>This method continuously reads messages from the vessel's TCP connection and processes them
    /// until either the vessel initiates a disconnection, a shutdown is requested, or an error occurs. If an exception
    /// is encountered, it is logged, and the vessel is disconnected gracefully.</remarks>
    /// <param name="vessel">The vessel for which TCP communication is being managed. This parameter cannot be null.</param>
  
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

    /// <summary>
    /// Processes an incoming TCP message from a specified vessel.
    /// </summary>
    /// <remarks>This method logs the received message for debugging purposes. Ensure that both <paramref
    /// name="vessel"/> and <paramref name="message"/> are valid before calling this method.</remarks>
    /// <param name="vessel">The vessel from which the TCP message was received. Cannot be <see langword="null"/>.</param>
    /// <param name="message">The content of the TCP message to process. Cannot be <see langword="null"/> or empty.</param>
    private void ProcessTcpMessage(Vessel vessel, string message)
    {
        Scribe.Debug($"[{Name}] Received TCP from Vessel {vessel.Id} : {message}");
    }

    /// <summary>
    /// Safely receives a UDP datagram from the specified <see cref="UdpClient"/>.
    /// </summary>
    /// <remarks>This method handles common exceptions that may occur during the receive operation, such as
    /// <see cref="OperationCanceledException"/>, <see cref="ObjectDisposedException"/>, and <see
    /// cref="SocketException"/> with <see cref="SocketError.OperationAborted"/>. In these cases, it returns an empty
    /// <see cref="UdpReceiveResult"/> with a placeholder endpoint.</remarks>
    /// <param name="client">The <see cref="UdpClient"/> instance used to receive the datagram.</param>
    /// <param name="token">A <see cref="CancellationToken"/> that can be used to cancel the receive operation.</param>
    /// <returns>A <see cref="UdpReceiveResult"/> containing the received datagram and the remote endpoint, or an empty result if
    /// the operation is canceled, the client is disposed, or the operation is aborted.</returns>
    private async static Task<UdpReceiveResult> SafeReceiveAsync(UdpClient client, CancellationToken token)
    {
        try
        {
            return await client.ReceiveAsync(token);
        }
        catch (OperationCanceledException)
        {
            return new UdpReceiveResult([], DummyEndPoint);
        }
        catch (ObjectDisposedException)
        {
            return new UdpReceiveResult([], DummyEndPoint);
        }
        catch (SocketException ex) when (ex.SocketErrorCode == SocketError.OperationAborted)
        {
            return new UdpReceiveResult([], DummyEndPoint);
        }
    }
    #endregion
}
