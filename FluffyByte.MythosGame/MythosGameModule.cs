// File: MythosGameModule.cs
// Project: FluffyByte.ProjectMythos.Game
// Purpose: Minimal game module to demonstrate integration with the server core.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluffyByte.Tools;
using FluffyByte.ProjectMythos.Server.Core.TickSystem;
using FluffyByte.ProjectMythos.Server.Core.IO;

namespace FluffyByte.MythosGame;

/// <summary>
/// Represents the game module for the "Project Mythos Prototype" game.
/// </summary>
/// <remarks>This module is responsible for initializing and managing the game systems for "Project Mythos
/// Prototype." It registers a world simulation tick processor that executes at a fixed interval to simulate game world
/// updates.</remarks>
public class MythosGameModule : IGameModule
{
    /// <summary>
    /// Gets the name of the game.
    /// </summary>
    public string GameName => "Project Mythos Prototype";

    /// <summary>
    /// Initializes the game module by setting up necessary systems and registering tick processors.
    /// </summary>
    /// <remarks>This method configures the game module to process periodic world simulation ticks. It
    /// registers a tick processor that executes at a fixed interval of 1000 milliseconds, simulating a "heartbeat" for
    /// the game world. The tick processor ensures that pending tasks are flushed and processed
    /// asynchronously.</remarks>
    /// <param name="weaver">The <see cref="Weaver"/> instance used to register tick processors and manage game system updates.</param>
    public void Initialize(Weaver weaver)
    {
        Scribe.Info($"[MythosGameModule] Initializing game systems...");

        // Register a simple test tick
        weaver.RegisterTickProcessor(
            tickType: TickType.WorldSimulation,
            intervalMs: 1000, // once per second
            hasPending: () => true,
            flushPending: () => new List<string> { "heartbeat" },
            processBatchAsync: async list =>
            {
                foreach (var item in list)
                {
                    Scribe.Info($"[MythosGameModule] Tick: {item} — World pulse at {DateTime.Now:T}");
                }
                await Task.CompletedTask;
            });

        Scribe.Info($"[MythosGameModule] Game module registration complete.");
    }
}
