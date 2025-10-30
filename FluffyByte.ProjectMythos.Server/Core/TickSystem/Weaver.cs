using System.Threading.Tasks;

using FluffyByte.ProjectMythos.Server.Core.IO.Debug;
using FluffyByte.ProjectMythos.Server.Core.IO.Networking;

namespace FluffyByte.ProjectMythos.Server.Core.TickSystem;

/// <summary>
/// Represents a process responsible for weaving operations, inheriting from <see cref="CoreProcessBase"/>.
/// </summary>
/// <remarks>The <see cref="Weaver"/> class provides functionality to manage the lifecycle of a weaving process,
/// including starting and stopping the process asynchronously. It is initialized with a cancellation token to handle
/// graceful shutdowns.</remarks>
/// <param name="shutdownToken">Reference to the cancellation token held by the Conductor.</param>
public class Weaver(CancellationToken shutdownToken) : CoreProcessBase(shutdownToken)
{
    public override string Name => "Weaver";

    #region Default Methods from Inheritance
    /// <summary>
    /// Starts the asynchronous operation.
    /// </summary>
    /// <remarks>This method initiates an asynchronous process. Callers should await the returned task to
    /// ensure the operation completes before proceeding.</remarks>
    public override async Task StartAsync()
    {
        await Task.CompletedTask;
    }

    /// <summary>
    /// Stops the asynchronous operation and performs any necessary cleanup.
    /// </summary>
    /// <remarks>This method should be called to gracefully stop the operation. It ensures that all resources
    /// are released and any pending tasks are completed before the operation is terminated.</remarks>
    /// <returns>A <see cref="Task"/> that represents the asynchronous stop operation.</returns>
    public override async Task StopAsync()
    {
        await Task.CompletedTask;
    }
    #endregion

    public async Task ProcessMovementTick(ulong tick)
    {

    }
}
