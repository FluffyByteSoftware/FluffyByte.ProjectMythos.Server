using System.IO;
using System.Reflection;

using FluffyByte.ProjectMythos.Server.Core.IO.FluffyFile;
using FluffyByte.ProjectMythos.Server.Core.IO.Debug;


namespace FluffyByte.ProjectMythos.Server.Core;

/// <summary>
/// Represents the entry point of the application.
/// </summary>
/// <remarks>The <see cref="Main"/> method serves as the starting point for the program's execution. It accepts
/// command-line arguments, which can be used to modify the program's behavior.</remarks>
public static class Program
{
    /// <summary>
    /// The entry point of the application. Executes the main program logic, including file reading, writing, and
    /// logging operations.
    /// </summary>
    /// <remarks>This method performs asynchronous file operations, including reading from and writing to
    /// disk, and logs key events during execution. It demonstrates basic file handling and logging functionality, and
    /// outputs documentation at the end of execution.</remarks>
    /// <param name="args">An array of command-line arguments passed to the application. If arguments are provided, they may influence
    /// future functionality.</param>
    public static async Task Main(string[] args)
    {
        Warden.Initialize();
        Scribe.Initialize();

        if (Scribe.IsInitialized)
        {
            WelcomeBanner();
        }

        if(args.Length > 0)
        {
            // Do some stuff in the future
        }

        GenerateDocumentation();

        Console.WriteLine("Press any key to terminate.");
        Console.ReadLine();

        await Task.CompletedTask;
    }

    /// <summary>
    /// Generates XML documentation for the specified assembly and writes it to the output directory.
    /// </summary>
    /// <remarks>This method uses the <see cref="Prophet"/> class to generate documentation for the current
    /// assembly. The output is written to a predefined directory relative to the executing assembly's location. Only
    /// members within the specified namespace are included in the generated documentation.</remarks>
    private static void GenerateDocumentation()
    {
        try
        {
            string assemblyPath = Assembly.GetExecutingAssembly().Location;
            string output = Path.Combine(
                Path.GetDirectoryName(assemblyPath)!,
                "..", "..", "..", "docs");

            string namespaceFilter = "FluffyByte.ProjectMythos";

            Console.WriteLine("Generating documentation...");

            Prophet prophet = new(assemblyPath, output, namespaceFilter);
            prophet.Generate();
        }
        catch(Exception ex)
        {
            Scribe.Error(ex);
        }
    }

    private static void WelcomeBanner()
    {
        Scribe.Info("=====================================");
        Scribe.Info("  FluffyByte Project Mythos Server   ");
        Scribe.Info("         Core Module Loaded          ");
        Scribe.Info("=====================================");
        Scribe.Info("Beginning bootstrap...");
    }
}