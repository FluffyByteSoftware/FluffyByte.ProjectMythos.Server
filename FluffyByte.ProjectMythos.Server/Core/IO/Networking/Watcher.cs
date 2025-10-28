using System;
using System.Net;
using System.Net.Sockets;
using FluffyByte.ProjectMythos.Server.Core.IO.Debug;

namespace FluffyByte.ProjectMythos.Server.Core.IO.Networking;

/// <summary>
/// Represents a process that monitors and manages vessels and raw TCP clients within the system.
/// </summary>
/// <remarks>The <see cref="Watcher"/> class is responsible for maintaining collections of connected and pending
/// vessels, as well as managing raw TCP client connections. It provides functionality to start and stop its operations
/// asynchronously, and includes methods for registering, unregistering, and authenticating vessels.</remarks>
/// <param name="shutdownToken">This token is passed from the Conductor and represents the necessity to shutdown.</param>
/// <param name="sentinel">Reference to the Sentinel currently in operation.</param>
public class Watcher(Sentinel sentinel, CancellationToken shutdownToken) : CoreProcessBase(shutdownToken)
{

    /// <summary>
    /// Gets the name of the watcher.
    /// </summary>
    public override string Name => "Watcher";

    /// <summary>
    /// Gets the collection of vessels currently connected to the system, identified by their unique identifiers.
    /// </summary>
    public List<Vessel> ConnectedVessels { get; private set; } = [];

    /// <summary>
    /// Gets the collection of vessels that are pending processing.
    /// </summary>
    public List<Vessel> PendingVessels { get; private set; } = [];

    private readonly Sentinel _sentinelReference = sentinel;
    private readonly object _lock = new();

    /// <summary>
    /// Starts the asynchronous operation for the Watcher.
    /// </summary>
    /// <remarks>This method initiates the operation in an asynchronous manner.  Ensure that any required
    /// setup or dependencies are configured before calling this method.</remarks>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public override async Task StartAsync()
    {
        try
        {
            lock (_lock)
            {
                ConnectedVessels.Clear();
                PendingVessels.Clear();
            }
        }
        catch(Exception ex)
        {
            Scribe.Error(ex);
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Stops the asynchronous operation and performs any necessary cleanup.
    /// </summary>
    /// <remarks>This method is typically called to gracefully shut down the operation.  Ensure that any
    /// resources used during the operation are properly released.</remarks>
    /// <returns>A <see cref="Task"/> that represents the asynchronous stop operation.</returns>
    public override async Task StopAsync()
    {
        foreach(var vessel in ConnectedVessels)
        {
            await vessel.DisconnectAsync();
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Registers a new vessel in the system.
    /// </summary>
    /// <remarks>This method asynchronously registers the provided vessel. Ensure that the <paramref
    /// name="vessel"/> object contains all required information before calling this method.</remarks>
    /// <param name="vessel">The vessel to be registered. Must not be <see langword="null"/>.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public void RegisterVessel(Vessel vessel)
    {
        try
        {
            lock (_lock)
                PendingVessels.Add(vessel);
        }
        catch(Exception ex)
        {
            Scribe.Error(ex);
        }
    }

    /// <summary>
    /// Unregisters the specified vessel from the system.
    /// </summary>
    /// <remarks>This method removes the provided vessel from the system's registry. Ensure that the vessel is
    /// no longer in use before calling this method to avoid inconsistencies.</remarks>
    /// <param name="vessel">The vessel to be unregistered. Cannot be <see langword="null"/>.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public void UnregisterVessel(Vessel vessel)
    {
        try
        {
            lock (_lock)
            {
                PendingVessels.Remove(vessel);
                ConnectedVessels.Remove(vessel);
            }
        }
        catch(Exception ex)
        {
            Scribe.Error(ex);
        }
    }

    /// <summary>
    /// Authenticates the specified vessel to ensure it meets the required criteria for operation.
    /// </summary>
    /// <param name="vessel">The vessel to be authenticated. Cannot be null.</param>
    public void AuthenticateVessel(Vessel vessel)
    {
        try
        {
            lock (_lock)
            {
                PendingVessels.Remove(vessel);
                ConnectedVessels.Add(vessel);
            }
        }
        catch(Exception ex)
        {
            Scribe.NetworkError(ex);
        }
    }
}
