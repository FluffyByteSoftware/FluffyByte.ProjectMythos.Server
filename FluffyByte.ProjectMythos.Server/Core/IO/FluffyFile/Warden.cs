using System;
using System.IO;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;

using FluffyByte.ProjectMythos.Server.Core.IO.Debug;

namespace FluffyByte.ProjectMythos.Server.Core.IO.FluffyFile;

/// <summary>
/// Provides utility methods for reading, writing, and managing text and binary files asynchronously.
/// </summary>
/// <remarks>The <see cref="Warden"/> class includes methods for handling text files (reading and writing
/// entire files or lines) and binary files, with built-in validation and logging for file-related exceptions. It
/// ensures that directories are created as needed when writing files and logs errors to a central log file for
/// troubleshooting. <para> All methods in this class are static and designed for asynchronous operations, making them
/// suitable for scenarios where non-blocking file I/O is required. </para></remarks>
public static class Warden
{
    /// <summary>
    /// Gets a value indicating whether the system has been initialized.
    /// </summary>
    public static bool IsInitialized { get; private set; } = false;

    /// <summary>
    /// Initializes the application or system by setting the internal state to indicate readiness.
    /// </summary>
    /// <remarks>This method must be called before using any functionality that depends on the initialized
    /// state. Calling this method multiple times has no additional effect.</remarks>
    public static void Initialize() { IsInitialized = true; }

    /// <summary>
    /// Resets the initialization state of the application or system.
    /// </summary>
    /// <remarks>This method sets the internal initialization flag to indicate that the application or system
    /// is no longer initialized.  It should be called when deinitialization is required, such as during application
    /// shutdown or reconfiguration.</remarks>
    public static void Deinitialize() { IsInitialized = false; }

    #region Text Files
    #region Text Readers

    /// <summary>
    /// Asynchronously reads all text from the specified file.
    /// </summary>
    /// <remarks>This method uses UTF-8 encoding to read the file. If the file contains invalid UTF-8 byte
    /// sequences, an exception may be thrown.</remarks>
    /// <param name="path">The full path of the file to read. The path must not be null, empty, or inaccessible.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the entire content of the file as a
    /// string.</returns>
    /// <exception cref="FileNotFoundException">Thrown if the file at the specified <paramref name="path"/> does not exist or is inaccessible.</exception>
    public static async Task<string> ReadAllTextFileAsync(string path)
    {
        if (!IsInitialized)
        {
            Initialize();
        }

        if (!ValidateFilePath(path))
        {
            throw new FileNotFoundException($"The file at path {path} does not exist or is inaccessible.");
        }

        try
        {
            return await File.ReadAllTextAsync(path, Encoding.UTF8);
        }
        catch(Exception ex)
        {
            LogFileException(ex, path);
            throw;
        }
    }
    
    /// <summary>
    /// Asynchronously reads all lines from the specified text file.
    /// </summary>
    /// <remarks>This method uses UTF-8 encoding to read the file. If an exception occurs during the
    /// operation, it is logged before being rethrown.</remarks>
    /// <param name="path">The full path to the text file to read. The path must not be null, empty, or point to an inaccessible file.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains an array of strings, where each
    /// string represents a line of text from the file.</returns>
    /// <exception cref="FileNotFoundException">Thrown if the file at the specified <paramref name="path"/> does not exist or is inaccessible.</exception>
    public static async Task<string[]> ReadAllTextFileAsLinesAsync(string path)
    {
        if(!ValidateFilePath(path))
        {
            throw new FileNotFoundException($"The file at path {path} does not exist or is inaccessible.");
        }

        try
        {
            return await File.ReadAllLinesAsync(path, Encoding.UTF8);
        }
        catch(Exception ex)
        {
            LogFileException(ex, path);
            throw;
        }
    }

    #endregion

    #region Text Writers
    /// <summary>
    /// Asynchronously writes the specified content to a text file at the given path.
    /// </summary>
    /// <param name="path">The file path where the content will be written. The path must be a valid file path and cannot be null or empty.</param>
    /// <param name="content">The text content to write to the file. If the file already exists, its contents will be overwritten.</param>
    /// <returns>A task that represents the asynchronous write operation.</returns>
    public static async Task WriteTextFileAsync(string path, string content)
        => await WriteTextFileAsync(path, [content]);

    /// <summary>
    /// Asynchronously writes an array of text lines to a file at the specified path, creating the directory if it does
    /// not exist.
    /// </summary>
    /// <remarks>This method uses UTF-8 encoding to write the text lines to the file. If the file already
    /// exists, it will be overwritten. Any exceptions encountered during the operation are logged before being
    /// rethrown.</remarks>
    /// <param name="path">The full file path where the text lines will be written. The directory is created if it does not exist.</param>
    /// <param name="lines">An array of strings representing the lines of text to write to the file.</param>
    /// <returns>A task that represents the asynchronous write operation.</returns>
    public static async Task WriteTextFileAsync(string path, string[] lines)
    {
        try
        {
            CreateDirectoryOrSkip(Path.GetDirectoryName(path) ?? string.Empty);
            await File.WriteAllLinesAsync(path, lines, Encoding.UTF8);
        }
        catch (Exception ex)
        {
            LogFileException(ex, path);
            throw;
        }
    }
    #endregion
    #endregion

    #region Binary Files
    /// <summary>
    /// Asynchronously reads the contents of a binary file from the specified path.
    /// </summary>
    /// <remarks>This method validates the file path before attempting to read the file. If the file is not
    /// found or cannot be accessed, an exception is logged and rethrown. Ensure that the specified path is valid and
    /// accessible to avoid exceptions.</remarks>
    /// <param name="path">The full path to the binary file to be read. The path must point to an existing and accessible file.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a byte array with the contents of
    /// the file.</returns>
    public static async Task<byte[]> ReadBinFileAsync(string path)
    {
        try
        {
            if (!ValidateFilePath(path))
            {
                throw new FileNotFoundException($"The file at path {path} does not exist or is inaccessible.");
            }

            return await File.ReadAllBytesAsync(path);
        }
        catch(Exception ex)
        {
            LogFileException(ex, path);
            throw;
        }
    }

    /// <summary>
    /// Writes the specified byte array to a file at the given path asynchronously.
    /// </summary>
    /// <remarks>If the directory specified in the <paramref name="path"/> does not exist, it will be created
    /// automatically. Any exceptions encountered during the write operation are logged before being rethrown.</remarks>
    /// <param name="path">The full file path where the contents will be written. The directory is created if it does not exist.</param>
    /// <param name="contents">The byte array containing the data to write to the file.</param>
    /// <returns>A task that represents the asynchronous write operation.</returns>
    public static async Task WriteBinFileAsync(string path, byte[] contents)
    {
        try
        {
            CreateDirectoryOrSkip(Path.GetDirectoryName(path) ?? string.Empty);
            await File.WriteAllBytesAsync(path, contents);
        }
        catch(Exception ex)
        {
            LogFileException(ex, path);
            throw;
        }
    }
    #endregion

    #region Local Methods
    /// <summary>
    /// Validates whether the specified file path is not null, empty, or whitespace, and points to an existing file.
    /// </summary>
    /// <param name="path">The file path to validate.</param>
    /// <returns><see langword="true"/> if the specified path is not null, empty, or whitespace, and the file exists; otherwise,
    /// <see langword="false"/>.</returns>
    private static bool ValidateFilePath(string path)
    {
        return !string.IsNullOrWhiteSpace(path) && File.Exists(path);
    }

    /// <summary>
    /// Logs detailed information about an exception that occurred while accessing a file, including the file path and
    /// error details.
    /// </summary>
    /// <remarks>This method writes the error details to both the console and a log file named
    /// <c>bootstrap.log</c> located in the <c>logs</c> directory. If the log file cannot be written to, a critical
    /// error message is displayed on the console.</remarks>
    /// <param name="ex">The exception that occurred. This can be a specific file-related exception such as <see
    /// cref="FileNotFoundException"/> or <see cref="UnauthorizedAccessException"/>, or a general <see
    /// cref="Exception"/>.</param>
    /// <param name="path">The path of the file involved in the operation that caused the exception.</param>
    private static void LogFileException(Exception ex, string path)
    {
        string message = ex switch
        {
            FileNotFoundException => $"File not found: {path}",
            UnauthorizedAccessException => $"Access denied: {path}",
            FileLoadException => $"File load error: {path}",
            IOException => $"I/O error accessing: {path}",
            _ => $"Unexpected error on {path}: {ex.Message}"
        };

        string timestamp = DateTime.Now.ToString("d hh:mm:ss.fff tt");
        
        Console.WriteLine($"BootstrapLog: [{timestamp}] [Warden ERROR]: {message}");

        try
        {
            string logPath = Path.Combine("logs", "bootstrap.log");
            Directory.CreateDirectory("logs");
            File.AppendAllText(logPath, $"[{timestamp}] {message}\n{ex}\n\n");
        }
        catch
        {
            Console.WriteLine($"[{timestamp}] [Warden CRITICAL]: Could not write to bootstrap log");
        }
    }

    /// <summary>
    /// Creates a directory at the specified path if it does not already exist. If the path is null, empty, or consists
    /// only of whitespace, the operation is skipped.
    /// </summary>
    /// <remarks>If the directory already exists, no action is taken. If the path is invalid or an error
    /// occurs during directory creation, the exception is logged before being rethrown.</remarks>
    /// <param name="path">The path of the directory to create. Must not be null, empty, or whitespace.</param>
    private static void CreateDirectoryOrSkip(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            Console.WriteLine($"Warden: I was given a directory that is null or empty. Skipping {path}...");
            return;
        }
        try
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }
        catch (Exception ex)
        {
            LogFileException(ex, path);
            throw;
        }
    }
    #endregion
}