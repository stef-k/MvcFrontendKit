using Microsoft.Extensions.Logging;
using MvcFrontendKit.Configuration;
using MvcFrontendKit.Build.Bundling;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using BuildConfig = MvcFrontendKit.Build.Configuration.FrontendConfig;

namespace MvcFrontendKit.Cli.Commands;

public class BuildCommand
{
    public static int Execute(bool dryRun, bool verbose)
    {
        var configPath = Path.Combine(Directory.GetCurrentDirectory(), "frontend.config.yaml");

        if (!File.Exists(configPath))
        {
            Console.Error.WriteLine($"Error: Config file not found at: {configPath}");
            Console.Error.WriteLine("Run 'dotnet frontend init' to create it");
            return 1;
        }

        try
        {
            var yaml = File.ReadAllText(configPath);
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();

            var config = deserializer.Deserialize<FrontendConfig>(yaml);

            if (dryRun)
            {
                return ExecuteDryRun(config, verbose);
            }
            else
            {
                // Parse again with the Build config type for the orchestrator
                var buildConfig = deserializer.Deserialize<BuildConfig>(yaml);
                return ExecuteBuild(buildConfig, verbose);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    private static int ExecuteDryRun(FrontendConfig config, bool verbose)
    {
        Console.WriteLine("Build Preview (Dry Run)");
        Console.WriteLine(new string('=', 50));
        Console.WriteLine();

        var jsRoot = config.JsRoot ?? "wwwroot/js";
        var cssRoot = config.CssRoot ?? "wwwroot/css";
        var conventions = config.Views?.Conventions ?? new List<ViewConvention>();
        var cssConventions = config.Views?.CssConventions ?? new List<CssConvention>();
        var bundleCount = 0;
        var totalJsSize = 0L;
        var totalCssSize = 0L;

        // Global bundles
        Console.WriteLine("Global Bundles:");
        var globalJs = config.Global?.Js ?? new List<string>();
        var globalCss = config.Global?.Css ?? new List<string>();

        if (globalJs.Any())
        {
            var sizes = globalJs.Select(f => GetFileSize(f)).ToList();
            var totalSize = sizes.Sum();
            totalJsSize += totalSize;
            bundleCount++;

            Console.WriteLine($"  global:js");
            Console.WriteLine($"    Output: /dist/js/global.[hash].js");
            Console.WriteLine($"    Input files ({globalJs.Count}):");
            for (int i = 0; i < globalJs.Count; i++)
            {
                Console.WriteLine($"      - {globalJs[i]} ({FormatSize(sizes[i])})");
            }
            Console.WriteLine($"    Total input: {FormatSize(totalSize)}");
            Console.WriteLine($"    Estimated output: ~{FormatSize((long)(totalSize * 0.3))} (minified)");
        }

        if (globalCss.Any())
        {
            var sizes = globalCss.Select(f => GetFileSize(f)).ToList();
            var totalSize = sizes.Sum();
            totalCssSize += totalSize;
            bundleCount++;

            Console.WriteLine($"  global:css");
            Console.WriteLine($"    Output: /dist/css/global.[hash].css");
            Console.WriteLine($"    Input files ({globalCss.Count}):");
            for (int i = 0; i < globalCss.Count; i++)
            {
                Console.WriteLine($"      - {globalCss[i]} ({FormatSize(sizes[i])})");
            }
            Console.WriteLine($"    Total input: {FormatSize(totalSize)}");
            Console.WriteLine($"    Estimated output: ~{FormatSize((long)(totalSize * 0.5))} (minified)");
        }

        Console.WriteLine();

        // View-specific bundles (overrides)
        var overrides = config.Views?.Overrides ?? new Dictionary<string, ViewOverride>();
        if (overrides.Any())
        {
            Console.WriteLine("View Override Bundles:");
            foreach (var (viewKey, viewOverride) in overrides)
            {
                var jsFiles = viewOverride?.Js ?? new List<string>();
                var cssFiles = viewOverride?.Css ?? new List<string>();

                if (jsFiles.Any())
                {
                    var sizes = jsFiles.Select(f => GetFileSize(f)).ToList();
                    var totalSize = sizes.Sum();
                    totalJsSize += totalSize;
                    bundleCount++;

                    Console.WriteLine($"  view:{viewKey}:js");
                    if (verbose)
                    {
                        foreach (var js in jsFiles)
                        {
                            Console.WriteLine($"    - {js}");
                        }
                    }
                    Console.WriteLine($"    Input: {FormatSize(totalSize)} -> ~{FormatSize((long)(totalSize * 0.3))}");
                }

                if (cssFiles.Any())
                {
                    var sizes = cssFiles.Select(f => GetFileSize(f)).ToList();
                    var totalSize = sizes.Sum();
                    totalCssSize += totalSize;
                    bundleCount++;

                    Console.WriteLine($"  view:{viewKey}:css");
                    if (verbose)
                    {
                        foreach (var css in cssFiles)
                        {
                            Console.WriteLine($"    - {css}");
                        }
                    }
                    Console.WriteLine($"    Input: {FormatSize(totalSize)} -> ~{FormatSize((long)(totalSize * 0.5))}");
                }
            }
            Console.WriteLine();
        }

        // Convention-based view bundles
        var jsAutoLink = config.Views?.JsAutoLinkByConvention ?? true;
        if (jsAutoLink && Directory.Exists(jsRoot))
        {
            var discoveredViews = DiscoverViewsByConvention(jsRoot, conventions);

            // Filter out views that are already overrides
            discoveredViews = discoveredViews
                .Where(kv => !overrides.ContainsKey(kv.Key))
                .ToDictionary(kv => kv.Key, kv => kv.Value);

            if (discoveredViews.Any())
            {
                Console.WriteLine($"Convention-based View Bundles ({discoveredViews.Count}):");
                foreach (var (viewKey, jsFile) in discoveredViews.OrderBy(x => x.Key))
                {
                    var size = GetFileSize(jsFile);
                    totalJsSize += size;
                    bundleCount++;

                    if (verbose)
                    {
                        Console.WriteLine($"  view:{viewKey}:js");
                        Console.WriteLine($"    - {jsFile} ({FormatSize(size)})");
                    }
                    else
                    {
                        Console.WriteLine($"  view:{viewKey}:js ({FormatSize(size)})");
                    }
                }
                Console.WriteLine();
            }
        }

        // Components
        var components = config.Components ?? new Dictionary<string, ComponentConfig>();
        if (components.Any())
        {
            Console.WriteLine($"Component Bundles ({components.Count}):");
            foreach (var (name, component) in components)
            {
                var jsFiles = component?.Js ?? new List<string>();
                var cssFiles = component?.Css ?? new List<string>();

                if (jsFiles.Any())
                {
                    var sizes = jsFiles.Select(f => GetFileSize(f)).ToList();
                    var totalSize = sizes.Sum();
                    totalJsSize += totalSize;
                    bundleCount++;

                    Console.WriteLine($"  component:{name}:js ({FormatSize(totalSize)})");
                }

                if (cssFiles.Any())
                {
                    var sizes = cssFiles.Select(f => GetFileSize(f)).ToList();
                    var totalSize = sizes.Sum();
                    totalCssSize += totalSize;
                    bundleCount++;

                    Console.WriteLine($"  component:{name}:css ({FormatSize(totalSize)})");
                }
            }
            Console.WriteLine();
        }

        // Summary
        Console.WriteLine(new string('=', 50));
        Console.WriteLine("Summary:");
        Console.WriteLine($"  Total bundles: {bundleCount}");
        Console.WriteLine($"  Total JS input: {FormatSize(totalJsSize)}");
        Console.WriteLine($"  Total CSS input: {FormatSize(totalCssSize)}");
        Console.WriteLine($"  Estimated JS output: ~{FormatSize((long)(totalJsSize * 0.3))}");
        Console.WriteLine($"  Estimated CSS output: ~{FormatSize((long)(totalCssSize * 0.5))}");
        Console.WriteLine();
        Console.WriteLine("To build for real, run: dotnet frontend build");
        Console.WriteLine("Or use MSBuild: dotnet publish -c Release");

        return 0;
    }

    private static int ExecuteBuild(BuildConfig config, bool verbose)
    {
        Console.WriteLine("Building frontend bundles...");
        Console.WriteLine();

        var projectRoot = Directory.GetCurrentDirectory();
        var logLevel = verbose ? LogLevel.Debug : LogLevel.Information;
        var logger = new ConsoleLogger("MvcFrontendKit", logLevel);

        var orchestrator = new BundleOrchestrator(config, projectRoot, logger);

        try
        {
            var success = orchestrator.BuildBundlesAsync().GetAwaiter().GetResult();

            if (success)
            {
                Console.WriteLine();
                Console.WriteLine("Build completed successfully!");
                Console.WriteLine($"  Output: {config.WebRoot}/dist/");
                Console.WriteLine($"  Manifest: {config.WebRoot}/frontend.manifest.json");
                return 0;
            }
            else
            {
                Console.Error.WriteLine("Build failed. Check the output above for errors.");
                return 1;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Build failed: {ex.Message}");
            if (verbose)
            {
                Console.Error.WriteLine(ex.StackTrace);
            }
            return 1;
        }
    }

    /// <summary>
    /// Simple console logger implementation for CLI use.
    /// </summary>
    private class ConsoleLogger : ILogger
    {
        private readonly string _name;
        private readonly LogLevel _minLevel;

        public ConsoleLogger(string name, LogLevel minLevel)
        {
            _name = name;
            _minLevel = minLevel;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= _minLevel;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;

            var message = formatter(state, exception);
            var prefix = logLevel switch
            {
                LogLevel.Debug => "  [DBG]",
                LogLevel.Information => "  [INF]",
                LogLevel.Warning => "  [WRN]",
                LogLevel.Error => "  [ERR]",
                _ => "  "
            };

            var output = logLevel >= LogLevel.Warning ? Console.Error : Console.Out;
            output.WriteLine($"{prefix} {message}");

            if (exception != null && logLevel >= LogLevel.Warning)
            {
                output.WriteLine($"       {exception.Message}");
            }
        }
    }

    private static Dictionary<string, string> DiscoverViewsByConvention(string jsRoot, List<ViewConvention> conventions)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (!Directory.Exists(jsRoot))
        {
            return result;
        }

        var jsFiles = Directory.GetFiles(jsRoot, "*.js", SearchOption.AllDirectories);

        foreach (var jsFile in jsFiles)
        {
            // Skip dist folder
            if (jsFile.Contains(Path.DirectorySeparatorChar + "dist" + Path.DirectorySeparatorChar) ||
                jsFile.Contains("/dist/"))
            {
                continue;
            }

            var relativePath = GetRelativePath(Directory.GetCurrentDirectory(), jsFile).Replace("\\", "/");
            var viewKey = TryGetViewKeyFromPath(relativePath, conventions);

            if (viewKey != null && !result.ContainsKey(viewKey))
            {
                result[viewKey] = relativePath;
            }
        }

        return result;
    }

    private static string? TryGetViewKeyFromPath(string jsPath, List<ViewConvention> conventions)
    {
        var normalizedPath = jsPath.Replace("\\", "/");

        if (!normalizedPath.EndsWith(".js", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var pathWithoutExt = normalizedPath.Substring(0, normalizedPath.Length - 3);

        if (pathWithoutExt.EndsWith("Page", StringComparison.OrdinalIgnoreCase))
        {
            pathWithoutExt = pathWithoutExt.Substring(0, pathWithoutExt.Length - 4);
        }

        foreach (var convention in conventions)
        {
            var normalizedPattern = convention.ScriptBasePattern.Replace("\\", "/");
            var patternParts = normalizedPattern.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var pathParts = pathWithoutExt.Split('/', StringSplitOptions.RemoveEmptyEntries);

            if (patternParts.Length != pathParts.Length)
            {
                continue;
            }

            var tokens = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var match = true;

            for (int i = 0; i < patternParts.Length; i++)
            {
                var patternPart = patternParts[i];
                var pathPart = pathParts[i];

                if (patternPart.StartsWith("{") && patternPart.EndsWith("}"))
                {
                    var tokenName = patternPart.Trim('{', '}');
                    tokens[tokenName] = pathPart;
                }
                else if (!patternPart.Equals(pathPart, StringComparison.OrdinalIgnoreCase))
                {
                    match = false;
                    break;
                }
            }

            if (match)
            {
                var viewKey = convention.ViewPattern;
                foreach (var (key, value) in tokens)
                {
                    viewKey = viewKey.Replace($"{{{key}}}", value, StringComparison.OrdinalIgnoreCase);
                }
                return viewKey;
            }
        }

        return null;
    }

    private static long GetFileSize(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return 0;
        }

        return new FileInfo(filePath).Length;
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024.0):F1} MB";
    }

    private static string GetRelativePath(string relativeTo, string path)
    {
        var relativeToUri = new Uri(relativeTo.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar);
        var pathUri = new Uri(path);
        var relativeUri = relativeToUri.MakeRelativeUri(pathUri);
        return Uri.UnescapeDataString(relativeUri.ToString().Replace('/', Path.DirectorySeparatorChar));
    }
}
