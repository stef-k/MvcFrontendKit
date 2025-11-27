using MvcFrontendKit.Cli.Commands;

namespace MvcFrontendKit.Cli;

class Program
{
    static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            PrintUsage();
            return 1;
        }

        var command = args[0].ToLowerInvariant();

        return command switch
        {
            "init" => InitCommand.Execute(args.Contains("--force")),
            "check" => CheckCommand.Execute(args.Contains("--verbose")),
            "help" or "--help" or "-h" => ShowHelp(),
            "version" or "--version" or "-v" => ShowVersion(),
            _ => UnknownCommand(command)
        };
    }

    static void PrintUsage()
    {
        Console.WriteLine("MvcFrontendKit CLI");
        Console.WriteLine();
        Console.WriteLine("Usage: dotnet frontend <command> [options]");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  init            Create frontend.config.yaml with default settings");
        Console.WriteLine("  check           Validate config and check if assets exist");
        Console.WriteLine("  help            Show this help message");
        Console.WriteLine("  version         Show version information");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --force         Force overwrite (for init)");
        Console.WriteLine("  --verbose       Show detailed output (for check)");
    }

    static int ShowHelp()
    {
        PrintUsage();
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  dotnet frontend init");
        Console.WriteLine("  dotnet frontend init --force");
        Console.WriteLine("  dotnet frontend check");
        Console.WriteLine("  dotnet frontend check --verbose");
        return 0;
    }

    static int ShowVersion()
    {
        var version = typeof(Program).Assembly.GetName().Version;
        Console.WriteLine($"MvcFrontendKit CLI v{version}");
        return 0;
    }

    static int UnknownCommand(string command)
    {
        Console.Error.WriteLine($"Unknown command: {command}");
        Console.Error.WriteLine();
        PrintUsage();
        return 1;
    }
}
