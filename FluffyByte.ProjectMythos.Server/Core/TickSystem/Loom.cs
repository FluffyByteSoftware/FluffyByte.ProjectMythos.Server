using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluffyByte.ProjectMythos.Server.Core.IO.Debug;

namespace FluffyByte.ProjectMythos.Server.Core.TickSystem;

/// <summary>
/// The Loom will oversee the in-game tick management.  
/// The Loomer will be responsible for ensuring that all game logic updates occur in a synchronized manner,
/// </summary>
public class Loom : CoreProcessBase
{
    /// <summary>
    /// Gets the name of the current object (Loom)
    /// </summary>
    public override string Name => "Loom";

    #region Local Variables
    private const int MOVEMENT_INTERVAL_MS = 50;         // 20 ticks/second
    private const int MESSAGING_INTERVAL_MS = 333;       // ~3 ticks/second
    private const int OBJECT_SPAWNING_INTERVAL_MS = 1000;    // 1 tick/second
    private const int OBJECT_CLEANUP_INTERVAL_MS = 1000;     // 1 tick/second
    private const int COMBAT_INTERVAL_MS = 100;          // 10 ticks/second
    private const int WORLD_SIMULATION_INTERVAL_MS = 10000;  // 0.1 ticks/second (every 10 seconds)
    private const int AUTO_SAVE_INTERVAL_MS = 60000;     // 0.0167 ticks/second (every 60 seconds)

    private ulong _movementTickCount = 0;
    private ulong _messagingTIckCount = 0;
    private ulong _objectSpawningTickCount = 0;
    private ulong _objectCleanUpTickCount = 0;
    private ulong _combatTickCount = 0;
    private ulong _worldSimulationTickCount = 0;
    private ulong _autoSaveTickCount = 0;

    private readonly Weaver weaverRef = Conductor.Instance.Weaver;

    private Task? _movementTickTask;
    private Task? _messagingTIckTask;
    private Task? _objectSpawningTickTask;
    private Task? _objectCleanupTickTask;
    private Task? _combatTickTask;
    private Task? _worldSimulationTickTask;
    private Task? _autoSaveTickTask;

    private readonly Dictionary<TickType, Stopwatch> _tickTimers = [];
    private readonly Dictionary<TickType, double> _averageTickTimes = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="Loom"/> class, setting up tick timers and average tick times for
    /// all tick types.
    /// </summary>
    /// <remarks>This constructor initializes internal timers and average tick time tracking for each tick
    /// type defined in the <see cref="TickType"/> enumeration. The <paramref name="shutdownToken"/> is passed to the
    /// base class to handle shutdown operations.</remarks>
    /// <param name="shutdownToken">A <see cref="CancellationToken"/> that signals when the Loom instance should shut down.</param>
    public Loom(CancellationToken shutdownToken)
        : base(shutdownToken)
    {
        foreach (TickType tickType in Enum.GetValues(typeof(TickType)))
        {
            _tickTimers[tickType] = new Stopwatch();
            _averageTickTimes[tickType] = 0.0;
        }
    }

    #endregion

    /// <summary>
    /// Starts the asynchronous operation for the current instance.
    /// </summary>
    /// <remarks>This method initiates the necessary processes or tasks required to start the operation
    /// asynchronously.  Override this method to implement custom startup logic for derived classes.</remarks>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public override async Task StartAsync()
    {
        Scribe.Info($"[{Name}] I am forming the weave of fate...");

        _movementTickCount = 0;
        _messagingTIckCount = 0;
        _objectSpawningTickCount = 0;
        _objectCleanUpTickCount = 0;
        _combatTickCount = 0;
        _worldSimulationTickCount = 0;
        _autoSaveTickCount = 0;

        _movementTickTask = Task.Run(() => MovementTickLoop(), _shutdownToken);
        _messagingTIckTask = Task.Run(() => MessagingTickLoop(), _shutdownToken);
        _objectSpawningTickTask = Task.Run(() => ObjectSpawningTickLoop(), _shutdownToken);
        _objectCleanupTickTask = Task.Run(() => ObjectCleanupTickLoop(), _shutdownToken);
        _combatTickTask = Task.Run(() => CombatTickLoop(), _shutdownToken);
        _worldSimulationTickTask = Task.Run(() => WorldSimulationTickLoop(), _shutdownToken);
        _autoSaveTickTask = Task.Run(() => AutoSaveTickLoop(), _shutdownToken);

        Scribe.Info($"[{Name}] The weave has now formed.");

        await Task.CompletedTask;
    }

    /// <summary>
    /// Stops the asynchronous operation and performs any necessary cleanup.
    /// </summary>
    /// <remarks>This method is typically called to gracefully shut down the service or operation.  Ensure
    /// that any resources used by the operation are properly released during the stop process.</remarks>
    /// <returns>A <see cref="Task"/> that represents the asynchronous stop operation.</returns>
    public override async Task StopAsync()
    {
        Scribe.Info($"[{Name}] The weave is unraveling!");

        try
        {
            await Task.WhenAll(
                _movementTickTask ?? Task.CompletedTask,
                _messagingTIckTask ?? Task.CompletedTask,
                _objectSpawningTickTask ?? Task.CompletedTask,
                _objectCleanupTickTask ?? Task.CompletedTask,
                _combatTickTask ?? Task.CompletedTask,
                _worldSimulationTickTask ?? Task.CompletedTask,
                _autoSaveTickTask ?? Task.CompletedTask
                );

            Scribe.Info($"[{Name}] All tick loops have been stopped.");
        }
        catch (Exception ex)
        {
            Scribe.Error(ex);
        }
    }

    #region TickLoops
    private async Task MovementTickLoop()
    {
        Scribe.Debug($"[{Name}] Movement tick loop started (20 ticks/second)");

        while (!_shutdownToken.IsCancellationRequested)
        {
            _tickTimers[TickType.Movement].Restart();

            try
            {
                _movementTickCount++;

                // TODO: Process player movement
                // TODO: Process NPC movement
                // TODO: Update positions in world state

                // Weaver will handle broadcasting state changes
                await Weaver.BroadcastMovementUpdatesAsync();

                // Log every 20 ticks (once per second) for performance monitoring
                if (_movementTickCount % 20 == 0)
                {
                    Scribe.Debug($"[{Name}] Movement tick #{_movementTickCount} completed in {_tickTimers[TickType.Movement].Elapsed.TotalMilliseconds:F2}ms");
                }
            }
            catch (Exception ex)
            {
                Scribe.Error(ex);
            }

            _tickTimers[TickType.Movement].Stop();
            UpdateAverageTickTime(TickType.Movement, _tickTimers[TickType.Movement].Elapsed.TotalMilliseconds);

            // Wait for next tick (compensate for processing time)
            int elapsedMs = (int)_tickTimers[TickType.Movement].Elapsed.TotalMilliseconds;
            int remainingMs = Math.Max(0, MOVEMENT_INTERVAL_MS - elapsedMs);

            if (remainingMs > 0)
            {
                await Task.Delay(remainingMs, _shutdownToken);
            }
        }

        Scribe.Debug($"[{Name}] Movement tick loop stopped.");
    }

    private async Task MessagingTickLoop()
    {
        Scribe.Debug($"[{Name}] Messaging tick loop started (~3 ticks/second)");

        while (!_shutdownToken.IsCancellationRequested)
        {
            _tickTimers[TickType.Messaging].Restart();
            try
            {
                _messagingTIckCount++;
                await Weaver.ProcessMessagingQueueAsync();

                if (_messagingTIckCount % 10 == 0)
                {
                    Scribe.Debug($"[{Name}] Messaging tick #{_messagingTIckCount} completed in " +
                        $"{_tickTimers[TickType.Messaging].Elapsed.TotalMilliseconds:F2}ms");
                }
            }
            catch (Exception ex)
            {
                Scribe.Error(ex);
            }

            _tickTimers[TickType.Messaging].Stop();
            UpdateAverageTickTime(TickType.Messaging, _tickTimers[TickType.Messaging].Elapsed.TotalMilliseconds);

            int elapsedMs = (int)_tickTimers[TickType.Messaging].Elapsed.TotalMilliseconds;
        }
    }

    private async Task ObjectSpawningTickLoop()
    {
        Scribe.Debug($"[{Name}] Object spawning tick loop started (1 tick/second)");

        while (!_shutdownToken.IsCancellationRequested)
        {
            _tickTimers[TickType.ObjectSpawning].Restart();

            try
            {
                _objectSpawningTickCount++;

                await Weaver.BroadcastObjectSpawnsAsync();

                Scribe.Debug($"[{Name}] Object spawning tick #{_objectSpawningTickCount} completed in " +
                    $"{_tickTimers[TickType.ObjectSpawning].Elapsed.TotalMilliseconds:F2}ms");
            }
            catch (Exception ex)
            {
                Scribe.Error(ex);
            }

            _tickTimers[TickType.ObjectSpawning].Stop();
            UpdateAverageTickTime(TickType.Messaging, _tickTimers[TickType.ObjectSpawning].Elapsed.TotalMilliseconds);

            int elapsedMs = (int)_tickTimers[TickType.ObjectSpawning].Elapsed.TotalMilliseconds;
            int remainingMs = Math.Max(0, OBJECT_SPAWNING_INTERVAL_MS - elapsedMs);

            if (remainingMs > 0)
            {
                await Task.Delay(remainingMs, _shutdownToken);
            }
        }
        Scribe.Debug($"[{Name}] Object spawning tick loop stopped.");
    }

    private async Task ObjectCleanupTickLoop()
    {
        Scribe.Debug($"[{Name}] Object cleanup tick loop started (1 tick/second)");

        while (!_shutdownToken.IsCancellationRequested)
        {
            _tickTimers[TickType.ObjectCleanup].Restart();
        }

        try
        {
            _objectCleanUpTickCount++;

            await Weaver.BroadcastObjectCleanupsAsync();

            Scribe.Debug($"[{Name}] Object cleanup tick #{_objectCleanUpTickCount} completed in " +
                $"{_tickTimers[TickType.ObjectCleanup].Elapsed.TotalMilliseconds:F2}ms");
        }
        catch (Exception ex)
        {
            Scribe.Error(ex);
        }

        _tickTimers[TickType.ObjectCleanup].Stop();

        UpdateAverageTickTime(TickType.ObjectCleanup, _tickTimers[TickType.ObjectCleanup].Elapsed.TotalMilliseconds);

        int elapsedMs = (int)_tickTimers[TickType.ObjectCleanup].Elapsed.TotalMilliseconds;
        int remainingMs = Math.Max(0, OBJECT_CLEANUP_INTERVAL_MS - elapsedMs);
        if (remainingMs > 0)
        {
            await Task.Delay(remainingMs, _shutdownToken);
        }
        Scribe.Debug($"[{Name}] Object cleanup tick loop stopped.");
    }

    private async Task CombatTickLoop()
    {
        Scribe.Debug($"[{Name}] Combat tick loop started (10 ticks/second)");
        while (!_shutdownToken.IsCancellationRequested)
        {
            _tickTimers[TickType.Combat].Restart();
            try
            {
                _combatTickCount++;
                await Weaver.ProcessCombatUpdatesAsync();
                if (_combatTickCount % 10 == 0)
                {
                    Scribe.Debug($"[{Name}] Combat tick #{_combatTickCount} completed in " +
                        $"{_tickTimers[TickType.Combat].Elapsed.TotalMilliseconds:F2}ms");
                }
            }
            catch (Exception ex)
            {
                Scribe.Error(ex);
            }
            _tickTimers[TickType.Combat].Stop();
            UpdateAverageTickTime(TickType.Combat, _tickTimers[TickType.Combat].Elapsed.TotalMilliseconds);
            int elapsedMs = (int)_tickTimers[TickType.Combat].Elapsed.TotalMilliseconds;
            int remainingMs = Math.Max(0, COMBAT_INTERVAL_MS - elapsedMs);
            if (remainingMs > 0)
            {
                await Task.Delay(remainingMs, _shutdownToken);
            }
        }
        Scribe.Debug($"[{Name}] Combat tick loop stopped.");
    }

    private async Task WorldSimulationTickLoop()
    {
        Scribe.Debug($"[{Name}] World simulation tick loop started (every 10 seconds)");
        while (!_shutdownToken.IsCancellationRequested)
        {
            _tickTimers[TickType.WorldSimulation].Restart();
            try
            {
                _worldSimulationTickCount++;
                await Weaver.UpdateWorldSimulationAsync();
                Scribe.Debug($"[{Name}] World simulation tick #{_worldSimulationTickCount} completed in " +
                    $"{_tickTimers[TickType.WorldSimulation].Elapsed.TotalMilliseconds:F2}ms");
            }
            catch (Exception ex)
            {
                Scribe.Error(ex);
            }
            _tickTimers[TickType.WorldSimulation].Stop();
            UpdateAverageTickTime(TickType.WorldSimulation, _tickTimers[TickType.WorldSimulation].Elapsed.TotalMilliseconds);
            int elapsedMs = (int)_tickTimers[TickType.WorldSimulation].Elapsed.TotalMilliseconds;
            int remainingMs = Math.Max(0, WORLD_SIMULATION_INTERVAL_MS - elapsedMs);
            if (remainingMs > 0)
            {
                await Task.Delay(remainingMs, _shutdownToken);
            }
        }
        Scribe.Debug($"[{Name}] World simulation tick loop stopped.");
    }

    private async Task AutoSaveTickLoop()
    {
        Scribe.Debug($"[{Name}] Auto-save tick loop started (every 60 seconds)");
        while (!_shutdownToken.IsCancellationRequested)
        {
            _tickTimers[TickType.AutoSave].Restart();
            try
            {
                _autoSaveTickCount++;
                await Weaver.PerformAutoSaveAsync();
                Scribe.Debug($"[{Name}] Auto-save tick #{_autoSaveTickCount} completed in " +
                    $"{_tickTimers[TickType.AutoSave].Elapsed.TotalMilliseconds:F2}ms");
            }
            catch (Exception ex)
            {
                Scribe.Error(ex);
            }
            _tickTimers[TickType.AutoSave].Stop();

            UpdateAverageTickTime(TickType.AutoSave, _tickTimers[TickType.AutoSave].Elapsed.TotalMilliseconds);

            int elapsedMs = (int)_tickTimers[TickType.AutoSave].Elapsed.TotalMilliseconds;
            int remainingMs = Math.Max(0, AUTO_SAVE_INTERVAL_MS - elapsedMs);
            
            if (remainingMs > 0)
            {
                await Task.Delay(remainingMs, _shutdownToken);
            }
        }
        Scribe.Debug($"[{Name}] Auto-save tick loop stopped.");
    }
    #endregion
    
    #region Performance Monitoring
    private void UpdateAverageTickTime(TickType tickType, double tickTimeMs)
    {
        _averageTickTimes[tickType] = (_averageTickTimes[tickType] * 0.9) + (tickTimeMs * 0.1);
    }

    /// <summary>
    /// Retrieves the average tick time for the specified tick type.
    /// </summary>
    /// <param name="tickType">The type of tick for which to retrieve the average time.</param>
    /// <returns>The average tick time as a <see cref="double"/>. Returns 0.0 if the specified tick type is not found.</returns>
    public double GetAverageTickTime(TickType tickType)
    {
        return _averageTickTimes.TryGetValue(tickType, out var avgTime) ? avgTime : 0.0;
    }
    #endregion
}
