using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluffyByte.ProjectMythos.Server.Core.TickSystem;

namespace FluffyByte.ProjectMythos.Server.Core.IO;

/// <summary>
/// Represents a module within a game, providing functionality specific to the module's purpose.
/// </summary>
/// <remarks>Implementations of this interface define the behavior and initialization logic for a specific game
/// module.</remarks>
public interface IGameModule
{
    /// <summary>
    /// Gets the name of the game.
    /// </summary>
    string GameName { get; }

    /// <summary>
    /// Initializes the specified <see cref="Weaver"/> instance, preparing it for use.
    /// </summary>
    /// <param name="weaver">The <see cref="Weaver"/> instance to initialize. Cannot be <see langword="null"/>.</param>
    void Initialize(Weaver weaver);
}
