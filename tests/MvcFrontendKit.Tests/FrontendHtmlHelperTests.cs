using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MvcFrontendKit.Configuration;
using MvcFrontendKit.Helpers;
using MvcFrontendKit.Manifest;
using MvcFrontendKit.Services;
using System.Text.Encodings.Web;

namespace MvcFrontendKit.Tests;

/// <summary>
/// Tests for FrontendHtmlHelpers debug output behavior.
/// </summary>
public class FrontendHtmlHelperTests : IDisposable
{
    private readonly string _tempDir;
    private readonly Mock<IWebHostEnvironment> _mockEnv;
    private readonly Mock<IFrontendManifestProvider> _mockManifestProvider;
    private readonly Mock<IFrontendConfigProvider> _mockConfigProvider;
    private readonly Mock<IFrontendComponentRegistry> _mockComponentRegistry;

    public FrontendHtmlHelperTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"MvcFrontendKit_HtmlHelper_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
        Directory.CreateDirectory(Path.Combine(_tempDir, "wwwroot", "js"));
        Directory.CreateDirectory(Path.Combine(_tempDir, "wwwroot", "css"));

        // Create test JS/CSS files
        File.WriteAllText(Path.Combine(_tempDir, "wwwroot", "js", "site.js"), "// site.js");
        File.WriteAllText(Path.Combine(_tempDir, "wwwroot", "css", "site.css"), "/* site.css */");

        _mockEnv = new Mock<IWebHostEnvironment>();
        _mockEnv.Setup(e => e.ContentRootPath).Returns(_tempDir);

        _mockManifestProvider = new Mock<IFrontendManifestProvider>();
        _mockConfigProvider = new Mock<IFrontendConfigProvider>();
        _mockComponentRegistry = new Mock<IFrontendComponentRegistry>();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    private IHtmlHelper CreateHtmlHelper(string environmentName, FrontendConfig config, FrontendManifest? manifest = null)
    {
        _mockEnv.Setup(e => e.EnvironmentName).Returns(environmentName);
        _mockManifestProvider.Setup(p => p.GetManifest()).Returns(manifest);
        _mockManifestProvider.Setup(p => p.IsProduction()).Returns(manifest != null);
        _mockConfigProvider.Setup(p => p.GetConfig()).Returns(config);
        _mockComponentRegistry.Setup(r => r.TryRegister(It.IsAny<string>())).Returns(true);

        var services = new ServiceCollection();
        services.AddSingleton(_mockEnv.Object);
        services.AddSingleton(_mockManifestProvider.Object);
        services.AddSingleton(_mockConfigProvider.Object);
        services.AddSingleton(_mockComponentRegistry.Object);
        var serviceProvider = services.BuildServiceProvider();

        var httpContext = new DefaultHttpContext { RequestServices = serviceProvider };

        var routeData = new RouteData();
        routeData.Values["controller"] = "Home";
        routeData.Values["action"] = "Index";

        var actionContext = new ActionContext(httpContext, routeData, new ActionDescriptor());

        var viewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary());
        var tempData = new Mock<ITempDataDictionary>();
        var mockView = new Mock<IView>();

        var viewContext = new ViewContext(
            actionContext,
            mockView.Object,
            viewData,
            tempData.Object,
            TextWriter.Null,
            new HtmlHelperOptions());

        // Set the view path for ViewKeyResolver
        viewContext.View = mockView.Object;
        viewContext.ExecutingFilePath = "/Views/Home/Index.cshtml";

        var htmlHelper = new Mock<IHtmlHelper>();
        htmlHelper.Setup(h => h.ViewContext).Returns(viewContext);

        return htmlHelper.Object;
    }

    private static string GetHtmlString(IHtmlContent content)
    {
        using var writer = new StringWriter();
        content.WriteTo(writer, HtmlEncoder.Default);
        return writer.ToString();
    }

    private static FrontendConfig CreateBasicConfig()
    {
        return new FrontendConfig
        {
            WebRoot = "wwwroot",
            Global = new GlobalAssetsConfig
            {
                Js = new List<string> { "wwwroot/js/site.js" },
                Css = new List<string> { "wwwroot/css/site.css" }
            },
            Views = new ViewsConfig
            {
                JsAutoLinkByConvention = false,
                CssAutoLinkByConvention = false
            },
            ImportMap = new ImportMapConfig { Enabled = false }
        };
    }

    #region FrontendGlobalScripts Tests

    [Fact]
    public void FrontendGlobalScripts_Development_EmitsDebugComments()
    {
        // Arrange
        var config = CreateBasicConfig();
        var html = CreateHtmlHelper("Development", config);

        // Act
        var result = html.FrontendGlobalScripts();
        var output = GetHtmlString(result);

        // Assert
        Assert.Contains("<!-- MvcFrontendKit:FrontendGlobalScripts -", output);
        Assert.Contains("Development mode", output);
        Assert.Contains("1 file(s)", output);
        Assert.Contains("wwwroot/js/site.js", output);
    }

    [Fact]
    public void FrontendGlobalScripts_Production_NoDebugComments()
    {
        // Arrange
        var config = CreateBasicConfig();
        var manifest = new FrontendManifest
        {
            GlobalJs = new List<string> { "/dist/js/site.abc123.js" }
        };
        var html = CreateHtmlHelper("Production", config, manifest);

        // Act
        var result = html.FrontendGlobalScripts();
        var output = GetHtmlString(result);

        // Assert
        Assert.DoesNotContain("<!-- MvcFrontendKit", output);
        Assert.Contains("/dist/js/site.abc123.js", output);
    }

    #endregion

    #region FrontendGlobalStyles Tests

    [Fact]
    public void FrontendGlobalStyles_Development_EmitsDebugComments()
    {
        // Arrange
        var config = CreateBasicConfig();
        var html = CreateHtmlHelper("Development", config);

        // Act
        var result = html.FrontendGlobalStyles();
        var output = GetHtmlString(result);

        // Assert
        Assert.Contains("<!-- MvcFrontendKit:FrontendGlobalStyles -", output);
        Assert.Contains("Development mode", output);
        Assert.Contains("1 file(s)", output);
        Assert.Contains("wwwroot/css/site.css", output);
    }

    [Fact]
    public void FrontendGlobalStyles_Production_NoDebugComments()
    {
        // Arrange
        var config = CreateBasicConfig();
        var manifest = new FrontendManifest
        {
            GlobalCss = new List<string> { "/dist/css/site.abc123.css" }
        };
        var html = CreateHtmlHelper("Production", config, manifest);

        // Act
        var result = html.FrontendGlobalStyles();
        var output = GetHtmlString(result);

        // Assert
        Assert.DoesNotContain("<!-- MvcFrontendKit", output);
        Assert.Contains("/dist/css/site.abc123.css", output);
    }

    #endregion

    #region FrontendDebugInfo Tests

    [Fact]
    public void FrontendDebugInfo_Development_RendersPanel()
    {
        // Arrange
        var config = CreateBasicConfig();
        var html = CreateHtmlHelper("Development", config);

        // Act
        var result = html.FrontendDebugInfo();
        var output = GetHtmlString(result);

        // Assert
        Assert.Contains("MvcFrontendKit Debug", output);
        Assert.Contains("Development (raw files)", output);
        Assert.Contains("<div", output); // Should contain styled panel
    }

    [Fact]
    public void FrontendDebugInfo_Production_ReturnsEmpty()
    {
        // Arrange
        var config = CreateBasicConfig();
        var manifest = new FrontendManifest();
        var html = CreateHtmlHelper("Production", config, manifest);

        // Act
        var result = html.FrontendDebugInfo();
        var output = GetHtmlString(result);

        // Assert
        Assert.Empty(output);
    }

    #endregion

    #region FrontendImportMap Tests

    [Fact]
    public void FrontendImportMap_Development_WithDisabledMap_ShowsSkipComment()
    {
        // Arrange
        var config = CreateBasicConfig();
        config.ImportMap = new ImportMapConfig { Enabled = false };
        var html = CreateHtmlHelper("Development", config);

        // Act
        var result = html.FrontendImportMap();
        var output = GetHtmlString(result);

        // Assert
        Assert.Contains("<!-- MvcFrontendKit:FrontendImportMap - Skipped", output);
    }

    [Fact]
    public void FrontendImportMap_Development_WithEnabledMap_ShowsDebugComment()
    {
        // Arrange
        var config = CreateBasicConfig();
        config.ImportMap = new ImportMapConfig
        {
            Enabled = true,
            Entries = new Dictionary<string, string>
            {
                ["vue"] = "/lib/vue/vue.esm-browser.js"
            }
        };
        var html = CreateHtmlHelper("Development", config);

        // Act
        var result = html.FrontendImportMap();
        var output = GetHtmlString(result);

        // Assert
        Assert.Contains("<!-- MvcFrontendKit:FrontendImportMap - 1 entries -->", output);
        Assert.Contains("importmap", output);
    }

    [Fact]
    public void FrontendImportMap_Production_ReturnsEmpty()
    {
        // Arrange
        var config = CreateBasicConfig();
        config.ImportMap = new ImportMapConfig { Enabled = true };
        var manifest = new FrontendManifest();
        var html = CreateHtmlHelper("Production", config, manifest);

        // Act
        var result = html.FrontendImportMap();
        var output = GetHtmlString(result);

        // Assert
        // In production, import map is typically handled by bundling strategy
        Assert.Empty(output);
    }

    #endregion

    #region Environment Name Case Insensitivity Tests

    [Theory]
    [InlineData("Development")]
    [InlineData("development")]
    [InlineData("DEVELOPMENT")]
    public void FrontendDebugInfo_VariousDevEnvironmentCases_RendersPanel(string envName)
    {
        // Arrange
        var config = CreateBasicConfig();
        var html = CreateHtmlHelper(envName, config);

        // Act
        var result = html.FrontendDebugInfo();
        var output = GetHtmlString(result);

        // Assert
        Assert.Contains("MvcFrontendKit Debug", output);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    [InlineData("Test")]
    public void FrontendDebugInfo_NonDevEnvironments_ReturnsEmpty(string envName)
    {
        // Arrange
        var config = CreateBasicConfig();
        var manifest = envName == "Production" ? new FrontendManifest() : null;
        var html = CreateHtmlHelper(envName, config, manifest);

        // Act
        var result = html.FrontendDebugInfo();
        var output = GetHtmlString(result);

        // Assert
        Assert.Empty(output);
    }

    #endregion

    #region FrontendInclude Tests

    [Fact]
    public void FrontendInclude_Development_ComponentNotFound_EmitsDebugComment()
    {
        // Arrange
        var config = CreateBasicConfig();
        config.Components = new Dictionary<string, ComponentConfig>();
        var html = CreateHtmlHelper("Development", config);

        // Act
        var result = html.FrontendInclude("nonexistent");
        var output = GetHtmlString(result);

        // Assert
        Assert.Contains("<!-- MvcFrontendKit:FrontendInclude - Component 'nonexistent' not found -->", output);
    }

    [Fact]
    public void FrontendInclude_Production_ComponentNotFound_ReturnsEmpty()
    {
        // Arrange
        var config = CreateBasicConfig();
        config.Components = new Dictionary<string, ComponentConfig>();
        var manifest = new FrontendManifest();
        var html = CreateHtmlHelper("Production", config, manifest);

        // Act
        var result = html.FrontendInclude("nonexistent");
        var output = GetHtmlString(result);

        // Assert
        Assert.Empty(output);
    }

    #endregion
}
