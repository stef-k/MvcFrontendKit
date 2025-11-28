using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using MvcFrontendKit.Services;
using System.Text.Json;

namespace MvcFrontendKit.Tests;

public class ManifestProviderTests : IDisposable
{
    private readonly string _tempDir;
    private readonly Mock<IWebHostEnvironment> _mockEnv;
    private readonly Mock<ILogger<FrontendManifestProvider>> _mockLogger;

    public ManifestProviderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"MvcFrontendKit_Tests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
        Directory.CreateDirectory(Path.Combine(_tempDir, "wwwroot"));

        _mockEnv = new Mock<IWebHostEnvironment>();
        _mockEnv.Setup(e => e.ContentRootPath).Returns(_tempDir);
        _mockEnv.Setup(e => e.EnvironmentName).Returns("Production");

        _mockLogger = new Mock<ILogger<FrontendManifestProvider>>();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    [Fact]
    public void LoadsManifestSuccessfully()
    {
        // Arrange
        var manifestPath = Path.Combine(_tempDir, "wwwroot", "frontend.manifest.json");
        var manifest = new Dictionary<string, object>
        {
            ["global:js"] = new[] { "/dist/js/global-abc123.js" },
            ["global:css"] = new[] { "/dist/css/global-def456.css" }
        };
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest));

        // Act
        var provider = new FrontendManifestProvider(_mockEnv.Object, _mockLogger.Object);
        var result = provider.GetManifest();

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.GlobalJs);
        Assert.Single(result.GlobalJs);
        Assert.Equal("/dist/js/global-abc123.js", result.GlobalJs[0]);
    }

    [Fact]
    public void ThrowsWhenManifestMissingInProduction()
    {
        // Arrange
        _mockEnv.Setup(e => e.EnvironmentName).Returns("Production");

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
            new FrontendManifestProvider(_mockEnv.Object, _mockLogger.Object));

        Assert.Contains("frontend.manifest.json", ex.Message);
    }

    [Fact]
    public void ReturnsNullWhenManifestMissingInDevelopment()
    {
        // Arrange
        _mockEnv.Setup(e => e.EnvironmentName).Returns("Development");

        // Act
        var provider = new FrontendManifestProvider(_mockEnv.Object, _mockLogger.Object);
        var result = provider.GetManifest();

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetViewJsReturnsCorrectBundle()
    {
        // Arrange
        var manifestPath = Path.Combine(_tempDir, "wwwroot", "frontend.manifest.json");
        var manifest = new Dictionary<string, object>
        {
            ["view:Views/Home/Index"] = new { js = new[] { "/dist/js/views/home-index-xyz789.js" } }
        };
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest));

        // Act
        var provider = new FrontendManifestProvider(_mockEnv.Object, _mockLogger.Object);
        var result = provider.GetManifest();
        var viewJs = result?.GetViewJs("Views/Home/Index");

        // Assert
        Assert.NotNull(viewJs);
        Assert.Single(viewJs);
        Assert.Equal("/dist/js/views/home-index-xyz789.js", viewJs[0]);
    }

    [Fact]
    public void GetComponentJsReturnsCorrectBundle()
    {
        // Arrange
        var manifestPath = Path.Combine(_tempDir, "wwwroot", "frontend.manifest.json");
        var manifest = new Dictionary<string, object>
        {
            ["component:datepicker:js"] = new[] { "/dist/js/components/datepicker-aaa111.js" }
        };
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest));

        // Act
        var provider = new FrontendManifestProvider(_mockEnv.Object, _mockLogger.Object);
        var result = provider.GetManifest();
        var componentJs = result?.GetComponentJs("datepicker");

        // Assert
        Assert.NotNull(componentJs);
        Assert.Single(componentJs);
        Assert.Equal("/dist/js/components/datepicker-aaa111.js", componentJs[0]);
    }

    [Fact]
    public void HandlesInvalidJsonGracefully()
    {
        // Arrange
        var manifestPath = Path.Combine(_tempDir, "wwwroot", "frontend.manifest.json");
        File.WriteAllText(manifestPath, "{ invalid json }");

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
            new FrontendManifestProvider(_mockEnv.Object, _mockLogger.Object));
    }

    [Fact]
    public void ReloadsManifestWhenFileChanges()
    {
        // Arrange
        var manifestPath = Path.Combine(_tempDir, "wwwroot", "frontend.manifest.json");
        var manifest1 = new Dictionary<string, object>
        {
            ["global:js"] = new[] { "/dist/js/global-v1.js" }
        };
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest1));

        var provider = new FrontendManifestProvider(_mockEnv.Object, _mockLogger.Object);
        var result1 = provider.GetManifest();

        // Act - update manifest
        var manifest2 = new Dictionary<string, object>
        {
            ["global:js"] = new[] { "/dist/js/global-v2.js" }
        };
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest2));

        // Give the FileSystemWatcher time to detect the change
        Thread.Sleep(500);

        var result2 = provider.GetManifest();

        // Assert
        Assert.NotNull(result1);
        Assert.NotNull(result2);
        Assert.Equal("/dist/js/global-v1.js", result1.GlobalJs?[0]);
        Assert.Equal("/dist/js/global-v2.js", result2.GlobalJs?[0]);
    }
}
