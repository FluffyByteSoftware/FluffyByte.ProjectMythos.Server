using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using FluffyByte.Tools;

namespace FluffyByte.ProjectMythos.Server.Core.IO.Networking.FluffyClient;

/// <summary>
/// Provides functionality for managing UDP-based input and output operations for an associated vessel.
/// </summary>
/// <remarks>The <see cref="UdpIO"/> class handles UDP packet transmission and reception with reliability
/// features including sequence numbering, acknowledgments, and duplicate detection. It wraps around a shared
/// UDP socket and maintains per-vessel endpoint information.</remarks>
public class UdpIO : IDisposable
{
    private readonly Vessel _vesselParentReference;
    private UdpClient _sharedUdpSocket;

    private bool _disposed = false;

    private const int MAX_UDP_PACKET_SIZE = 1024; // 1 KB for game state updates

    /// <summary>
    /// Gets the remote UDP endpoint for this vessel.
    /// </summary>
    public IPEndPoint RemoteEndPoint { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the UDP handshake has been completed.
    /// </summary>
    public bool IsHandshakeComplete { get; private set; }

    // Reliability layer
    private uint _lastSequenceNumberSent = 0;
    private uint _lastSequenceNumberReceived = 0;


    /// <summary>
    /// Initializes a new instance of the <see cref="UdpIO"/> class with UDP endpoint information.
    /// </summary>
    /// <param name="parent">The vessel instance that owns this UDP I/O handler.</param>
    /// <param name="udpEndpoint">The client's UDP endpoint obtained during handshake.</param>
    public UdpIO(Vessel parent, IPEndPoint udpEndpoint)
    {
        _vesselParentReference = parent;

        // ✅ this line ensures we use the same shared UDP socket from the server
        _sharedUdpSocket = parent.UdpClient;

        RemoteEndPoint = udpEndpoint;
        IsHandshakeComplete = true;

        if (_vesselParentReference.Metrics != null)
            _vesselParentReference.Metrics.LastPacketUdpReceivedTime = DateTime.UtcNow;
    }


    /// <summary>
    /// Marks the handshake process as complete.
    /// </summary>
    /// <remarks>This method sets the <see cref="IsHandshakeComplete"/> property to <see langword="true"/>, 
    /// indicating that the handshake process has successfully finished.</remarks>
    public void MarkHandshakeComplete()
    {
        IsHandshakeComplete = true;
    }

    /// <summary>
    /// Initializes the shared UDP socket for all vessels.
    /// </summary>
    /// <remarks>This must be called once during server startup before any vessels are created.</remarks>
    /// <param name="sharedSocket">The shared UDP socket instance.</param>
    public void Initialize(UdpClient sharedSocket)
    {
        _sharedUdpSocket = sharedSocket;
        Scribe.Info("[UdpIO] Shared UDP socket initialized.");
    }

    /// <summary>
    /// Completes the UDP handshake by storing the client's endpoint.
    /// </summary>
    /// <param name="clientEndpoint">The endpoint from which the client sent the UDP handshake.</param>
    public void CompleteHandshake(IPEndPoint clientEndpoint)
    {
        RemoteEndPoint = clientEndpoint;
        IsHandshakeComplete = true;
        _vesselParentReference.Metrics.LastPacketUdpReceivedTime = DateTime.UtcNow;

        Scribe.Info($"[Vessel {_vesselParentReference.Name}] UDP handshake completed for {clientEndpoint}");
        _vesselParentReference.Metrics.JustReacted();
    }

    /// <summary>
    /// Sends a UDP packet to this vessel's remote endpoint.
    /// </summary>
    /// <remarks>Packets are sent with a 4-byte sequence number prefix for reliability tracking.
    /// This method performs safety checks before sending and updates vessel metrics.</remarks>
    /// <param name="data">The data to send. Must not exceed MAX_UDP_PACKET_SIZE.</param>
    public async Task SendAsync(byte[] data)
    {
        if (!SafetyVest.NetworkSafetyChecks(_vesselParentReference))
            return;

        if (!IsHandshakeComplete)
        {
            Scribe.Warn($"[Vessel {_vesselParentReference.Name}] Attempted to send UDP before handshake complete.");
            return;
        }

        if (data == null || data.Length == 0)
        {
            Scribe.Warn($"[Vessel {_vesselParentReference.Name}] Attempted to send null or empty UDP data. Ignoring.");
            return;
        }

        if (data.Length > MAX_UDP_PACKET_SIZE)
        {
            Scribe.Warn($"[Vessel {_vesselParentReference.Name}] UDP packet size {data.Length} exceeds maximum {MAX_UDP_PACKET_SIZE}. Truncating or rejecting.");
            return;
        }

        try
        {
            _vesselParentReference.Metrics.JustReacted();

            // Prepend sequence number (4 bytes)
            uint seqNum = ++_lastSequenceNumberSent;
            byte[] packet = new byte[data.Length + 4];
            BitConverter.GetBytes(seqNum).CopyTo(packet, 0);
            data.CopyTo(packet, 4);

            // Send via shared socket
            int bytesSent = await _sharedUdpSocket.SendAsync(packet, packet.Length, RemoteEndPoint);

            _vesselParentReference.Metrics.TotalBytesSent += (ulong)bytesSent;

            Scribe.Debug($"[Vessel {_vesselParentReference.Name}] Sent UDP packet (seq: {seqNum}, size: {data.Length} bytes)");
        }
        catch (Exception ex)
        {
            Scribe.NetworkError(ex, _vesselParentReference);
            // Note: Unlike TCP, we don't disconnect on UDP errors (packets can be lost)
        }
    }

    /// <summary>
    /// Sends a text message as a UDP packet.
    /// </summary>
    /// <param name="message">The text message to send.</param>
    public async Task SendTextAsync(string message)
    {
        if (string.IsNullOrEmpty(message))
            return;

        byte[] data = Encoding.UTF8.GetBytes(message);
        await SendAsync(data);
    }

 
    /// <summary>
    /// Handles the receipt of a UDP packet, performing validation, sequence tracking, and payload processing.
    /// </summary>
    /// <remarks>This method validates the packet for minimum size and ensures it is not a duplicate or
    /// out-of-order based on the sequence number. If the packet is valid, it updates metrics, detects packet loss, and
    /// processes the payload. Malformed packets, duplicate packets, or packets received out of order are
    /// ignored.</remarks>
    /// <param name="packet">The received UDP packet as a byte array. The first 4 bytes represent the sequence number, and the remaining
    /// bytes represent the payload.</param>
    public void OnPacketReceived(byte[] packet)
    {
        if (_vesselParentReference.Disconnecting)
            return;

        if (packet == null || packet.Length < 4)
        {
            Scribe.Warn($"[Vessel {_vesselParentReference.Name}] Received malformed UDP packet (too small). Ignoring.");
            return;
        }

        try
        {
            // Extract sequence number (first 4 bytes)
            uint sequenceNumber = BitConverter.ToUInt32(packet, 0);

            // Extract payload (everything after sequence number)
            byte[] payload = new byte[packet.Length - 4];
            Array.Copy(packet, 4, payload, 0, payload.Length);

            // Update timing via Metrics
            _vesselParentReference.Metrics.LastPacketUdpReceivedTime = DateTime.UtcNow;
            _vesselParentReference.Metrics.TotalBytesReceived += (ulong)packet.Length;

            // ✅ WRAPAROUND-SAFE: Check for duplicate or out-of-order packets
            if (!IsSequenceNewer(sequenceNumber, _lastSequenceNumberReceived))
            {
                Scribe.Debug($"[Vessel {_vesselParentReference.Name}] Received duplicate/old UDP packet (seq: {sequenceNumber}, last: {_lastSequenceNumberReceived}). Ignoring.");
                return;
            }

            // ✅ WRAPAROUND-SAFE: Check for packet loss (gap in sequence numbers)
            uint expectedNext = _lastSequenceNumberReceived + 1;
            if (sequenceNumber != expectedNext)
            {
                // Calculate packets lost (handles wraparound)
                uint packetsLost;
                if (sequenceNumber > _lastSequenceNumberReceived)
                {
                    // Normal case: no wraparound
                    packetsLost = sequenceNumber - _lastSequenceNumberReceived - 1;
                }
                else
                {
                    // Wraparound case: sequence wrapped from max to 0
                    packetsLost = (uint.MaxValue - _lastSequenceNumberReceived) + sequenceNumber;
                }

                Scribe.Debug($"[Vessel {_vesselParentReference.Name}] Detected {packetsLost} lost UDP packet(s) (last: {_lastSequenceNumberReceived}, received: {sequenceNumber})");
            }

            _lastSequenceNumberReceived = sequenceNumber;

            Scribe.Debug($"[Vessel {_vesselParentReference.Name}] Received UDP packet (seq: {sequenceNumber}, size: {payload.Length} bytes)");

            _vesselParentReference.Metrics.JustReacted();

            // Process the payload (delegate to game logic)
            ProcessUdpPayload(payload);
        }
        catch (Exception ex)
        {
            Scribe.Error(ex);
            // Don't disconnect on UDP errors - packets can be corrupted
        }
    }

    /// <summary>
    /// Processes the payload of a validated UDP packet.
    /// </summary>
    /// <remarks>Override or extend this method to implement game-specific UDP packet handling.
    /// By default, this attempts to deserialize JSON payloads.</remarks>
    /// <param name="payload">The packet payload with sequence number already stripped.</param>
    private void ProcessUdpPayload(byte[] payload)
    {
        try
        {
            // Convert to string (assuming JSON payload)
            string jsonData = Encoding.UTF8.GetString(payload);

            Scribe.Debug($"[Vessel {_vesselParentReference.Name}] UDP Payload: {jsonData}");

            // TODO: Deserialize and route to appropriate game systems
            // Example: Parse JSON, check "type" field, dispatch to handlers
            // This is where you'd integrate with your game state managers
        }
        catch (Exception ex)
        {
            Scribe.Error(ex);
        }
    }

    /// <summary>
    /// Checks if this vessel's UDP connection has timed out.
    /// </summary>
    /// <param name="timeoutSeconds">The timeout threshold in seconds.</param>
    /// <returns>True if no packets have been received within the timeout period.</returns>
    public bool IsTimedOut(int timeoutSeconds = 30)
    {
        return (DateTime.UtcNow - _vesselParentReference.Metrics.LastPacketUdpReceivedTime).TotalSeconds > timeoutSeconds;
    }

    /// <summary>
    /// Releases the resources used by this UDP I/O handler.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases resources used by this instance.
    /// </summary>
    /// <param name="disposing">True to release both managed and unmanaged resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            // UdpIO doesn't own the shared socket, so nothing to dispose
            // Just log the cleanup
            Scribe.Debug($"[Vessel {_vesselParentReference.Name}] UdpIO disposed.");
        }

        _disposed = true;
    }

    #region Sequence Number Utilities (Wraparound-Safe)

    /// <summary>
    /// Compares two sequence numbers in a wraparound-safe manner using RFC 1982 Serial Number Arithmetic.
    /// </summary>
    /// <remarks>
    /// This assumes sequence numbers don't jump more than 2^31 values at once.
    /// Treats the sequence space as circular to handle wraparound at uint.MaxValue.
    /// </remarks>
    /// <param name="s1">First sequence number</param>
    /// <param name="s2">Second sequence number</param>
    /// <returns>
    /// Negative if s1 is "before" s2,
    /// Zero if s1 equals s2,
    /// Positive if s1 is "after" s2
    /// </returns>
    private static int CompareSequenceNumbers(uint s1, uint s2)
    {
        // Handle the simple case: they're equal
        if (s1 == s2)
            return 0;

        // Use RFC 1982 Serial Number Arithmetic
        // This works because we treat the sequence space as circular
        const uint halfRange = uint.MaxValue / 2;

        if ((s1 < s2 && s2 - s1 < halfRange) || (s1 > s2 && s1 - s2 > halfRange))
            return -1; // s1 is "before" s2
        else
            return 1;  // s1 is "after" s2
    }

    /// <summary>
    /// Checks if sequence number s1 is newer than s2 (wraparound-safe).
    /// </summary>
    private static bool IsSequenceNewer(uint s1, uint s2)
    {
        return CompareSequenceNumbers(s1, s2) > 0;
    }

    #endregion
}