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
            }
        }
        catch(Exception ex)
        {
            Scribe.Error(ex);
        }

    }

    /// <summary>
    /// Requests a graceful shutdown of the application.
    /// </summary>
    /// <remarks>This method initiates an asynchronous shutdown process, allowing the application to complete
    /// any ongoing tasks  before terminating. Ensure that all necessary cleanup operations are handled during the
    /// shutdown process.</remarks>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task RequestShutdownAsync()
    {
        try
        {
            CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(ShutdownToken);

            await cts.CancelAsync();

            foreach(ICoreProcess process in LaunchedProcesses)
            {
                await process.RequestStopAsync();
                LaunchedProcesses.Remove(process);
            }

            if(LaunchedProcesses.Count > 0)
            {
                Scribe.Error($"[Conductor] I was unable to release all processes during shutdown. Remaining: {LaunchedProcesses.Count}");

                foreach(ICoreProcess process in LaunchedProcesses)
                {
                    Scribe.Error($"[Conductor] Remaining Process: {process.Name} ({process.Guid}) State: {process.State}");
                }


                throw new IndexOutOfRangeException(nameof(LaunchedProcesses));
            }
        }
        catch(Exception ex)
        {
            Scribe.Error(ex);
        }
    }
}
