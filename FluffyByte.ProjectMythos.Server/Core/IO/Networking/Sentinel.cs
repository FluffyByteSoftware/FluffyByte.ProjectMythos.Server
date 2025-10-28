using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FluffyByte.ProjectMythos.Server.Core.IO.Networking;

/// <summary>
/// Todo Sentinel handles monitoring for new network connections.
/// </summary>
public class Sentinel(CancellationToken shutdownToken) : CoreProcessBase(shutdownToken)
{
    /// <summary>
    /// Sentinel process name.
    /// </summary>
    public override string Name => "Sentinel";

    /// <summary>
    /// Starts the asynchronous operation.
    /// </summary>
    /// <remarks>This method initiates an asynchronous process. Ensure that any required preconditions  are
    /// met before calling this method. The operation runs asynchronously and does not block  the calling
    /// thread.</remarks>

    public override async Task StartAsync()
    {
        await Task.CompletedTask;
    }

    /// <summary>
    /// Local method to implement custom behaviour for Sentinel to do when stopping.
    /// </summary>
    public override async Task StopAsync()
    {
        await Task.CompletedTask;
    }
}