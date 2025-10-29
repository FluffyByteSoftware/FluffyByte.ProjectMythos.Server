using System;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
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
    private readonly Sentinel _parentReference = parentSentinel;

    /// <summary>
    /// Represents a thread-safe collection of <see cref="Vessel"/> objects.
    /// </summary>
    /// <remarks>This collection ensures thread safety for concurrent access and modifications.  It is
    /// suitable for scenarios where multiple threads need to add, remove, or enumerate vessels  without requiring
    /// external synchronization.</remarks>
    public ThreadSafeList<Vessel> Vessels = [];
    /// <summary>
    /// Represents a thread-safe collection of <see cref="TcpClient"/> objects.
    /// </summary>
    /// <remarks>This collection ensures thread-safe access and modification of the underlying list of <see
    /// cref="TcpClient"/> instances. It is suitable for scenarios where multiple threads need to concurrently add,
    /// remove, or enumerate TCP clients.</remarks>
    public ThreadSafeList<TcpClient> RawTcpClients = [];

    /// <summary>
    /// Clears the current cache of users connected at various stages.
    /// </summary>
    internal void ClearLists()
    {
        Vessels.Clear();
        RawTcpClients.Clear();
    }

    /// <summary>
    /// Registers a new vessel and adds it to the collection of tracked vessels.
    /// </summary>
    /// <remarks>This method adds the specified vessel to the internal collection and logs the registration 
    /// for debugging purposes. Ensure the vessel is not already registered to avoid duplicates.</remarks>
    /// <param name="vessel">The vessel to register. Cannot be <see langword="null"/>.</param>
    public void RegisterVessel(Vessel vessel)
    {
        Vessels.Add(vessel);
        Scribe.Debug($"[Watcher] Registered Vessel {vessel.Id} (Total Vessels: {Vessels.Count})");
    }

    /// <summary>
    /// Unregisters the specified vessel from the collection of tracked vessels.
    /// </summary>
    /// <remarks>Removes the specified vessel from the internal collection of tracked vessels.  After this
    /// method is called, the vessel will no longer be monitored.</remarks>
    /// <param name="vessel">The vessel to be unregistered. Cannot be <see langword="null"/>.</param>
    public void UnregisterVessel(Vessel vessel)
    {
        Vessels.Remove(vessel);
        Scribe.Debug($"[Watcher] Unregistered Vessel {vessel.Id} (Total Vessels: {Vessels.Count})");


    }


    /// <summary>
    /// Registers a raw <see cref="TcpClient"/> for monitoring and management.
    /// </summary>
    /// <remarks>This method adds the specified <see cref="TcpClient"/> to the internal collection of raw TCP
    /// clients  and logs the registration for debugging purposes. The caller is responsible for ensuring the  <see
    /// cref="TcpClient"/> is properly initialized before calling this method.</remarks>
    /// <param name="tcpClient">The <see cref="TcpClient"/> instance to register. Must not be <c>null</c>.</param>
    public void RegisterRawTcpClient(TcpClient tcpClient)
    {
        RawTcpClients.Add(tcpClient);
        Scribe.Debug($"[Watcher] Registered Raw TcpClient {tcpClient.Client.RemoteEndPoint} (Total Raw TcpClients: {RawTcpClients.Count})");
    }

    /// <summary>
    /// Unregisters a raw TCP client from the collection of active clients.
    /// </summary>
    /// <remarks>This method removes the specified TCP client from the internal collection of raw TCP clients.
    /// It is typically used to manage and track active client connections.</remarks>
    /// <param name="tcpClient">The <see cref="TcpClient"/> instance to unregister.</param>
    public void UnregisterRawTcpClient(TcpClient tcpClient)
    {
        RawTcpClients.Add(tcpClient);
        Scribe.Debug($"[Watcher] Unregistered Raw TcpClient {tcpClient.Client.RemoteEndPoint} (Total Raw TcpClients: {RawTcpClients.Count})");
    }
}