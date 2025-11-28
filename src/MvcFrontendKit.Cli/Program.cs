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
            "check" => HandleCheckCommand(args),
            "build" => HandleBuildCommand(args),
            "help" or "--help" or "-h" => ShowHelp(),
            "version" or "--version" or "-v" => ShowVersion(),
            _ => UnknownCommand(command)
        };
    }

    static int HandleCheckCommand(string[] args)
    {
        var verbose = args.Contains("--verbose") || args.Contains("-v");
        var checkAll = args.Contains("--all");
        var skipImports = args.Contains("--skip-imports");

        // Check for --view <viewKey>
        string? viewKey = null;
        var viewIndex = Array.IndexOf(args, "--view");
        if (viewIndex >= 0 && viewIndex < args.Length - 1)
        {
            viewKey = args[viewIndex + 1];
        }

        return CheckCommand.Execute(verbose, viewKey, checkAll, validateImports: !skipImports);
    }

    static int HandleBuildCommand(string[] args)
    {
        var dryRun = args.Contains("--dry-run");
        var verbose = args.Contains("--verbose") || args.Contains("-v");

        return BuildCommand.Execute(dryRun, verbose);
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
        Console.WriteLine("  build           Build frontend bundles (production)");
        Console.WriteLine("  help            Show this help message");
        Console.WriteLine("  version         Show version information");
        Console.WriteLine();
        Console.WriteLine("Run 'dotnet frontend help' for detailed options.");
    }

    static int ShowHelp()
    {
        Console.WriteLine("MvcFrontendKit CLI");
        Console.WriteLine();
        Console.WriteLine("Usage: dotnet frontend <command> [options]");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine();
        Console.WriteLine("  init [--force]");
        Console.WriteLine("    Create frontend.config.yaml with default settings.");
        Console.WriteLine("    --force       Overwrite existing config file");
        Console.WriteLine();
        Console.WriteLine("  check [options]");
        Console.WriteLine("    Validate configuration and check asset paths.");
        Console.WriteLine("    --verbose     Show detailed output");
        Console.WriteLine("    --view <key>  Check a specific view (e.g., 'Areas/Admin/Settings/Index')");
        Console.WriteLine("    --all         Check all discoverable views");
        Console.WriteLine("    --skip-imports  Skip import path validation");
        Console.WriteLine();
        Console.WriteLine("  build [options]");
        Console.WriteLine("    Build frontend bundles for production.");
        Console.WriteLine("    --dry-run     Preview bundles without writing files");
        Console.WriteLine("    --verbose     Show detailed build output");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  dotnet frontend init");
        Console.WriteLine("  dotnet frontend init --force");
        Console.WriteLine("  dotnet frontend check");
        Console.WriteLine("  dotnet frontend check --verbose");
        Console.WriteLine("  dotnet frontend check --view \"Areas/Admin/Settings/Index\"");
        Console.WriteLine("  dotnet frontend check --all");
        Console.WriteLine("  dotnet frontend build --dry-run");
        Console.WriteLine("  dotnet frontend build --verbose");
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
