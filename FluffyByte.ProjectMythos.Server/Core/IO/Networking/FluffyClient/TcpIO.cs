using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;
using FluffyByte.ProjectMythos.Server.Core.IO.Debug;

namespace FluffyByte.ProjectMythos.Server.Core.IO.Networking.FluffyClient;

/// <summary>
/// Provides functionality for managing TCP-based input and output operations for an associated vessel.
/// </summary>
/// <remarks>The <see cref="TcpIO"/> class is responsible for handling text and binary data transmission over a
/// TCP connection in the context of a specific <see cref="Vessel"/>. It supports asynchronous read and write operations
/// for both text and binary data. Ensure that the associated vessel and cancellation token are properly managed to
/// avoid unexpected behavior.</remarks>
public class TcpIO : IDisposable
{
    private readonly Vessel _vesselParentReference;
    private readonly StreamReader _tcpTextReader;
    private readonly StreamWriter _tcpTextWriter;
    private readonly BinaryReader _tcpBinReader;
    private readonly BinaryWriter _tcpBinWriter;

    private bool _disposed = false;

    private const int MAX_BINARY_SIZE = 10 * 1024 * 1024; // 10 MB

    /// <summary>
    /// Initializes a new instance of the <see cref="TcpIO"/> class, which provides text and binary input/output
    /// operations over a TCP stream associated with the specified vessel.
    /// </summary>
    /// <remarks>This constructor initializes text and binary readers and writers for the TCP stream
    /// associated with the provided <see cref="Vessel"/> instance. The text readers and writers use UTF-8 encoding, and
    /// the binary readers and writers are configured to detect encoding from byte order marks.</remarks>
    /// <param name="parent">The <see cref="Vessel"/> instance that owns the TCP stream used for communication. This parameter cannot be <see
    /// langword="null"/>.</param>
    public TcpIO(Vessel parent)
    {
        _vesselParentReference = parent;
        
        _tcpTextReader = new(_vesselParentReference._tcpStream, Encoding.UTF8, 
            detectEncodingFromByteOrderMarks: false);
        _tcpTextWriter = new(parent._tcpStream, Encoding.UTF8) { AutoFlush = true };

        _tcpBinReader = new(parent._tcpStream, Encoding.UTF8, true);
        _tcpBinWriter = new(parent._tcpStream, Encoding.UTF8, true);
    }

    /// <summary>
    /// Writes the specified message to the underlying TCP text writer asynchronously.
    /// </summary>
    /// <remarks>This method performs standard safety checks before writing the message. If the safety checks
    /// fail, the method exits without performing any operation. Exceptions encountered during the write operation are
    /// logged but do not propagate to the caller.</remarks>
    /// <param name="message">The message to be written to the TCP text writer.</param>
    /// <param name="ignoreNewLine">A value indicating whether to write the message without appending a newline character. If <see
    /// langword="true"/>, the message is written as-is; otherwise, a newline character is appended.</param>
    /// <returns>A task that represents the asynchronous write operation.</returns>
    public async Task WriteTextAsync(string message, bool ignoreNewLine = false)
    {
        if (!SafetyVest.NetworkSafetyChecks(_vesselParentReference))
            return;

        try
        {
            _vesselParentReference.Metrics.JustReacted();

            if(!ignoreNewLine)
                await _tcpTextWriter.WriteLineAsync(message);
            else
                await _tcpTextWriter.WriteAsync(message);
        }
        catch(Exception ex)
        {
            Scribe.Error(ex);
        }
        await Task.CompletedTask;
    }

    /// <summary>
    /// Asynchronously reads a line of text from the TCP connection.
    /// </summary>
    /// <remarks>If the parent vessel is in the process of disconnecting, the method immediately returns an
    /// empty string. If an exception occurs during the read operation, the error is logged, the parent vessel is
    /// disconnected, and an empty string is returned.</remarks>
    /// <returns>The line of text read from the TCP connection, or an empty string if no text is available or an error occurs.</returns>
    public async Task<string> ReadTextAsync()
    {
        if (_vesselParentReference.Disconnecting) return string.Empty;

        try
        {
            string? response = await _tcpTextReader.ReadLineAsync();
            return response ?? string.Empty;
        }
        catch(Exception ex)
        {
            Scribe.NetworkError(ex, _vesselParentReference);
            _vesselParentReference.Disconnect();
        }

        return string.Empty;
    }

    /// <summary>
    /// Reads a binary payload from the network stream, performs validation checks, and returns the data if valid.
    /// </summary>
    /// <remarks>This method performs several safety and validation checks before processing the binary data:
    /// <list type="bullet"> <item> Ensures that the vessel's standard safety checks pass before proceeding. </item>
    /// <item> Validates the binary payload length to ensure it is within acceptable bounds. </item> <item> Verifies
    /// that the received data matches the expected length. </item> </list> If any of these checks fail, the method logs
    /// a warning, disconnects the vessel, and returns <see langword="null"/>.</remarks>
    /// <returns>A byte array containing the binary data if successfully read and validated; otherwise, <see langword="null"/>.</returns>
    public byte[]? ReadyBinary()
    {
        if (!SafetyVest.NetworkSafetyChecks(_vesselParentReference))
            return null;

        try
        {
            int length = _tcpBinReader.ReadInt32();

            if (length <= 0)
            {
                Scribe.Warn($"[Vessel {_vesselParentReference.Name}] Received invalid binary length of {length}. Disconnecting.");
                _vesselParentReference.Disconnect();

                return null;
            }

            if(length > MAX_BINARY_SIZE)
            {
                Scribe.Warn($"[Vessel {_vesselParentReference.Name}] Received excessive binary length of {length}. " +
                    $"Maximum Size: {MAX_BINARY_SIZE}. Disconnecting.");
                _vesselParentReference.Disconnect();
                return null;
            }

            byte[] data = _tcpBinReader.ReadBytes(length);

            if(data.Length != length)
            {
                Scribe.Warn($"[Vessel {_vesselParentReference.Name}] Expected {length} bytes but received {data.Length} bytes. Disconnecting.");
                _vesselParentReference.Disconnect();
                return null;
            }

            _vesselParentReference.Metrics.TotalBytesReceived += (ulong)(4 + length);

            Scribe.Debug($"[Vessel {_vesselParentReference.Name}] Received {length} bytes of binary data.");

            return data;
        }
        catch(Exception ex)
        {
            Scribe.NetworkError(ex, _vesselParentReference);
            _vesselParentReference.Disconnect();

            return null;
        }
    }
    /// <summary>
    /// Writes binary data to the underlying TCP stream with a length prefix.
    /// </summary>
    public async Task WriteBinaryAsync(byte[] dataToWrite)
    {
        if (_vesselParentReference.Disconnecting)
            return;

        if (dataToWrite == null || dataToWrite.Length == 0)
        {
            Scribe.Warn($"[Vessel {_vesselParentReference.Name}] Attempted to write null or empty binary data. Ignoring.");
            return;
        }

        if (dataToWrite.Length > MAX_BINARY_SIZE)
        {
            Scribe.Warn($"[Vessel {_vesselParentReference.Name}] Attempted to write excessive binary length of {dataToWrite.Length}. " +
                $"Maximum Size: {MAX_BINARY_SIZE}. Disconnecting.");
            _vesselParentReference.Disconnect();
            return;
        }

        try
        {
            // Write length prefix (4 bytes)
            byte[] lengthBytes = BitConverter.GetBytes(dataToWrite.Length);
            await _vesselParentReference._tcpStream.WriteAsync(lengthBytes.AsMemory(0, 4));

            // Write actual data
            await _vesselParentReference._tcpStream.WriteAsync(dataToWrite);

            // Flush the stream
            await _vesselParentReference._tcpStream.FlushAsync();

            _vesselParentReference.Metrics.TotalBytesSent += (ulong)(4 + dataToWrite.Length);

            Scribe.Debug($"[Vessel {_vesselParentReference.Name}] Sent {dataToWrite.Length} bytes of binary data.");
        }
        catch (Exception ex)
        {
            Scribe.NetworkError(ex, _vesselParentReference);
            _vesselParentReference.Disconnect();
        }
    }


    /// <summary>
    /// Releases the resources used by the current instance of the class.
    /// </summary>
    /// <remarks>This method should be called when the instance is no longer needed to free unmanaged
    /// resources  and perform other cleanup operations. After calling this method, the instance should not be
    /// used.</remarks>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases the resources used by the current instance of the class.
    /// </summary>
    /// <remarks>This method is called to release both managed and unmanaged resources. Override this method
    /// in a derived class to provide custom cleanup logic. Ensure that the base class implementation is called to
    /// release resources properly.</remarks>
    /// <param name="disposing">A value indicating whether to release both managed and unmanaged resources (<see langword="true"/>) or only
    /// unmanaged resources (<see langword="false"/>).</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            try
            {
                _tcpBinReader.Dispose();
                _tcpBinWriter.Dispose();
                _tcpTextReader.Dispose();
                _tcpTextWriter.Dispose();
            }
            catch(Exception ex)
            {
                Scribe.Error(ex);
            }
        }

        _disposed = true;
    }
}
