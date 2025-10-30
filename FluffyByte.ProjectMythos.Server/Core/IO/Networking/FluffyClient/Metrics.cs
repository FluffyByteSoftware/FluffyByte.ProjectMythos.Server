using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using FluffyByte.Tools;

namespace FluffyByte.ProjectMythos.Server.Core.IO.Networking.FluffyClient;

/// <summary>
/// Provides functionality for tracking and managing metrics related to a <see cref="Vessel"/> instance, including
/// connection status, data transfer statistics, and response timestamps.
/// </summary>
/// <remarks>This class is designed to monitor and record various metrics for a <see cref="Vessel"/> object, such
/// as the total bytes sent and received, the last response time, and the login time. It also provides methods for
/// testing the connection status and updating response timestamps.   Instances of this class implement <see
/// cref="IDisposable"/> to ensure proper resource management.</remarks>
/// <param name="parentVessel">A reference to the Vessel holding this Metrics class</param>
public class Metrics(Vessel parentVessel) : IDisposable
{
    private readonly Vessel _parentVesselReference = parentVessel;
    
    private bool _disposed = false;

    /// <summary>
    /// Gets the timestamp of the most recent response.
    /// </summary>
    public DateTime LastResponseTime { get; private set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets the date and time when the user logged in.
    /// </summary>
    public DateTime LoginTime { get; private set; }

    /// <summary>
    /// Gets the timestamp of the most recent UDP packet received.
    /// </summary>
    public DateTime LastPacketUdpReceivedTime { get; internal set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets the total number of bytes received by the Vessel.
    /// </summary>
    public ulong TotalBytesReceived { get; internal set; } = 0;

    /// <summary>
    /// Gets the total number of bytes sent by the Vessel.
    /// </summary>
    public ulong TotalBytesSent { get; internal set; } = 0;

    /// <summary>
    /// Updates the timestamp of the last response to the current UTC time.
    /// </summary>
    /// <remarks>This method sets the <see cref="LastResponseTime"/> property to the current Coordinated
    /// Universal Time (UTC). It is typically used to record the most recent interaction or reaction time.</remarks>
    public void JustReacted()
    {
        LastResponseTime = DateTime.UtcNow;
    }

    /// <summary>
    /// Attempts to poll the connection for the associated vessel to determine if it is still active.
    /// </summary>    
    /// <returns>Returns true if the connection is polling, and false if it isn't.</returns>
    public bool TestConnection()
    {
        try
        {
            if(_parentVesselReference.tcpClient.Client.Poll(1, SelectMode.SelectRead) &&
                _parentVesselReference.tcpClient.Client.Available == 0)
                return false;
            else
                LastResponseTime = DateTime.Now;

            return true;

        }
        catch(Exception ex)
        {
            Scribe.NetworkError(ex, _parentVesselReference);
            return false;
        }
    }

    /// <summary>
    /// Releases the resources used by the current instance of the class.
    /// </summary>
    /// <remarks>This method should be called when the instance is no longer needed to free up resources.  It
    /// suppresses finalization to prevent the garbage collector from calling the finalizer.</remarks>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases the resources used by the current instance of the class.
    /// </summary>
    /// <remarks>This method is called by the public <c>Dispose</c> method and the finalizer.  When <paramref
    /// name="disposing"/> is <see langword="true"/>, it releases all resources  held by managed objects that this
    /// instance references. Override this method in a derived  class to release additional resources.</remarks>
    /// <param name="disposing">A value indicating whether to release both managed and unmanaged resources  (<see langword="true"/>), or only
    /// unmanaged resources (<see langword="false"/>).</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            try
            {
                // Dispose any local objects here
            }
            catch(Exception ex)
            {
                Scribe.Error(ex);
            }
        }

        _disposed = true;
    }
}
