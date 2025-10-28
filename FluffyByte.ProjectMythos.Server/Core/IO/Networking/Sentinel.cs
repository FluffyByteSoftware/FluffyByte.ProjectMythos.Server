using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using FluffyByte.ProjectMythos.Server.Core.IO.Debug;

namespace FluffyByte.ProjectMythos.Server.Core.IO.Networking;

/// <summary>
/// Todo Sentinel handles monitoring for new network connections.
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
    /// <remarks>This constant specifies the port number that the host listens on by default.  Ensure that
    /// this port is not already in use by another application to avoid conflicts.</remarks>
    public const int HOST_TCP_PORT = 9997;

    /// <summary>
    /// Represents the default TCP address of the host.
    /// </summary>
    /// <remarks>This constant specifies the IP address used to connect to the host over TCP. It is intended
    /// for use in scenarios where a predefined host address is required.</remarks>
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

    private TcpListener _listener = new(IPAddress.Any, 1111);
    private int _currentClientCount;

    /// <summary>
    /// Initializes a new instance of the <see cref="Sentinel"/> class, which monitors and manages client connections.
    /// </summary>
    /// <remarks>The <see cref="Sentinel"/> class is designed to welcome client connections and ensure proper
    /// handling during shutdown scenarios. The provided <paramref name="shutdownToken"/> is used to propagate
    /// cancellation requests to the sentinel and its associated watcher.</remarks>
    /// <param name="shutdownToken">A <see cref="CancellationToken"/> that signals when the sentinel should shut down.</param>
    public Sentinel(CancellationToken shutdownToken) : base(shutdownToken)
    {
        Watcher = new(this, shutdownToken);
        _currentClientCount = 0;
    }

    /// <summary>
    /// Starts the asynchronous operation.
    /// </summary>
    /// <remarks>This method initiates an asynchronous process. Ensure that any required preconditions  are
    /// met before calling this method. The operation runs asynchronously and does not block  the calling
    /// thread.</remarks>

    public override async Task StartAsync()
    {
        AcceptingNewClients = true;

        _listener = new(IPAddress.Parse(HOST_TCP_ADDRESS), HOST_TCP_PORT);

        _listener.Start();

        Scribe.Info($"[{Name}] Listening for new TCP connections on {HOST_TCP_ADDRESS}:{HOST_TCP_PORT}");
        
        try
        {
            while (!_shutdownToken.IsCancellationRequested)
            {
                if (AcceptingNewClients && _currentClientCount < MAX_CLIENTS)
                    await ListenForNewConnection();
            }
        }
        catch(Exception ex)
        {
            Scribe.Error(ex);
        }

        await RequestStopAsync();
    }

    /// <summary>
    /// Local method to implement custom behaviour for Sentinel to do when stopping.
    /// </summary>
    public override async Task StopAsync()
    {
        try
        {
            _listener.Stop();

            AcceptingNewClients = false;
        }
        catch(Exception ex)
        {
            Scribe.Error(ex);
        }

        await Task.CompletedTask;
    }

    private async Task ListenForNewConnection()
    {
        try
        {
            
            TcpClient tcpClient = await _listener.AcceptTcpClientAsync();
            
            Scribe.Info($"A new user has joined the server!");

            _currentClientCount++;
        }
        catch(Exception ex)
        {
            Scribe.Error(ex);
        }
    }
}