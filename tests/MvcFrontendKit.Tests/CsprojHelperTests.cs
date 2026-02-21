using System.Xml.Linq;
using MvcFrontendKit.Cli.Helpers;

namespace MvcFrontendKit.Tests;

public class CsprojHelperTests : IDisposable
{
    private readonly string _tempDir;

    public CsprojHelperTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"MvcFrontendKit_CsprojHelper_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    #region FindCsprojFile Tests

    [Fact]
    public void FindCsprojFile_NoCsproj_ReturnsNull()
    {
        var result = CsprojHelper.FindCsprojFile(_tempDir);

        Assert.Null(result);
    }

    [Fact]
    public void FindCsprojFile_SingleCsproj_ReturnsPath()
    {
        var csprojPath = Path.Combine(_tempDir, "MyApp.csproj");
        File.WriteAllText(csprojPath, "<Project />");

        var result = CsprojHelper.FindCsprojFile(_tempDir);

        Assert.Equal(csprojPath, result);
    }

    [Fact]
    public void FindCsprojFile_MultipleCsproj_ReturnsNull()
    {
        File.WriteAllText(Path.Combine(_tempDir, "App1.csproj"), "<Project />");
        File.WriteAllText(Path.Combine(_tempDir, "App2.csproj"), "<Project />");

        var result = CsprojHelper.FindCsprojFile(_tempDir);

        Assert.Null(result);
    }

    #endregion

    #region HasConfigContentItem Tests

    [Fact]
    public void HasConfigContentItem_NotPresent_ReturnsFalse()
    {
        var doc = XDocument.Parse("""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        Assert.False(CsprojHelper.HasConfigContentItem(doc));
    }

    [Fact]
    public void HasConfigContentItem_Present_ReturnsTrue()
    {
        var doc = XDocument.Parse("""
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <Content Include="frontend.config.yaml" CopyToPublishDirectory="PreserveNewest" />
              </ItemGroup>
            </Project>
            """);

        Assert.True(CsprojHelper.HasConfigContentItem(doc));
    }

    [Fact]
    public void HasConfigContentItem_CaseInsensitive_ReturnsTrue()
    {
        var doc = XDocument.Parse("""
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <Content Include="Frontend.Config.Yaml" CopyToPublishDirectory="PreserveNewest" />
              </ItemGroup>
            </Project>
            """);

        Assert.True(CsprojHelper.HasConfigContentItem(doc));
    }

    #endregion

    #region AddConfigContentItem Tests

    [Fact]
    public void AddConfigContentItem_AddsNewItemGroup()
    {
        var doc = XDocument.Parse("""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        var result = CsprojHelper.AddConfigContentItem(doc);

        Assert.True(result);
        Assert.True(CsprojHelper.HasConfigContentItem(doc));

        var contentEl = doc.Descendants("Content").First();
        Assert.Equal("frontend.config.yaml", contentEl.Attribute("Include")?.Value);
        Assert.Equal("PreserveNewest", contentEl.Attribute("CopyToPublishDirectory")?.Value);
    }

    [Fact]
    public void AddConfigContentItem_AlreadyExists_ReturnsFalse()
    {
        var doc = XDocument.Parse("""
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <Content Include="frontend.config.yaml" CopyToPublishDirectory="PreserveNewest" />
              </ItemGroup>
            </Project>
            """);

        var result = CsprojHelper.AddConfigContentItem(doc);

        Assert.False(result);
    }

    [Fact]
    public void AddConfigContentItem_Idempotent_NoDuplicates()
    {
        var doc = XDocument.Parse("""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        CsprojHelper.AddConfigContentItem(doc);
        CsprojHelper.AddConfigContentItem(doc);

        var contentElements = doc.Descendants("Content")
            .Where(el => string.Equals(
                el.Attribute("Include")?.Value,
                "frontend.config.yaml",
                StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.Single(contentElements);
    }

    [Fact]
    public void AddConfigContentItem_PreservesExistingElements()
    {
        var doc = XDocument.Parse("""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="SomePackage" Version="1.0.0" />
              </ItemGroup>
            </Project>
            """);

        CsprojHelper.AddConfigContentItem(doc);

        // Original elements still present
        Assert.Single(doc.Descendants("PackageReference"));
        Assert.Single(doc.Descendants("PropertyGroup"));

        // New Content item added
        Assert.True(CsprojHelper.HasConfigContentItem(doc));
    }

    #endregion

    #region GetManualXmlSnippet Tests

    [Fact]
    public void GetManualXmlSnippet_ContainsRequiredElements()
    {
        var snippet = CsprojHelper.GetManualXmlSnippet();

        Assert.Contains("frontend.config.yaml", snippet);
        Assert.Contains("CopyToPublishDirectory", snippet);
        Assert.Contains("PreserveNewest", snippet);
        Assert.Contains("<Content", snippet);
        Assert.Contains("<ItemGroup>", snippet);
    }

    #endregion

    #region End-to-End Tests

    [Fact]
    public void EndToEnd_FindParsePatchSaveReload()
    {
        // Arrange: write a realistic .csproj
        var csprojPath = Path.Combine(_tempDir, "MyWebApp.csproj");
        File.WriteAllText(csprojPath, """
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <Nullable>enable</Nullable>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="MvcFrontendKit" Version="1.1.0" />
              </ItemGroup>
            </Project>
            """);

        // Act: find, parse, patch, save
        var foundPath = CsprojHelper.FindCsprojFile(_tempDir);
        Assert.NotNull(foundPath);

        var doc = XDocument.Load(foundPath);
        Assert.False(CsprojHelper.HasConfigContentItem(doc));

        var added = CsprojHelper.AddConfigContentItem(doc);
        Assert.True(added);

        doc.Save(foundPath);

        // Verify: reload and check
        var reloaded = XDocument.Load(foundPath);
        Assert.True(CsprojHelper.HasConfigContentItem(reloaded));

        // Verify idempotent on reload
        Assert.False(CsprojHelper.AddConfigContentItem(reloaded));
    }

    #endregion
}
