using System.Diagnostics;
using FluffyByte.ProjectMythos.Server.Core.IO.Debug;
using FluffyByte.ProjectMythos.Server.Core.IO.Networking;

namespace FluffyByte.ProjectMythos.Server.Core;

/// <summary>
/// Represents a singleton class responsible for managing the lifecycle of application processes, including startup,
/// shutdown, and monitoring operations.
/// </summary>
/// <remarks>The <see cref="Conductor"/> class provides a globally accessible instance that ensures only one
/// instance of the class is created. It manages the application's processes, monitors their state through the <see
/// cref="Watcher"/>, and handles shutdown signals using the <see cref="ShutdownToken"/>. This class is thread-safe and
/// designed to coordinate the application's core operations.</remarks>
public class Conductor
{
    private readonly static Lazy<Conductor> _instance = new(() => new());
    
    /// <summary>
    /// Gets the singleton instance of the <see cref="Conductor"/> class.
    /// </summary>
    /// <remarks>This property ensures that only one instance of the <see cref="Conductor"/> class is created 
    /// and provides global access to it. The instance is initialized in a thread-safe manner.</remarks>
    public static Conductor Instance => _instance.Value;

    /// <summary>
    /// Gets the list of processes that have been launched.
    /// </summary>
    public List<ICoreProcess> LaunchedProcesses { get; private set; } = [];

    /// <summary>
    /// Gets the sentinel instance used to monitor and manage the state of the system.
    /// </summary>
    public Sentinel Sentinel { get; private set; }
    
    /// <summary>
    /// Gets the <see cref="Watcher"/> instance associated with this object.
    /// </summary>
    public Watcher Watcher { get; private set; }

    /// <summary>
    /// Gets the <see cref="CancellationToken"/> that is triggered when the application is shutting down.
    /// </summary>
    public CancellationToken ShutdownToken { get; private set; }

    /// <summary>
    /// Raw list of all processes to start.
    /// </summary>
    private readonly List<ICoreProcess> _processList = [];


    /// <summary>
    /// Initializes a new instance of the <see cref="Conductor"/> class.
    /// </summary>
    /// <remarks>This constructor initializes the <see cref="ShutdownToken"/> and sets up the <see
    /// cref="Sentinel"/>  and its associated <see cref="Watcher"/>. The <see cref="ShutdownToken"/> is used to manage 
    /// cancellation signals for the conductor's operations.</remarks>
    private Conductor() 
    {
        ShutdownToken = new();

        Sentinel = new(ShutdownToken);

        Watcher = Sentinel.Watcher;

        _processList.Add(Sentinel);
    }

    /// <summary>
    /// Initiates the startup process for the application, preparing necessary resources and handling any potential
    /// errors.
    /// </summary>
    /// <remarks>This method initializes a new shutdown token and performs any required startup tasks
    /// asynchronously. If an exception occurs during the startup process, it is logged for diagnostic
    /// purposes.</remarks>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task RequestStartupAsync()
    {
        ShutdownToken = new();

        try
        {
            foreach(ICoreProcess process in _processList)
            {
                await process.RequestStartAsync(ShutdownToken);
                LaunchedProcesses.Add(process);

                if(process.Name == "Sentinel")
                {
                    Watcher = Sentinel.Watcher;
                }
            }
        }
        catch(Exception ex)
        {
            Scribe.Error(ex);
        }

    }

    /// <summary>
    /// Initiates an asynchronous shutdown process, stopping all launched processes and clearing their references.
    /// </summary>
    /// <remarks>This method signals a shutdown by canceling the associated shutdown token and attempts to
    /// stop all processes that were previously launched. If any processes fail to stop, an exception is thrown, and the
    /// details of the stuck processes are logged. The method ensures that the list of launched processes is cleared
    /// after the shutdown attempt.</remarks>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException">Thrown if one or more processes fail to stop during the shutdown process.</exception>
    public async Task RequestShutdownAsync()
    {
        try
        {
            Scribe.Info("[Conductor] Initiating shutdown...");

            // Signal cancellation
            CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(ShutdownToken);

            await cts.CancelAsync();

            var processesToStop = LaunchedProcesses.ToList();  

            Scribe.Info($"[Conductor] Stopping {processesToStop.Count} processes...");

            foreach (ICoreProcess process in processesToStop)
            {
                Scribe.Debug($"[Conductor] Stopping: {process.Name}");
                await process.RequestStopAsync();
            }

            // Now clear the original list
            LaunchedProcesses.Clear();

            // Check for processes that failed to stop
            var stillRunning = processesToStop.Where(p =>
                p.State != CoreProcessState.Stopped &&
                p.State != CoreProcessState.Stopping).ToList();

            if (stillRunning.Count > 0)
            {
                Scribe.Error($"[Conductor] Unable to stop all processes. Remaining: {stillRunning.Count}");

                foreach (ICoreProcess process in stillRunning)
                {
                    Scribe.Error($"[Conductor] Stuck process: {process.Name} ({process.Guid}) State: {process.State}");
                }

                throw new InvalidOperationException($"Failed to stop {stillRunning.Count} processes");
            }

            Scribe.Info("[Conductor] Shutdown complete");
        }
        catch (Exception ex)
        {
            Scribe.Error(ex);
        }
    }
}
