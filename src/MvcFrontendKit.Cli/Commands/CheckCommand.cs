using System.Text.RegularExpressions;
using MvcFrontendKit.Configuration;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace MvcFrontendKit.Cli.Commands;

public class CheckCommand
{
    private static readonly Regex ImportRegex = new Regex(
        @"(?:import\s+.*?\s+from\s+['""]|import\s*\(\s*['""]|import\s+['""])(\.{1,2}/[^'""]+)['""]",
        RegexOptions.Compiled | RegexOptions.Multiline);

    private static readonly Regex ExportFromRegex = new Regex(
        @"export\s+.*?\s+from\s+['""](\.[^'""]+)['""]",
        RegexOptions.Compiled | RegexOptions.Multiline);

    public static int Execute(bool verbose, string? viewKey = null, bool checkAll = false, bool validateImports = true)
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

            // If checking a specific view
            if (!string.IsNullOrEmpty(viewKey))
            {
                return CheckSpecificView(config, viewKey, verbose, validateImports);
            }

            // If checking all views
            if (checkAll)
            {
                return CheckAllViews(config, verbose, validateImports);
            }

            // Standard check
            errors += CheckGlobalAssets(config, verbose, validateImports);
            errors += CheckViewOverrides(config, verbose, validateImports);
            errors += CheckComponents(config, verbose, validateImports);

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

    /// <summary>
    /// Checks a specific view by key and shows detailed diagnostic information.
    /// </summary>
    private static int CheckSpecificView(FrontendConfig config, string viewKey, bool verbose, bool validateImports)
    {
        Console.WriteLine($"View Diagnostics: {viewKey}");
        Console.WriteLine(new string('=', 50));
        Console.WriteLine();

        var errors = 0;

        // 1. Check if it's an explicit override
        var overrides = config.Views?.Overrides ?? new Dictionary<string, ViewOverride>();
        if (overrides.TryGetValue(viewKey, out var viewOverride))
        {
            Console.WriteLine("Resolution: Explicit Override");
            Console.WriteLine();

            var jsFiles = viewOverride?.Js ?? new List<string>();
            var cssFiles = viewOverride?.Css ?? new List<string>();

            if (jsFiles.Any())
            {
                Console.WriteLine("JS Files:");
                foreach (var jsFile in jsFiles)
                {
                    var exists = File.Exists(jsFile);
                    var status = exists ? "✓" : "✗";
                    Console.WriteLine($"  {status} {jsFile}");
                    if (!exists) errors++;
                    else if (validateImports)
                    {
                        errors += ValidateImportsInFile(jsFile, verbose);
                    }
                }
            }
            else
            {
                Console.WriteLine("JS Files: None");
            }

            Console.WriteLine();

            if (cssFiles.Any())
            {
                Console.WriteLine("CSS Files:");
                foreach (var cssFile in cssFiles)
                {
                    var exists = File.Exists(cssFile);
                    var status = exists ? "✓" : "✗";
                    Console.WriteLine($"  {status} {cssFile}");
                    if (!exists) errors++;
                }
            }
            else
            {
                Console.WriteLine("CSS Files: None");
            }
        }
        else
        {
            // 2. Try convention-based resolution
            Console.WriteLine("Resolution: Convention-based");
            Console.WriteLine();

            var conventions = config.Views?.Conventions ?? new List<ViewConvention>();
            var cssConventions = config.Views?.CssConventions ?? new List<CssConvention>();
            var jsAutoLink = config.Views?.JsAutoLinkByConvention ?? true;
            var cssAutoLink = config.Views?.CssAutoLinkByConvention ?? true;

            // JS Convention matching
            Console.WriteLine($"JS Auto-Link: {(jsAutoLink ? "Enabled" : "Disabled")}");
            if (jsAutoLink)
            {
                var jsMatch = TryMatchJsConvention(config, viewKey, conventions, verbose);
                if (jsMatch != null)
                {
                    Console.WriteLine($"  Matched Convention: {jsMatch.Convention.ViewPattern}");
                    Console.WriteLine($"  Script Base Pattern: {jsMatch.Convention.ScriptBasePattern}");
                    Console.WriteLine($"  Resolved Path: {jsMatch.ResolvedPath}");

                    if (jsMatch.FoundFile != null)
                    {
                        Console.WriteLine($"  ✓ Found: {jsMatch.FoundFile}");
                        if (validateImports)
                        {
                            errors += ValidateImportsInFile(jsMatch.FoundFile, verbose);
                        }
                    }
                    else
                    {
                        Console.WriteLine($"  ✗ No matching file found");
                        Console.WriteLine($"  Tried:");
                        foreach (var candidate in jsMatch.Candidates)
                        {
                            Console.WriteLine($"    - {candidate}");
                        }
                        errors++;
                    }
                }
                else
                {
                    Console.WriteLine($"  ✗ No convention matched view key");
                    Console.WriteLine($"  Available conventions:");
                    foreach (var conv in conventions)
                    {
                        Console.WriteLine($"    - {conv.ViewPattern}");
                    }
                    errors++;
                }
            }

            Console.WriteLine();

            // CSS Convention matching
            Console.WriteLine($"CSS Auto-Link: {(cssAutoLink ? "Enabled" : "Disabled")}");
            if (cssAutoLink)
            {
                var cssMatch = TryMatchCssConvention(config, viewKey, cssConventions, verbose);
                if (cssMatch != null)
                {
                    Console.WriteLine($"  Matched Convention: {cssMatch.Convention.ViewPattern}");
                    Console.WriteLine($"  CSS Pattern: {cssMatch.Convention.CssPattern}");
                    Console.WriteLine($"  Resolved Path: {cssMatch.ResolvedPath}");

                    if (File.Exists(cssMatch.ResolvedPath))
                    {
                        Console.WriteLine($"  ✓ Found: {cssMatch.ResolvedPath}");
                    }
                    else
                    {
                        Console.WriteLine($"  ✗ File not found");
                        // CSS not found is a warning, not error (CSS is optional)
                    }
                }
                else
                {
                    Console.WriteLine($"  No convention matched (CSS is optional)");
                }
            }
        }

        Console.WriteLine();
        Console.WriteLine(new string('=', 50));

        // Show what would be bundled in production
        Console.WriteLine();
        Console.WriteLine("Production Bundle Preview:");
        Console.WriteLine($"  Manifest Key: view:{viewKey}");

        if (errors > 0)
        {
            Console.WriteLine($"  ⚠ Bundle would fail: {errors} error(s)");
            return 1;
        }
        else
        {
            Console.WriteLine("  ✓ Would bundle successfully");
            return 0;
        }
    }

    /// <summary>
    /// Checks all discoverable views based on conventions.
    /// </summary>
    private static int CheckAllViews(FrontendConfig config, bool verbose, bool validateImports)
    {
        Console.WriteLine("Checking All Views");
        Console.WriteLine(new string('=', 50));
        Console.WriteLine();

        var totalErrors = 0;
        var viewsChecked = 0;
        var viewsWithIssues = new List<string>();

        // Discover all JS files and match them to view keys
        var jsRoot = config.JsRoot ?? "wwwroot/js";
        var conventions = config.Views?.Conventions ?? new List<ViewConvention>();

        if (!Directory.Exists(jsRoot))
        {
            Console.WriteLine($"JS Root not found: {jsRoot}");
            return 1;
        }

        var discoveredViews = new Dictionary<string, DiscoveredView>(StringComparer.OrdinalIgnoreCase);

        // Scan for JS files
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

            if (viewKey != null && !discoveredViews.ContainsKey(viewKey))
            {
                discoveredViews[viewKey] = new DiscoveredView
                {
                    ViewKey = viewKey,
                    JsFile = relativePath
                };
            }
        }

        // Also add explicit overrides
        var overrides = config.Views?.Overrides ?? new Dictionary<string, ViewOverride>();
        foreach (var kvp in overrides)
        {
            if (!discoveredViews.ContainsKey(kvp.Key))
            {
                discoveredViews[kvp.Key] = new DiscoveredView
                {
                    ViewKey = kvp.Key,
                    IsOverride = true
                };
            }
            else
            {
                discoveredViews[kvp.Key].IsOverride = true;
            }
        }

        Console.WriteLine($"Discovered {discoveredViews.Count} view(s)");
        Console.WriteLine();

        foreach (var kvp in discoveredViews.OrderBy(x => x.Key))
        {
            viewsChecked++;
            var view = kvp.Value;
            var viewErrors = 0;

            if (verbose)
            {
                Console.WriteLine($"[{viewsChecked}] {view.ViewKey}");
                Console.WriteLine($"    Source: {(view.IsOverride ? "Override" : "Convention")}");
            }

            if (view.IsOverride && overrides.TryGetValue(view.ViewKey, out var viewOverride))
            {
                var jsFiles2 = viewOverride?.Js ?? new List<string>();
                foreach (var js in jsFiles2)
                {
                    if (!File.Exists(js))
                    {
                        if (verbose) Console.WriteLine($"    ✗ JS missing: {js}");
                        viewErrors++;
                    }
                    else if (validateImports)
                    {
                        viewErrors += ValidateImportsInFile(js, false); // Quiet mode for batch
                    }
                }

                var cssFiles = viewOverride?.Css ?? new List<string>();
                foreach (var css in cssFiles)
                {
                    if (!File.Exists(css))
                    {
                        if (verbose) Console.WriteLine($"    ✗ CSS missing: {css}");
                        viewErrors++;
                    }
                }
            }
            else if (!string.IsNullOrEmpty(view.JsFile))
            {
                if (!File.Exists(view.JsFile))
                {
                    if (verbose) Console.WriteLine($"    ✗ JS missing: {view.JsFile}");
                    viewErrors++;
                }
                else if (validateImports)
                {
                    viewErrors += ValidateImportsInFile(view.JsFile, false);
                }
            }

            if (viewErrors > 0)
            {
                viewsWithIssues.Add(view.ViewKey);
                totalErrors += viewErrors;
                if (!verbose) Console.WriteLine($"✗ {view.ViewKey}: {viewErrors} issue(s)");
            }
            else if (verbose)
            {
                Console.WriteLine($"    ✓ OK");
            }

            if (verbose) Console.WriteLine();
        }

        Console.WriteLine(new string('=', 50));
        Console.WriteLine();
        Console.WriteLine($"Summary: {viewsChecked} view(s) checked");

        if (totalErrors > 0)
        {
            Console.WriteLine($"✗ {viewsWithIssues.Count} view(s) with issues ({totalErrors} total error(s))");
            return 1;
        }
        else
        {
            Console.WriteLine("✓ All views OK");
            return 0;
        }
    }

    /// <summary>
    /// Validates import paths in a JS file.
    /// </summary>
    private static int ValidateImportsInFile(string jsFilePath, bool verbose)
    {
        if (!File.Exists(jsFilePath))
        {
            return 0;
        }

        var errors = 0;
        var jsDir = Path.GetDirectoryName(jsFilePath) ?? ".";

        try
        {
            var content = File.ReadAllText(jsFilePath);

            // Find all relative imports
            var imports = new List<string>();

            foreach (Match match in ImportRegex.Matches(content))
            {
                imports.Add(match.Groups[1].Value);
            }

            foreach (Match match in ExportFromRegex.Matches(content))
            {
                imports.Add(match.Groups[1].Value);
            }

            foreach (var importPath in imports.Distinct())
            {
                var resolvedPath = ResolveImportPath(jsDir, importPath);

                if (!File.Exists(resolvedPath))
                {
                    if (verbose)
                    {
                        Console.WriteLine($"    ✗ Import not found: {importPath}");
                        Console.WriteLine($"      Resolved to: {resolvedPath}");
                    }
                    else
                    {
                        Console.WriteLine($"  ✗ Broken import in {Path.GetFileName(jsFilePath)}: {importPath}");
                    }
                    errors++;
                }
                else if (verbose)
                {
                    Console.WriteLine($"    ✓ Import OK: {importPath}");
                }
            }
        }
        catch (Exception ex)
        {
            if (verbose)
            {
                Console.WriteLine($"    ⚠ Could not parse imports: {ex.Message}");
            }
        }

        return errors;
    }

    private static string ResolveImportPath(string baseDir, string importPath)
    {
        // Handle .js extension
        var pathWithExt = importPath;
        if (!importPath.EndsWith(".js", StringComparison.OrdinalIgnoreCase))
        {
            pathWithExt = importPath + ".js";
        }

        var resolved = Path.GetFullPath(Path.Combine(baseDir, pathWithExt));

        // Also try index.js for directory imports
        if (!File.Exists(resolved) && !importPath.EndsWith(".js"))
        {
            var indexPath = Path.GetFullPath(Path.Combine(baseDir, importPath, "index.js"));
            if (File.Exists(indexPath))
            {
                return indexPath;
            }
        }

        return resolved;
    }

    private static int CheckGlobalAssets(FrontendConfig config, bool verbose, bool validateImports)
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
            else
            {
                if (verbose) Console.WriteLine($"  ✓ JS: {jsFile}");
                if (validateImports)
                {
                    errors += ValidateImportsInFile(jsFile, verbose);
                }
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

    private static int CheckViewOverrides(FrontendConfig config, bool verbose, bool validateImports)
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
                else
                {
                    if (verbose) Console.WriteLine($"    ✓ JS: {jsFile}");
                    if (validateImports)
                    {
                        errors += ValidateImportsInFile(jsFile, verbose);
                    }
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

    private static int CheckComponents(FrontendConfig config, bool verbose, bool validateImports)
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
                else
                {
                    if (verbose) Console.WriteLine($"    ✓ JS: {jsFile}");
                    if (validateImports)
                    {
                        errors += ValidateImportsInFile(jsFile, verbose);
                    }
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

    #region Convention Matching Helpers

    private class JsConventionMatch
    {
        public ViewConvention Convention { get; set; } = null!;
        public string ResolvedPath { get; set; } = "";
        public string? FoundFile { get; set; }
        public List<string> Candidates { get; set; } = new();
    }

    private class CssConventionMatch
    {
        public CssConvention Convention { get; set; } = null!;
        public string ResolvedPath { get; set; } = "";
    }

    private class DiscoveredView
    {
        public string ViewKey { get; set; } = "";
        public string? JsFile { get; set; }
        public bool IsOverride { get; set; }
    }

    private static JsConventionMatch? TryMatchJsConvention(
        FrontendConfig config,
        string viewKey,
        List<ViewConvention> conventions,
        bool verbose)
    {
        foreach (var convention in conventions)
        {
            if (TryMatchViewPattern(viewKey, convention.ViewPattern, out var tokens))
            {
                var scriptBasePath = ApplyTokens(convention.ScriptBasePattern, tokens);
                var candidates = GetJsCandidates(scriptBasePath);

                string? foundFile = null;
                foreach (var candidate in candidates)
                {
                    if (File.Exists(candidate))
                    {
                        foundFile = candidate;
                        break;
                    }
                }

                return new JsConventionMatch
                {
                    Convention = convention,
                    ResolvedPath = scriptBasePath,
                    FoundFile = foundFile,
                    Candidates = candidates
                };
            }
        }

        return null;
    }

    private static CssConventionMatch? TryMatchCssConvention(
        FrontendConfig config,
        string viewKey,
        List<CssConvention> conventions,
        bool verbose)
    {
        foreach (var convention in conventions)
        {
            if (TryMatchViewPattern(viewKey, convention.ViewPattern, out var tokens))
            {
                var cssPath = ApplyTokens(convention.CssPattern, tokens);

                return new CssConventionMatch
                {
                    Convention = convention,
                    ResolvedPath = cssPath
                };
            }
        }

        return null;
    }

    private static bool TryMatchViewPattern(string viewKey, string pattern, out Dictionary<string, string> tokens)
    {
        tokens = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var patternParts = pattern.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var keyParts = viewKey.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (patternParts.Length != keyParts.Length)
        {
            return false;
        }

        for (int i = 0; i < patternParts.Length; i++)
        {
            var patternPart = patternParts[i];
            var keyPart = keyParts[i];

            if (patternPart.StartsWith("{") && patternPart.EndsWith("}"))
            {
                var tokenName = patternPart.Trim('{', '}');
                tokens[tokenName] = keyPart;
            }
            else if (!patternPart.Equals(keyPart, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static string ApplyTokens(string pattern, Dictionary<string, string> tokens)
    {
        var result = pattern;

        foreach (var (key, value) in tokens)
        {
            result = result.Replace($"{{{key}}}", value, StringComparison.OrdinalIgnoreCase);
        }

        return result;
    }

    private static List<string> GetJsCandidates(string basePath)
    {
        var action = Path.GetFileName(basePath);
        var directory = Path.GetDirectoryName(basePath) ?? basePath;

        var camelCase = ToCamelCase(action);
        var lowercase = action.ToLowerInvariant();
        var pascalCase = action;

        return new List<string>
        {
            Path.Combine(directory, $"{camelCase}.js"),
            Path.Combine(directory, $"{lowercase}.js"),
            Path.Combine(directory, $"{pascalCase}.js"),
            Path.Combine(directory, $"{camelCase}Page.js"),
            Path.Combine(directory, $"{lowercase}Page.js"),
        };
    }

    private static string ToCamelCase(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        return char.ToLowerInvariant(input[0]) + input.Substring(1);
    }

    private static string? TryGetViewKeyFromPath(string jsPath, List<ViewConvention> conventions)
    {
        var normalizedPath = jsPath.Replace("\\", "/");

        // Remove .js extension
        if (!normalizedPath.EndsWith(".js", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
        var pathWithoutExt = normalizedPath.Substring(0, normalizedPath.Length - 3);

        // Remove "Page" suffix if present
        if (pathWithoutExt.EndsWith("Page", StringComparison.OrdinalIgnoreCase))
        {
            pathWithoutExt = pathWithoutExt.Substring(0, pathWithoutExt.Length - 4);
        }

        foreach (var convention in conventions)
        {
            var viewKey = TryMatchScriptPattern(pathWithoutExt, convention);
            if (viewKey != null)
            {
                return viewKey;
            }
        }

        return null;
    }

    private static string? TryMatchScriptPattern(string jsPath, ViewConvention convention)
    {
        var normalizedPattern = convention.ScriptBasePattern.Replace("\\", "/");
        var patternParts = normalizedPattern.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var pathParts = jsPath.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (patternParts.Length != pathParts.Length)
        {
            return null;
        }

        var tokens = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

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
                return null;
            }
        }

        return ApplyTokens(convention.ViewPattern, tokens);
    }

    private static string GetRelativePath(string relativeTo, string path)
    {
        return Path.GetRelativePath(relativeTo, path);
    }

    #endregion
}
