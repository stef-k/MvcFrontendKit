using MvcFrontendKit.Build.Bundling;

namespace MvcFrontendKit.Tests;

/// <summary>
/// Tests for TypeScript and SCSS preprocessor support.
/// </summary>
public class PreprocessorTests
{
    #region TypeScript Detection Tests

    [Fact]
    public void DetectTypeScriptLoaders_NoTypeScriptFiles_ReturnsEmpty()
    {
        // Arrange
        var files = new List<string>
        {
            "/project/wwwroot/js/app.js",
            "/project/wwwroot/js/utils.js"
        };

        // Act
        var loaders = DetectTypeScriptLoaders(files);

        // Assert
        Assert.Empty(loaders);
    }

    [Fact]
    public void DetectTypeScriptLoaders_WithTsFile_ReturnsTsLoader()
    {
        // Arrange
        var files = new List<string>
        {
            "/project/wwwroot/js/app.ts",
            "/project/wwwroot/js/utils.js"
        };

        // Act
        var loaders = DetectTypeScriptLoaders(files);

        // Assert
        Assert.Single(loaders);
        Assert.Equal("ts", loaders[".ts"]);
    }

    [Fact]
    public void DetectTypeScriptLoaders_WithTsxFile_ReturnsTsxLoader()
    {
        // Arrange
        var files = new List<string>
        {
            "/project/wwwroot/js/component.tsx",
            "/project/wwwroot/js/utils.js"
        };

        // Act
        var loaders = DetectTypeScriptLoaders(files);

        // Assert
        Assert.Single(loaders);
        Assert.Equal("tsx", loaders[".tsx"]);
    }

    [Fact]
    public void DetectTypeScriptLoaders_WithBothTsAndTsx_ReturnsBothLoaders()
    {
        // Arrange
        var files = new List<string>
        {
            "/project/wwwroot/js/app.ts",
            "/project/wwwroot/js/component.tsx",
            "/project/wwwroot/js/utils.js"
        };

        // Act
        var loaders = DetectTypeScriptLoaders(files);

        // Assert
        Assert.Equal(2, loaders.Count);
        Assert.Equal("ts", loaders[".ts"]);
        Assert.Equal("tsx", loaders[".tsx"]);
    }

    [Theory]
    [InlineData("/project/app.TS")]
    [InlineData("/project/app.Ts")]
    [InlineData("/project/app.ts")]
    public void DetectTypeScriptLoaders_CaseInsensitive(string filePath)
    {
        // Arrange
        var files = new List<string> { filePath };

        // Act
        var loaders = DetectTypeScriptLoaders(files);

        // Assert
        Assert.Single(loaders);
        Assert.Equal("ts", loaders[".ts"]);
    }

    [Theory]
    [InlineData("/project/component.TSX")]
    [InlineData("/project/component.Tsx")]
    [InlineData("/project/component.tsx")]
    public void DetectTypeScriptLoaders_TsxCaseInsensitive(string filePath)
    {
        // Arrange
        var files = new List<string> { filePath };

        // Act
        var loaders = DetectTypeScriptLoaders(files);

        // Assert
        Assert.Single(loaders);
        Assert.Equal("tsx", loaders[".tsx"]);
    }

    #endregion

    #region SCSS Detection Tests

    [Theory]
    [InlineData("/project/wwwroot/css/site.scss", true)]
    [InlineData("/project/wwwroot/css/site.sass", true)]
    [InlineData("/project/wwwroot/css/site.css", false)]
    [InlineData("/project/wwwroot/js/app.js", false)]
    public void IsScssFile_CorrectlyIdentifiesScssFiles(string filePath, bool expected)
    {
        // Act
        var result = IsScssFile(filePath);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("/project/styles.SCSS")]
    [InlineData("/project/styles.Scss")]
    [InlineData("/project/styles.scss")]
    public void IsScssFile_CaseInsensitive(string filePath)
    {
        // Act
        var result = IsScssFile(filePath);

        // Assert
        Assert.True(result);
    }

    #endregion

    #region SassOptions Tests

    [Fact]
    public void SassOptions_DefaultValues()
    {
        // Arrange & Act
        var options = new SassOptions();

        // Assert
        Assert.True(options.Compressed);
        Assert.False(options.SourceMap);
        Assert.False(options.Charset);
        Assert.Null(options.LoadPaths);
    }

    [Fact]
    public void SassOptions_CanSetAllProperties()
    {
        // Arrange & Act
        var options = new SassOptions
        {
            Compressed = false,
            SourceMap = true,
            Charset = true,
            LoadPaths = new List<string> { "/project/node_modules", "/project/src" }
        };

        // Assert
        Assert.False(options.Compressed);
        Assert.True(options.SourceMap);
        Assert.True(options.Charset);
        Assert.Equal(2, options.LoadPaths.Count);
    }

    #endregion

    #region SassResult Tests

    [Fact]
    public void SassResult_SuccessResult()
    {
        // Arrange & Act
        var result = new SassResult
        {
            Success = true,
            ExitCode = 0,
            OutputPath = "/project/obj/frontend/scss/site.css",
            Output = "",
            Error = ""
        };

        // Assert
        Assert.True(result.Success);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal("/project/obj/frontend/scss/site.css", result.OutputPath);
    }

    [Fact]
    public void SassResult_FailureResult()
    {
        // Arrange & Act
        var result = new SassResult
        {
            Success = false,
            ExitCode = 1,
            Error = "Error: Can't find variable $unknown-variable"
        };

        // Assert
        Assert.False(result.Success);
        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Can't find variable", result.Error);
    }

    #endregion

    #region File Extension Detection Helper Tests

    [Theory]
    [InlineData("app.js", ".js")]
    [InlineData("app.ts", ".ts")]
    [InlineData("component.tsx", ".tsx")]
    [InlineData("styles.css", ".css")]
    [InlineData("styles.scss", ".scss")]
    [InlineData("styles.sass", ".sass")]
    [InlineData("file.min.js", ".js")]
    [InlineData("file.bundle.tsx", ".tsx")]
    public void GetFileExtension_ReturnsCorrectExtension(string filename, string expected)
    {
        // Act
        var extension = Path.GetExtension(filename).ToLowerInvariant();

        // Assert
        Assert.Equal(expected, extension);
    }

    #endregion

    #region Helper Methods (mimicking BundleOrchestrator private methods for testing)

    /// <summary>
    /// Detects TypeScript files in the entry list and returns appropriate esbuild loaders.
    /// This mirrors the logic in BundleOrchestrator.DetectTypeScriptLoaders()
    /// </summary>
    private static Dictionary<string, string> DetectTypeScriptLoaders(List<string> entryFiles)
    {
        var loaders = new Dictionary<string, string>();

        var hasTs = entryFiles.Any(f => f.EndsWith(".ts", StringComparison.OrdinalIgnoreCase) &&
                                        !f.EndsWith(".tsx", StringComparison.OrdinalIgnoreCase));
        var hasTsx = entryFiles.Any(f => f.EndsWith(".tsx", StringComparison.OrdinalIgnoreCase));

        if (hasTs)
        {
            loaders[".ts"] = "ts";
        }
        if (hasTsx)
        {
            loaders[".tsx"] = "tsx";
        }

        return loaders;
    }

    /// <summary>
    /// Checks if a file is an SCSS/Sass file.
    /// This mirrors the logic in BundleOrchestrator.BuildCssBundle()
    /// </summary>
    private static bool IsScssFile(string filePath)
    {
        return filePath.EndsWith(".scss", StringComparison.OrdinalIgnoreCase) ||
               filePath.EndsWith(".sass", StringComparison.OrdinalIgnoreCase);
    }

    #endregion
}
