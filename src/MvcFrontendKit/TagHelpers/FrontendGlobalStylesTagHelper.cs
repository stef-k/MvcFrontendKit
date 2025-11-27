using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Razor.TagHelpers;
using MvcFrontendKit.Services;
using System.Text.Encodings.Web;

namespace MvcFrontendKit.TagHelpers;

[HtmlTargetElement("frontend-global-styles")]
public class FrontendGlobalStylesTagHelper : TagHelper
{
    private readonly IFrontendManifestProvider _manifestProvider;
    private readonly IFrontendConfigProvider _configProvider;
    private readonly IWebHostEnvironment _environment;

    public FrontendGlobalStylesTagHelper(
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
            RenderManifestStyles(output, manifest.GlobalCss);
        }
        else
        {
            RenderDevStyles(output, config.Global.Css);
        }
    }

    private void RenderManifestStyles(TagHelperOutput output, List<string>? cssUrls)
    {
        if (cssUrls == null || !cssUrls.Any())
        {
            output.SuppressOutput();
            return;
        }

        var tags = cssUrls.Select(url => $"<link rel=\"stylesheet\" href=\"{HtmlEncoder.Default.Encode(url)}\" />");
        output.Content.SetHtmlContent(string.Join("\n", tags));
    }

    private void RenderDevStyles(TagHelperOutput output, List<string> cssFiles)
    {
        if (!cssFiles.Any())
        {
            output.SuppressOutput();
            return;
        }

        var contentRoot = _environment.ContentRootPath;
        var config = _configProvider.GetConfig();
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
            tags.Add($"<link rel=\"stylesheet\" href=\"{HtmlEncoder.Default.Encode(url)}?v={version}\" />");
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
