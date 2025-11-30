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
        var environment = services.GetRequiredService<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>();

        var isDev = IsDevelopment(environment);

        if (manifestProvider.IsProduction())
        {
            return isDev
                ? new HtmlString("<!-- MvcFrontendKit:FrontendImportMap - Skipped (Production mode) -->")
                : HtmlString.Empty;
        }

        var config = configProvider.GetConfig();

        if (!config.ImportMap.Enabled || !config.ImportMap.Entries.Any())
        {
            return isDev
                ? new HtmlString("<!-- MvcFrontendKit:FrontendImportMap - Skipped (ImportMap disabled or empty) -->")
                : HtmlString.Empty;
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

        if (isDev)
        {
            var debugComment = $"<!-- MvcFrontendKit:FrontendImportMap - {config.ImportMap.Entries.Count} entries -->";
            return new HtmlString($"{debugComment}\n{importMapJson}");
        }

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
        var isDev = IsDevelopment(environment);

        // Check if current area is isolated
        if (IsAreaIsolated(html.ViewContext, config))
        {
            return isDev
                ? new HtmlString($"<!-- MvcFrontendKit:FrontendGlobalStyles - Skipped (Area '{ViewKeyResolver.ResolveAreaName(html.ViewContext)}' is isolated) -->")
                : HtmlString.Empty;
        }

        if (manifest != null)
        {
            var cssFiles = manifest.GlobalCss;
            var content = RenderManifestStyles(cssFiles);

            if (isDev)
            {
                return WrapWithDebugComments("FrontendGlobalStyles",
                    $"Production mode | {cssFiles?.Count ?? 0} file(s)",
                    cssFiles,
                    content);
            }
            return content;
        }

        var devContent = RenderDevStyles(config.Global.Css, environment, config);

        if (isDev)
        {
            return WrapWithDebugComments("FrontendGlobalStyles",
                $"Development mode | {config.Global.Css.Count} file(s)",
                config.Global.Css,
                devContent);
        }

        return devContent;
    }

    public static IHtmlContent FrontendViewStyles(this IHtmlHelper html)
    {
        var services = html.ViewContext.HttpContext.RequestServices;
        var manifestProvider = services.GetRequiredService<IFrontendManifestProvider>();
        var configProvider = services.GetRequiredService<IFrontendConfigProvider>();
        var environment = services.GetRequiredService<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>();

        var viewKey = ViewKeyResolver.ResolveViewKey(html.ViewContext);
        var isDev = IsDevelopment(environment);

        if (string.IsNullOrEmpty(viewKey))
        {
            return isDev
                ? new HtmlString("<!-- MvcFrontendKit:FrontendViewStyles - No view key resolved -->")
                : HtmlString.Empty;
        }

        var manifest = manifestProvider.GetManifest();
        var config = configProvider.GetConfig();

        if (manifest != null)
        {
            var cssFiles = manifest.GetViewCss(viewKey);
            var content = RenderManifestStyles(cssFiles);

            if (isDev)
            {
                return WrapWithDebugComments("FrontendViewStyles",
                    $"Production mode | View: {viewKey} | {cssFiles?.Count ?? 0} file(s)",
                    cssFiles,
                    content);
            }
            return content;
        }

        var resolver = new AssetResolver(environment, config);
        var devCssFiles = resolver.ResolveViewCss(viewKey);
        var devContent = RenderDevStyles(devCssFiles, environment, config);

        if (isDev)
        {
            var resolution = config.Views.Overrides.ContainsKey(viewKey) ? "Override" : "Convention";
            return WrapWithDebugComments("FrontendViewStyles",
                $"Development mode | View: {viewKey} | Resolution: {resolution} | {devCssFiles.Count} file(s)",
                devCssFiles,
                devContent);
        }

        return devContent;
    }

    public static IHtmlContent FrontendGlobalScripts(this IHtmlHelper html)
    {
        var services = html.ViewContext.HttpContext.RequestServices;
        var manifestProvider = services.GetRequiredService<IFrontendManifestProvider>();
        var configProvider = services.GetRequiredService<IFrontendConfigProvider>();
        var environment = services.GetRequiredService<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>();

        var manifest = manifestProvider.GetManifest();
        var config = configProvider.GetConfig();
        var isDev = IsDevelopment(environment);

        // Check if current area is isolated
        if (IsAreaIsolated(html.ViewContext, config))
        {
            return isDev
                ? new HtmlString($"<!-- MvcFrontendKit:FrontendGlobalScripts - Skipped (Area '{ViewKeyResolver.ResolveAreaName(html.ViewContext)}' is isolated) -->")
                : HtmlString.Empty;
        }

        if (manifest != null)
        {
            var jsFiles = manifest.GlobalJs;
            var content = RenderManifestScripts(jsFiles, isProd: true);

            if (isDev)
            {
                return WrapWithDebugComments("FrontendGlobalScripts",
                    $"Production mode | {jsFiles?.Count ?? 0} file(s)",
                    jsFiles,
                    content);
            }
            return content;
        }

        var devContent = RenderDevScripts(config.Global.Js, environment, config);

        if (isDev)
        {
            return WrapWithDebugComments("FrontendGlobalScripts",
                $"Development mode | {config.Global.Js.Count} file(s)",
                config.Global.Js,
                devContent);
        }

        return devContent;
    }

    public static IHtmlContent FrontendViewScripts(this IHtmlHelper html)
    {
        var services = html.ViewContext.HttpContext.RequestServices;
        var manifestProvider = services.GetRequiredService<IFrontendManifestProvider>();
        var configProvider = services.GetRequiredService<IFrontendConfigProvider>();
        var environment = services.GetRequiredService<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>();

        var viewKey = ViewKeyResolver.ResolveViewKey(html.ViewContext);
        var isDev = IsDevelopment(environment);

        if (string.IsNullOrEmpty(viewKey))
        {
            return isDev
                ? new HtmlString("<!-- MvcFrontendKit:FrontendViewScripts - No view key resolved -->")
                : HtmlString.Empty;
        }

        var manifest = manifestProvider.GetManifest();
        var config = configProvider.GetConfig();

        if (manifest != null)
        {
            var jsFiles = manifest.GetViewJs(viewKey);
            var content = RenderManifestScripts(jsFiles, isProd: true);

            if (isDev)
            {
                return WrapWithDebugComments("FrontendViewScripts",
                    $"Production mode | View: {viewKey} | {jsFiles?.Count ?? 0} file(s)",
                    jsFiles,
                    content);
            }
            return content;
        }

        var resolver = new AssetResolver(environment, config);
        var devJsFiles = resolver.ResolveViewJs(viewKey);
        var devContent = RenderDevScripts(devJsFiles, environment, config);

        if (isDev)
        {
            var resolution = config.Views.Overrides.ContainsKey(viewKey) ? "Override" : "Convention";
            return WrapWithDebugComments("FrontendViewScripts",
                $"Development mode | View: {viewKey} | Resolution: {resolution} | {devJsFiles.Count} file(s)",
                devJsFiles,
                devContent);
        }

        return devContent;
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
        if (!IsDevelopment(environment))
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
  <div style=""font-weight:bold;color:#569cd6;margin-bottom:8px;"">MvcFrontendKit Debug</div>
  <div style=""margin:4px 0;""><span style=""color:#9cdcfe;"">Mode:</span> <span style=""color:#ce9178;"">{(isProduction ? "Production (manifest)" : "Development (raw files)")}</span></div>
  <div style=""margin:4px 0;""><span style=""color:#9cdcfe;"">View Key:</span> <span style=""color:#ce9178;"">{HtmlEncoder.Default.Encode(viewKey ?? "(none)")}</span></div>
  <div style=""margin:4px 0;""><span style=""color:#9cdcfe;"">Manifest Key:</span> <span style=""color:#ce9178;"">view:{HtmlEncoder.Default.Encode(viewKey ?? "")}</span></div>
  <div style=""margin:8px 0 4px 0;color:#4ec9b0;"">JS Files ({jsFiles?.Count ?? 0}):</div>
  {RenderDebugFileList(jsFiles)}
  <div style=""margin:8px 0 4px 0;color:#4ec9b0;"">CSS Files ({cssFiles?.Count ?? 0}):</div>
  {RenderDebugFileList(cssFiles)}
  <div style=""margin-top:8px;font-size:10px;color:#6a9955;"">Remove @Html.FrontendDebugInfo() for production</div>
</div>";

        return new HtmlString(debugHtml);
    }

    public static IHtmlContent FrontendInclude(this IHtmlHelper html, string componentName)
    {
        var services = html.ViewContext.HttpContext.RequestServices;
        var registry = services.GetRequiredService<IFrontendComponentRegistry>();
        var manifestProvider = services.GetRequiredService<IFrontendManifestProvider>();
        var configProvider = services.GetRequiredService<IFrontendConfigProvider>();
        var environment = services.GetRequiredService<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>();

        var config = configProvider.GetConfig();
        var isDev = IsDevelopment(environment);

        if (!config.Components.TryGetValue(componentName, out var component))
        {
            return isDev
                ? new HtmlString($"<!-- MvcFrontendKit:FrontendInclude - Component '{componentName}' not found -->")
                : HtmlString.Empty;
        }

        var htmlParts = new List<string>();
        var debugInfo = new List<string>();

        // Process dependencies
        if (component.Depends != null && component.Depends.Any())
        {
            if (isDev) debugInfo.Add($"Dependencies: {string.Join(", ", component.Depends)}");

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

        // Check if already registered (deduplication)
        if (!registry.TryRegister(componentName))
        {
            if (isDev)
            {
                htmlParts.Insert(0, $"<!-- MvcFrontendKit:FrontendInclude({componentName}) - Already rendered (deduplicated) -->");
            }
            return new HtmlString(string.Join("\n", htmlParts));
        }

        var manifest = manifestProvider.GetManifest();
        var componentCssFiles = new List<string>();
        var componentJsFiles = new List<string>();

        if (manifest != null)
        {
            var cssFiles = manifest.GetComponentCss(componentName);
            if (cssFiles != null && cssFiles.Any())
            {
                componentCssFiles.AddRange(cssFiles);
                using (var writer = new System.IO.StringWriter())
                {
                    RenderManifestStyles(cssFiles).WriteTo(writer, HtmlEncoder.Default);
                    htmlParts.Add(writer.ToString());
                }
            }

            var jsFiles = manifest.GetComponentJs(componentName);
            if (jsFiles != null && jsFiles.Any())
            {
                componentJsFiles.AddRange(jsFiles);
                using (var writer = new System.IO.StringWriter())
                {
                    RenderManifestScripts(jsFiles, isProd: true).WriteTo(writer, HtmlEncoder.Default);
                    htmlParts.Add(writer.ToString());
                }
            }

            if (isDev) debugInfo.Add("Production mode");
        }
        else
        {
            if (component.Css != null && component.Css.Any())
            {
                componentCssFiles.AddRange(component.Css);
                using (var writer = new System.IO.StringWriter())
                {
                    RenderDevStyles(component.Css, environment, config).WriteTo(writer, HtmlEncoder.Default);
                    htmlParts.Add(writer.ToString());
                }
            }

            if (component.Js != null && component.Js.Any())
            {
                componentJsFiles.AddRange(component.Js);
                using (var writer = new System.IO.StringWriter())
                {
                    RenderDevScripts(component.Js, environment, config).WriteTo(writer, HtmlEncoder.Default);
                    htmlParts.Add(writer.ToString());
                }
            }

            if (isDev) debugInfo.Add("Development mode");
        }

        if (isDev)
        {
            debugInfo.Add($"CSS: {componentCssFiles.Count}, JS: {componentJsFiles.Count}");
            var debugHeader = $"<!-- MvcFrontendKit:FrontendInclude({componentName}) - {string.Join(" | ", debugInfo)} -->";

            // Add file details
            var fileDetails = new List<string>();
            foreach (var css in componentCssFiles)
            {
                fileDetails.Add($"<!--   CSS: {css} -->");
            }
            foreach (var js in componentJsFiles)
            {
                fileDetails.Add($"<!--   JS: {js} -->");
            }

            htmlParts.Insert(0, debugHeader);
            if (fileDetails.Any())
            {
                htmlParts.Insert(1, string.Join("\n", fileDetails));
            }
        }

        return new HtmlString(string.Join("\n", htmlParts));
    }

    #region Private Helper Methods

    private static bool IsDevelopment(Microsoft.AspNetCore.Hosting.IWebHostEnvironment environment)
    {
        return environment.EnvironmentName.Equals("Development", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Checks if the current view is in an isolated area.
    /// Isolated areas do not receive global JS/CSS.
    /// </summary>
    private static bool IsAreaIsolated(ViewContext viewContext, FrontendConfig config)
    {
        var areaName = ViewKeyResolver.ResolveAreaName(viewContext);

        // "Root" means no area, so not isolated
        if (areaName == "Root")
        {
            return false;
        }

        // Check if this area is configured and has isolate: true
        if (config.Areas.TryGetValue(areaName, out var areaConfig))
        {
            return areaConfig.Isolate;
        }

        return false;
    }

    private static IHtmlContent WrapWithDebugComments(string helperName, string summary, List<string>? files, IHtmlContent content)
    {
        var parts = new List<string>();

        // Opening comment with summary
        parts.Add($"<!-- MvcFrontendKit:{helperName} - {summary} -->");

        // File list comments
        if (files != null && files.Any())
        {
            foreach (var file in files)
            {
                parts.Add($"<!--   {file} -->");
            }
        }

        // Actual content
        using (var writer = new System.IO.StringWriter())
        {
            content.WriteTo(writer, HtmlEncoder.Default);
            var contentStr = writer.ToString();
            if (!string.IsNullOrWhiteSpace(contentStr))
            {
                parts.Add(contentStr);
            }
        }

        return new HtmlString(string.Join("\n", parts));
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
            // For SCSS/Sass files, look for the compiled .css version
            var resolvedFile = ResolveDevCssFile(cssFile, contentRoot);
            if (resolvedFile == null)
            {
                continue;
            }

            var url = ConvertToUrl(resolvedFile, config.WebRoot);
            var fullPath = Path.Combine(contentRoot, resolvedFile);
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
            // For TypeScript files, look for the compiled .js version
            var resolvedFile = ResolveDevJsFile(jsFile, contentRoot);
            if (resolvedFile == null)
            {
                continue;
            }

            var url = ConvertToUrl(resolvedFile, config.WebRoot);
            var fullPath = Path.Combine(contentRoot, resolvedFile);
            var version = File.GetLastWriteTimeUtc(fullPath).Ticks;
            tags.Add($"  <script type=\"module\" src=\"{HtmlEncoder.Default.Encode(url)}?v={version}\"></script>");
        }

        return new HtmlString(string.Join("\n", tags));
    }

    /// <summary>
    /// Resolves a CSS file path for development mode.
    /// If the config declares a .scss/.sass file, looks for the compiled .css version.
    /// Returns null if no suitable file is found.
    /// </summary>
    private static string? ResolveDevCssFile(string cssFile, string contentRoot)
    {
        var fullPath = Path.Combine(contentRoot, cssFile);

        // If it's a plain CSS file and exists, use it directly
        if (cssFile.EndsWith(".css", StringComparison.OrdinalIgnoreCase))
        {
            return File.Exists(fullPath) ? cssFile : null;
        }

        // For SCSS/Sass files, look for the compiled CSS version
        if (cssFile.EndsWith(".scss", StringComparison.OrdinalIgnoreCase) ||
            cssFile.EndsWith(".sass", StringComparison.OrdinalIgnoreCase))
        {
            // Try the compiled .css file (site.scss -> site.css)
            var compiledCss = Path.ChangeExtension(cssFile, ".css");
            var compiledPath = Path.Combine(contentRoot, compiledCss);
            if (File.Exists(compiledPath))
            {
                return compiledCss;
            }

            // Fall back to original file if it somehow exists (shouldn't in browsers)
            return File.Exists(fullPath) ? cssFile : null;
        }

        return File.Exists(fullPath) ? cssFile : null;
    }

    /// <summary>
    /// Resolves a JS file path for development mode.
    /// If the config declares a .ts/.tsx file, looks for the compiled .js version.
    /// Returns null if no suitable file is found.
    /// </summary>
    private static string? ResolveDevJsFile(string jsFile, string contentRoot)
    {
        var fullPath = Path.Combine(contentRoot, jsFile);

        // If it's a plain JS file and exists, use it directly
        if (jsFile.EndsWith(".js", StringComparison.OrdinalIgnoreCase))
        {
            return File.Exists(fullPath) ? jsFile : null;
        }

        // For TypeScript files, look for the compiled JS version
        if (jsFile.EndsWith(".ts", StringComparison.OrdinalIgnoreCase) ||
            jsFile.EndsWith(".tsx", StringComparison.OrdinalIgnoreCase))
        {
            // Try the compiled .js file (site.ts -> site.js)
            var compiledJs = Path.ChangeExtension(jsFile, ".js");
            var compiledPath = Path.Combine(contentRoot, compiledJs);
            if (File.Exists(compiledPath))
            {
                return compiledJs;
            }

            // Fall back to original file if it somehow exists (shouldn't in browsers)
            return File.Exists(fullPath) ? jsFile : null;
        }

        return File.Exists(fullPath) ? jsFile : null;
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

    #endregion
}
