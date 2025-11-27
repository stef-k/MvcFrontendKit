using Microsoft.AspNetCore.Hosting;
using MvcFrontendKit.Configuration;

namespace MvcFrontendKit.Utilities;

public class AssetResolver
{
    private readonly IWebHostEnvironment _environment;
    private readonly FrontendConfig _config;

    public AssetResolver(IWebHostEnvironment environment, FrontendConfig config)
    {
        _environment = environment;
        _config = config;
    }

    /// <summary>
    /// Resolves JS files for a view based on conventions or overrides.
    /// Returns the list of JS file paths relative to the project root.
    /// </summary>
    public List<string> ResolveViewJs(string viewKey)
    {
        if (_config.Views.Overrides.TryGetValue(viewKey, out var viewOverride))
        {
            return viewOverride.Js;
        }

        if (!_config.Views.JsAutoLinkByConvention)
        {
            return new List<string>();
        }

        return FindJsByConvention(viewKey);
    }

    /// <summary>
    /// Resolves CSS files for a view based on conventions or overrides.
    /// Returns the list of CSS file paths relative to the project root.
    /// </summary>
    public List<string> ResolveViewCss(string viewKey)
    {
        if (_config.Views.Overrides.TryGetValue(viewKey, out var viewOverride))
        {
            return viewOverride.Css;
        }

        if (!_config.Views.CssAutoLinkByConvention)
        {
            return new List<string>();
        }

        return FindCssByConvention(viewKey);
    }

    private List<string> FindJsByConvention(string viewKey)
    {
        var contentRoot = _environment.ContentRootPath;

        foreach (var convention in _config.Views.Conventions)
        {
            if (TryMatchConvention(viewKey, convention.ViewPattern, out var tokens))
            {
                var scriptBasePath = ApplyTokens(convention.ScriptBasePattern, tokens);
                var candidates = GetJsCandidates(scriptBasePath);

                foreach (var candidate in candidates)
                {
                    var fullPath = Path.Combine(contentRoot, candidate);
                    if (File.Exists(fullPath))
                    {
                        return new List<string> { candidate };
                    }
                }
            }
        }

        return new List<string>();
    }

    private List<string> FindCssByConvention(string viewKey)
    {
        var contentRoot = _environment.ContentRootPath;

        foreach (var cssConvention in _config.Views.CssConventions)
        {
            if (TryMatchConvention(viewKey, cssConvention.ViewPattern, out var tokens))
            {
                var cssPath = ApplyTokens(cssConvention.CssPattern, tokens);
                var fullPath = Path.Combine(contentRoot, cssPath);

                if (File.Exists(fullPath))
                {
                    return new List<string> { cssPath };
                }
            }
        }

        return new List<string>();
    }

    private bool TryMatchConvention(string viewKey, string pattern, out Dictionary<string, string> tokens)
    {
        tokens = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var patternParts = pattern.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var keyParts = viewKey.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (patternParts.Length != keyParts.Length)
        {
            return false;
        }

        for (int i = 0; i < patternParts.Length; i++)
        {
            var patternPart = patternParts[i];
            var keyPart = keyParts[i];

            if (patternPart.StartsWith("{") && patternPart.EndsWith("}"))
            {
                var tokenName = patternPart.Trim('{', '}', '.');
                tokens[tokenName] = keyPart.Replace(".cshtml", "");
            }
            else if (!patternPart.Equals(keyPart, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private string ApplyTokens(string pattern, Dictionary<string, string> tokens)
    {
        var result = pattern;

        foreach (var (key, value) in tokens)
        {
            result = result.Replace($"{{{key}}}", value, StringComparison.OrdinalIgnoreCase);
        }

        return result;
    }

    private List<string> GetJsCandidates(string basePath)
    {
        var action = Path.GetFileName(basePath);
        var directory = Path.GetDirectoryName(basePath) ?? basePath;

        var camelCase = ToCamelCase(action);
        var lowercase = action.ToLowerInvariant();
        var pascalCase = action;

        return new List<string>
        {
            Path.Combine(directory, $"{camelCase}.js"),
            Path.Combine(directory, $"{lowercase}.js"),
            Path.Combine(directory, $"{pascalCase}.js"),
            Path.Combine(directory, $"{camelCase}Page.js"),
            Path.Combine(directory, $"{lowercase}Page.js"),
        };
    }

    private string ToCamelCase(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        return char.ToLowerInvariant(input[0]) + input.Substring(1);
    }
}
