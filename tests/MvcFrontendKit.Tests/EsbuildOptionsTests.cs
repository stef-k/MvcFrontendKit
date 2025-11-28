using MvcFrontendKit.Build.Bundling;

namespace MvcFrontendKit.Tests;

/// <summary>
/// Tests for the EsbuildOptions and EsbuildResult classes used in the build task.
/// </summary>
public class EsbuildOptionsTests
{
    [Fact]
    public void EsbuildOptions_DefaultValues()
    {
        var options = new EsbuildOptions();

        Assert.NotNull(options.EntryPoints);
        Assert.Empty(options.EntryPoints);
        Assert.Null(options.OutFile);
        Assert.Null(options.OutDir);
        // Default to true for production builds
        Assert.True(options.Minify);
        Assert.True(options.Sourcemap);
        Assert.Null(options.Target);
        Assert.Null(options.Format);
        Assert.Null(options.WorkingDirectory);
        // External is nullable, not initialized
        Assert.Null(options.External);
    }

    [Fact]
    public void EsbuildOptions_CanSetAllProperties()
    {
        var options = new EsbuildOptions
        {
            EntryPoints = new List<string> { "src/app.js", "src/utils.js" },
            OutFile = "dist/bundle.js",
            OutDir = "dist",
            Minify = true,
            Sourcemap = true,
            Target = "es2020",
            Format = "esm",
            WorkingDirectory = "/project",
            External = new List<string> { "lodash", "react" }
        };

        Assert.Equal(2, options.EntryPoints.Count);
        Assert.Equal("src/app.js", options.EntryPoints[0]);
        Assert.Equal("dist/bundle.js", options.OutFile);
        Assert.Equal("dist", options.OutDir);
        Assert.True(options.Minify);
        Assert.True(options.Sourcemap);
        Assert.Equal("es2020", options.Target);
        Assert.Equal("esm", options.Format);
        Assert.Equal("/project", options.WorkingDirectory);
        Assert.Equal(2, options.External.Count);
    }

    [Fact]
    public void EsbuildResult_SuccessResult()
    {
        var result = new EsbuildResult
        {
            Success = true,
            Output = "Bundle created successfully",
            Error = null
        };

        Assert.True(result.Success);
        Assert.Equal("Bundle created successfully", result.Output);
        Assert.Null(result.Error);
    }

    [Fact]
    public void EsbuildResult_FailureResult()
    {
        var result = new EsbuildResult
        {
            Success = false,
            Output = "",
            Error = "File not found: src/missing.js"
        };

        Assert.False(result.Success);
        Assert.Equal("File not found: src/missing.js", result.Error);
    }
}
