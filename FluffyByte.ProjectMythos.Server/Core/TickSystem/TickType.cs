using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FluffyByte.ProjectMythos.Server.Core.TickSystem;

/// <summary>
/// Specifies the types of actions or categories that can occur within the system.
/// </summary>
/// <remarks>This enumeration is used to define distinct categories or modes of operation within the application, 
/// such as movement, messaging, object management, and simulation. Each value represents a specific  type of action or
/// context, which can be used to control or differentiate system behavior.</remarks>
public enum TickType
{
    /// <summary>
    /// Represents the movement action in the system.
    /// </summary>
    Movement = 0,
    /// <summary>
    /// Represents the messaging category in the application.
    /// </summary>
    Messaging = 1,
    /// <summary>
    /// Represents the object spawning mode in the system.
    /// </summary>
    ObjectSpawning = 2,
    /// <summary>
    /// Represents the cleanup operation for objects.
    /// </summary>
    /// <remarks>This enumeration value is typically used to indicate that an object cleanup process should be
    /// performed.</remarks>
    ObjectCleanup = 3,
    /// <summary>
    /// Represents the combat game mode.
    /// </summary>
    /// <remarks>This value is used to indicate a game mode focused on combat-related activities.</remarks>
    Combat = 4,
    /// <summary>
    /// Represents the simulation mode for a virtual world environment.
    /// </summary>
    /// <remarks>This mode is typically used to simulate complex interactions within a virtual world,  such as
    /// physics, AI behavior, and environmental dynamics.</remarks>
    WorldSimulation = 5,
    /// <summary>
    /// Represents the AutoSave option in the application settings.
    /// </summary>
    /// <remarks>This value is typically used to enable or configure automatic saving functionality.</remarks>
    AutoSave = 6
}
