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
using System.Threading;
using System.Threading.Tasks;
using FluffyByte.ProjectMythos.Server.Core.IO;
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
        /// Called by the Loom every time a tick type fires.
        /// Delegates execution to the game-defined TickProcessor for that tick type.
        /// </summary>
        public async Task ProcessTick(TickType tickType)
        {
            TickProcessor? processor;
            lock (_registryLock)
            {
                if (!_tickProcessors.TryGetValue(tickType, out processor))
                    return; // no handler registered for this tick type
            }

            if (!processor.HasPending())
                return;

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