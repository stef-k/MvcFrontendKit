using Microsoft.AspNetCore.Hosting;
using Moq;
using MvcFrontendKit.Configuration;
using MvcFrontendKit.Utilities;

namespace MvcFrontendKit.Tests;

public class AssetResolverTests : IDisposable
{
    private readonly string _tempDir;
    private readonly Mock<IWebHostEnvironment> _mockEnv;

    public AssetResolverTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"MvcFrontendKit_AssetResolver_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
        Directory.CreateDirectory(Path.Combine(_tempDir, "wwwroot", "js", "Home"));
        Directory.CreateDirectory(Path.Combine(_tempDir, "wwwroot", "css", "Home"));
        Directory.CreateDirectory(Path.Combine(_tempDir, "wwwroot", "js", "Areas", "Admin", "Settings"));
        Directory.CreateDirectory(Path.Combine(_tempDir, "wwwroot", "css", "Areas", "Admin", "Settings"));

        _mockEnv = new Mock<IWebHostEnvironment>();
        _mockEnv.Setup(e => e.ContentRootPath).Returns(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    [Fact]
    public void ResolveViewJs_MatchesViewsControllerActionPattern()
    {
        // Arrange
        var jsFile = Path.Combine(_tempDir, "wwwroot", "js", "Home", "index.js");
        File.WriteAllText(jsFile, "// test");

        var config = new FrontendConfig
        {
            Views = new ViewsConfig
            {
                JsAutoLinkByConvention = true,
                Conventions = new List<ViewConvention>
                {
                    new ViewConvention
                    {
                        ViewPattern = "Views/{Controller}/{Action}",
                        ScriptBasePattern = "wwwroot/js/{Controller}/{Action}"
                    }
                }
            }
        };

        var resolver = new AssetResolver(_mockEnv.Object, config);

        // Act - ViewKeyResolver produces "Views/Home/Index"
        var result = resolver.ResolveViewJs("Views/Home/Index");

        // Assert
        Assert.Single(result);
        // Path separators may differ by OS
        Assert.Contains("index.js", result[0]);
    }

    [Fact]
    public void ResolveViewJs_MatchesAreasPattern()
    {
        // Arrange
        var jsFile = Path.Combine(_tempDir, "wwwroot", "js", "Areas", "Admin", "Settings", "index.js");
        File.WriteAllText(jsFile, "// test");

        var config = new FrontendConfig
        {
            Views = new ViewsConfig
            {
                JsAutoLinkByConvention = true,
                Conventions = new List<ViewConvention>
                {
                    new ViewConvention
                    {
                        ViewPattern = "Areas/{Area}/{Controller}/{Action}",
                        ScriptBasePattern = "wwwroot/js/Areas/{Area}/{Controller}/{Action}"
                    }
                }
            }
        };

        var resolver = new AssetResolver(_mockEnv.Object, config);

        // Act - ViewKeyResolver produces "Areas/Admin/Settings/Index"
        var result = resolver.ResolveViewJs("Areas/Admin/Settings/Index");

        // Assert
        Assert.Single(result);
        // Path separators may differ by OS
        Assert.Contains("index.js", result[0]);
        Assert.Contains("Admin", result[0]);
        Assert.Contains("Settings", result[0]);
    }

    [Fact]
    public void ResolveViewJs_DoesNotMatchOldCshtmlPattern()
    {
        // Arrange - This test verifies that the OLD pattern format doesn't work
        var jsFile = Path.Combine(_tempDir, "wwwroot", "js", "Home", "index.js");
        File.WriteAllText(jsFile, "// test");

        var config = new FrontendConfig
        {
            Views = new ViewsConfig
            {
                JsAutoLinkByConvention = true,
                Conventions = new List<ViewConvention>
                {
                    // OLD BROKEN PATTERN - has .cshtml suffix
                    new ViewConvention
                    {
                        ViewPattern = "Views/{Controller}/{Action}.cshtml",
                        ScriptBasePattern = "wwwroot/js/{Controller}/{Action}"
                    }
                }
            }
        };

        var resolver = new AssetResolver(_mockEnv.Object, config);

        // Act - ViewKeyResolver produces "Views/Home/Index" (no .cshtml)
        var result = resolver.ResolveViewJs("Views/Home/Index");

        // Assert - Should NOT match because pattern has extra segment ".cshtml"
        Assert.Empty(result);
    }

    [Fact]
    public void ResolveViewCss_MatchesViewsPattern()
    {
        // Arrange
        var cssFile = Path.Combine(_tempDir, "wwwroot", "css", "Home", "Index.css");
        File.WriteAllText(cssFile, "/* test */");

        var config = new FrontendConfig
        {
            Views = new ViewsConfig
            {
                CssAutoLinkByConvention = true,
                CssConventions = new List<CssConvention>
                {
                    new CssConvention
                    {
                        ViewPattern = "Views/{Controller}/{Action}",
                        CssPattern = "wwwroot/css/{Controller}/{Action}.css"
                    }
                }
            }
        };

        var resolver = new AssetResolver(_mockEnv.Object, config);

        // Act
        var result = resolver.ResolveViewCss("Views/Home/Index");

        // Assert
        Assert.Single(result);
        Assert.Equal("wwwroot/css/Home/Index.css", result[0]);
    }

    [Fact]
    public void ResolveViewJs_UsesOverrideWhenPresent()
    {
        // Arrange
        var overrideJsFile = Path.Combine(_tempDir, "wwwroot", "js", "custom", "home.js");
        Directory.CreateDirectory(Path.GetDirectoryName(overrideJsFile)!);
        File.WriteAllText(overrideJsFile, "// custom");

        var config = new FrontendConfig
        {
            Views = new ViewsConfig
            {
                JsAutoLinkByConvention = true,
                Conventions = new List<ViewConvention>
                {
                    new ViewConvention
                    {
                        ViewPattern = "Views/{Controller}/{Action}",
                        ScriptBasePattern = "wwwroot/js/{Controller}/{Action}"
                    }
                },
                Overrides = new Dictionary<string, ViewOverride>
                {
                    ["Views/Home/Index"] = new ViewOverride
                    {
                        Js = new List<string> { "wwwroot/js/custom/home.js" }
                    }
                }
            }
        };

        var resolver = new AssetResolver(_mockEnv.Object, config);

        // Act
        var result = resolver.ResolveViewJs("Views/Home/Index");

        // Assert - Should use override, not convention
        Assert.Single(result);
        Assert.Equal("wwwroot/js/custom/home.js", result[0]);
    }

    [Fact]
    public void ResolveViewJs_ReturnsEmptyWhenAutoLinkDisabled()
    {
        // Arrange
        var jsFile = Path.Combine(_tempDir, "wwwroot", "js", "Home", "index.js");
        File.WriteAllText(jsFile, "// test");

        var config = new FrontendConfig
        {
            Views = new ViewsConfig
            {
                JsAutoLinkByConvention = false,
                Conventions = new List<ViewConvention>
                {
                    new ViewConvention
                    {
                        ViewPattern = "Views/{Controller}/{Action}",
                        ScriptBasePattern = "wwwroot/js/{Controller}/{Action}"
                    }
                }
            }
        };

        var resolver = new AssetResolver(_mockEnv.Object, config);

        // Act
        var result = resolver.ResolveViewJs("Views/Home/Index");

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void ResolveViewJs_FindsCamelCaseFile()
    {
        // Arrange - Create camelCase file
        var jsFile = Path.Combine(_tempDir, "wwwroot", "js", "Home", "index.js");
        File.WriteAllText(jsFile, "// test");

        var config = new FrontendConfig
        {
            Views = new ViewsConfig
            {
                JsAutoLinkByConvention = true,
                Conventions = new List<ViewConvention>
                {
                    new ViewConvention
                    {
                        ViewPattern = "Views/{Controller}/{Action}",
                        ScriptBasePattern = "wwwroot/js/{Controller}/{Action}"
                    }
                }
            }
        };

        var resolver = new AssetResolver(_mockEnv.Object, config);

        // Act
        var result = resolver.ResolveViewJs("Views/Home/Index");

        // Assert - Should find index.js (camelCase of Index)
        Assert.Single(result);
        Assert.Contains("index.js", result[0]);
    }

    [Fact]
    public void ResolveViewJs_FindsPascalCaseFile()
    {
        // Arrange - Create PascalCase file
        var jsFile = Path.Combine(_tempDir, "wwwroot", "js", "Home", "Index.js");
        File.WriteAllText(jsFile, "// test");

        var config = new FrontendConfig
        {
            Views = new ViewsConfig
            {
                JsAutoLinkByConvention = true,
                Conventions = new List<ViewConvention>
                {
                    new ViewConvention
                    {
                        ViewPattern = "Views/{Controller}/{Action}",
                        ScriptBasePattern = "wwwroot/js/{Controller}/{Action}"
                    }
                }
            }
        };

        var resolver = new AssetResolver(_mockEnv.Object, config);

        // Act
        var result = resolver.ResolveViewJs("Views/Home/Index");

        // Assert - Should find Index.js (PascalCase)
        Assert.Single(result);
        // The resolver tries camelCase first (index.js), then lowercase, then PascalCase
        // So if index.js exists, it will be found. PascalCase is a fallback.
        Assert.True(result[0].EndsWith("Index.js") || result[0].EndsWith("index.js"));
    }
}
