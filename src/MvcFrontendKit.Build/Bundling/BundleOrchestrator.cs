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
    private readonly FrontendConfig _config;
    private readonly string _projectRoot;
    private readonly ILogger _logger;

    public BundleOrchestrator(FrontendConfig config, string projectRoot, ILogger logger)
    {
        _config = config;
        _projectRoot = projectRoot;
        _logger = logger;
    }

    public async Task<bool> BuildBundlesAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting frontend bundle process...");

        try
        {
            var distDir = Path.Combine(_projectRoot, _config.WebRoot, "dist");
            var distJsDir = Path.Combine(distDir, _config.DistJsSubPath);
            var distCssDir = Path.Combine(distDir, _config.DistCssSubPath);

            if (_config.Output.CleanDistOnBuild)
            {
                CleanDistDirectories(distJsDir, distCssDir);
            }

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
            }
        }
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
            Format = "esm",
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
        var webRoot = Path.Combine(_projectRoot, _config.WebRoot);
        var relativePath = GetRelativePath(webRoot, filePath);
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
