using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using FluffyByte.Tools;

namespace FluffyByte.ProjectMythos.Server.Core.IO.Networking.FluffyClient;

/// <summary>
/// Represents a vessel class which wraps around a TcpClient and UdpClient for managing a network connection.
/// </summary>
public class Vessel : IDisposable
{
    private bool _disconnecting = false;

    /// <summary>
    /// Gets a value indicating whether the system is in the process of disconnecting.
    /// </summary>
    public bool Disconnecting => _disconnecting;

    /// <summary>
    /// Indicates whether the user has completed the full TCP/UDP handshake and authentication.
    /// </summary>
    public bool IsAuthenticated = false;

    internal readonly TcpClient tcpClient;
    internal readonly NetworkStream tcpStream;

    private static int _id = 0;
    /// <summary>
    /// Gets the simple client identifier for the instance.
    /// </summary>
    public int Id { get; private set; } = _id++;

    /// <summary>
    /// Gets the <see cref="UdpClient"/> instance used for sending and receiving UDP datagrams.
    /// </summary>
    public UdpClient UdpClient { get; internal set; }

    /// <summary>
    /// Gets the UDP endpoint used for network communication.
    /// </summary>
    public IPEndPoint UdpEndPoint { get; internal set; }

    /// <summary>
    /// Gets the unique identifier for this instance (used for TCP/UDP linking).
    /// </summary>
    public Guid Guid { get; private set; } = Guid.NewGuid();

    /// <summary>
    /// Represents the name of the vessel (set during authentication).
    /// </summary>
    public string Name { get; set; } = "Unnamed Vessel";

    /// <summary>
    /// The TcpIO instance handles TCP input/output operations for this vessel.
    /// </summary>
    public TcpIO TcpIO { get; private set; }

    /// <summary>
    /// Gets the UdpIO instance associated with this vessel.
    /// </summary>
    public UdpIO UdpIO { get; private set; }

    /// <summary>
    /// Gets the Metrics instance for tracking connection statistics.
    /// </summary>
    public Metrics Metrics { get; private set; }

    /// <summary>
    /// Initializes a new instance of the Vessel class.
    /// </summary>
    public Vessel(TcpClient tcpClient, IPEndPoint udpEndPoint, UdpClient sharedUdpSocket)
    {
        this.tcpClient = tcpClient;
        tcpStream = tcpClient.GetStream();
        UdpClient = sharedUdpSocket;
        UdpEndPoint = udpEndPoint;

        // Initialize components
        TcpIO = new TcpIO(this);

        // This constructor sets IsHandshakeComplete = true automatically
        Metrics = new Metrics(this);
        UdpIO = new UdpIO(this, udpEndPoint);

        
    }

    /// <summary>
    /// Disconnects the current connection, ensuring that the operation is performed only once.
    /// </summary>
    public async Task DisconnectAsync()
    {
        if (_disconnecting) return;

        _disconnecting = true;

        try
        {
            tcpClient.Close();
            HandleDisconnect();
        }
        catch (Exception ex)
        {
            Scribe.Error(ex);
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Disconnects the current instance from its associated watcher and performs necessary cleanup.
    /// </summary>
    public void Disconnect()
    {
        if (_disconnecting) return;

        _disconnecting = true;

        HandleDisconnect();
    }

    /// <summary>
    /// Releases the resources used by the current instance of the class.
    /// </summary>
    public void Dispose()
    {
        if (!_disconnecting)
            return;

        try
        {
            TcpIO?.Dispose();
            UdpIO?.Dispose();
            Metrics?.Dispose();
            tcpClient?.Dispose();
        }
        catch (Exception ex)
        {
            Scribe.Error(ex);
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Handles the disconnection of the TCP client by closing and disposing of the connection.
    /// </summary>
    private void HandleDisconnect()
    {
        try
        {
            if(Conductor.Instance.Sentinel == null || Conductor.Instance.Sentinel.Watcher == null)
            {
                Scribe.Critical("Vessel HandleDisconnect() called but Conductor.Instance.Sentinel.Watcher is null.");
                return;
            }

            Conductor.Instance.Sentinel.Watcher.UnregisterVessel(this);

            tcpClient.Close();
            Dispose();
        }
        catch (Exception ex)
        {
            Scribe.Error(ex);
        }
    }
}