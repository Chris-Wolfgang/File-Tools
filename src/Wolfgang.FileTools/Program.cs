using System.Reflection;
using JetBrains.Annotations;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Wolfgang.FileTools.Command;
using Wolfgang.FileTools.Framework;

namespace Wolfgang.FileTools;

[Command
(
    Description = "A template for a console application complete with command line parse, logging, DI and more.",

    UnrecognizedArgumentHandling = UnrecognizedArgumentHandling.Throw,

    ResponseFileHandling = ResponseFileHandling.ParseArgsAsLineSeparated
)]
[Subcommand(typeof(SplitCommand))]

internal class Program
{
    private static async Task<int> Main(string[] args)
    {
        try
        {
            // Create a new HostBuilder to build the application
            return await new HostBuilder()
                .AddConfigurationFile
                    (
                        ConfigurationFileMethod.SingleFile,
                        optional: false,
                        reloadOnChange: false
                    )

                // UseSerilog
                .UseSerilog((context, configuration) =>
                {
                    configuration
                        .ReadFrom.Configuration(context.Configuration)
                        .Enrich.WithProperty("Version", Assembly.GetEntryAssembly()?.GetName().Version)
                        ;
                })

                // Configure dependency injection
                .ConfigureServices((_, serviceCollection) =>
                {
                    serviceCollection
                        .AddSingleton<IReporter, ConsoleReporter>()
                        ;
                })
                .RunCommandLineApplicationAsync<Program>(args);
        }
        catch (Exception e)
        {
            await Console.Error.WriteLineAsync(e.Message);
            Log.Logger.Fatal(e, "Unhandled exception: {Message}", e.Message);
            return ExitCode.UnhandledException;
        }
        finally
        {
            await Log.CloseAndFlushAsync();
        }
    }



    /// <summary>
    /// This method is called if the user does not specify a sub command
    /// </summary>
    /// <param name="application"></param>
    /// <returns>0 on success or any positive number for failure</returns>
    /// <remarks>
    /// - If you are not using sub commands you can rewrite this method to meet your needs
    /// - You can add and remove any parameters as needed, but you will need to configure dependency injection
    /// - If you modify this method to do async work, it is recommended to change the signature to
    ///   Task&lt;int&gt; OnExecuteAsync
    /// </remarks>
    [UsedImplicitly]
    internal int OnExecute
    (
        CommandLineApplication<Program> application
    )
    {
        application.ShowHelp();
        return ExitCode.Success;
    }
}
