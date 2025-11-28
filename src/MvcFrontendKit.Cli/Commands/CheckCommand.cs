using MvcFrontendKit.Configuration;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace MvcFrontendKit.Cli.Commands;

public class CheckCommand
{
    public static int Execute(bool verbose)
    {
        var configPath = Path.Combine(Directory.GetCurrentDirectory(), "frontend.config.yaml");

        if (!File.Exists(configPath))
        {
            Console.Error.WriteLine($"Error: Config file not found at: {configPath}");
            Console.Error.WriteLine("Run 'dotnet frontend init' to create it");
            return 1;
        }

        Console.WriteLine($"Checking config: {configPath}");
        Console.WriteLine();

        try
        {
            var yaml = File.ReadAllText(configPath);
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();

            var config = deserializer.Deserialize<FrontendConfig>(yaml);

            Console.WriteLine("✓ Config file is valid YAML");
            Console.WriteLine($"  Mode: {config.Mode}");
            Console.WriteLine($"  App Base Path: {config.AppBasePath}");
            Console.WriteLine($"  Web Root: {config.WebRoot}");
            Console.WriteLine();

            var errors = 0;
            var warnings = 0;

            errors += CheckGlobalAssets(config, verbose);
            errors += CheckViewOverrides(config, verbose);
            errors += CheckComponents(config, verbose);

            if (verbose)
            {
                PrintDetailedInfo(config);
            }

            Console.WriteLine();
            if (errors > 0)
            {
                Console.WriteLine($"✗ Found {errors} error(s)");
                return 1;
            }
            else
            {
                Console.WriteLine($"✓ All checks passed");
                if (warnings > 0)
                {
                    Console.WriteLine($"  ({warnings} warning(s))");
                }
                return 0;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error parsing config: {ex.Message}");
            return 1;
        }
    }

    private static int CheckGlobalAssets(FrontendConfig config, bool verbose)
    {
        var errors = 0;

        Console.WriteLine("Global Assets:");

        var globalJs = config.Global?.Js ?? new List<string>();
        var globalCss = config.Global?.Css ?? new List<string>();

        foreach (var jsFile in globalJs)
        {
            if (!File.Exists(jsFile))
            {
                Console.WriteLine($"  ✗ JS not found: {jsFile}");
                errors++;
            }
            else if (verbose)
            {
                Console.WriteLine($"  ✓ JS: {jsFile}");
            }
        }

        foreach (var cssFile in globalCss)
        {
            if (!File.Exists(cssFile))
            {
                Console.WriteLine($"  ✗ CSS not found: {cssFile}");
                errors++;
            }
            else if (verbose)
            {
                Console.WriteLine($"  ✓ CSS: {cssFile}");
            }
        }

        if (errors == 0 && !verbose)
        {
            Console.WriteLine($"  ✓ {globalJs.Count} JS, {globalCss.Count} CSS");
        }

        Console.WriteLine();
        return errors;
    }

    private static int CheckViewOverrides(FrontendConfig config, bool verbose)
    {
        var errors = 0;

        var overrides = config.Views?.Overrides ?? new Dictionary<string, ViewOverride>();

        if (!overrides.Any())
        {
            if (verbose)
            {
                Console.WriteLine("View Overrides: None");
                Console.WriteLine();
            }
            return 0;
        }

        Console.WriteLine("View Overrides:");

        foreach (var (viewKey, viewOverride) in overrides)
        {
            if (verbose)
            {
                Console.WriteLine($"  {viewKey}:");
            }

            var jsFiles = viewOverride?.Js ?? new List<string>();
            var cssFiles = viewOverride?.Css ?? new List<string>();

            foreach (var jsFile in jsFiles)
            {
                if (!File.Exists(jsFile))
                {
                    Console.WriteLine($"    ✗ JS not found: {jsFile}");
                    errors++;
                }
                else if (verbose)
                {
                    Console.WriteLine($"    ✓ JS: {jsFile}");
                }
            }

            foreach (var cssFile in cssFiles)
            {
                if (!File.Exists(cssFile))
                {
                    Console.WriteLine($"    ✗ CSS not found: {cssFile}");
                    errors++;
                }
                else if (verbose)
                {
                    Console.WriteLine($"    ✓ CSS: {cssFile}");
                }
            }
        }

        if (errors == 0 && !verbose)
        {
            Console.WriteLine($"  ✓ {overrides.Count} view override(s)");
        }

        Console.WriteLine();
        return errors;
    }

    private static int CheckComponents(FrontendConfig config, bool verbose)
    {
        var errors = 0;

        var components = config.Components ?? new Dictionary<string, ComponentConfig>();

        if (!components.Any())
        {
            if (verbose)
            {
                Console.WriteLine("Components: None");
                Console.WriteLine();
            }
            return 0;
        }

        Console.WriteLine("Components:");

        foreach (var (componentName, component) in components)
        {
            if (verbose)
            {
                Console.WriteLine($"  {componentName}:");
            }

            var jsFiles = component?.Js ?? new List<string>();
            var cssFiles = component?.Css ?? new List<string>();
            var depends = component?.Depends ?? new List<string>();

            foreach (var jsFile in jsFiles)
            {
                if (!File.Exists(jsFile))
                {
                    Console.WriteLine($"    ✗ JS not found: {jsFile}");
                    errors++;
                }
                else if (verbose)
                {
                    Console.WriteLine($"    ✓ JS: {jsFile}");
                }
            }

            foreach (var cssFile in cssFiles)
            {
                if (!File.Exists(cssFile))
                {
                    Console.WriteLine($"    ✗ CSS not found: {cssFile}");
                    errors++;
                }
                else if (verbose)
                {
                    Console.WriteLine($"    ✓ CSS: {cssFile}");
                }
            }

            if (depends.Any() && verbose)
            {
                Console.WriteLine($"    Dependencies: {string.Join(", ", depends)}");
            }
        }

        if (errors == 0 && !verbose)
        {
            Console.WriteLine($"  ✓ {components.Count} component(s)");
        }

        Console.WriteLine();
        return errors;
    }

    private static void PrintDetailedInfo(FrontendConfig config)
    {
        Console.WriteLine("Configuration Details:");
        Console.WriteLine($"  Config Version: {config.ConfigVersion}");
        Console.WriteLine($"  JS Auto-Link: {config.Views?.JsAutoLinkByConvention ?? true}");
        Console.WriteLine($"  CSS Auto-Link: {config.Views?.CssAutoLinkByConvention ?? true}");
        Console.WriteLine($"  Import Map Enabled: {config.ImportMap?.Enabled ?? true}");
        Console.WriteLine($"  CSS Allow Relative: {config.CssUrlPolicy?.AllowRelative ?? false}");
        Console.WriteLine($"  CSS Resolve Imports: {config.CssUrlPolicy?.ResolveImports ?? true}");
        Console.WriteLine($"  Clean Dist On Build: {config.Output?.CleanDistOnBuild ?? true}");

        var conventions = config.Views?.Conventions ?? new List<ViewConvention>();
        if (conventions.Any())
        {
            Console.WriteLine();
            Console.WriteLine("JS Conventions:");
            foreach (var convention in conventions)
            {
                Console.WriteLine($"  {convention.ViewPattern}");
                Console.WriteLine($"    -> {convention.ScriptBasePattern}");
            }
        }

        var cssConventions = config.Views?.CssConventions ?? new List<CssConvention>();
        if (cssConventions.Any())
        {
            Console.WriteLine();
            Console.WriteLine("CSS Conventions:");
            foreach (var convention in cssConventions)
            {
                Console.WriteLine($"  {convention.ViewPattern}");
                Console.WriteLine($"    -> {convention.CssPattern}");
            }
        }
    }
}
