using Microsoft.AspNetCore.Mvc.Rendering;

namespace MvcFrontendKit.Utilities;

public static class ViewKeyResolver
{
    /// <summary>
    /// Resolves the logical view key from the current ViewContext.
    /// Format: "Views/Controller/Action" or "Areas/Area/Controller/Action"
    ///
    /// Resolution order:
    /// 1. Actual view path from ViewContext.View.Path (most accurate)
    /// 2. ViewContext.ExecutingFilePath (fallback for partials)
    /// 3. Route data (legacy fallback)
    /// </summary>
    public static string ResolveViewKey(ViewContext viewContext)
    {
        // Try to get the actual view path first (most accurate)
        var viewPath = viewContext.View?.Path;

        if (!string.IsNullOrEmpty(viewPath))
        {
            var key = ParseViewPath(viewPath);
            if (!string.IsNullOrEmpty(key))
            {
                return key;
            }
        }

        // Try ExecutingFilePath as fallback (useful for partials)
        var executingPath = viewContext.ExecutingFilePath;
        if (!string.IsNullOrEmpty(executingPath))
        {
            var key = ParseViewPath(executingPath);
            if (!string.IsNullOrEmpty(key))
            {
                return key;
            }
        }

        // Fall back to route data (legacy behavior for edge cases)
        return ResolveFromRouteData(viewContext);
    }

    /// <summary>
    /// Parses a view file path to extract the logical view key.
    /// Handles paths like:
    /// - /Views/Home/Index.cshtml -> Views/Home/Index
    /// - ~/Views/Home/Index.cshtml -> Views/Home/Index
    /// - /Areas/Admin/Views/Settings/Index.cshtml -> Areas/Admin/Settings/Index
    /// - ~/Areas/Admin/Views/Settings/Index.cshtml -> Areas/Admin/Settings/Index
    /// </summary>
    internal static string ParseViewPath(string viewPath)
    {
        if (string.IsNullOrEmpty(viewPath))
        {
            return string.Empty;
        }

        // Normalize path separators and remove leading ~/ or /
        var path = viewPath.Replace('\\', '/');
        if (path.StartsWith("~/"))
        {
            path = path.Substring(2);
        }
        else if (path.StartsWith("/"))
        {
            path = path.Substring(1);
        }

        // Remove .cshtml extension
        if (path.EndsWith(".cshtml", StringComparison.OrdinalIgnoreCase))
        {
            path = path.Substring(0, path.Length - 7);
        }

        // Handle Areas path: Areas/{Area}/Views/{Controller}/{Action}
        if (path.StartsWith("Areas/", StringComparison.OrdinalIgnoreCase))
        {
            var parts = path.Split('/');
            // Expected: Areas, {Area}, Views, {Controller}, {Action}
            if (parts.Length >= 5 && parts[2].Equals("Views", StringComparison.OrdinalIgnoreCase))
            {
                var area = parts[1];
                var controller = parts[3];
                var action = parts[4];
                return $"Areas/{area}/{controller}/{action}";
            }
            // Alternative: Areas/{Area}/{Controller}/{Action} (no Views subfolder)
            else if (parts.Length >= 4)
            {
                var area = parts[1];
                var controller = parts[2];
                var action = parts[3];
                return $"Areas/{area}/{controller}/{action}";
            }
        }

        // Handle standard Views path: Views/{Controller}/{Action}
        if (path.StartsWith("Views/", StringComparison.OrdinalIgnoreCase))
        {
            var parts = path.Split('/');
            // Expected: Views, {Controller}, {Action}
            if (parts.Length >= 3)
            {
                var controller = parts[1];
                var action = parts[2];
                return $"Views/{controller}/{action}";
            }
        }

        return string.Empty;
    }

    /// <summary>
    /// Legacy method: resolves view key from route data.
    /// Used as fallback when view path is not available.
    /// </summary>
    private static string ResolveFromRouteData(ViewContext viewContext)
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
    /// Uses view path first, falls back to route data.
    /// </summary>
    public static string ResolveAreaName(ViewContext viewContext)
    {
        // Try to extract from view path first
        var viewPath = viewContext.View?.Path ?? viewContext.ExecutingFilePath;
        if (!string.IsNullOrEmpty(viewPath))
        {
            var path = viewPath.Replace('\\', '/');
            if (path.StartsWith("~/"))
            {
                path = path.Substring(2);
            }
            else if (path.StartsWith("/"))
            {
                path = path.Substring(1);
            }

            if (path.StartsWith("Areas/", StringComparison.OrdinalIgnoreCase))
            {
                var parts = path.Split('/');
                if (parts.Length >= 2)
                {
                    return parts[1];
                }
            }
        }

        // Fall back to route data
        var routeData = viewContext.RouteData.Values;
        var area = routeData.TryGetValue("area", out var areaValue) ? areaValue?.ToString() : null;

        return string.IsNullOrEmpty(area) ? "Root" : area;
    }
}
