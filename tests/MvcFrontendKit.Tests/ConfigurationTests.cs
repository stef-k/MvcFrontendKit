using MvcFrontendKit.Configuration;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace MvcFrontendKit.Tests;

public class ConfigurationTests
{
    [Fact]
    public void CanDeserializeDefaultConfig()
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

        var config = deserializer.Deserialize<FrontendConfig>(yaml);

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
    public void DefaultConfigHasCorrectDefaults()
    {
        var config = new FrontendConfig();

        Assert.Equal(1, config.ConfigVersion);
        Assert.Equal("views", config.Mode);
        Assert.Equal("/", config.AppBasePath);
        Assert.Equal("wwwroot", config.WebRoot);
        Assert.Equal("es2020", config.Esbuild.JsTarget);
        Assert.True(config.Esbuild.JsSourcemap);
        Assert.True(config.Esbuild.CssSourcemap);
    }

    [Fact]
    public void CanDeserializeSingleMode()
    {
        var yaml = @"
configVersion: 1
mode: single
";
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        var config = deserializer.Deserialize<FrontendConfig>(yaml);

        Assert.Equal("single", config.Mode);
    }

    [Fact]
    public void CanDeserializeAreasMode()
    {
        var yaml = @"
configVersion: 1
mode: areas
";
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        var config = deserializer.Deserialize<FrontendConfig>(yaml);

        Assert.Equal("areas", config.Mode);
    }

    [Fact]
    public void CanDeserializeComponents()
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

        var config = deserializer.Deserialize<FrontendConfig>(yaml);

        Assert.NotNull(config.Components);
        Assert.Equal(2, config.Components.Count);
        Assert.True(config.Components.ContainsKey("datepicker"));
        Assert.True(config.Components.ContainsKey("calendar"));

        var datepicker = config.Components["datepicker"];
        Assert.Single(datepicker.Js);
        Assert.Single(datepicker.Css);

        var calendar = config.Components["calendar"];
        Assert.Single(calendar.Js);
        Assert.Empty(calendar.Css); // Empty list, not null
    }

    [Fact]
    public void CanDeserializeImportMap()
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

        var config = deserializer.Deserialize<FrontendConfig>(yaml);

        Assert.True(config.ImportMap.Enabled);
        Assert.Equal("bundle", config.ImportMap.ProdStrategy);
        Assert.NotNull(config.ImportMap.Entries);
        Assert.Equal(2, config.ImportMap.Entries.Count);
        Assert.Equal("/lib/lodash/lodash.min.js", config.ImportMap.Entries["lodash"]);
        Assert.Equal("/lib/chartjs/chart.min.js", config.ImportMap.Entries["chart.js"]);
    }

    [Fact]
    public void CanDeserializeEsbuildOptions()
    {
        var yaml = @"
configVersion: 1
esbuild:
  jsTarget: es2022
  jsSourcemap: false
  cssSourcemap: false
";
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        var config = deserializer.Deserialize<FrontendConfig>(yaml);

        Assert.Equal("es2022", config.Esbuild.JsTarget);
        Assert.False(config.Esbuild.JsSourcemap);
        Assert.False(config.Esbuild.CssSourcemap);
    }

    [Fact]
    public void EsbuildJsFormatDefaultsToIife()
    {
        var config = new FrontendConfig();

        Assert.Equal("iife", config.Esbuild.JsFormat);
    }

    [Theory]
    [InlineData("iife")]
    [InlineData("esm")]
    public void CanDeserializeEsbuildJsFormat(string format)
    {
        var yaml = $@"
configVersion: 1
esbuild:
  jsFormat: {format}
";
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        var config = deserializer.Deserialize<FrontendConfig>(yaml);

        Assert.Equal(format, config.Esbuild.JsFormat);
    }

    [Fact]
    public void CanDeserializeCssUrlPolicy()
    {
        var yaml = @"
configVersion: 1
cssUrlPolicy:
  allowRelative: true
  resolveImports: false
";
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        var config = deserializer.Deserialize<FrontendConfig>(yaml);

        Assert.True(config.CssUrlPolicy.AllowRelative);
        Assert.False(config.CssUrlPolicy.ResolveImports);
    }

    [Fact]
    public void CanDeserializeAppBasePathSubPath()
    {
        var yaml = @"
configVersion: 1
appBasePath: /hr-app
";
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        var config = deserializer.Deserialize<FrontendConfig>(yaml);

        Assert.Equal("/hr-app", config.AppBasePath);
    }

    [Fact]
    public void CanDeserializeGlobalAssets()
    {
        var yaml = @"
configVersion: 1
global:
  js:
    - wwwroot/js/app.js
    - wwwroot/js/utils.js
  css:
    - wwwroot/css/main.css
    - wwwroot/css/theme.css
";
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        var config = deserializer.Deserialize<FrontendConfig>(yaml);

        Assert.Equal(2, config.Global.Js.Count);
        Assert.Equal(2, config.Global.Css.Count);
        Assert.Contains("wwwroot/js/app.js", config.Global.Js);
        Assert.Contains("wwwroot/css/main.css", config.Global.Css);
    }
}
