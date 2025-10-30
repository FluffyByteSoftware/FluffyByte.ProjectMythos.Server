using System.Collections.Generic;
using System;
using System.Threading.Tasks;
using System.Diagnostics;
using FluffyByte.Tools;
using FluffyByte.ProjectMythos.Server.Core;

namespace FluffyByte.ProjectMythos.Server.Core.TickSystem;

/// <summary>
/// Represents the Loom process, which manages and executes periodic tasks based on predefined tick types.
/// </summary>
/// <remarks>The Loom process is responsible for coordinating and executing tasks at specific intervals, as
/// defined by the tick types. It is initialized with a shutdown token to handle graceful termination.</remarks>
public class Loom(CancellationToken shutdownToken): CoreProcessBase(shutdownToken)
{
    #region Necessary CoreProcessBase Implementations
    /// <summary>
    /// The name of the CoreProcessBase (Loom)
    /// </summary>
    public override string Name => "Loom";

    /// <summary>
    /// Starts the asynchronous operation.
    /// </summary>
    /// <remarks>This method initiates the operation and completes immediately. Override this method to
    /// provide custom startup logic.</remarks>
    public override async Task StartAsync()
    {
        Scribe.Debug($"[{Name}] Forming the weave (initializing tick loops)...");

        if(Conductor.Instance.Weaver == null)
        {
            Scribe.Critical($"[{Name}] Weaver instance is null. Loom will remain idle.");
            return;
        }

        var definitions = Conductor.Instance.Weaver.GetTickDefinitions();

        if(definitions.Count == 0)
        {
            Scribe.Warn($"[{Name}] Weaver returned no tick definitions. Loom will remain idle.");

            await Task.CompletedTask;
            return;
        }

        foreach(KeyValuePair<TickType, int> kvp in definitions)
        {
            TickType tickType = kvp.Key;
            int intervalMs = kvp.Value;

            if (!_tickTimers.ContainsKey(tickType))
                _tickTimers[tickType] = new();

            if (!_avgTickTimesMs.ContainsKey(tickType))
                _avgTickTimesMs[tickType] = 0.0;

            if(!_tickCounters.ContainsKey(tickType))
                _tickCounters[tickType] = 0;

            // Spin the loop as a long-running Task bound to the shared shutdown Token
            // Each loop is independent, which prevents one slow tick type from blocking others
            _tickTasks[tickType] = Task.Run(() => TickLoop(tickType, intervalMs), _shutdownToken);
        }

        Scribe.Debug($"[{Name}] The weave has formed with {definitions.Count} tick patterns.");
        await Task.CompletedTask;
    }

    /// <summary>
    /// Stops the asynchronous operation.
    /// </summary>
    /// <remarks>This method is intended to be overridden in a derived class to implement custom stop logic.
    /// The default implementation completes immediately.</remarks>
    public override async Task StopAsync()
    {
        Scribe.Debug($"[{Name}] Unraveling the weave (stopping tick loops)...");

        try
        {
            CancellationTokenSource _shutdownTokenSource = CancellationTokenSource.CreateLinkedTokenSource(_shutdownToken);

            if (_shutdownTokenSource is not null && !_shutdownTokenSource.IsCancellationRequested)
                _shutdownTokenSource.Cancel();

            // Wait a brief grace period for tasks that respect the token.
            var allTasks = _tickTasks.Values.ToArray();
            var stopTimeout = Task.Delay(2000); // 2s grace window

            var completed = await Task.WhenAny(Task.WhenAll(allTasks), stopTimeout);
            if (completed == stopTimeout)
            {
                Scribe.Debug($"[{Name}] Some tick loops did not shut down within timeout.");
            }

            Scribe.Debug($"[{Name}] All tick loops stopped cleanly.");
        }
        catch (TaskCanceledException)
        {
            Scribe.Debug($"[{Name}] Tick loops canceled via shutdown token during stop.");
        }
        catch (OperationCanceledException)
        {
            Scribe.Debug($"[{Name}] Tick loops canceled via shutdown token.");
        }
        catch (Exception ex)
        {
            Scribe.Error(ex);
        }
    }

    #endregion   

    private readonly Dictionary<TickType, Task> _tickTasks = [];

    private readonly Dictionary<TickType, Stopwatch> _tickTimers = [];
    private readonly Dictionary<TickType, double> _avgTickTimesMs = [];
    private readonly Dictionary<TickType, ulong> _tickCounters = [];

    /// <summary>
    /// Smoothing factor for moving average (closer to 1.0 = more weight on recent samples).
    /// </summary>
    private const double AverageAlpha = 0.10;


    /// <summary>
    /// Retrieves the average tick time, in milliseconds, for the specified tick type.
    /// </summary>
    /// <remarks>This method returns <see langword="0.0"/> if the specified tick type is not found in the
    /// collection.</remarks>
    /// <param name="tickType">The type of tick for which to retrieve the average time.</param>
    /// <returns>The average tick time in milliseconds if the specified tick type exists; otherwise, <see langword="0.0"/>.</returns>
    public double GetAverageTickTime(TickType tickType)
    {
        return _avgTickTimesMs.TryGetValue(tickType, out var avg) ? avg : 0.0;
    }

    /// <summary>
    /// The per-TickType timing loop:
    /// - waits the prescribed interval
    /// - measures execution time
    /// - updates moving average
    /// - calls Weaver.ProcessTick(tickType, count)
    /// </summary>
    /// <summary>
    /// The per-TickType timing loop:
    /// - waits the prescribed interval
    /// - measures execution time
    /// - updates moving average
    /// - calls Weaver.ProcessTick(tickType)
    /// </summary>
    private async Task TickLoop(TickType tickType, int intervalMs)
    {
        var weaver = Conductor.Instance.Weaver;

        if (weaver == null)
        {
            Scribe.Critical($"[{Name}] Weaver reference is null inside tick loop for {tickType}. Loop will exit.");
            return;
        }

        Stopwatch stopwatch = _tickTimers[tickType];

        while (!_shutdownToken.IsCancellationRequested)
        {
            stopwatch.Restart();

            try
            {
                // Execute the tick logic in the Weaver
                await weaver.ProcessTick(tickType);
                _tickCounters[tickType]++;

                // Measure execution time
                stopwatch.Stop();
                UpdateAverageTickTime(tickType, stopwatch.Elapsed.TotalMilliseconds);

                Scribe.Debug($"[{Name}] Tick {tickType} processed in {stopwatch.Elapsed.TotalMilliseconds:F2} ms (avg {_avgTickTimesMs[tickType]:F2} ms)");
            }
            catch (TaskCanceledException)
            {
                Scribe.Debug($"[{Name}] Tick loop for {tickType} canceled via shutdown token during processing.");
                break;
            }
            catch (OperationCanceledException)
            {
                Scribe.Debug($"[{Name}] Tick loop for {tickType} canceled via shutdown token.");
                break;
            }
            catch (Exception ex)
            {
                Scribe.Error($"[{Name}] Exception during {tickType} tick: {ex}");
            }

            try
            {
                // Wait until the next interval
                await Task.Delay(intervalMs, _shutdownToken);
            }
            catch (TaskCanceledException)
            {
                Scribe.Debug($"[{Name}] Tick loop for {tickType} canceled during delay.");
                break;
            }
            catch (Exception ex)
            {
                Scribe.Error($"[{Name}] Exception in delay loop for {tickType}: {ex.Message}");
            }
        }

        Scribe.Debug($"[{Name}] Tick loop for {tickType} exited cleanly.");
    }


    private void UpdateAverageTickTime(TickType tickType, double elapsedMs)
    {
        double current = _avgTickTimesMs[tickType];
        _avgTickTimesMs[tickType] = (current * (1.0 - AverageAlpha)) + (elapsedMs * AverageAlpha);
    }

}