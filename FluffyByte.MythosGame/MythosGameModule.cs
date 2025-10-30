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
using FluffyByte.MythosGame.GameState;

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

    private Overlord? _overlord;
    private Realm? _realm;

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
        Scribe.Info($"[{GameName}] Initializing game module...");

        try
        {
            //var worldConfig = LoadWorldConfiguration();

            //_realm = new(worldConfig.WorldWidth, worldConfig.WorldHeight);
            
            //Scribe.Info($"[{GameName}] Realm created with dimensions: {worldConfig.WorldWidth}x{worldConfig.WorldHeight}");

            //_overlord = new(_realm);

        }
        catch(Exception ex)
        {
            Scribe.Error(ex);
            throw;
        }
    }

    private void LoadWorldConfiguration()
    {

    }
}
