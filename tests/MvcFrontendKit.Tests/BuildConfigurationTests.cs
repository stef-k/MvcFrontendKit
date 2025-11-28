using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using BuildConfig = MvcFrontendKit.Build.Configuration;

namespace MvcFrontendKit.Tests;

/// <summary>
/// Tests for the MvcFrontendKit.Build configuration classes.
/// These are separate from the runtime configuration classes to avoid ASP.NET dependencies in the build task.
/// </summary>
public class BuildConfigurationTests
{
    [Fact]
    public void BuildConfig_CanDeserializeDefaultConfig()
    {
        var yaml = @"
configVersion: 1
mode: views
appBasePath: /
webRoot: wwwroot
jsRoot: wwwroot/js
cssRoot: wwwroot/css
libRoot: wwwroot/lib
distUrlRoot: /dist
distJsSubPath: js
distCssSubPath: css

output:
  cleanDistOnBuild: true

cssUrlPolicy:
  allowRelative: false
  resolveImports: true

importMap:
  enabled: true
  prodStrategy: bundle
  entries: {}

global:
  js:
    - wwwroot/js/site.js
  css:
    - wwwroot/css/site.css

views:
  jsAutoLinkByConvention: true
  cssAutoLinkByConvention: true

components: {}

esbuild:
  jsTarget: es2020
  jsSourcemap: true
  cssSourcemap: true
";

        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        var config = deserializer.Deserialize<BuildConfig.FrontendConfig>(yaml);

        Assert.NotNull(config);
        Assert.Equal(1, config.ConfigVersion);
        Assert.Equal("views", config.Mode);
        Assert.Equal("/", config.AppBasePath);
        Assert.Equal("wwwroot", config.WebRoot);
        Assert.True(config.Output.CleanDistOnBuild);
        Assert.False(config.CssUrlPolicy.AllowRelative);
        Assert.True(config.CssUrlPolicy.ResolveImports);
        Assert.Single(config.Global.Js);
        Assert.Single(config.Global.Css);
    }

    [Fact]
    public void BuildConfig_DefaultsAreCorrect()
    {
        var config = new BuildConfig.FrontendConfig();

        Assert.Equal(1, config.ConfigVersion);
        Assert.Equal("views", config.Mode);
        Assert.Equal("/", config.AppBasePath);
        Assert.Equal("wwwroot", config.WebRoot);
        Assert.Equal("es2020", config.Esbuild.JsTarget);
        Assert.True(config.Esbuild.JsSourcemap);
        Assert.True(config.Esbuild.CssSourcemap);
    }

    [Fact]
    public void BuildConfig_CanDeserializeSingleMode()
    {
        var yaml = @"
configVersion: 1
mode: single
";
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        var config = deserializer.Deserialize<BuildConfig.FrontendConfig>(yaml);

        Assert.Equal("single", config.Mode);
    }

    [Fact]
    public void BuildConfig_CanDeserializeComponents()
    {
        var yaml = @"
configVersion: 1
components:
  datepicker:
    js:
      - wwwroot/js/components/datepicker.js
    css:
      - wwwroot/css/components/datepicker.css
  calendar:
    js:
      - wwwroot/js/components/calendar.js
";
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        var config = deserializer.Deserialize<BuildConfig.FrontendConfig>(yaml);

        Assert.NotNull(config.Components);
        Assert.Equal(2, config.Components.Count);
        Assert.True(config.Components.ContainsKey("datepicker"));
        Assert.True(config.Components.ContainsKey("calendar"));

        var datepicker = config.Components["datepicker"];
        Assert.Single(datepicker.Js);
        Assert.Single(datepicker.Css);

        var calendar = config.Components["calendar"];
        Assert.Single(calendar.Js);
        Assert.Empty(calendar.Css);
    }

    [Fact]
    public void BuildConfig_CanDeserializeImportMap()
    {
        var yaml = @"
configVersion: 1
importMap:
  enabled: true
  prodStrategy: bundle
  entries:
    lodash: /lib/lodash/lodash.min.js
    chart.js: /lib/chartjs/chart.min.js
";
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        var config = deserializer.Deserialize<BuildConfig.FrontendConfig>(yaml);

        Assert.True(config.ImportMap.Enabled);
        Assert.Equal("bundle", config.ImportMap.ProdStrategy);
        Assert.NotNull(config.ImportMap.Entries);
        Assert.Equal(2, config.ImportMap.Entries.Count);
        Assert.Equal("/lib/lodash/lodash.min.js", config.ImportMap.Entries["lodash"]);
        Assert.Equal("/lib/chartjs/chart.min.js", config.ImportMap.Entries["chart.js"]);
    }

    [Fact]
    public void BuildConfig_CanDeserializeViewOverrides()
    {
        var yaml = @"
configVersion: 1
mode: views
views:
  jsAutoLinkByConvention: true
  cssAutoLinkByConvention: true
  overrides:
    Views/Home/Index:
      js:
        - wwwroot/js/home/index.js
      css:
        - wwwroot/css/home/index.css
    Views/Admin/Dashboard:
      js:
        - wwwroot/js/admin/dashboard.js
";
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        var config = deserializer.Deserialize<BuildConfig.FrontendConfig>(yaml);

        Assert.NotNull(config.Views.Overrides);
        Assert.Equal(2, config.Views.Overrides.Count);
        Assert.True(config.Views.Overrides.ContainsKey("Views/Home/Index"));
        Assert.True(config.Views.Overrides.ContainsKey("Views/Admin/Dashboard"));

        var homeIndex = config.Views.Overrides["Views/Home/Index"];
        Assert.Single(homeIndex.Js);
        Assert.Single(homeIndex.Css);
    }

    [Fact]
    public void BuildConfig_EsbuildJsFormatDefaultsToIife()
    {
        var config = new BuildConfig.FrontendConfig();

        Assert.Equal("iife", config.Esbuild.JsFormat);
    }

    [Theory]
    [InlineData("iife")]
    [InlineData("esm")]
    public void BuildConfig_CanDeserializeJsFormat(string format)
    {
        var yaml = $@"
configVersion: 1
esbuild:
  jsFormat: {format}
";
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        var config = deserializer.Deserialize<BuildConfig.FrontendConfig>(yaml);

        Assert.Equal(format, config.Esbuild.JsFormat);
    }
}
