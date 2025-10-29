using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluffyByte.ProjectMythos.Server.Core.IO.Debug;

namespace FluffyByte.ProjectMythos.Server.Core;

/// <summary>
/// Provides a base class for implementing core processes with lifecycle management, including start and stop
/// operations.
/// </summary>
/// <remarks>This abstract class defines the foundational structure for core processes, including properties for
/// process state, unique identification, and lifecycle management. Derived classes must implement the <see
/// cref="StartAsync"/> and <see cref="StopAsync"/> methods to define the specific behavior for starting and stopping
/// the process.</remarks>
/// <param name="shutdownToken">CancellationToken that is passed to the inner CoreProcessBase functionality</param>
public abstract class CoreProcessBase(CancellationToken shutdownToken) : ICoreProcess
{
    /// <summary>
    /// Gets the name associated with the current CoreProcess
    /// </summary>
    public abstract string Name { get; }
    /// <summary>
    /// Get the unique id for the current Core Process.
    /// </summary>
    public Guid Guid { get; } = Guid.NewGuid();

    /// <summary>
    /// Gets the current state of the core process.
    /// </summary>
    public CoreProcessState State { get; private set; } = CoreProcessState.New;
    /// <summary>
    /// A cancellation token used to signal the shutdown process.
    /// </summary>
    /// <remarks>This token is initialized as a new instance of <see cref="CancellationToken"/>  and can be
    /// used to monitor or trigger cancellation during the shutdown process.</remarks>
    internal CancellationToken _shutdownToken = shutdownToken;

    /// <summary>
    /// Initiates the start process for the current instance asynchronously.
    /// </summary>
    /// <remarks>This method logs the start request and begins the asynchronous start process. The provided
    /// <paramref name="shutdownToken"/> is used to monitor for cancellation requests during the operation.</remarks>
    /// <param name="shutdownToken">A <see cref="CancellationToken"/> that can be used to signal a request to cancel the operation.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task RequestStartAsync(CancellationToken shutdownToken)
    {
        Scribe.Debug($"[{Name}] I have been asked to start.");

        try
        {
            _shutdownToken = shutdownToken;
            
            State = CoreProcessState.Loading;
            
            await StartAsync();

            State = CoreProcessState.Running;

            Scribe.Debug($"[{Name}] I am now running.");
        }
        catch(Exception ex)
        {
            Scribe.Error(ex);
        }
    }

    /// <summary>
    /// Requests the service to stop its operations asynchronously.
    /// </summary>
    /// <remarks>This method signals the service to initiate its shutdown process.  It does not guarantee an
    /// immediate stop, as the service may perform cleanup or other tasks before halting.</remarks>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task RequestStopAsync()
    {
        Scribe.Debug($"[{Name}] I have been asked to stop.");

        try
        {
            State = CoreProcessState.Stopping;

            await StopAsync();

            State = CoreProcessState.Stopped;
            
            Scribe.Debug($"[{Name}] I have stopped.");
        }
        catch(Exception ex)
        {
            Scribe.Error(ex);
        }
    }

    /// <summary>
    /// Starts the asynchronous operation.
    /// </summary>
    /// <remarks>This method must be implemented by derived classes to define the specific behavior of the
    /// asynchronous operation.  It is intended to be called to initiate the operation and should return a <see
    /// cref="Task"/> that represents the operation.</remarks>
    /// <returns>A <see cref="Task"/> that represents the asynchronous operation.</returns>
    public abstract Task StartAsync();

    /// <summary>
    /// Initiates an asynchronous operation to stop the service.
    /// </summary>
    /// <remarks>This method is typically called during the shutdown process to release resources  and perform
    /// any necessary cleanup. Implementations should ensure that the service  stops gracefully and any ongoing
    /// operations are completed or canceled appropriately.</remarks>
    /// <returns>A <see cref="Task"/> that represents the asynchronous stop operation.</returns>
    public abstract Task StopAsync();

}
