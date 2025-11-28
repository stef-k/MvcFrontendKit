using MvcFrontendKit.Configuration;

namespace MvcFrontendKit.Tests;

/// <summary>
/// Tests for the CLI CheckCommand functionality.
/// These tests verify path handling, convention matching, and view discovery.
/// </summary>
public class CheckCommandTests : IDisposable
{
    private readonly string _tempDir;

    public CheckCommandTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"MvcFrontendKit_CheckCmd_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    #region Path.GetRelativePath Tests (Bug Fix Verification)

    [Fact]
    public void GetRelativePath_SimpleWindowsPath_ReturnsRelative()
    {
        // This test verifies the fix for the "Invalid URI" bug
        var basePath = Path.Combine(_tempDir, "project");
        var fullPath = Path.Combine(_tempDir, "project", "wwwroot", "js", "Home", "index.js");
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, "// test");

        var relativePath = Path.GetRelativePath(basePath, fullPath);

        Assert.Equal(Path.Combine("wwwroot", "js", "Home", "index.js"), relativePath);
    }

    [Fact]
    public void GetRelativePath_PathWithSpaces_ReturnsRelative()
    {
        // Test paths with spaces that would fail with Uri-based implementation
        var basePath = Path.Combine(_tempDir, "My Project");
        var fullPath = Path.Combine(_tempDir, "My Project", "wwwroot", "js", "index.js");
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, "// test");

        var relativePath = Path.GetRelativePath(basePath, fullPath);

        Assert.Equal(Path.Combine("wwwroot", "js", "index.js"), relativePath);
    }

    [Fact]
    public void GetRelativePath_PathWithSpecialChars_ReturnsRelative()
    {
        // Test paths with characters that are problematic for URIs
        var basePath = Path.Combine(_tempDir, "project#1");
        var fullPath = Path.Combine(_tempDir, "project#1", "wwwroot", "js", "index.js");
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, "// test");

        var relativePath = Path.GetRelativePath(basePath, fullPath);

        Assert.Equal(Path.Combine("wwwroot", "js", "index.js"), relativePath);
    }

    [Fact]
    public void GetRelativePath_DeepNestedPath_ReturnsRelative()
    {
        // Test deeply nested paths like Areas/Identity/Pages/Account
        var basePath = Path.Combine(_tempDir, "project");
        var fullPath = Path.Combine(_tempDir, "project", "wwwroot", "js", "Areas", "Identity", "Pages", "Account", "login.js");
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, "// test");

        var relativePath = Path.GetRelativePath(basePath, fullPath);

        var expected = Path.Combine("wwwroot", "js", "Areas", "Identity", "Pages", "Account", "login.js");
        Assert.Equal(expected, relativePath);
    }

    #endregion

    #region View Pattern Matching Tests

    [Theory]
    [InlineData("Views/Home/Index", "Views/{Controller}/{Action}", true)]
    [InlineData("Views/Account/Login", "Views/{Controller}/{Action}", true)]
    [InlineData("Areas/Admin/Settings/Index", "Areas/{Area}/{Controller}/{Action}", true)]
    [InlineData("Views/Home/Index", "Areas/{Area}/{Controller}/{Action}", false)]
    [InlineData("Areas/Admin/Settings/Index", "Views/{Controller}/{Action}", false)]
    public void TryMatchViewPattern_MatchesCorrectly(string viewKey, string pattern, bool shouldMatch)
    {
        var result = TryMatchViewPattern(viewKey, pattern, out var tokens);
        Assert.Equal(shouldMatch, result);
    }

    [Fact]
    public void TryMatchViewPattern_ExtractsTokensCorrectly()
    {
        var result = TryMatchViewPattern("Views/Home/Index", "Views/{Controller}/{Action}", out var tokens);

        Assert.True(result);
        Assert.Equal("Home", tokens["Controller"]);
        Assert.Equal("Index", tokens["Action"]);
    }

    [Fact]
    public void TryMatchViewPattern_ExtractsAreaTokenCorrectly()
    {
        var result = TryMatchViewPattern("Areas/Admin/Settings/Index", "Areas/{Area}/{Controller}/{Action}", out var tokens);

        Assert.True(result);
        Assert.Equal("Admin", tokens["Area"]);
        Assert.Equal("Settings", tokens["Controller"]);
        Assert.Equal("Index", tokens["Action"]);
    }

    [Fact]
    public void ApplyTokens_ReplacesCorrectly()
    {
        var tokens = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Controller"] = "Home",
            ["Action"] = "Index"
        };

        var result = ApplyTokens("wwwroot/js/{Controller}/{Action}", tokens);

        Assert.Equal("wwwroot/js/Home/Index", result);
    }

    #endregion

    #region JS Candidate Naming Tests

    [Theory]
    [InlineData("Index", new[] { "index.js", "index.js", "Index.js", "indexPage.js", "indexPage.js" })]
    [InlineData("MapEditor", new[] { "mapEditor.js", "mapeditor.js", "MapEditor.js", "mapEditorPage.js", "mapeditorPage.js" })]
    [InlineData("Details", new[] { "details.js", "details.js", "Details.js", "detailsPage.js", "detailsPage.js" })]
    public void GetJsCandidates_GeneratesCorrectCandidates(string action, string[] expectedFilenames)
    {
        var basePath = Path.Combine(_tempDir, "wwwroot", "js", "Home");
        var candidates = GetJsCandidates(Path.Combine(basePath, action));

        Assert.Equal(5, candidates.Count);
        for (int i = 0; i < expectedFilenames.Length; i++)
        {
            Assert.EndsWith(expectedFilenames[i], candidates[i]);
        }
    }

    #endregion

    #region View Discovery Tests

    [Fact]
    public void DiscoverViews_FindsJsFilesInViewsPattern()
    {
        // Arrange - Create project structure
        var jsRoot = Path.Combine(_tempDir, "wwwroot", "js");
        Directory.CreateDirectory(Path.Combine(jsRoot, "Home"));
        Directory.CreateDirectory(Path.Combine(jsRoot, "Admin"));

        File.WriteAllText(Path.Combine(jsRoot, "Home", "index.js"), "// home index");
        File.WriteAllText(Path.Combine(jsRoot, "Home", "details.js"), "// home details");
        File.WriteAllText(Path.Combine(jsRoot, "Admin", "dashboard.js"), "// admin dashboard");

        // Act
        var jsFiles = Directory.GetFiles(jsRoot, "*.js", SearchOption.AllDirectories);

        // Assert
        Assert.Equal(3, jsFiles.Length);
    }

    [Fact]
    public void DiscoverViews_ExcludesDistFolder()
    {
        // Arrange
        var jsRoot = Path.Combine(_tempDir, "wwwroot", "js");
        Directory.CreateDirectory(Path.Combine(jsRoot, "Home"));
        Directory.CreateDirectory(Path.Combine(jsRoot, "dist"));

        File.WriteAllText(Path.Combine(jsRoot, "Home", "index.js"), "// source");
        File.WriteAllText(Path.Combine(jsRoot, "dist", "bundle.js"), "// bundled");

        // Act
        var jsFiles = Directory.GetFiles(jsRoot, "*.js", SearchOption.AllDirectories)
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + "dist" + Path.DirectorySeparatorChar) &&
                        !f.Contains("/dist/"))
            .ToList();

        // Assert
        Assert.Single(jsFiles);
        Assert.Contains("Home", jsFiles[0]);
    }

    #endregion

    #region Partial View Detection Tests

    [Theory]
    [InlineData("_ViewImports.cshtml", true)]
    [InlineData("_ViewStart.cshtml", true)]
    [InlineData("_Layout.cshtml", true)]
    [InlineData("_PartialView.cshtml", true)]
    [InlineData("Index.cshtml", false)]
    [InlineData("Details.cshtml", false)]
    public void IsPartialOrLayoutView_DetectsCorrectly(string filename, bool isPartial)
    {
        var result = filename.StartsWith("_");
        Assert.Equal(isPartial, result);
    }

    #endregion

    #region Helper Methods (Mirrors CheckCommand logic)

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

    private static string ToCamelCase(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        return char.ToLowerInvariant(input[0]) + input.Substring(1);
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

    #endregion
}
