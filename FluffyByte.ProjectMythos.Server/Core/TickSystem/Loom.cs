// Loom.cs
// This code is part of the FluffyByte.ProjectMythos.Server project.
using System.Collections.Generic;
using System;
using System.Threading.Tasks;
using System.Diagnostics;
using FluffyByte.ProjectMythos.Server.Core.IO.Debug;
using FluffyByte.ProjectMythos.Server.Core;

namespace FluffyByte.ProjectMythos.Server.Core.TickSystem;

/// <summary>
/// Represents the Loom process, which manages and executes periodic tasks based on predefined tick types.
/// </summary>
/// <remarks>The Loom process is responsible for coordinating and executing tasks at specific intervals, as
/// defined by the tick types. It is initialized with a shutdown token to handle graceful termination.</remarks>
public class Loom: CoreProcessBase
{
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
        await Task.CompletedTask;
    }

    /// <summary>
    /// Stops the asynchronous operation.
    /// </summary>
    /// <remarks>This method is intended to be overridden in a derived class to implement custom stop logic.
    /// The default implementation completes immediately.</remarks>
    public override async Task StopAsync()
    {
        await Task.CompletedTask;
    }
}