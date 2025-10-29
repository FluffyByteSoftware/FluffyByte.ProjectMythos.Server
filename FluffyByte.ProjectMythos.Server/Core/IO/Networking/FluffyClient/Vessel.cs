using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using FluffyByte.ProjectMythos.Server.Core.IO.Debug;

namespace FluffyByte.ProjectMythos.Server.Core.IO.Networking.FluffyClient;

/// <summary>
/// Represents a vessel class which wraps around a TcpClient and UdpClient for managing a network connection.
/// </summary>
public class Vessel(TcpClient tcpClient, IPEndPoint udpEndPoint, UdpClient sharedUdpSocket) : IDisposable
{
    private bool _disconnecting = false;

    /// <summary>
    /// Gets a value indicating whether the system is in the process of disconnecting.
    /// </summary>
    public bool Disconnecting => _disconnecting;

    /// <summary>
    /// Indicates whether the user has completed the full TCP/UDP handshake.
    /// </summary>
    /// <remarks>
    /// This becomes true after:
    /// 1. TCP connection established
    /// 2. Server sent handshake with ClientID and UDP info
    /// 3. Client sent first UDP packet with ClientID
    /// 4. Server matched UDP endpoint to this Vessel
    /// </remarks>
    public bool IsAuthenticated = false;

    internal readonly TcpClient _tcpClient = tcpClient;
    internal readonly NetworkStream _tcpStream = tcpClient.GetStream();

    private static int _id = 0;
    /// <summary>
    /// Gets the simple client identifier for the instance.
    /// </summary>
    public int Id { get; private set; } = _id++;

    /// <summary>
    /// Gets the <see cref="UdpClient"/> instance used for sending and receiving UDP datagrams.
    /// </summary>
    public UdpClient UdpClient { get; internal set; } = sharedUdpSocket;

    /// <summary>
    /// Gets the UDP endpoint used for network communication.
    /// </summary>
    public IPEndPoint UdpEndPoint { get; internal set; } = udpEndPoint;

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
    public TcpIO TcpIO => new(this);

    /// <summary>
    /// Gets an instance of the <see cref="UdpIO"/> class associated with this object.
    /// </summary>
    public UdpIO UdpIO => new(this);
    /// <summary>
    /// Gets an object that provides access to various metrics related to the system's performance and behavior.
    /// </summary>
    public Metrics Metrics => new(this);

    /// <summary>
    /// Disconnects the current connection, ensuring that the operation is performed only once.
    /// </summary>
    public async Task DisconnectAsync()
    {
        if (_disconnecting) return;

        _disconnecting = true;

        _tcpClient.Close();

        HandleDisconnect();
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

        _tcpClient.Dispose();
        Metrics.Dispose();
        TcpIO.Dispose();

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Handles the disconnection of the TCP client by closing and disposing of the connection.
    /// </summary>
    private void HandleDisconnect()
    {
        try
        {
            Conductor.Instance.Sentinel.Watcher.UnregisterVessel(this);

            _tcpClient.Close();
        }
        catch (Exception ex)
        {
            Scribe.Error(ex);
        }
    }
}