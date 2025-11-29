using MvcFrontendKit.Configuration;

namespace MvcFrontendKit.Tests;

/// <summary>
/// Tests for the CLI DevCommand functionality.
/// These tests verify file collection, path handling, and output path calculation.
/// </summary>
public class DevCommandTests : IDisposable
{
    private readonly string _tempDir;

    public DevCommandTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"MvcFrontendKit_DevCmd_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    #region GetOutputPath Tests

    [Theory]
    [InlineData("wwwroot/js/site.ts", ".js", "wwwroot/js/site.js")]
    [InlineData("wwwroot/js/Home/Index.ts", ".js", "wwwroot/js/Home/Index.js")]
    [InlineData("wwwroot/js/components/button.tsx", ".js", "wwwroot/js/components/button.js")]
    [InlineData("wwwroot/css/site.scss", ".css", "wwwroot/css/site.css")]
    [InlineData("wwwroot/css/components/card.sass", ".css", "wwwroot/css/components/card.css")]
    public void GetOutputPath_ConvertsExtensionCorrectly(string inputPath, string newExtension, string expectedOutput)
    {
        var result = GetOutputPath(inputPath, newExtension);

        // Normalize for cross-platform comparison
        Assert.Equal(
            expectedOutput.Replace('/', Path.DirectorySeparatorChar),
            result.Replace('/', Path.DirectorySeparatorChar));
    }

    [Fact]
    public void GetOutputPath_HandlesWindowsAbsolutePath()
    {
        var inputPath = @"C:\Users\test\project\wwwroot\js\site.ts";
        var result = GetOutputPath(inputPath, ".js");

        Assert.Equal(@"C:\Users\test\project\wwwroot\js\site.js", result);
    }

    [Fact]
    public void GetOutputPath_PreservesDirectoryStructure()
    {
        // Test that deeply nested paths work correctly
        var inputPath = Path.Combine("wwwroot", "js", "Areas", "Admin", "Dashboard", "index.ts");
        var result = GetOutputPath(inputPath, ".js");
        var expected = Path.Combine("wwwroot", "js", "Areas", "Admin", "Dashboard", "index.js");

        Assert.Equal(expected, result);
    }

    #endregion

    #region CollectFilesFromConfig Tests

    [Fact]
    public void CollectFilesFromConfig_CollectsGlobalTsFiles()
    {
        var config = new FrontendConfig
        {
            Global = new GlobalAssetsConfig
            {
                Js = new List<string> { "wwwroot/js/site.ts", "wwwroot/js/app.tsx", "wwwroot/js/vendor.js" }
            }
        };

        var tsFiles = new List<string>();
        var scssFiles = new List<string>();
        CollectFilesFromConfig(config, tsFiles, scssFiles);

        Assert.Equal(2, tsFiles.Count);
        Assert.Contains("wwwroot/js/site.ts", tsFiles);
        Assert.Contains("wwwroot/js/app.tsx", tsFiles);
        Assert.DoesNotContain("wwwroot/js/vendor.js", tsFiles); // .js files excluded
    }

    [Fact]
    public void CollectFilesFromConfig_CollectsGlobalScssFiles()
    {
        var config = new FrontendConfig
        {
            Global = new GlobalAssetsConfig
            {
                Css = new List<string> { "wwwroot/css/site.scss", "wwwroot/css/theme.sass", "wwwroot/css/vendor.css" }
            }
        };

        var tsFiles = new List<string>();
        var scssFiles = new List<string>();
        CollectFilesFromConfig(config, tsFiles, scssFiles);

        Assert.Equal(2, scssFiles.Count);
        Assert.Contains("wwwroot/css/site.scss", scssFiles);
        Assert.Contains("wwwroot/css/theme.sass", scssFiles);
        Assert.DoesNotContain("wwwroot/css/vendor.css", scssFiles); // .css files excluded
    }

    [Fact]
    public void CollectFilesFromConfig_CollectsFromViewOverrides()
    {
        var config = new FrontendConfig
        {
            Views = new ViewsConfig
            {
                Overrides = new Dictionary<string, ViewOverride>
                {
                    ["Views/Home/Index"] = new ViewOverride
                    {
                        Js = new List<string> { "wwwroot/js/Home/Index.ts" },
                        Css = new List<string> { "wwwroot/css/Home/Index.scss" }
                    }
                }
            }
        };

        var tsFiles = new List<string>();
        var scssFiles = new List<string>();
        CollectFilesFromConfig(config, tsFiles, scssFiles);

        Assert.Single(tsFiles);
        Assert.Single(scssFiles);
        Assert.Contains("wwwroot/js/Home/Index.ts", tsFiles);
        Assert.Contains("wwwroot/css/Home/Index.scss", scssFiles);
    }

    [Fact]
    public void CollectFilesFromConfig_CollectsFromComponents()
    {
        var config = new FrontendConfig
        {
            Components = new Dictionary<string, ComponentConfig>
            {
                ["notification"] = new ComponentConfig
                {
                    Js = new List<string> { "wwwroot/js/components/notification.ts" },
                    Css = new List<string> { "wwwroot/css/components/notification.scss" }
                },
                ["datepicker"] = new ComponentConfig
                {
                    Js = new List<string> { "wwwroot/js/components/datepicker.tsx" }
                }
            }
        };

        var tsFiles = new List<string>();
        var scssFiles = new List<string>();
        CollectFilesFromConfig(config, tsFiles, scssFiles);

        Assert.Equal(2, tsFiles.Count);
        Assert.Single(scssFiles);
    }

    [Fact]
    public void CollectFilesFromConfig_CollectsFromAreas()
    {
        var config = new FrontendConfig
        {
            Areas = new Dictionary<string, AreaConfig>
            {
                ["Admin"] = new AreaConfig
                {
                    Js = new List<string> { "wwwroot/js/Areas/Admin/admin.ts" },
                    Css = new List<string> { "wwwroot/css/Areas/Admin/admin.scss" }
                }
            }
        };

        var tsFiles = new List<string>();
        var scssFiles = new List<string>();
        CollectFilesFromConfig(config, tsFiles, scssFiles);

        Assert.Single(tsFiles);
        Assert.Single(scssFiles);
        Assert.Contains("wwwroot/js/Areas/Admin/admin.ts", tsFiles);
        Assert.Contains("wwwroot/css/Areas/Admin/admin.scss", scssFiles);
    }

    [Fact]
    public void CollectFilesFromConfig_HandlesEmptyConfig()
    {
        var config = new FrontendConfig();

        var tsFiles = new List<string>();
        var scssFiles = new List<string>();
        CollectFilesFromConfig(config, tsFiles, scssFiles);

        Assert.Empty(tsFiles);
        Assert.Empty(scssFiles);
    }

    [Fact]
    public void CollectFilesFromConfig_IsCaseInsensitive()
    {
        var config = new FrontendConfig
        {
            Global = new GlobalAssetsConfig
            {
                Js = new List<string> { "site.TS", "app.TSX" },
                Css = new List<string> { "site.SCSS", "theme.SASS" }
            }
        };

        var tsFiles = new List<string>();
        var scssFiles = new List<string>();
        CollectFilesFromConfig(config, tsFiles, scssFiles);

        Assert.Equal(2, tsFiles.Count);
        Assert.Equal(2, scssFiles.Count);
    }

    #endregion

    #region Path Deduplication Tests

    [Fact]
    public void PathDeduplication_RemovesDuplicatesWithMixedSeparators()
    {
        // On Linux/macOS, backslash is a valid filename character, not a path separator
        // This test only makes sense on Windows where both / and \ are path separators
        if (!OperatingSystem.IsWindows())
        {
            return; // Skip on non-Windows platforms
        }

        var projectRoot = _tempDir;
        var files = new List<string>
        {
            "wwwroot/js/site.ts",
            @"wwwroot\js\site.ts",  // Same file, different separator (Windows only)
            "wwwroot/js/Home/Index.ts"
        };

        var normalized = files
            .Select(f => Path.GetFullPath(Path.IsPathRooted(f) ? f : Path.Combine(projectRoot, f)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        Assert.Equal(2, normalized.Count);
    }

    [Fact]
    public void PathDeduplication_HandlesAbsoluteAndRelativePaths()
    {
        var projectRoot = _tempDir;
        var absolutePath = Path.Combine(projectRoot, "wwwroot", "js", "site.ts");
        var relativePath = "wwwroot/js/site.ts";

        var files = new List<string> { absolutePath, relativePath };

        var normalized = files
            .Select(f => Path.GetFullPath(Path.IsPathRooted(f) ? f : Path.Combine(projectRoot, f)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        Assert.Single(normalized);
    }

    [Fact]
    public void PathDeduplication_IsCaseInsensitiveOnWindows()
    {
        var projectRoot = _tempDir;
        var files = new List<string>
        {
            "wwwroot/js/Site.ts",
            "wwwroot/js/site.ts",
            "wwwroot/js/SITE.ts"
        };

        var normalized = files
            .Select(f => Path.GetFullPath(Path.IsPathRooted(f) ? f : Path.Combine(projectRoot, f)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        Assert.Single(normalized);
    }

    #endregion

    #region GetRelativePath Tests

    [Fact]
    public void GetRelativePath_ReturnsRelativeFromBasePath()
    {
        var basePath = Path.Combine(_tempDir, "project");
        var fullPath = Path.Combine(_tempDir, "project", "wwwroot", "js", "site.ts");

        var result = GetRelativePath(basePath, fullPath);

        Assert.Equal(Path.Combine("wwwroot", "js", "site.ts"), result);
    }

    [Fact]
    public void GetRelativePath_ReturnsFullPathWhenNotUnderBase()
    {
        var basePath = Path.Combine(_tempDir, "project1");
        var fullPath = Path.Combine(_tempDir, "project2", "wwwroot", "js", "site.ts");

        var result = GetRelativePath(basePath, fullPath);

        // Should return original path when not under base
        Assert.Equal(fullPath, result);
    }

    [Fact]
    public void GetRelativePath_TrimsLeadingSeparators()
    {
        var basePath = _tempDir;
        var fullPath = Path.Combine(_tempDir, "wwwroot", "js", "site.ts");

        var result = GetRelativePath(basePath, fullPath);

        Assert.False(result.StartsWith(Path.DirectorySeparatorChar.ToString()));
        Assert.False(result.StartsWith("/"));
    }

    #endregion

    #region Helper Methods (Mirrors DevCommand logic)

    private static string GetOutputPath(string inputPath, string newExtension)
    {
        var dir = Path.GetDirectoryName(inputPath) ?? "";
        var nameWithoutExt = Path.GetFileNameWithoutExtension(inputPath);
        return Path.Combine(dir, nameWithoutExt + newExtension);
    }

    private static void CollectFilesFromConfig(FrontendConfig config, List<string> tsFiles, List<string> scssFiles)
    {
        // Global assets
        if (config.Global?.Js != null)
        {
            tsFiles.AddRange(config.Global.Js.Where(f =>
                f.EndsWith(".ts", StringComparison.OrdinalIgnoreCase) ||
                f.EndsWith(".tsx", StringComparison.OrdinalIgnoreCase)));
        }

        if (config.Global?.Css != null)
        {
            scssFiles.AddRange(config.Global.Css.Where(f =>
                f.EndsWith(".scss", StringComparison.OrdinalIgnoreCase) ||
                f.EndsWith(".sass", StringComparison.OrdinalIgnoreCase)));
        }

        // View overrides
        if (config.Views?.Overrides != null)
        {
            foreach (var viewOverride in config.Views.Overrides.Values)
            {
                if (viewOverride?.Js != null)
                {
                    tsFiles.AddRange(viewOverride.Js.Where(f =>
                        f.EndsWith(".ts", StringComparison.OrdinalIgnoreCase) ||
                        f.EndsWith(".tsx", StringComparison.OrdinalIgnoreCase)));
                }

                if (viewOverride?.Css != null)
                {
                    scssFiles.AddRange(viewOverride.Css.Where(f =>
                        f.EndsWith(".scss", StringComparison.OrdinalIgnoreCase) ||
                        f.EndsWith(".sass", StringComparison.OrdinalIgnoreCase)));
                }
            }
        }

        // Components
        if (config.Components != null)
        {
            foreach (var component in config.Components.Values)
            {
                if (component?.Js != null)
                {
                    tsFiles.AddRange(component.Js.Where(f =>
                        f.EndsWith(".ts", StringComparison.OrdinalIgnoreCase) ||
                        f.EndsWith(".tsx", StringComparison.OrdinalIgnoreCase)));
                }

                if (component?.Css != null)
                {
                    scssFiles.AddRange(component.Css.Where(f =>
                        f.EndsWith(".scss", StringComparison.OrdinalIgnoreCase) ||
                        f.EndsWith(".sass", StringComparison.OrdinalIgnoreCase)));
                }
            }
        }

        // Areas
        if (config.Areas != null)
        {
            foreach (var area in config.Areas.Values)
            {
                if (area?.Js != null)
                {
                    tsFiles.AddRange(area.Js.Where(f =>
                        f.EndsWith(".ts", StringComparison.OrdinalIgnoreCase) ||
                        f.EndsWith(".tsx", StringComparison.OrdinalIgnoreCase)));
                }

                if (area?.Css != null)
                {
                    scssFiles.AddRange(area.Css.Where(f =>
                        f.EndsWith(".scss", StringComparison.OrdinalIgnoreCase) ||
                        f.EndsWith(".sass", StringComparison.OrdinalIgnoreCase)));
                }
            }
        }
    }

    private static string GetRelativePath(string basePath, string fullPath)
    {
        if (fullPath.StartsWith(basePath, StringComparison.OrdinalIgnoreCase))
        {
            return fullPath.Substring(basePath.Length).TrimStart(Path.DirectorySeparatorChar, '/');
        }
        return fullPath;
    }

    #endregion
}
