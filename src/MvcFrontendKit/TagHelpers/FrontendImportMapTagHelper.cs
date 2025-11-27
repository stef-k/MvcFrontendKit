using Microsoft.AspNetCore.Razor.TagHelpers;
using MvcFrontendKit.Services;
using System.Text.Encodings.Web;

namespace MvcFrontendKit.TagHelpers;

[HtmlTargetElement("frontend-import-map")]
public class FrontendImportMapTagHelper : TagHelper
{
    private readonly IFrontendManifestProvider _manifestProvider;
    private readonly IFrontendConfigProvider _configProvider;

    public FrontendImportMapTagHelper(
        IFrontendManifestProvider manifestProvider,
        IFrontendConfigProvider configProvider)
    {
        _manifestProvider = manifestProvider;
        _configProvider = configProvider;
    }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = null;

        if (_manifestProvider.IsProduction())
        {
            output.SuppressOutput();
            return;
        }

        var config = _configProvider.GetConfig();

        if (!config.ImportMap.Enabled || !config.ImportMap.Entries.Any())
        {
            output.SuppressOutput();
            return;
        }

        var imports = string.Join(",\n      ",
            config.ImportMap.Entries.Select(kvp => $"\"{kvp.Key}\": \"{kvp.Value}\""));

        var importMapJson = $@"<script type=""importmap"">
  {{
    ""imports"": {{
      {imports}
    }}
  }}
</script>";

        output.Content.SetHtmlContent(importMapJson);
    }
}
