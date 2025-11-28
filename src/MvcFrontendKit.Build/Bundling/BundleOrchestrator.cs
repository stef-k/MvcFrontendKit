using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MvcFrontendKit.Build.Configuration;

namespace MvcFrontendKit.Build.Bundling;

public class BundleOrchestrator
{
    private const string VersionMarkerFileName = ".mvcfrontendkit-version";
    private readonly FrontendConfig _config;
    private readonly string _projectRoot;
    private readonly ILogger _logger;

    // Tool version - update this when releasing new versions
    private static string ToolVersion => typeof(BundleOrchestrator).Assembly.GetName().Version?.ToString() ?? "1.0.0";

    public BundleOrchestrator(FrontendConfig config, string projectRoot, ILogger logger)
    {
        _config = config;
        _projectRoot = projectRoot;
        _logger = logger;
    }

    public async Task<bool> BuildBundlesAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting frontend bundle process...");
        _logger.LogInformation("MvcFrontendKit version: {Version}", ToolVersion);

        try
        {
            var distDir = Path.Combine(_projectRoot, _config.WebRoot, "dist");
            var distJsDir = Path.Combine(distDir, _config.DistJsSubPath);
            var distCssDir = Path.Combine(distDir, _config.DistCssSubPath);

            // Check version marker and clean if version changed
            var versionChanged = CheckAndHandleVersionChange(distDir);

            if (_config.Output.CleanDistOnBuild || versionChanged)
            {
                CleanDistDirectories(distJsDir, distCssDir);
            }

            // Clean SDK compressed cache to prevent stale reference errors
            CleanSdkCompressedCache();

            Directory.CreateDirectory(distJsDir);
            Directory.CreateDirectory(distCssDir);

            var manifest = new Dictionary<string, object>();
            var runner = new EsbuildRunner(_logger);

            switch (_config.Mode.ToLowerInvariant())
            {
                case "single":
                    await BuildSingleModeAsync(runner, distJsDir, distCssDir, manifest, cancellationToken);
                    break;

                case "areas":
                    await BuildAreasModeAsync(runner, distJsDir, distCssDir, manifest, cancellationToken);
                    break;

                case "views":
                    await BuildViewsModeAsync(runner, distJsDir, distCssDir, manifest, cancellationToken);
                    break;

                default:
                    _logger.LogError("Unknown bundling mode: {Mode}", _config.Mode);
                    return false;
            }

            await BuildComponentsAsync(runner, distJsDir, distCssDir, manifest, cancellationToken);

            await WriteManifestAsync(manifest);

            // Write version marker after successful build
            WriteVersionMarker(distDir);

            _logger.LogInformation("Frontend bundle process completed successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError("Frontend bundle process failed: {Message}", ex.Message);
            _logger.LogError("Exception type: {Type}", ex.GetType().FullName);
            if (ex.InnerException != null)
            {
                _logger.LogError("Inner exception: {Inner}", ex.InnerException.Message);
            }
            _logger.LogError("Stack trace: {StackTrace}", ex.StackTrace);
            return false;
        }
    }

    /// <summary>
    /// Checks if the tool version has changed since last build.
    /// If changed, performs a full clean to prevent stale cache issues.
    /// </summary>
    private bool CheckAndHandleVersionChange(string distDir)
    {
        var markerPath = Path.Combine(distDir, VersionMarkerFileName);

        if (!File.Exists(markerPath))
        {
            _logger.LogInformation("No version marker found - will perform clean build");
            return true;
        }

        try
        {
            var previousVersion = File.ReadAllText(markerPath).Trim();
            if (previousVersion != ToolVersion)
            {
                _logger.LogInformation("Version changed from {Previous} to {Current} - performing clean build",
                    previousVersion, ToolVersion);
                return true;
            }

            _logger.LogDebug("Version unchanged ({Version})", ToolVersion);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to read version marker: {Message} - performing clean build", ex.Message);
            return true;
        }
    }

    /// <summary>
    /// Writes the current tool version to a marker file in the dist directory.
    /// </summary>
    private void WriteVersionMarker(string distDir)
    {
        try
        {
            Directory.CreateDirectory(distDir);
            var markerPath = Path.Combine(distDir, VersionMarkerFileName);
            File.WriteAllText(markerPath, ToolVersion);
            _logger.LogDebug("Wrote version marker: {Version}", ToolVersion);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to write version marker: {Message}", ex.Message);
        }
    }

    /// <summary>
    /// Cleans the ASP.NET SDK's compressed static assets cache in obj/ folder.
    /// This prevents stale reference errors when fingerprinted filenames change.
    /// </summary>
    private void CleanSdkCompressedCache()
    {
        try
        {
            var objDir = Path.Combine(_projectRoot, "obj");
            if (!Directory.Exists(objDir))
            {
                return;
            }

            // Find and clean compressed cache directories
            // Pattern: obj/{Configuration}/{TargetFramework}/compressed/
            var compressedDirs = Directory.GetDirectories(objDir, "compressed", SearchOption.AllDirectories);

            foreach (var compressedDir in compressedDirs)
            {
                try
                {
                    _logger.LogInformation("Cleaning SDK compressed cache: {Dir}", compressedDir);
                    Directory.Delete(compressedDir, recursive: true);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Failed to clean compressed cache {Dir}: {Message}", compressedDir, ex.Message);
                }
            }

            // Also clean staticwebassets intermediate files that reference fingerprinted assets
            var staticAssetFiles = new[]
            {
                "staticwebassets.build.json",
                "staticwebassets.publish.json",
                "staticwebassets/*.json"
            };

            foreach (var pattern in staticAssetFiles)
            {
                var files = Directory.GetFiles(objDir, Path.GetFileName(pattern), SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    // Only delete files that might contain stale frontend asset references
                    if (file.Contains("staticwebassets"))
                    {
                        try
                        {
                            _logger.LogDebug("Cleaning static assets cache file: {File}", file);
                            File.Delete(file);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning("Failed to clean cache file {File}: {Message}", file, ex.Message);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to clean SDK compressed cache: {Message}", ex.Message);
        }
    }

    private void CleanDistDirectories(string distJsDir, string distCssDir)
    {
        _logger.LogInformation("Cleaning dist directories...");

        if (Directory.Exists(distJsDir))
        {
            Directory.Delete(distJsDir, recursive: true);
        }

        if (Directory.Exists(distCssDir))
        {
            Directory.Delete(distCssDir, recursive: true);
        }
    }

    private async Task BuildSingleModeAsync(
        EsbuildRunner runner,
        string distJsDir,
        string distCssDir,
        Dictionary<string, object> manifest,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Building in single mode...");

        if (_config.Global.Js.Any())
        {
            var jsResult = await BuildJsBundle(
                runner,
                _config.Global.Js,
                Path.Combine(distJsDir, "global.[hash].js"),
                cancellationToken);

            if (jsResult != null)
            {
                manifest["global:js"] = new List<string> { jsResult };
            }
        }

        if (_config.Global.Css.Any())
        {
            var cssResult = await BuildCssBundle(
                runner,
                _config.Global.Css,
                Path.Combine(distCssDir, "global.[hash].css"),
                cancellationToken);

            if (cssResult != null)
            {
                manifest["global:css"] = new List<string> { cssResult };
            }
        }
    }

    private async Task BuildAreasModeAsync(
        EsbuildRunner runner,
        string distJsDir,
        string distCssDir,
        Dictionary<string, object> manifest,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Building in areas mode...");

        await BuildSingleModeAsync(runner, distJsDir, distCssDir, manifest, cancellationToken);
    }

    private async Task BuildViewsModeAsync(
        EsbuildRunner runner,
        string distJsDir,
        string distCssDir,
        Dictionary<string, object> manifest,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Building in views mode...");
        _logger.LogInformation("  Global JS files: {Count}", _config.Global.Js.Count);
        foreach (var js in _config.Global.Js)
        {
            _logger.LogInformation("    - {File}", js);
        }
        _logger.LogInformation("  Global CSS files: {Count}", _config.Global.Css.Count);
        foreach (var css in _config.Global.Css)
        {
            _logger.LogInformation("    - {File}", css);
        }
        _logger.LogInformation("  View overrides: {Count}", _config.Views.Overrides.Count);
        _logger.LogInformation("  Components: {Count}", _config.Components.Count);

        if (_config.Global.Js.Any())
        {
            var jsResult = await BuildJsBundle(
                runner,
                _config.Global.Js,
                Path.Combine(distJsDir, "global.[hash].js"),
                cancellationToken);

            if (jsResult != null)
            {
                manifest["global:js"] = new List<string> { jsResult };
            }
        }

        if (_config.Global.Css.Any())
        {
            var cssResult = await BuildCssBundle(
                runner,
                _config.Global.Css,
                Path.Combine(distCssDir, "global.[hash].css"),
                cancellationToken);

            if (cssResult != null)
            {
                manifest["global:css"] = new List<string> { cssResult };
            }
        }

        // Track which view keys have been processed (to avoid duplicate bundling)
        var processedViewKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Process explicit overrides first
        foreach (var kvp in _config.Views.Overrides)
        {
            var viewKey = kvp.Key;
            var viewOverride = kvp.Value;
            var viewData = new Dictionary<string, object>();

            if (viewOverride.Js.Any())
            {
                var safeName = MakeSafeName(viewKey);
                var jsResult = await BuildJsBundle(
                    runner,
                    viewOverride.Js,
                    Path.Combine(distJsDir, $"{safeName}.[hash].js"),
                    cancellationToken);

                if (jsResult != null)
                {
                    viewData["js"] = new List<string> { jsResult };
                }
            }

            if (viewOverride.Css.Any())
            {
                var safeName = MakeSafeName(viewKey);
                var cssResult = await BuildCssBundle(
                    runner,
                    viewOverride.Css,
                    Path.Combine(distCssDir, $"{safeName}.[hash].css"),
                    cancellationToken);

                if (cssResult != null)
                {
                    viewData["css"] = new List<string> { cssResult };
                }
            }

            if (viewData.Any())
            {
                manifest[$"view:{viewKey}"] = viewData;
                processedViewKeys.Add(viewKey);
            }
        }

        // Discover and bundle convention-based view JS files
        if (_config.Views.JsAutoLinkByConvention)
        {
            var discoveredJs = DiscoverViewJsByConvention();
            _logger.LogInformation("Discovered {Count} view JS files by convention", discoveredJs.Count);

            foreach (var kvp in discoveredJs)
            {
                var viewKey = kvp.Key;
                var jsFilePath = kvp.Value;

                if (processedViewKeys.Contains(viewKey))
                {
                    _logger.LogInformation("  Skipping {ViewKey} (already has override)", viewKey);
                    continue;
                }

                _logger.LogInformation("  Bundling {ViewKey} <- {JsFile}", viewKey, jsFilePath);

                var safeName = MakeSafeName(viewKey);
                var jsResult = await BuildJsBundle(
                    runner,
                    new List<string> { jsFilePath },
                    Path.Combine(distJsDir, $"{safeName}.[hash].js"),
                    cancellationToken);

                if (jsResult != null)
                {
                    if (manifest.ContainsKey($"view:{viewKey}"))
                    {
                        var viewData = (Dictionary<string, object>)manifest[$"view:{viewKey}"];
                        viewData["js"] = new List<string> { jsResult };
                    }
                    else
                    {
                        manifest[$"view:{viewKey}"] = new Dictionary<string, object>
                        {
                            ["js"] = new List<string> { jsResult }
                        };
                    }
                    processedViewKeys.Add(viewKey);
                }
            }
        }

        // Discover and bundle convention-based view CSS files
        if (_config.Views.CssAutoLinkByConvention)
        {
            var discoveredCss = DiscoverViewCssByConvention();
            _logger.LogInformation("Discovered {Count} view CSS files by convention", discoveredCss.Count);

            foreach (var kvp in discoveredCss)
            {
                var viewKey = kvp.Key;
                var cssFilePath = kvp.Value;

                // Check if this view key already has CSS from override
                if (_config.Views.Overrides.TryGetValue(viewKey, out var existingOverride) && existingOverride.Css.Any())
                {
                    _logger.LogInformation("  Skipping CSS for {ViewKey} (already has override)", viewKey);
                    continue;
                }

                _logger.LogInformation("  Bundling CSS {ViewKey} <- {CssFile}", viewKey, cssFilePath);

                var safeName = MakeSafeName(viewKey);
                var cssResult = await BuildCssBundle(
                    runner,
                    new List<string> { cssFilePath },
                    Path.Combine(distCssDir, $"{safeName}.[hash].css"),
                    cancellationToken);

                if (cssResult != null)
                {
                    if (manifest.ContainsKey($"view:{viewKey}"))
                    {
                        var viewData = (Dictionary<string, object>)manifest[$"view:{viewKey}"];
                        viewData["css"] = new List<string> { cssResult };
                    }
                    else
                    {
                        manifest[$"view:{viewKey}"] = new Dictionary<string, object>
                        {
                            ["css"] = new List<string> { cssResult }
                        };
                    }
                }
            }
        }
    }

    /// <summary>
    /// Discovers view JS files by scanning jsRoot and matching against conventions.
    /// Returns a dictionary of view key -> relative JS file path.
    /// </summary>
    private Dictionary<string, string> DiscoverViewJsByConvention()
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var jsRootPath = Path.Combine(_projectRoot, _config.JsRoot);

        if (!Directory.Exists(jsRootPath))
        {
            _logger.LogWarning("JS root directory does not exist: {JsRoot}", jsRootPath);
            return result;
        }

        // Scan for all JS files
        var jsFiles = Directory.GetFiles(jsRootPath, "*.js", SearchOption.AllDirectories);
        _logger.LogInformation("Found {Count} JS files in {JsRoot}", jsFiles.Length, _config.JsRoot);

        foreach (var jsFile in jsFiles)
        {
            // Skip files in dist folder
            if (jsFile.Contains(Path.DirectorySeparatorChar + "dist" + Path.DirectorySeparatorChar) ||
                jsFile.Contains("/dist/"))
            {
                continue;
            }

            // Get relative path from project root
            var relativePath = GetRelativePath(_projectRoot, jsFile).Replace("\\", "/");

            // Try to match against each convention
            foreach (var convention in _config.Views.Conventions)
            {
                if (TryMatchScriptPath(relativePath, convention.ScriptBasePattern, out var tokens))
                {
                    var viewKey = ApplyTokens(convention.ViewPattern, tokens);

                    // Only add if not already found (first match wins)
                    if (!result.ContainsKey(viewKey))
                    {
                        result[viewKey] = relativePath;
                        _logger.LogDebug("  Matched: {JsFile} -> {ViewKey}", relativePath, viewKey);
                    }
                    break;
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Discovers view CSS files by scanning cssRoot and matching against conventions.
    /// Returns a dictionary of view key -> relative CSS file path.
    /// </summary>
    private Dictionary<string, string> DiscoverViewCssByConvention()
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var cssRootPath = Path.Combine(_projectRoot, _config.CssRoot);

        if (!Directory.Exists(cssRootPath))
        {
            _logger.LogWarning("CSS root directory does not exist: {CssRoot}", cssRootPath);
            return result;
        }

        // Scan for all CSS files
        var cssFiles = Directory.GetFiles(cssRootPath, "*.css", SearchOption.AllDirectories);
        _logger.LogInformation("Found {Count} CSS files in {CssRoot}", cssFiles.Length, _config.CssRoot);

        foreach (var cssFile in cssFiles)
        {
            // Skip files in dist folder
            if (cssFile.Contains(Path.DirectorySeparatorChar + "dist" + Path.DirectorySeparatorChar) ||
                cssFile.Contains("/dist/"))
            {
                continue;
            }

            // Get relative path from project root
            var relativePath = GetRelativePath(_projectRoot, cssFile).Replace("\\", "/");

            // Try to match against each CSS convention
            foreach (var convention in _config.Views.CssConventions)
            {
                if (TryMatchCssPath(relativePath, convention.CssPattern, out var tokens))
                {
                    var viewKey = ApplyTokens(convention.ViewPattern, tokens);

                    // Only add if not already found (first match wins)
                    if (!result.ContainsKey(viewKey))
                    {
                        result[viewKey] = relativePath;
                        _logger.LogDebug("  Matched: {CssFile} -> {ViewKey}", relativePath, viewKey);
                    }
                    break;
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Tries to match a JS file path against a script base pattern.
    /// Extracts tokens (Controller, Action, Area) from the path.
    /// Handles various JS file naming conventions (camelCase, lowercase, PascalCase, *Page.js).
    /// </summary>
    private bool TryMatchScriptPath(string jsPath, string scriptBasePattern, out Dictionary<string, string> tokens)
    {
        tokens = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Normalize paths
        var normalizedJsPath = jsPath.Replace("\\", "/");
        var normalizedPattern = scriptBasePattern.Replace("\\", "/");

        // Remove .js extension from the file path
        if (!normalizedJsPath.EndsWith(".js", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        var jsPathWithoutExt = normalizedJsPath.Substring(0, normalizedJsPath.Length - 3);

        // Remove "Page" suffix if present (e.g., indexPage.js -> index)
        var jsPathBase = jsPathWithoutExt;
        if (jsPathBase.EndsWith("Page", StringComparison.OrdinalIgnoreCase))
        {
            jsPathBase = jsPathBase.Substring(0, jsPathBase.Length - 4);
        }

        // Split into parts
        var patternParts = normalizedPattern.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
        var pathParts = jsPathBase.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

        if (patternParts.Length != pathParts.Length)
        {
            return false;
        }

        for (int i = 0; i < patternParts.Length; i++)
        {
            var patternPart = patternParts[i];
            var pathPart = pathParts[i];

            if (patternPart.StartsWith("{") && patternPart.EndsWith("}"))
            {
                // Extract token name
                var tokenName = patternPart.Trim('{', '}');
                tokens[tokenName] = pathPart;
            }
            else if (!patternPart.Equals(pathPart, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Tries to match a CSS file path against a CSS pattern.
    /// Extracts tokens (Controller, Action, Area) from the path.
    /// </summary>
    private bool TryMatchCssPath(string cssPath, string cssPattern, out Dictionary<string, string> tokens)
    {
        tokens = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Normalize paths
        var normalizedCssPath = cssPath.Replace("\\", "/");
        var normalizedPattern = cssPattern.Replace("\\", "/");

        // Remove .css extension from both
        if (!normalizedCssPath.EndsWith(".css", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        var cssPathWithoutExt = normalizedCssPath.Substring(0, normalizedCssPath.Length - 4);

        var patternWithoutExt = normalizedPattern;
        if (patternWithoutExt.EndsWith(".css", StringComparison.OrdinalIgnoreCase))
        {
            patternWithoutExt = patternWithoutExt.Substring(0, patternWithoutExt.Length - 4);
        }

        // Split into parts
        var patternParts = patternWithoutExt.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
        var pathParts = cssPathWithoutExt.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

        if (patternParts.Length != pathParts.Length)
        {
            return false;
        }

        for (int i = 0; i < patternParts.Length; i++)
        {
            var patternPart = patternParts[i];
            var pathPart = pathParts[i];

            if (patternPart.StartsWith("{") && patternPart.EndsWith("}"))
            {
                // Extract token name
                var tokenName = patternPart.Trim('{', '}');
                tokens[tokenName] = pathPart;
            }
            else if (!patternPart.Equals(pathPart, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Applies token values to a pattern string.
    /// </summary>
    private string ApplyTokens(string pattern, Dictionary<string, string> tokens)
    {
        var result = pattern;

        foreach (var kvp in tokens)
        {
            // Case-insensitive replace for .NET Framework compatibility
            result = ReplaceIgnoreCase(result, $"{{{kvp.Key}}}", kvp.Value);
        }

        return result;
    }

    /// <summary>
    /// Case-insensitive string replace for .NET Framework compatibility.
    /// </summary>
    private static string ReplaceIgnoreCase(string source, string oldValue, string newValue)
    {
        var sb = new StringBuilder();
        var previousIndex = 0;
        var index = source.IndexOf(oldValue, StringComparison.OrdinalIgnoreCase);

        while (index != -1)
        {
            sb.Append(source.Substring(previousIndex, index - previousIndex));
            sb.Append(newValue);
            previousIndex = index + oldValue.Length;
            index = source.IndexOf(oldValue, previousIndex, StringComparison.OrdinalIgnoreCase);
        }

        sb.Append(source.Substring(previousIndex));
        return sb.ToString();
    }

    private async Task BuildComponentsAsync(
        EsbuildRunner runner,
        string distJsDir,
        string distCssDir,
        Dictionary<string, object> manifest,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Building components...");

        foreach (var kvp in _config.Components)
        {
            var componentName = kvp.Key;
            var component = kvp.Value;

            if (component.Js.Any())
            {
                var jsResult = await BuildJsBundle(
                    runner,
                    component.Js,
                    Path.Combine(distJsDir, $"component-{componentName}.[hash].js"),
                    cancellationToken);

                if (jsResult != null)
                {
                    manifest[$"component:{componentName}:js"] = new List<string> { jsResult };
                }
            }

            if (component.Css.Any())
            {
                var cssResult = await BuildCssBundle(
                    runner,
                    component.Css,
                    Path.Combine(distCssDir, $"component-{componentName}.[hash].css"),
                    cancellationToken);

                if (cssResult != null)
                {
                    manifest[$"component:{componentName}:css"] = new List<string> { cssResult };
                }
            }
        }
    }

    private async Task<string?> BuildJsBundle(
        EsbuildRunner runner,
        List<string> entryFiles,
        string outFile,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Building JS bundle: {OutFile}", outFile);
        _logger.LogInformation("  Entry files ({Count}):", entryFiles.Count);
        foreach (var entry in entryFiles)
        {
            _logger.LogInformation("    - {Entry}", entry);
        }

        var absoluteEntries = entryFiles.Select(f => Path.Combine(_projectRoot, f)).ToList();
        _logger.LogInformation("  Absolute paths:");
        foreach (var entry in absoluteEntries)
        {
            var exists = File.Exists(entry);
            _logger.LogInformation("    - {Entry} (exists: {Exists})", entry, exists);
        }

        var missingFiles = absoluteEntries.Where(f => !File.Exists(f)).ToList();
        if (missingFiles.Any())
        {
            _logger.LogError("Missing JS files: {Files}", string.Join(", ", missingFiles));
            return null;
        }

        var options = new EsbuildOptions
        {
            EntryPoints = absoluteEntries,
            OutFile = Path.Combine(_projectRoot, outFile),
            Minify = true,
            Sourcemap = _config.Esbuild.JsSourcemap,
            Target = _config.Esbuild.JsTarget,
            Format = _config.Esbuild.JsFormat,
            WorkingDirectory = _projectRoot
        };

        if (_config.ImportMap.ProdStrategy == "bundle" && _config.ImportMap.Entries.Any())
        {
            options.External = _config.ImportMap.Entries.Keys.ToList();
        }

        var result = await runner.RunAsync(options, cancellationToken);

        if (!result.Success)
        {
            _logger.LogError("JS bundling failed: {Error}", result.Error);
            return null;
        }

        var actualOutFile = options.OutFile.Replace("[hash]", GenerateHash());
        var relativeUrl = ConvertToUrl(actualOutFile);

        if (File.Exists(options.OutFile))
        {
            File.Move(options.OutFile, actualOutFile);
        }

        return relativeUrl;
    }

    private async Task<string?> BuildCssBundle(
        EsbuildRunner runner,
        List<string> cssFiles,
        string outFile,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Building CSS bundle: {OutFile}", outFile);
        _logger.LogInformation("  CSS files ({Count}):", cssFiles.Count);
        foreach (var css in cssFiles)
        {
            _logger.LogInformation("    - {File}", css);
        }

        var absoluteFiles = cssFiles.Select(f => Path.Combine(_projectRoot, f)).ToList();
        _logger.LogInformation("  Absolute paths:");
        foreach (var file in absoluteFiles)
        {
            var exists = File.Exists(file);
            _logger.LogInformation("    - {File} (exists: {Exists})", file, exists);
        }

        var missingFiles = absoluteFiles.Where(f => !File.Exists(f)).ToList();
        if (missingFiles.Any())
        {
            _logger.LogError("Missing CSS files: {Files}", string.Join(", ", missingFiles));
            return null;
        }

        var virtualEntry = await CreateVirtualCssEntry(absoluteFiles);

        var options = new EsbuildOptions
        {
            EntryPoints = new List<string> { virtualEntry },
            OutFile = Path.Combine(_projectRoot, outFile),
            Minify = true,
            Sourcemap = _config.Esbuild.CssSourcemap,
            WorkingDirectory = _projectRoot,
            External = new List<string> { "/img/*", "/icons/*", "/fonts/*" }
        };

        var result = await runner.RunAsync(options, cancellationToken);

        try
        {
            File.Delete(virtualEntry);
        }
        catch { }

        if (!result.Success)
        {
            _logger.LogError("CSS bundling failed: {Error}", result.Error);
            return null;
        }

        var actualOutFile = options.OutFile.Replace("[hash]", GenerateHash());
        var relativeUrl = ConvertToUrl(actualOutFile);

        if (File.Exists(options.OutFile))
        {
            File.Move(options.OutFile, actualOutFile);
        }

        return relativeUrl;
    }

    private async Task<string> CreateVirtualCssEntry(List<string> cssFiles)
    {
        var tempDir = Path.Combine(_projectRoot, "obj", "frontend");
        Directory.CreateDirectory(tempDir);

        var entryFile = Path.Combine(tempDir, $"css-entry-{Guid.NewGuid()}.css");
        var sb = new StringBuilder();

        foreach (var cssFile in cssFiles)
        {
            sb.AppendLine($"@import \"{cssFile.Replace("\\", "/")}\";");
        }

        await WriteAllTextAsync(entryFile, sb.ToString());
        return entryFile;
    }

    private async Task WriteManifestAsync(Dictionary<string, object> manifest)
    {
        var manifestPath = Path.Combine(_projectRoot, _config.WebRoot, "frontend.manifest.json");

        var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        await WriteAllTextAsync(manifestPath, json);
        _logger.LogInformation("Wrote manifest to {Path}", manifestPath);
    }

    // Helper for netstandard2.0 compatibility
    private static async Task WriteAllTextAsync(string path, string contents)
    {
        using (var writer = new StreamWriter(path, false, Encoding.UTF8))
        {
            await writer.WriteAsync(contents);
        }
    }

    private string ConvertToUrl(string filePath)
    {
        // Get relative path from dist directory (not webRoot) to avoid double distUrlRoot
        // e.g., filePath = wwwroot/dist/js/global.xxx.js
        //       distDir  = wwwroot/dist
        //       relative = js/global.xxx.js
        //       result   = /dist/js/global.xxx.js
        var distDir = Path.Combine(_projectRoot, _config.WebRoot, "dist");
        var relativePath = GetRelativePath(distDir, filePath);
        return _config.DistUrlRoot.TrimEnd('/') + "/" + relativePath.Replace("\\", "/");
    }

    // Helper for netstandard2.0 compatibility (Path.GetRelativePath not available)
    private static string GetRelativePath(string relativeTo, string path)
    {
        var relativeToUri = new Uri(relativeTo.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar);
        var pathUri = new Uri(path);
        var relativeUri = relativeToUri.MakeRelativeUri(pathUri);
        return Uri.UnescapeDataString(relativeUri.ToString().Replace('/', Path.DirectorySeparatorChar));
    }

    private string MakeSafeName(string viewKey)
    {
        return viewKey.Replace("/", "-").Replace("\\", "-").ToLowerInvariant();
    }

    private string GenerateHash()
    {
        return Guid.NewGuid().ToString("N").Substring(0, 8);
    }
}
