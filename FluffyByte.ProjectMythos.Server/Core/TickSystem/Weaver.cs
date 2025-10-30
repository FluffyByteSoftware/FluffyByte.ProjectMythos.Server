// File: Weaver.cs
// Project: FluffyByte.ProjectMythos.Server.Core
// Purpose: The "conductor" of world updates. Executes tick-based logic registered by an external game library.
// Notes:
// - The Weaver owns the registry of TickProcessors (handlers for each TickType).
// - A game library (DLL) will register its processors via RegisterTickProcessor() during initialization.
// - The Loom will call ProcessTick() for each TickType based on timing data returned by GetTickDefinitions().

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluffyByte.ProjectMythos.Server.Core.IO;
using FluffyByte.ProjectMythos.Server.Core.IO.Networking;
using FluffyByte.ProjectMythos.Server.Core.IO.Networking.FluffyClient;
using FluffyByte.Tools;

namespace FluffyByte.ProjectMythos.Server.Core.TickSystem
{
    /// <summary>
    /// The Weaver executes game logic bound to tick events fired by the Loom.
    /// It is agnostic to what game is running; game modules plug in their logic via registration.
    /// </summary>
    public sealed class Weaver : CoreProcessBase
    {
        /// <summary>
        /// Name of the Weaver process.
        /// </summary>
        public override string Name => "Weaver";

        private readonly Dictionary<TickType, TickProcessor> _tickProcessors = [];
        private readonly object _registryLock = new();
        private readonly Dictionary<TickType, ulong> _tickCounters = [];

        private const string GAME_ASSEMBLY_PATH = "FluffyByte.MythosGame.dll";

        /// <summary>
        /// Initializes a new instance of the <see cref="Weaver"/> class.
        /// </summary>
        /// <param name="shutdownToken">Reference ot the shutdown cancellation token held by the conductor</param>
        public Weaver(CancellationToken shutdownToken) : base(shutdownToken)
        {
            // At startup, Weaver will either auto-load a game module DLL or wait for manual registration.
            TryAutoLoadGameModule();
        }

        #region Lifecycle

        /// <summary>
        /// Starts the Weaver. Typically invoked by the Conductor during server boot.
        /// </summary>
        public override async Task StartAsync()
        {
            Scribe.Info("[Weaver] Waking the threads of the world...");

            lock (_registryLock)
            {
                // Optionally clear and reload processors to ensure a clean state.
                _tickProcessors.Clear();
            }

            // Reload any available game module (optional if already loaded via constructor)
            TryAutoLoadGameModule();

            Scribe.Info("[Weaver] All systems initialized and ready for the Loom's call.");
            await Task.CompletedTask;
        }

        /// <summary>
        /// Stops the Weaver gracefully. Called when the server is shutting down.
        /// </summary>
        public override async Task StopAsync()
        {
            Scribe.Info("[Weaver] Gently unwinding the threads of fate...");

            lock (_registryLock)
            {
                _tickProcessors.Clear();
            }

            // Simulate cleanup delay or resource release.
            await Task.Delay(50);

            Scribe.Info("[Weaver] All processors cleared. The weave is at rest.");
        }

        #endregion

        #region Registration API

        /// <summary>
        /// Registers a tick processor provided by a game module. Overwrites any existing entry for the same TickType.
        /// </summary>
        public void RegisterTickProcessor(
            TickType tickType,
            int intervalMs,
            Func<bool>? hasPending,
            Func<IList>? flushPending,
            Func<IList, Task>? processBatchAsync)
        {
            lock (_registryLock)
            {
                _tickProcessors[tickType] = new TickProcessor(
                    intervalMs,
                    hasPending ?? (() => true),
                    flushPending ?? (() => new List<object>()),
                    processBatchAsync ?? (_ => Task.CompletedTask)
                );

                Scribe.Info($"[Weaver] Registered tick processor for {tickType} ({intervalMs} ms).");
            }
        }

        /// <summary>
        /// Called by the Loom to discover which ticks should run and at what intervals.
        /// </summary>
        public IReadOnlyDictionary<TickType, int> GetTickDefinitions()
        {
            lock (_registryLock)
            {
                return _tickProcessors.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.IntervalMs);
            }
        }

        #endregion

        #region Execution
        /// <summary>
        /// Processes a single tick of the specified type, executing any registered handlers and broadcasting the tick
        /// to connected vessels.
        /// </summary>
        /// <remarks>This method performs the following operations: <list type="bullet"> <item>Maintains
        /// an internal counter for the specified tick type.</item> <item>Executes any registered handlers for the tick
        /// type, if pending tasks exist.</item> <item>Broadcasts the tick information to connected and authenticated
        /// vessels via UDP, if applicable.</item> </list> Exceptions during handler execution or broadcasting are
        /// logged but do not interrupt the overall processing flow.</remarks>
        /// <param name="tickType">The type of tick to process. This determines the associated handlers and the tick's context.</param>
        public async Task ProcessTick(TickType tickType)
        {
            // Maintain per-tick counter safely
            if (!_tickCounters.ContainsKey(tickType))
                _tickCounters[tickType] = 0;

            ulong tickCount = ++_tickCounters[tickType];

            Scribe.Info($"[Weaver] Processing tick: {tickType}");

            TickProcessor? processor;
            lock (_registryLock)
            {
                if (!_tickProcessors.TryGetValue(tickType, out processor))
                    return; // no handler registered for this tick type
            }

            // Run processor only if it has pending work
            if (processor.HasPending())
            {
                var batch = processor.FlushPending();
                try
                {
                    await processor.ProcessBatchAsync(batch);
                }
                catch (Exception ex)
                {
                    Scribe.Error($"[Weaver] Exception in {tickType} tick: {ex.Message}");
                }
            }

            // Always broadcast tick (even if processor didn't run)
            try
            {
                var sentinel = Conductor.Instance.Sentinel;
                if (sentinel?.Watcher == null)
                {
                    Scribe.Critical("[Weaver] ProcessTick() called but Conductor.Instance.Sentinel.Watcher is null.");
                    return;
                }

                // Build packet once; reuse for all vessels
                byte[] packet = BuildTickPacket(tickType, tickCount);

                // Snapshot current vessels to avoid concurrent modifications
                List<Vessel> vessels = [.. sentinel.Watcher.Vessels];

                if(vessels.Count <= 0)
                {
                    Scribe.Warn($"[Weaver] No vessels connected to broadcast {tickType} tick {tickCount}.");
                }

                int sentCount = 0;
                foreach (var vessel in vessels)
                {
                    if (!vessel.IsAuthenticated || vessel.Disconnecting)
                        continue;

                    // Fire-and-forget; UDP send is non-blocking
                    _ = vessel.UdpIO.SendAsync(packet);
                    sentCount++;
                }

                Scribe.Debug($"[Weaver] Broadcasted {tickType} tick {tickCount} to {sentCount} authenticated vessel(s).");
            }
            catch (Exception ex)
            {
                Scribe.Warn($"[Weaver] Failed to broadcast tick for {tickType}: {ex.Message}");
            }
        }


        /// <summary>
        /// Builds a binary tick packet with fixed layout:
        /// [1 byte type][4 bytes tickType][8 bytes tickCount][8 bytes timestamp]
        /// </summary>
        private static byte[] BuildTickPacket(TickType tickType, ulong tickCount)
        {
            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            byte[] packet = new byte[21];

            packet[0] = 1; // Packet type: TICK
            BitConverter.GetBytes((int)tickType).CopyTo(packet, 1);
            BitConverter.GetBytes(tickCount).CopyTo(packet, 5);
            BitConverter.GetBytes(timestamp).CopyTo(packet, 13);

            // Enforce consistent little-endian byte order for cross-platform compatibility
            if (!BitConverter.IsLittleEndian)
            {
                Array.Reverse(packet, 1, 4);
                Array.Reverse(packet, 5, 8);
                Array.Reverse(packet, 13, 8);
            }

            return packet;
        }
        #endregion

        #region Module Loading

        /// <summary>
        /// Attempts to auto-load a game library DLL implementing IGameModule.
        /// </summary>
        private void TryAutoLoadGameModule()
        {
            try
            {
                // You can later externalize this to config (path or module name).
                string gameAssemblyPath = GAME_ASSEMBLY_PATH;

                if (!System.IO.File.Exists(gameAssemblyPath))
                {
                    Scribe.Warn($"[Weaver] No game module DLL found at {gameAssemblyPath}. Skipping auto-load.");
                    return;
                }

                var assembly = Assembly.LoadFrom(gameAssemblyPath);
                var moduleType = assembly.GetTypes().FirstOrDefault(t => typeof(IGameModule).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

                if (moduleType == null)
                {
                    Scribe.Warn($"[Weaver] No class implementing IGameModule found in {gameAssemblyPath}.");
                    Scribe.Warn($"Path was null: {gameAssemblyPath}");
                    return;
                }

                var module = (IGameModule)Activator.CreateInstance(moduleType)!;
                module.Initialize(this); // let the game module register its ticks
                Scribe.Info($"[Weaver] Loaded game module: {module.GameName}");
            }
            catch (Exception ex)
            {
                Scribe.Error($"[Weaver] Failed to load game module: {ex.Message}");
            }
        }

        #endregion

        /// <summary>
        /// Internal record representing a single tick processor registered by a game module.
        /// </summary>
        private record TickProcessor(
            int IntervalMs,
            Func<bool> HasPending,
            Func<IList> FlushPending,
            Func<IList, Task> ProcessBatchAsync);
    }
}