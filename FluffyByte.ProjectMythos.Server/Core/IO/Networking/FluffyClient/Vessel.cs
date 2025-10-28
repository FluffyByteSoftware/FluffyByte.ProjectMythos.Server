using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using FluffyByte.ProjectMythos.Server.Core.IO.Debug;

namespace FluffyByte.ProjectMythos.Server.Core.IO.Networking.FluffyClient;

// The networking wrapper for our clients.

/// <summary>
/// Represents a networking wrapper for client communication.
/// </summary>
/// <remarks>This class serves as the primary interface for managing network interactions with clients.  It
/// provides functionality to facilitate communication and data exchange in a networked environment.</remarks>
/// <remarks>
/// Initializes a new instance of the <see cref="Vessel"/> class with the specified TCP client and sentinel.
/// </remarks>
/// <param name="tcpClient">The <see cref="TcpClient"/> used to establish and manage the network connection.</param>
/// <param name="sentinel">The <see cref="Sentinel"/> instance associated with this vessel, used for 
/// monitoring or controlling its behavior.</param>
public class Vessel(TcpClient tcpClient, Sentinel sentinel) : IDisposable
{
    private bool _disconnecting = false;
    private readonly TcpClient _tcpClient = tcpClient;
    private readonly Sentinel _sentinelReference = sentinel;

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
        _sentinelReference.Watcher.UnregisterVessel(this);

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

        _sentinelReference.Watcher.UnregisterVessel(this);

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

        GC.SuppressFinalize(this);
        GC.Collect();
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
