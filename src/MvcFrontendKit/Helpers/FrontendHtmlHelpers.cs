using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.DependencyInjection;
using MvcFrontendKit.Configuration;
using MvcFrontendKit.Services;
using MvcFrontendKit.Utilities;
using System.Text.Encodings.Web;

namespace MvcFrontendKit.Helpers;

public static class FrontendHtmlHelpers
{
    public static IHtmlContent FrontendImportMap(this IHtmlHelper html)
    {
        var services = html.ViewContext.HttpContext.RequestServices;
        var manifestProvider = services.GetRequiredService<IFrontendManifestProvider>();
        var configProvider = services.GetRequiredService<IFrontendConfigProvider>();

        if (manifestProvider.IsProduction())
        {
            return HtmlString.Empty;
        }

        var config = configProvider.GetConfig();

        if (!config.ImportMap.Enabled || !config.ImportMap.Entries.Any())
        {
            return HtmlString.Empty;
        }

        var imports = string.Join(",\n      ",
            config.ImportMap.Entries.Select(kvp => $"\"{kvp.Key}\": \"{kvp.Value}\""));

        var importMapJson = $@"  <script type=""importmap"">
  {{
    ""imports"": {{
      {imports}
    }}
  }}
  </script>";

        return new HtmlString(importMapJson);
    }

    public static IHtmlContent FrontendGlobalStyles(this IHtmlHelper html)
    {
        var services = html.ViewContext.HttpContext.RequestServices;
        var manifestProvider = services.GetRequiredService<IFrontendManifestProvider>();
        var configProvider = services.GetRequiredService<IFrontendConfigProvider>();
        var environment = services.GetRequiredService<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>();

        var manifest = manifestProvider.GetManifest();
        var config = configProvider.GetConfig();

        if (manifest != null)
        {
            return RenderManifestStyles(manifest.GlobalCss);
        }

        return RenderDevStyles(config.Global.Css, environment, config);
    }

    public static IHtmlContent FrontendViewStyles(this IHtmlHelper html)
    {
        var services = html.ViewContext.HttpContext.RequestServices;
        var manifestProvider = services.GetRequiredService<IFrontendManifestProvider>();
        var configProvider = services.GetRequiredService<IFrontendConfigProvider>();
        var environment = services.GetRequiredService<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>();

        var viewKey = ViewKeyResolver.ResolveViewKey(html.ViewContext);
        if (string.IsNullOrEmpty(viewKey))
        {
            return HtmlString.Empty;
        }

        var manifest = manifestProvider.GetManifest();
        var config = configProvider.GetConfig();

        if (manifest != null)
        {
            var cssFiles = manifest.GetViewCss(viewKey);
            return RenderManifestStyles(cssFiles);
        }

        var resolver = new AssetResolver(environment, config);
        var devCssFiles = resolver.ResolveViewCss(viewKey);
        return RenderDevStyles(devCssFiles, environment, config);
    }

    public static IHtmlContent FrontendGlobalScripts(this IHtmlHelper html)
    {
        var services = html.ViewContext.HttpContext.RequestServices;
        var manifestProvider = services.GetRequiredService<IFrontendManifestProvider>();
        var configProvider = services.GetRequiredService<IFrontendConfigProvider>();
        var environment = services.GetRequiredService<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>();

        var manifest = manifestProvider.GetManifest();
        var config = configProvider.GetConfig();

        if (manifest != null)
        {
            return RenderManifestScripts(manifest.GlobalJs, isProd: true);
        }

        return RenderDevScripts(config.Global.Js, environment, config);
    }

    public static IHtmlContent FrontendViewScripts(this IHtmlHelper html)
    {
        var services = html.ViewContext.HttpContext.RequestServices;
        var manifestProvider = services.GetRequiredService<IFrontendManifestProvider>();
        var configProvider = services.GetRequiredService<IFrontendConfigProvider>();
        var environment = services.GetRequiredService<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>();

        var viewKey = ViewKeyResolver.ResolveViewKey(html.ViewContext);
        if (string.IsNullOrEmpty(viewKey))
        {
            return HtmlString.Empty;
        }

        var manifest = manifestProvider.GetManifest();
        var config = configProvider.GetConfig();

        if (manifest != null)
        {
            var jsFiles = manifest.GetViewJs(viewKey);
            return RenderManifestScripts(jsFiles, isProd: true);
        }

        var resolver = new AssetResolver(environment, config);
        var devJsFiles = resolver.ResolveViewJs(viewKey);
        return RenderDevScripts(devJsFiles, environment, config);
    }

    /// <summary>
    /// Renders debug information about the current view's frontend asset resolution.
    /// Only renders output in Development environment.
    /// </summary>
    public static IHtmlContent FrontendDebugInfo(this IHtmlHelper html)
    {
        var services = html.ViewContext.HttpContext.RequestServices;
        var environment = services.GetRequiredService<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>();

        // Only show in Development
        if (!environment.EnvironmentName.Equals("Development", StringComparison.OrdinalIgnoreCase))
        {
            return HtmlString.Empty;
        }

        var manifestProvider = services.GetRequiredService<IFrontendManifestProvider>();
        var configProvider = services.GetRequiredService<IFrontendConfigProvider>();
        var config = configProvider.GetConfig();
        var manifest = manifestProvider.GetManifest();

        var viewKey = ViewKeyResolver.ResolveViewKey(html.ViewContext);
        var isProduction = manifest != null;

        var resolver = new AssetResolver(environment, config);
        var jsFiles = isProduction
            ? manifest!.GetViewJs(viewKey)
            : resolver.ResolveViewJs(viewKey);
        var cssFiles = isProduction
            ? manifest!.GetViewCss(viewKey)
            : resolver.ResolveViewCss(viewKey);

        var debugHtml = $@"
<!-- MvcFrontendKit Debug Info -->
<div style=""position:fixed;bottom:10px;right:10px;background:#1e1e1e;color:#d4d4d4;padding:12px;border-radius:6px;font-family:monospace;font-size:12px;max-width:400px;z-index:9999;box-shadow:0 2px 10px rgba(0,0,0,0.3);"">
  <div style=""font-weight:bold;color:#569cd6;margin-bottom:8px;"">🔧 MvcFrontendKit Debug</div>
  <div style=""margin:4px 0;""><span style=""color:#9cdcfe;"">Mode:</span> <span style=""color:#ce9178;"">{(isProduction ? "Production (manifest)" : "Development (raw files)")}</span></div>
  <div style=""margin:4px 0;""><span style=""color:#9cdcfe;"">View Key:</span> <span style=""color:#ce9178;"">{HtmlEncoder.Default.Encode(viewKey ?? "(none)")}</span></div>
  <div style=""margin:4px 0;""><span style=""color:#9cdcfe;"">Manifest Key:</span> <span style=""color:#ce9178;"">view:{HtmlEncoder.Default.Encode(viewKey ?? "")}</span></div>
  <div style=""margin:8px 0 4px 0;color:#4ec9b0;"">JS Files ({jsFiles?.Count ?? 0}):</div>
  {RenderDebugFileList(jsFiles)}
  <div style=""margin:8px 0 4px 0;color:#4ec9b0;"">CSS Files ({cssFiles?.Count ?? 0}):</div>
  {RenderDebugFileList(cssFiles)}
  <div style=""margin-top:8px;font-size:10px;color:#6a9955;""><!-- Remove @Html.FrontendDebugInfo() for production --></div>
</div>";

        return new HtmlString(debugHtml);
    }

    private static string RenderDebugFileList(List<string>? files)
    {
        if (files == null || !files.Any())
        {
            return "<div style=\"color:#808080;margin-left:8px;\">(none)</div>";
        }

        return string.Join("", files.Select(f =>
            $"<div style=\"color:#dcdcaa;margin-left:8px;word-break:break-all;\">{HtmlEncoder.Default.Encode(f)}</div>"));
    }

    public static IHtmlContent FrontendInclude(this IHtmlHelper html, string componentName)
    {
        var services = html.ViewContext.HttpContext.RequestServices;
        var registry = services.GetRequiredService<IFrontendComponentRegistry>();
        var manifestProvider = services.GetRequiredService<IFrontendManifestProvider>();
        var configProvider = services.GetRequiredService<IFrontendConfigProvider>();
        var environment = services.GetRequiredService<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>();

        var config = configProvider.GetConfig();

        if (!config.Components.TryGetValue(componentName, out var component))
        {
            return HtmlString.Empty;
        }

        var htmlParts = new List<string>();

        if (component.Depends != null && component.Depends.Any())
        {
            foreach (var dependency in component.Depends)
            {
                var depHtml = FrontendInclude(html, dependency);
                using (var writer = new System.IO.StringWriter())
                {
                    depHtml.WriteTo(writer, HtmlEncoder.Default);
                    var depString = writer.ToString();
                    if (!string.IsNullOrWhiteSpace(depString))
                    {
                        htmlParts.Add(depString);
                    }
                }
            }
        }

        if (!registry.TryRegister(componentName))
        {
            return new HtmlString(string.Join("\n", htmlParts));
        }

        var manifest = manifestProvider.GetManifest();

        if (manifest != null)
        {
            var cssFiles = manifest.GetComponentCss(componentName);
            if (cssFiles != null && cssFiles.Any())
            {
                using (var writer = new System.IO.StringWriter())
                {
                    RenderManifestStyles(cssFiles).WriteTo(writer, HtmlEncoder.Default);
                    htmlParts.Add(writer.ToString());
                }
            }

            var jsFiles = manifest.GetComponentJs(componentName);
            if (jsFiles != null && jsFiles.Any())
            {
                using (var writer = new System.IO.StringWriter())
                {
                    RenderManifestScripts(jsFiles, isProd: true).WriteTo(writer, HtmlEncoder.Default);
                    htmlParts.Add(writer.ToString());
                }
            }
        }
        else
        {
            if (component.Css != null && component.Css.Any())
            {
                using (var writer = new System.IO.StringWriter())
                {
                    RenderDevStyles(component.Css, environment, config).WriteTo(writer, HtmlEncoder.Default);
                    htmlParts.Add(writer.ToString());
                }
            }

            if (component.Js != null && component.Js.Any())
            {
                using (var writer = new System.IO.StringWriter())
                {
                    RenderDevScripts(component.Js, environment, config).WriteTo(writer, HtmlEncoder.Default);
                    htmlParts.Add(writer.ToString());
                }
            }
        }

        return new HtmlString(string.Join("\n", htmlParts));
    }

    private static IHtmlContent RenderManifestStyles(List<string>? cssUrls)
    {
        if (cssUrls == null || !cssUrls.Any())
        {
            return HtmlString.Empty;
        }

        var tags = cssUrls.Select(url => $"  <link rel=\"stylesheet\" href=\"{HtmlEncoder.Default.Encode(url)}\" />");
        return new HtmlString(string.Join("\n", tags));
    }

    private static IHtmlContent RenderManifestScripts(List<string>? jsUrls, bool isProd)
    {
        if (jsUrls == null || !jsUrls.Any())
        {
            return HtmlString.Empty;
        }

        var tags = jsUrls.Select(url => $"  <script src=\"{HtmlEncoder.Default.Encode(url)}\"></script>");
        return new HtmlString(string.Join("\n", tags));
    }

    private static IHtmlContent RenderDevStyles(List<string> cssFiles, Microsoft.AspNetCore.Hosting.IWebHostEnvironment environment, FrontendConfig config)
    {
        if (!cssFiles.Any())
        {
            return HtmlString.Empty;
        }

        var contentRoot = environment.ContentRootPath;
        var tags = new List<string>();

        foreach (var cssFile in cssFiles)
        {
            var fullPath = Path.Combine(contentRoot, cssFile);
            if (!File.Exists(fullPath))
            {
                continue;
            }

            var url = ConvertToUrl(cssFile, config.WebRoot);
            var version = File.GetLastWriteTimeUtc(fullPath).Ticks;
            tags.Add($"  <link rel=\"stylesheet\" href=\"{HtmlEncoder.Default.Encode(url)}?v={version}\" />");
        }

        return new HtmlString(string.Join("\n", tags));
    }

    private static IHtmlContent RenderDevScripts(List<string> jsFiles, Microsoft.AspNetCore.Hosting.IWebHostEnvironment environment, FrontendConfig config)
    {
        if (!jsFiles.Any())
        {
            return HtmlString.Empty;
        }

        var contentRoot = environment.ContentRootPath;
        var tags = new List<string>();

        foreach (var jsFile in jsFiles)
        {
            var fullPath = Path.Combine(contentRoot, jsFile);
            if (!File.Exists(fullPath))
            {
                continue;
            }

            var url = ConvertToUrl(jsFile, config.WebRoot);
            var version = File.GetLastWriteTimeUtc(fullPath).Ticks;
            tags.Add($"  <script type=\"module\" src=\"{HtmlEncoder.Default.Encode(url)}?v={version}\"></script>");
        }

        return new HtmlString(string.Join("\n", tags));
    }

    private static string ConvertToUrl(string filePath, string webRoot)
    {
        var normalized = filePath.Replace('\\', '/');

        if (normalized.StartsWith(webRoot + "/", StringComparison.OrdinalIgnoreCase))
        {
            return "/" + normalized.Substring(webRoot.Length + 1);
        }

        if (normalized.StartsWith(webRoot, StringComparison.OrdinalIgnoreCase))
        {
            return "/" + normalized.Substring(webRoot.Length).TrimStart('/');
        }

        return "/" + normalized;
    }
}
