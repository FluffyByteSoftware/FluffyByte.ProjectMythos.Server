using System;
using System.Net.Sockets;
using FluffyByte.ProjectMythos.Server.Core.IO.Debug;
using FluffyByte.ProjectMythos.Server.Core.IO.Networking.FluffyClient;
using FluffyByte.ProjectMythos.Server.FluffyTypes;

namespace FluffyByte.ProjectMythos.Server.Core.IO.Networking;

/// <summary>
/// Watcher tracks all active network connections and their connection states (TCP-only vs TCP+UDP).
/// </summary>
/// <remarks>
/// Connection lifecycle in Watcher:
/// 1. Raw TcpClient arrives from Sentinel
/// 2. Upgraded to Vessel (TCP-only, unauthenticated)
/// 3. UDP handshake completes → Vessel becomes "authenticated" (has TCP+UDP)
/// 4. Player assignment happens much later (handled by GateWarden, not Watcher)
/// </remarks>
public class Watcher(Sentinel parentSentinel)
{
    private readonly Sentinel _sentinelParentReference = parentSentinel;

    // State tracking collections
    private readonly ThreadSafeDictionary<int, Vessel> _allVessels = [];

    /// <summary>
    /// Raw TCP clients that haven't been upgraded to Vessels yet.
    /// </summary>
    public ThreadSafeList<TcpClient> PendingTcpClients { get; private set; } = [];

    /// <summary>
    /// Gets the total number of vessels (both authenticated and unauthenticated).
    /// </summary>
    public int TotalVessels => _allVessels.Count;

    /// <summary>
    /// Gets the number of pending TCP clients that haven't been upgraded yet.
    /// </summary>
    public int PendingTcpCount => PendingTcpClients.Count;

    #region Registration Methods

    /// <summary>
    /// Registers a raw TCP client that just connected.
    /// This adds it to the pending list until it can be upgraded to a Vessel.
    /// </summary>
    public void RegisterTcpClient(TcpClient tcpClient)
    {
        try
        {
            PendingTcpClients.Add(tcpClient);
            Scribe.Debug($"[Watcher] Registered raw TCP client. Pending: {PendingTcpCount}");
        }
        catch (Exception ex)
        {
            Scribe.Error(ex);
        }
    }

    /// <summary>
    /// Upgrades a pending TCP client to a Vessel (wraps it with connection ID and streams).
    /// Returns the assigned connection ID, or -1 on failure.
    /// </summary>
    public int UpgradeToVessel(TcpClient tcpClient, Vessel newVessel)
    {
        try
        {
            PendingTcpClients.Remove(tcpClient);

            _allVessels.Add(newVessel.Id, newVessel);

            Scribe.Debug($"[Watcher] Upgraded TCP client to Vessel {newVessel.Id}. " +
                       $"Total vessels: {TotalVessels}, Pending: {PendingTcpCount}");

            return newVessel.Id;
        }
        catch (Exception ex)
        {
            Scribe.Error(ex);
            return -1;
        }
    }

    /// <summary>
    /// Marks a vessel as authenticated after UDP handshake completes.
    /// This means the vessel now has BOTH TCP and UDP connections established.
    /// </summary>
    public bool AuthenticateVessel(int connectionId)
    {
        try
        {
            Vessel? upgradeableVessel = GetVessel(connectionId);

            if (upgradeableVessel == null)
            {
                Scribe.Warn($"[Watcher] Cannot authenticate - vessel {connectionId} not found");
                return false;
            }

            if (upgradeableVessel.IsAuthenticated)
            {
                Scribe.Warn($"[Watcher] Vessel {connectionId} is already authenticated");
                return false;
            }

            upgradeableVessel.IsAuthenticated = true;
            Scribe.Debug($"[Watcher] Vessel {connectionId} authenticated (UDP handshake complete)");

            return true;
        }
        catch (Exception ex)
        {
            Scribe.Error(ex);
            return false;
        }
    }

    #endregion

    #region Unregistration Methods

    /// <summary>
    /// Removes a raw TCP client from pending list without creating a vessel.
    /// Used when a connection is rejected before upgrade.
    /// </summary>
    public void UnregisterTcpClient(TcpClient tcpClient)
    {
        try
        {
            PendingTcpClients.Remove(tcpClient);
            tcpClient.Close();
            Scribe.Debug($"[Watcher] Unregistered raw TCP client. Pending: {PendingTcpCount}");
        }
        catch (Exception ex)
        {
            Scribe.Error(ex);
        }
    }

    /// <summary>
    /// Unregisters a vessel by connection ID and cleans up resources.
    /// </summary>
    public void UnregisterVessel(int connectionId)
    {
        try
        {
            if (_allVessels.Remove(connectionId, out Vessel? vessel))
            {
                if(vessel == null)
                {
                    Scribe.Error($"[Watcher] Vessel {connectionId} was null during unregistration. THROWING.");
                    throw new NullReferenceException(nameof(vessel));
                }

                vessel.Disconnect();
                Scribe.Debug($"[Watcher] Unregistered vessel {connectionId}. Total vessels: {TotalVessels}");
            }
            else
            {
                Scribe.Warn($"[Watcher] Attempted to unregister non-existent vessel {connectionId}");
            }
        }
        catch (Exception ex)
        {
            Scribe.NetworkError(ex);
        }
    }

    /// <summary>
    /// Removes all vessels that are marked as disconnecting.
    /// Returns the number of vessels cleaned up.
    /// </summary>
    public int CleanupDisconnectedVessels()
    {
        try
        {
            var disconnecting = GetDisconnectingVessels();

            foreach (var vessel in disconnecting)
            {
                _allVessels.Remove(vessel.Id);
            }

            if (disconnecting.Count > 0)
            {
                Scribe.Debug($"[Watcher] Cleaned up {disconnecting.Count} disconnected vessels");
            }

            return disconnecting.Count;
        }
        catch (Exception ex)
        {
            Scribe.Error(ex);
            return 0;
        }
    }

    /// <summary>
    /// Unregisters ALL connections and cleans up resources.
    /// Called during server shutdown.
    /// </summary>
    public void UnregisterAll()
    {
        try
        {
            int vesselCount = TotalVessels;
            int pendingCount = PendingTcpCount;

            // Disconnect all vessels
            _allVessels.ForEach((id, vessel) =>
            {
                try
                {
                    vessel.Disconnect();
                }
                catch (Exception ex)
                {
                    Scribe.NetworkError(ex);
                }
            });

            // Close all pending TCP clients
            PendingTcpClients.ForEach(client =>
            {
                try
                {
                    client.Close();
                }
                catch (Exception ex)
                {
                    Scribe.NetworkError(ex);
                }
            });

            // Clear collections
            _allVessels.Clear();
            PendingTcpClients.Clear();

            Scribe.Debug($"[Watcher] Unregistered all connections " +
                       $"({vesselCount} vessels, {pendingCount} pending)");
        }
        catch (Exception ex)
        {
            Scribe.Error(ex);
        }
    }

    #endregion

    #region Query Methods

    /// <summary>
    /// Gets a vessel by its connection ID.
    /// </summary>
    public Vessel? GetVessel(int connectionId)
    {
        return _allVessels.GetValueOrDefault(connectionId);
    }

    /// <summary>
    /// Gets all vessels regardless of authentication state.
    /// </summary>
    public List<Vessel> GetAllVessels()
    {
        return [.. _allVessels.Values];
    }

    /// <summary>
    /// Gets all vessels that have NOT completed UDP handshake (TCP-only).
    /// </summary>
    public List<Vessel> GetUnauthenticatedVessels()
    {
        return [.. _allVessels.WhereValue(v => !v.IsAuthenticated).Values];
    }

    /// <summary>
    /// Gets all vessels that HAVE completed UDP handshake (TCP+UDP).
    /// </summary>
    public List<Vessel> GetAuthenticatedVessels()
    {
        return [.. _allVessels.WhereValue(v => v.IsAuthenticated).Values];
    }

    /// <summary>
    /// Gets all vessels that are currently disconnecting.
    /// </summary>
    public List<Vessel> GetDisconnectingVessels()
    {
        return [.. _allVessels.WhereValue(v => v.Disconnecting).Values];
    }

    /// <summary>
    /// Checks if a connection ID is currently active.
    /// </summary>
    public bool IsVesselActive(int connectionId)
    {
        return _allVessels.ContainsKey(connectionId);
    }
    #endregion
}