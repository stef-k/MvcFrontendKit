using System.Text.Json;
using System.Text.Json.Serialization;

namespace MvcFrontendKit.Manifest;

public class FrontendManifest
{
    [JsonPropertyName("global:js")]
    public List<string>? GlobalJs { get; set; }

    [JsonPropertyName("global:css")]
    public List<string>? GlobalCss { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object>? AdditionalData { get; set; }

    public List<string>? GetViewJs(string viewKey)
    {
        var key = $"view:{viewKey}";
        if (AdditionalData?.TryGetValue(key, out var value) == true)
        {
            if (value is JsonElement element && element.ValueKind == JsonValueKind.Object)
            {
                if (element.TryGetProperty("js", out var jsElement) && jsElement.ValueKind == JsonValueKind.Array)
                {
                    return JsonSerializer.Deserialize<List<string>>(jsElement.GetRawText());
                }
            }
        }
        return null;
    }

    public List<string>? GetViewCss(string viewKey)
    {
        var key = $"view:{viewKey}";
        if (AdditionalData?.TryGetValue(key, out var value) == true)
        {
            if (value is JsonElement element && element.ValueKind == JsonValueKind.Object)
            {
                if (element.TryGetProperty("css", out var cssElement) && cssElement.ValueKind == JsonValueKind.Array)
                {
                    return JsonSerializer.Deserialize<List<string>>(cssElement.GetRawText());
                }
            }
        }
        return null;
    }

    public List<string>? GetAreaJs(string areaName)
    {
        var key = $"area:{areaName}";
        if (AdditionalData?.TryGetValue(key, out var value) == true)
        {
            if (value is JsonElement element && element.ValueKind == JsonValueKind.Object)
            {
                if (element.TryGetProperty("js", out var jsElement) && jsElement.ValueKind == JsonValueKind.Array)
                {
                    return JsonSerializer.Deserialize<List<string>>(jsElement.GetRawText());
                }
            }
        }
        return null;
    }

    public List<string>? GetComponentJs(string componentName)
    {
        var key = $"component:{componentName}:js";
        if (AdditionalData?.TryGetValue(key, out var value) == true)
        {
            if (value is JsonElement element && element.ValueKind == JsonValueKind.Array)
            {
                return JsonSerializer.Deserialize<List<string>>(element.GetRawText());
            }
        }
        return null;
    }

    public List<string>? GetComponentCss(string componentName)
    {
        var key = $"component:{componentName}:css";
        if (AdditionalData?.TryGetValue(key, out var value) == true)
        {
            if (value is JsonElement element && element.ValueKind == JsonValueKind.Array)
            {
                return JsonSerializer.Deserialize<List<string>>(element.GetRawText());
            }
        }
        return null;
    }
}
