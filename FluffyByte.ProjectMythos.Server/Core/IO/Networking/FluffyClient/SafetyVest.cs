using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluffyByte.ProjectMythos.Server.Core.IO.Debug;

namespace FluffyByte.ProjectMythos.Server.Core.IO.Networking.FluffyClient;

/// <summary>
/// Provides a set of safety-related utility methods for validating and managing vessel network connections.
/// </summary>
/// <remarks>The <see cref="SafetyVest"/> class contains static methods designed to ensure the reliability and
/// safety of network connections for vessels. These methods perform various checks and validations to detect and handle
/// potential issues, such as null or inaccessible network streams.</remarks>
public static class SafetyVest
{
    /// <summary>
    /// Performs a series of safety checks on the network connection of the specified vessel.
    /// </summary>
    /// <remarks>This method verifies that the vessel's TCP stream is not null, is both readable and writable,
    /// and that the connection test succeeds. If any of these checks fail, the vessel is disconnected and the method
    /// returns <see langword="false"/>.</remarks>
    /// <param name="vesselParent">The vessel whose network connection is to be validated.</param>
    /// <returns><see langword="true"/> if all network safety checks pass; otherwise, <see langword="false"/>.</returns>
    public static bool NetworkSafetyChecks(Vessel vesselParent)
    {
        if(vesselParent.tcpStream == null)
        {
            Scribe.Debug($"[SafetyVest] Vessel {vesselParent.Id} has a null TCP stream.");
            vesselParent.Disconnect();

            return false;
        }

        if(vesselParent.tcpStream.CanRead == false || vesselParent.tcpStream.CanWrite == false)
        {
            Scribe.Debug($"[SafetyVest] Vessel {vesselParent.Id} has an unreadable or unwritable TCP stream.");
            vesselParent.Disconnect();

            return false;
        }

        if(!vesselParent.Metrics.TestConnection())
        {
            Scribe.Debug($"[SafetyVest] Vessel {vesselParent.Name} failed connection test.");
            vesselParent.Disconnect();

            return false;
        }

        return true;
    }
    
}
