using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FluffyByte.ProjectMythos.Server.Core.IO.Networking;

/// <summary>
/// Defines the types of UDP packets used in the communication protocol.
/// </summary>
/// <remarks>This enumeration provides a set of predefined packet types, each represented by a unique byte value.
/// These packet types are used to identify the purpose or action associated with a specific UDP packet within the
/// system. The values range from handshake messages to operational commands and acknowledgments.</remarks>
public enum UdpPacketType : byte
{
    /// <summary>
    /// Represents the "Hello" message type in the protocol.
    /// </summary>
    Hello = 0x01,

    /// <summary>
    /// Represents an acknowledgment message in response to a "Hello" request.
    /// </summary>
    /// <remarks>This value is typically used in communication protocols to confirm receipt of an initial
    /// handshake or greeting message.</remarks>

    HelloAck = 0x02,
    /// <summary>
    /// Represents the movement action in the system, identified by the value 0x10.
    /// </summary>
    Movement = 0x10,

    /// <summary>
    /// Represents an action operation in the system.
    /// </summary>
    Action = 0x11,

    /// <summary>
    /// Represents the state of the world in the current context.
    /// </summary>
    /// <remarks>This value is typically used to indicate a specific condition or status within a broader
    /// system. The exact meaning of the state depends on the implementation and usage context.</remarks>
    WorldState = 0x20,

    /// <summary>
    /// Represents the operation code for spawning an entity in the system.
    /// </summary>
    /// <remarks>This value is used to identify the action of creating and initializing a new entity. It is
    /// typically utilized in scenarios where entities need to be dynamically added to the system.</remarks>
    EntitySpawn = 0x21,

    /// <summary>
    /// Represents the event code for an entity being removed or despawned in the system.
    /// </summary>
    EntityDespawn = 0x22,

    /// <summary>
    /// Represents an acknowledgment signal with a hexadecimal value of 0xFF.
    /// </summary>
    /// <remarks>This value is typically used to indicate a successful operation or receipt of data in
    /// communication protocols or signaling systems.</remarks>
    Ack = 0xFF
}
