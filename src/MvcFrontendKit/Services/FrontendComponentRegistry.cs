using Microsoft.AspNetCore.Http;

namespace MvcFrontendKit.Services;

public class FrontendComponentRegistry : IFrontendComponentRegistry
{
    private const string HttpContextKey = "MvcFrontendKit_RegisteredComponents";
    private readonly IHttpContextAccessor _httpContextAccessor;

    public FrontendComponentRegistry(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public bool TryRegister(string componentKey)
    {
        var components = GetOrCreateComponentSet();

        if (components.Contains(componentKey))
        {
            return false;
        }

        components.Add(componentKey);
        return true;
    }

    public bool IsRegistered(string componentKey)
    {
        var components = GetOrCreateComponentSet();
        return components.Contains(componentKey);
    }

    private HashSet<string> GetOrCreateComponentSet()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
        {
            throw new InvalidOperationException(
                "HttpContext is not available. Frontend components can only be used within an HTTP request.");
        }

        if (httpContext.Items[HttpContextKey] is not HashSet<string> components)
        {
            components = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            httpContext.Items[HttpContextKey] = components;
        }

        return components;
    }
}
