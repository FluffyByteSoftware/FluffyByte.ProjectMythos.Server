using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace FluffyByte.Tools;
/// <summary>
/// LogLevel enumeration defines various levels of logging severity.
/// Debug = 0, Info = 1, Warn = 2, Error = 3, Critical = 4
/// </summary>
public enum LogLevel
{
    /// <summary>
    /// Represents the debug logging level, used to log detailed information primarily for development and debugging
    /// purposes.
    /// </summary>
    /// <remarks>This logging level is typically used to capture fine-grained details about the application's
    /// execution,  such as variable values, method calls, and other diagnostic information. It is not recommended for
    /// use in production environments  due to the potential for large log volumes.</remarks>
    Debug = 0,
    /// <summary>
    /// Represents an informational log level.
    /// </summary>
    /// <remarks>This log level is typically used to log general information about the application's
    /// operation. It is less severe than warnings or errors and is often used for tracing or debugging
    /// purposes.</remarks>
    Info = 1,
    /// <summary>
    /// Represents a warning log level, used to indicate potentially harmful situations.
    /// </summary>
    Warn = 2,
    /// <summary>
    /// Represents an error state or condition.
    /// </summary>
    Error = 3,
    /// <summary>
    /// Represents a critical log level, typically used for logging fatal errors or application crashes.
    /// </summary>
    Critical = 4
}

/// <summary>
/// Provides logging functionality for various log levels, including debug, informational, warning, error, and critical
/// messages.
/// </summary>
/// <remarks>The <see cref="Scribe"/> class is a static utility for logging messages to the console with
/// customizable log level colors. It supports toggling debug mode, setting output colors for specific log levels, and
/// logging exceptions with detailed stack traces. This class is thread-safe and ensures proper synchronization when
/// writing to the console.</remarks>
public static class Scribe
{
    private readonly static object _logLock = new();

    private static bool _debugMode = false;
    private static ConsoleColor _infoColor = ConsoleColor.White;
    private static ConsoleColor _debugColor = ConsoleColor.Green;
    private static ConsoleColor _warningColor = ConsoleColor.Yellow;
    private static ConsoleColor _errorColor = ConsoleColor.Red;
    private static ConsoleColor _criticalColor = ConsoleColor.Magenta;

    /// <summary>
    /// Gets a value indicating whether the system has been successfully initialized.
    /// </summary>
    public static bool IsInitialized { get; private set; } = false;

    /// <summary>
    /// Initializes the application or system by setting the internal state to indicate readiness.
    /// </summary>
    /// <remarks>This method must be called before using any functionality that depends on the initialized
    /// state. Calling this method multiple times has no additional effect.</remarks>
    public static void Initialize() { IsInitialized = true; }
    /// <summary>
    /// Resets the initialization state of the system.
    /// </summary>
    /// <remarks>This method sets the internal initialization flag to indicate that the system is no longer
    /// initialized. It should be called when the system needs to be deinitialized or reset to its uninitialized
    /// state.</remarks>
    public static void Deinitialize() { IsInitialized = false; }

    /// <summary>
    /// Sets the foreground color used for console output for a specific log level.
    /// </summary>
    /// <remarks>This method allows customization of the console output appearance for different log levels.
    /// The specified color will be used when messages of the corresponding log level are written to the
    /// console.</remarks>
    /// <param name="level">The log level for which the foreground color should be set. Must be one of the defined <see cref="LogLevel"/>
    /// values.</param>
    /// <param name="fgColor">The foreground color to associate with the specified log level. Defaults to <see cref="ConsoleColor.White"/> if
    /// not specified.</param>
    public static void SetOutputColor(LogLevel level, ConsoleColor fgColor = ConsoleColor.White)
    {
        switch (level)
        {
            case LogLevel.Debug:
                _debugColor = fgColor;
                break;
            case LogLevel.Info:
                _infoColor = fgColor;
                break;
            case LogLevel.Warn:
                _warningColor = fgColor;
                break;
            case LogLevel.Error:
                _errorColor = fgColor;
                break;
            case LogLevel.Critical:
                _criticalColor = fgColor;
                break;
        }
    }

    /// <summary>
    /// Logs a debug-level message along with the caller's file path if debug mode is enabled.
    /// </summary>
    /// <param name="message">The debug message to log.</param>
    /// <param name="callerFilePath">The file path of the source code that invoked this method. 
    /// This parameter is automatically populated by the
    /// compiler and defaults to the caller's file path.</param>
    public static void Debug(string message, [CallerFilePath] string callerFilePath = "")
    { 
        if(_debugMode)
            InternalLog(LogLevel.Debug, message, null, callerFilePath);
    }

    /// <summary>
    /// Logs an informational message along with the file path of the caller.
    /// </summary>
    /// <param name="message">The informational message to log.</param>
    /// <param name="callerFilePath">The full path of the source file that contains the caller. 
    /// This parameter is automatically populated by the
    /// compiler and defaults to an empty string if not provided.</param>
    public static void Info(string message, [CallerFilePath] string callerFilePath = "")
        => InternalLog(LogLevel.Info, message, null, callerFilePath);

    /// <summary>
    /// Logs a warning message with optional caller file path information.
    /// </summary>
    /// <param name="message">The warning message to log. Cannot be null or empty.</param>
    /// <param name="callerFilePath">The file path of the source code that invoked this method. 
    /// This parameter is automatically populated by the
    /// compiler and should not typically be provided manually. Defaults to an empty string.</param>
    public static void Warn(string message, [CallerFilePath] string callerFilePath = "")
        => InternalLog(LogLevel.Warn, message, null, callerFilePath);

    /// <summary>
    /// Logs an error message with the specified content and the caller's file path.
    /// </summary>
    /// <param name="message">The error message to log. Cannot be null or empty.</param>
    /// <param name="callerFilePath">The file path of the source code that invoked this method. 
    /// This parameter is automatically populated by the
    /// compiler and should not be explicitly provided in most cases.</param>
    public static void Error(string message, [CallerFilePath] string callerFilePath = "")
        => InternalLog(LogLevel.Error, message, null, callerFilePath);

    /// <summary>
    /// Logs an error message along with the associated exception details.
    /// </summary>
    /// <param name="exception">The exception to log. Cannot be <see langword="null"/>.</param>
    /// <param name="callerFilePath">The file path of the source code that invoked this method. 
    /// This parameter is automatically populated by the
    /// compiler and should not be explicitly provided in most cases.</param>
    public static void Error(Exception exception, [CallerFilePath] string callerFilePath = "")
        => InternalLog(LogLevel.Error, exception.Message, exception, callerFilePath);

    /// <summary>
    /// Logs a critical message, typically used to indicate a failure that requires immediate attention.
    /// </summary>
    /// <param name="message">The critical message to log. This should describe the nature of the failure or issue.</param>
    /// <param name="callerFilePath">The file path of the source code that invoked this method. 
    /// This parameter is automatically populated by the
    /// compiler  and is optional. Defaults to an empty string if not provided.</param>
    public static void Critical(string message, [CallerFilePath] string callerFilePath = "")
        => InternalLog(LogLevel.Critical, message, null, callerFilePath);

    /// <summary>
    /// Logs a critical error message along with the exception details.
    /// </summary>
    /// <param name="exception">The exception to log. Cannot be <see langword="null"/>.</param>
    /// <param name="callerFilePath">The file path of the source code file that invoked this method. 
    /// This parameter is automatically populated by the
    /// compiler.</param>
    public static void Critical(Exception exception, [CallerFilePath] string callerFilePath = "")
        => InternalLog(LogLevel.Critical, exception.Message, exception, callerFilePath);


    /// <summary>
    /// Logs network-related exceptions and handles specific cases based on the exception type.
    /// </summary>
    /// <remarks>This method categorizes network exceptions into common and unexpected types. Common network
    /// exceptions, such as <see cref="IOException"/>, <see cref="ObjectDisposedException"/>, and <see
    /// cref="System.Net.Sockets.SocketException"/>, are logged at the debug level. Unexpected exceptions are logged as
    /// errors, and additional handling may be performed for specific caller types.  If the <paramref name="caller"/> is
    /// of type "Vessel", the method attempts to invoke its asynchronous disconnection logic.</remarks>
    /// <param name="exception">The exception that occurred. Must not be <see langword="null"/>.</param>
    /// <param name="caller">The object that triggered the exception, or <see langword="null"/> if unavailable.</param>
    /// <param name="callerFilePath">The file path of the source code that invoked this method. 
    /// This parameter is automatically populated by the compiler.</param>
    public static void NetworkError(Exception exception, object? caller, [CallerFilePath] string callerFilePath = "")
    {
        switch (exception)
        {
            case IOException:
            case ObjectDisposedException:
            case System.Net.Sockets.SocketException:
                // Common network exceptions that can be ignored or logged at a lower level
                InternalLog(LogLevel.Debug, $"Network exception occurred: {exception.Message}", exception, callerFilePath);
                break;
            default:
                // Other exceptions are logged as errors
                InternalLog(LogLevel.Error, $"Unexpected network exception: {exception.Message}", exception, callerFilePath);
                
                var type = caller?.GetType().Name;

                if(type == "Vessel")
                {
                    Task.Run(async () =>
                    {
                        await (caller?.GetType().GetMethod("DisconnectAsync")?.Invoke(caller, []) as Task ?? Task.CompletedTask);
                    });
                }
                break;

        }
    }

    /// <summary>
    /// Toggles the application's debug mode state.
    /// </summary>
    /// <remarks>This method switches the debug mode between enabled and disabled states.  The current state
    /// is inverted each time the method is called.</remarks>
    public static void ToggleDebugMode() { _debugMode = !_debugMode; }

    /// <summary>
    /// Logs a message with the specified log level, including optional exception details and caller information.
    /// </summary>
    /// <remarks>This method formats and writes log entries to the console and other configured outputs. If
    /// the log level is <see cref="LogLevel.Critical"/>, the application will terminate after logging the message. The
    /// method ensures thread safety by locking during the write operation.</remarks>
    /// <param name="level">The severity level of the log entry. Determines the log formatting and behavior.</param>
    /// <param name="message">The message to log. Cannot be null or empty.</param>
    /// <param name="ex">An optional exception to include in the log entry. If provided, the exception details and stack trace are
    /// logged.</param>
    /// <param name="callerFilePath">The file path of the caller, used to determine the class name for the log entry. Automatically populated by the
    /// compiler.</param>
    private static void InternalLog(LogLevel level, string message, Exception? ex, string callerFilePath)
    {
        if (IsInitialized is false)
        {
            Console.WriteLine("Scribe was called before it was initialized.");
            return;
        }

        string className = Path.GetFileNameWithoutExtension(callerFilePath);

        if (string.IsNullOrWhiteSpace(className))
        {
            className = "Server";
        }

        string timestamp = DateTime.Now.ToString("d HH:mm:ss.fff");
        string formattedLog;

        if (level == LogLevel.Error || level == LogLevel.Critical)
        {
            formattedLog = $"[ {timestamp} - {level} ] [{className}] encountered an error.\nMessage: {message}";
        }
        else
        {
            formattedLog = $"[ {timestamp} - {level} ] [{className}]: {message}";
        }

        // 3. Append Exception Details if present (rest of the logic is unchanged)
        if (ex != null)
        {
            formattedLog += $"\nStackTrace: {ex.StackTrace}";

            Exception? inner = ex.InnerException;
            int depth = 1;
            while (inner != null)
            {
                formattedLog += $"\nInner Exception ({depth}): {inner.GetType().Name}\nMessage: {inner.Message}";
                inner = inner.InnerException;
                depth++;

                if (depth > 10) // Prevent excessive log
                {
                    formattedLog += "\n... (inner exceptions truncated at 10)";
                    break;
                }
            }
        }

        if(level == LogLevel.Critical)
        {
            Environment.Exit(1);
        }

        lock (_logLock)
        {
            WriteToConsole(level, $"{formattedLog}");

            // Implement FluffyFile Logging here
        }
    }

/// <summary>
/// Writes a log message to the console with a color corresponding to the specified log level.
/// </summary>
/// <remarks>The console text color is set based on the <paramref name="level">logLevel: <list type="bullet">
/// <item><description><see cref="LogLevel.Debug"/>: Gray</description></item> <item><description><see
/// cref="LogLevel.Info"/>: Green</description></item> <item><description><see cref="LogLevel.Warn"/>:
/// Yellow</description></item> <item><description><see cref="LogLevel.Error"/>: Red</description></item>
/// <item><description><see cref="LogLevel.Critical"/>: Magenta</description></item> </list> After writing the message,
/// the console color is reset to its default state.</paramref></remarks>
/// <param name="level">The severity level of the log message. Determines the console text color.</param>
/// <param name="message">The log message to be written to the console. Cannot be null.</param>
    private static void WriteToConsole(LogLevel level, string message)
    {
        Console.ResetColor();
        switch (level)
        {
            case LogLevel.Debug:
                Console.ForegroundColor = ConsoleColor.Gray;
                break;
            case LogLevel.Info:
                Console.ForegroundColor = ConsoleColor.Green;
                break;
            case LogLevel.Warn:
                Console.ForegroundColor = ConsoleColor.Yellow;
                break;
            case LogLevel.Error:
                Console.ForegroundColor = ConsoleColor.Red;
                break;
            case LogLevel.Critical:
                Console.ForegroundColor = ConsoleColor.Magenta;
                break;
            default:
                Console.ResetColor();
                break;
        }

        Console.WriteLine(message);
        Console.ResetColor();
    }
}

