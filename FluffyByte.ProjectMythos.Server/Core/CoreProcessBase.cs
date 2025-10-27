using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluffyByte.ProjectMythos.Server.Core.IO.Debug;

namespace FluffyByte.ProjectMythos.Server.Core;

/// <summary>
/// Represents the base class for core processes, providing a framework for managing process lifecycle operations such
/// as starting and stopping.
/// </summary>
/// <remarks>This abstract class defines the essential structure and behavior for core processes, including a
/// unique identifier, a name, and lifecycle management methods. Derived classes must implement the <see cref="Name"/>,
/// <see cref="RequestStartAsync(CancellationToken)"/>, and <see cref="RequestStopAsync"/> members.</remarks>
/// <param name="shutdownToken"></param>
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
    private CancellationToken _shutdownToken = shutdownToken;

    /// <summary>
    /// Initiates the start process for the current instance asynchronously.
    /// </summary>
    /// <remarks>This method logs the start request and begins the asynchronous start process. The provided
    /// <paramref name="shutdownToken"/> is used to monitor for cancellation requests during the operation.</remarks>
    /// <param name="shutdownToken">A <see cref="CancellationToken"/> that can be used to signal a request to cancel the operation.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task RequestStartAsync(CancellationToken shutdownToken)
    {
        Scribe.Info($"[{Name}] I have been asked to start.");


        _shutdownToken = shutdownToken;

        await StartAsync();
    }

    /// <summary>
    /// Requests the service to stop its operations asynchronously.
    /// </summary>
    /// <remarks>This method signals the service to initiate its shutdown process.  It does not guarantee an
    /// immediate stop, as the service may perform cleanup or other tasks before halting.</remarks>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task RequestStopAsync()
    {
        Scribe.Info($"[{Name}] I have been asked to stop.");

        try
        {

        }
        catch(Exception ex)
        {
            Scribe.Error(ex);
        }

        await StopAsync();
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
