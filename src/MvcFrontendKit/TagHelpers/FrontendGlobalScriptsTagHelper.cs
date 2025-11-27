using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Razor.TagHelpers;
using MvcFrontendKit.Services;
using System.Text.Encodings.Web;

namespace MvcFrontendKit.TagHelpers;

[HtmlTargetElement("frontend-global-scripts")]
public class FrontendGlobalScriptsTagHelper : TagHelper
{
    private readonly IFrontendManifestProvider _manifestProvider;
    private readonly IFrontendConfigProvider _configProvider;
    private readonly IWebHostEnvironment _environment;

    public FrontendGlobalScriptsTagHelper(
        IFrontendManifestProvider manifestProvider,
        IFrontendConfigProvider configProvider,
        IWebHostEnvironment environment)
    {
        _manifestProvider = manifestProvider;
        _configProvider = configProvider;
        _environment = environment;
    }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = null;

        var manifest = _manifestProvider.GetManifest();
        var config = _configProvider.GetConfig();

        if (manifest != null)
        {
            RenderManifestScripts(output, manifest.GlobalJs);
        }
        else
        {
            RenderDevScripts(output, config.Global.Js);
        }
    }

    private void RenderManifestScripts(TagHelperOutput output, List<string>? jsUrls)
    {
        if (jsUrls == null || !jsUrls.Any())
        {
            output.SuppressOutput();
            return;
        }

        var tags = jsUrls.Select(url => $"<script src=\"{HtmlEncoder.Default.Encode(url)}\"></script>");
        output.Content.SetHtmlContent(string.Join("\n", tags));
    }

    private void RenderDevScripts(TagHelperOutput output, List<string> jsFiles)
    {
        if (!jsFiles.Any())
        {
            output.SuppressOutput();
            return;
        }

        var contentRoot = _environment.ContentRootPath;
        var config = _configProvider.GetConfig();
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
            tags.Add($"<script type=\"module\" src=\"{HtmlEncoder.Default.Encode(url)}?v={version}\"></script>");
        }

        output.Content.SetHtmlContent(string.Join("\n", tags));
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
