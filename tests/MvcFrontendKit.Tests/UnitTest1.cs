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
  conventions: []
  cssConventions: []
  overrides: {}

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
}
