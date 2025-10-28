using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using FluffyByte.ProjectMythos.Server.Core.IO.Debug;

namespace FluffyByte.ProjectMythos.Server.Core.IO.Networking.FluffyClient;

/// <summary>
/// Represents a vessel class which wraps around a TcpClient and UdpClient for managing a network connection.
/// </summary>
/// <remarks>The <see cref="Vessel"/> class encapsulates the functionality for managing a network connection using
/// a <see cref="TcpClient"/>. It provides mechanisms for safe disconnection, resource cleanup, and access to utilities
/// such as metrics and safety mechanisms. Instances of this class are uniquely identified by an <see cref="Id"/> and a
/// <see cref="Guid"/>. The class is designed to be used in scenarios where reliable network communication and
/// monitoring are required.</remarks>
/// <param name="tcpClient">The TcpClient that the Vessel wraps around.</param>
public class Vessel(TcpClient tcpClient) : IDisposable
{
    private bool _disconnecting = false;

    /// <summary>
    /// Gets a value indicating whether the system is in the process of disconnecting.
    /// </summary>
    public bool Disconnecting => _disconnecting;

    /// <summary>
    /// Represents the underlying TCP client used for network communication.
    /// </summary>
    /// <remarks>This field is used internally to manage the connection to a remote endpoint. It is
    /// initialized with the provided <see cref="TcpClient"/> instance and cannot be modified after
    /// construction.</remarks>
    internal readonly TcpClient _tcpClient = tcpClient;
    internal readonly UdpClient? _udpClient;

    internal readonly NetworkStream _tcpStream = tcpClient.GetStream();
    internal int UdpPort = -1;

    private static int _id = 0;
    /// <summary>
    /// Gets the simple client identifier for the instance.
    /// </summary>
    public int Id { get; private set; } = _id++;

    /// <summary>
    /// Gets the unique identifier for this instance.
    /// </summary>
    public Guid Guid { get; private set; } = Guid.NewGuid();

    /// <summary>
    /// Represents the name of the vessel.
    /// </summary>
    public string Name = "Unnamed Vessel";

    /// <summary>
    /// The TcpIO instance handles TCP input/output operations for this vessel.
    /// </summary>
    public TcpIO TcpIO => new(this);
    /// <summary>
    /// Gets an object that provides access to various metrics related to the system's performance and behavior.
    /// </summary>
    public Metrics Metrics => new(this);
    
    /// <summary>
    /// Disconnects the current connection, ensuring that the operation is performed only once.
    /// </summary>
    /// <remarks>This method is idempotent and can be called multiple times without adverse effects. 
    /// Subsequent calls will have no effect if the disconnection process is already in progress.</remarks>
    public async Task DisconnectAsync()
    {
        if (_disconnecting) return;

        _disconnecting = true;

        _tcpClient.Close();

        Conductor.Instance.Sentinel.Watcher.UnregisterVessel(this);

        HandleDisconnect();
        await Task.CompletedTask;
    }

    /// <summary>
    /// Disconnects the current instance from its associated watcher and performs necessary cleanup.
    /// </summary>
    /// <remarks>This method ensures that the instance is unregistered from the watcher and any required 
    /// disconnection logic is executed. Subsequent calls to this method will have no effect  if the instance is already
    /// in the process of disconnecting.</remarks>
    public void Disconnect()
    {
        if (_disconnecting) return;

        _disconnecting = true;

        Conductor.Instance.Sentinel.Watcher.UnregisterVessel(this);

        HandleDisconnect();
    }

    /// <summary>
    /// Releases the resources used by the current instance of the class.
    /// </summary>
    /// <remarks>This method should be called when the instance is no longer needed to ensure proper cleanup
    /// of resources. It is the caller's responsibility to ensure that this method is invoked when
    /// appropriate.</remarks>
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
    /// <remarks>This method ensures that the TCP client is properly closed and disposed of to release
    /// resources.  Any exceptions that occur during the process are logged for diagnostic purposes.</remarks>
    private void HandleDisconnect()
    {
        try
        {
            _tcpClient.Close();
            Dispose();
        }
        catch(Exception ex)
        {
            Scribe.Error(ex);
        }
    }
}
