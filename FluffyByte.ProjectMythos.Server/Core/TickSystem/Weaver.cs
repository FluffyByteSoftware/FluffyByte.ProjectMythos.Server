using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FluffyByte.ProjectMythos.Server.Core.TickSystem;

/// <summary>
/// Represents a process responsible for weaving operations, inheriting from <see cref="CoreProcessBase"/>.
/// </summary>
/// <remarks>The <see cref="Weaver"/> class provides functionality to manage the lifecycle of a weaving process,
/// including starting and stopping the process asynchronously. It is initialized with a cancellation token to handle
/// graceful shutdowns.</remarks>
/// <param name="shutdownToken">Reference to the cancellation token held by the Conductor.</param>
public class Weaver(CancellationToken shutdownToken) : CoreProcessBase(shutdownToken)
{
    /// <summary>
    /// Gets the name of the weaver.
    /// </summary>
    public override string Name => "Weaver";

    /// <summary>
    /// todo
    /// </summary>
    /// <returns></returns>
    public override async Task StartAsync()
    {
        await Task.CompletedTask;
    }

    /// <summary>
    /// todo
    /// 
    /// </summary>
    /// <returns></returns>
    public override async Task StopAsync()
    {
        await Task.CompletedTask;
    }

}
