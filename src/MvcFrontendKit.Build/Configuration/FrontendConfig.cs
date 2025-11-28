using System.Collections.Generic;
using YamlDotNet.Serialization;

namespace MvcFrontendKit.Build.Configuration;

public class FrontendConfig
{
    [YamlMember(Alias = "configVersion")]
    public int ConfigVersion { get; set; } = 1;

    [YamlMember(Alias = "mode")]
    public string Mode { get; set; } = "views";

    [YamlMember(Alias = "appBasePath")]
    public string AppBasePath { get; set; } = "/";

    [YamlMember(Alias = "webRoot")]
    public string WebRoot { get; set; } = "wwwroot";

    [YamlMember(Alias = "jsRoot")]
    public string JsRoot { get; set; } = "wwwroot/js";

    [YamlMember(Alias = "cssRoot")]
    public string CssRoot { get; set; } = "wwwroot/css";

    [YamlMember(Alias = "libRoot")]
    public string LibRoot { get; set; } = "wwwroot/lib";

    [YamlMember(Alias = "distUrlRoot")]
    public string DistUrlRoot { get; set; } = "/dist";

    [YamlMember(Alias = "distJsSubPath")]
    public string DistJsSubPath { get; set; } = "js";

    [YamlMember(Alias = "distCssSubPath")]
    public string DistCssSubPath { get; set; } = "css";

    [YamlMember(Alias = "output")]
    public OutputConfig Output { get; set; } = new OutputConfig();

    [YamlMember(Alias = "cssUrlPolicy")]
    public CssUrlPolicyConfig CssUrlPolicy { get; set; } = new CssUrlPolicyConfig();

    [YamlMember(Alias = "importMap")]
    public ImportMapConfig ImportMap { get; set; } = new ImportMapConfig();

    [YamlMember(Alias = "global")]
    public GlobalAssetsConfig Global { get; set; } = new GlobalAssetsConfig();

    [YamlMember(Alias = "views")]
    public ViewsConfig Views { get; set; } = new ViewsConfig();

    [YamlMember(Alias = "components")]
    public Dictionary<string, ComponentConfig> Components { get; set; } = new Dictionary<string, ComponentConfig>();

    [YamlMember(Alias = "esbuild")]
    public EsbuildConfig Esbuild { get; set; } = new EsbuildConfig();
}

public class OutputConfig
{
    [YamlMember(Alias = "cleanDistOnBuild")]
    public bool CleanDistOnBuild { get; set; } = true;
}

public class CssUrlPolicyConfig
{
    [YamlMember(Alias = "allowRelative")]
    public bool AllowRelative { get; set; } = false;

    [YamlMember(Alias = "resolveImports")]
    public bool ResolveImports { get; set; } = true;
}

public class ImportMapConfig
{
    [YamlMember(Alias = "enabled")]
    public bool Enabled { get; set; } = true;

    [YamlMember(Alias = "prodStrategy")]
    public string ProdStrategy { get; set; } = "bundle";

    [YamlMember(Alias = "entries")]
    public Dictionary<string, string> Entries { get; set; } = new Dictionary<string, string>();
}

public class GlobalAssetsConfig
{
    [YamlMember(Alias = "js")]
    public List<string> Js { get; set; } = new List<string>();

    [YamlMember(Alias = "css")]
    public List<string> Css { get; set; } = new List<string>();
}

public class ViewsConfig
{
    [YamlMember(Alias = "jsAutoLinkByConvention")]
    public bool JsAutoLinkByConvention { get; set; } = true;

    [YamlMember(Alias = "cssAutoLinkByConvention")]
    public bool CssAutoLinkByConvention { get; set; } = true;

    [YamlMember(Alias = "conventions")]
    public List<ViewConvention> Conventions { get; set; } = new List<ViewConvention>();

    [YamlMember(Alias = "cssConventions")]
    public List<CssConvention> CssConventions { get; set; } = new List<CssConvention>();

    [YamlMember(Alias = "overrides")]
    public Dictionary<string, ViewOverride> Overrides { get; set; } = new Dictionary<string, ViewOverride>();
}

public class ViewConvention
{
    [YamlMember(Alias = "viewPattern")]
    public string ViewPattern { get; set; } = string.Empty;

    [YamlMember(Alias = "scriptBasePattern")]
    public string ScriptBasePattern { get; set; } = string.Empty;
}

public class CssConvention
{
    [YamlMember(Alias = "viewPattern")]
    public string ViewPattern { get; set; } = string.Empty;

    [YamlMember(Alias = "cssPattern")]
    public string CssPattern { get; set; } = string.Empty;
}

public class ViewOverride
{
    [YamlMember(Alias = "js")]
    public List<string> Js { get; set; } = new List<string>();

    [YamlMember(Alias = "css")]
    public List<string> Css { get; set; } = new List<string>();
}

public class ComponentConfig
{
    [YamlMember(Alias = "js")]
    public List<string> Js { get; set; } = new List<string>();

    [YamlMember(Alias = "css")]
    public List<string> Css { get; set; } = new List<string>();

    [YamlMember(Alias = "depends")]
    public List<string> Depends { get; set; } = new List<string>();
}

public class EsbuildConfig
{
    [YamlMember(Alias = "jsTarget")]
    public string JsTarget { get; set; } = "es2020";

    /// <summary>
    /// JS output format: "iife" (default) or "esm".
    /// IIFE wraps bundle in (function(){...})(); - works with regular script tags.
    /// ESM preserves ES module syntax - requires type="module" on script tags.
    /// </summary>
    [YamlMember(Alias = "jsFormat")]
    public string JsFormat { get; set; } = "iife";

    [YamlMember(Alias = "jsSourcemap")]
    public bool JsSourcemap { get; set; } = true;

    [YamlMember(Alias = "cssSourcemap")]
    public bool CssSourcemap { get; set; } = true;
}
