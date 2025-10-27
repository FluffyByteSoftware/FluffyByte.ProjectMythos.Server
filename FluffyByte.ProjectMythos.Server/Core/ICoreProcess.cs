using System;

namespace FluffyByte.ProjectMythos.Server.Core;

/// <summary>
/// Defines the various states that a CoreProcess can be in.
/// New = 0, Loading = 1, Running = 2, Stopping = 3, Stopped = 4
/// </summary>
/// <remarks>This enumeration is used to represent the lifecycle states of a process or operation, from
/// initialization to termination. The states include transitions such as loading, running, and stopping, providing a
/// clear representation of the process's current status.</remarks>
public enum CoreProcessState
{
    /// <summary>
    /// Represents the new state of an operation or process (only exists when program first boots).
    /// </summary>
    New,
    /// <summary>
    /// The current process is in the Loading state.
    /// </summary>
    Loading,
    /// <summary>
    /// Represents the running state of an operation or process.
    /// </summary>
    Running,
    /// <summary>
    /// Represents the state of an operation or process that is in the process of stopping.
    /// </summary>
    /// <remarks>This state typically indicates that the operation is transitioning from an active state to a
    /// stopped state. It may involve cleanup or shutdown tasks before the operation is fully stopped.</remarks>
    Stopping,
    /// <summary>
    /// Represents the state of an operation or process that has been stopped.
    /// </summary>
    Stopped
}

/// <summary>
/// Defines the contract for a core process, providing properties to retrieve its name and unique identifier, as well as
/// methods to manage its lifecycle through asynchronous start and stop operations.
/// </summary>
/// <remarks>Implementations of this interface are expected to handle the lifecycle of a core process, including
/// starting and stopping operations asynchronously. The <see cref="RequestStartAsync(CancellationToken)"/> and <see
/// cref="RequestStopAsync"/> methods allow for controlled initiation and termination of the process.</remarks>
public interface ICoreProcess
{
    /// <summary>
    /// Gets the name associated with the current CoreProcess
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Get the unique id for the current Core Process.
    /// </summary>
    Guid Guid { get; }

    /// <summary>
    /// Gets the current state of the core process.
    /// </summary>
    CoreProcessState State { get; }

    /// <summary>
    /// Initiates an asynchronous request to start the CoreProcess.
    /// </summary>
    /// <param name="shutdownToken">A <see cref="CancellationToken"/> that can be used to perform an immediate stop on the method.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task RequestStartAsync(CancellationToken shutdownToken);

    /// <summary>
    /// Requests the asynchronous termination of an ongoing CoreProcess.
    /// </summary>
    /// <remarks>This method signals the operation to stop gracefully. The actual termination  may not occur
    /// immediately, as it depends on the operation's ability to handle  the stop request. Callers should await the
    /// returned task to ensure the stop  process completes.</remarks>
    /// <returns>A <see cref="Task"/> that represents the asynchronous operation. The task completes when the stop request has
    /// been fully processed.</returns>
    Task RequestStopAsync();
}

