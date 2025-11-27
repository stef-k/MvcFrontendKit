using Microsoft.AspNetCore.Mvc.Rendering;

namespace MvcFrontendKit.Utilities;

public static class ViewKeyResolver
{
    /// <summary>
    /// Resolves the logical view key from the current ViewContext.
    /// Format: "Views/Controller/Action" or "Areas/Area/Controller/Action"
    /// </summary>
    public static string ResolveViewKey(ViewContext viewContext)
    {
        var routeData = viewContext.RouteData.Values;

        var area = routeData.TryGetValue("area", out var areaValue) ? areaValue?.ToString() : null;
        var controller = routeData.TryGetValue("controller", out var controllerValue) ? controllerValue?.ToString() : null;
        var action = routeData.TryGetValue("action", out var actionValue) ? actionValue?.ToString() : null;

        if (string.IsNullOrEmpty(controller) || string.IsNullOrEmpty(action))
        {
            return string.Empty;
        }

        if (!string.IsNullOrEmpty(area))
        {
            return $"Areas/{area}/{controller}/{action}";
        }

        return $"Views/{controller}/{action}";
    }

    /// <summary>
    /// Resolves the area name from the current ViewContext, or "Root" if no area.
    /// </summary>
    public static string ResolveAreaName(ViewContext viewContext)
    {
        var routeData = viewContext.RouteData.Values;
        var area = routeData.TryGetValue("area", out var areaValue) ? areaValue?.ToString() : null;

        return string.IsNullOrEmpty(area) ? "Root" : area;
    }
}
