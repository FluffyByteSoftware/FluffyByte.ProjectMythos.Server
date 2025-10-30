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

        var definitions = _weaverRef.GetTickDefinitions();

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
            if(_tickTasks.Count > 0)
            {
                await Task.WhenAll(_tickTasks.Values);
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
        catch(Exception ex)
        {
            Scribe.Error(ex);
        }

        await Task.CompletedTask;
    }
    #endregion

    private readonly Weaver _weaverRef = Conductor.Instance.Weaver;

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
    private async Task TickLoop(TickType tickType, int intervalMs)
    {
        Stopwatch stopwatch = _tickTimers[tickType];

        while (!_shutdownToken.IsCancellationRequested)
        {
            stopwatch.Restart();

            try
            {
                var count = _tickCounters[tickType]++;
            }
            catch (TaskCanceledException)
            {
                Scribe.Debug($"[{Name}] Tick loop for {tickType} canceled via shutdown token during processing.");
                break;
            }
            catch (OperationCanceledException)
            {
                Scribe.Debug($"[{Name}] Tick loop for {tickType} canceled via shutdown token.");
            }
            catch(Exception ex)
            {
                Scribe.Error(ex);
            }

            try
            {
                await Task.Delay(intervalMs, _shutdownToken);
            }
            catch(TaskCanceledException)
            {
                Scribe.Debug($"[{Name}] Tick loop for {tickType} canceled via shutdown token during delay.");
                break;
            }
            catch(Exception ex)
            {
                Scribe.Error(ex);
            }
        }
    }

    private void UpdateAverageTickTime(TickType tickType, double elapsedMs)
    {
        double current = _avgTickTimesMs[tickType];
        _avgTickTimesMs[tickType] = (current * (1.0 - AverageAlpha)) + (elapsedMs * AverageAlpha);
    }

}