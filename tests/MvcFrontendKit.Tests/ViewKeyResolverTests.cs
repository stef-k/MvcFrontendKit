using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Routing;
using Moq;
using MvcFrontendKit.Utilities;

namespace MvcFrontendKit.Tests;

public class ViewKeyResolverTests
{
    [Fact]
    public void ResolveViewKey_ReturnsViewsControllerAction_ForNonAreaRoute()
    {
        // Arrange
        var viewContext = CreateViewContext(controller: "Home", action: "Index", area: null);

        // Act
        var result = ViewKeyResolver.ResolveViewKey(viewContext);

        // Assert
        Assert.Equal("Views/Home/Index", result);
    }

    [Fact]
    public void ResolveViewKey_ReturnsAreasFormat_ForAreaRoute()
    {
        // Arrange
        var viewContext = CreateViewContext(controller: "Settings", action: "Index", area: "Admin");

        // Act
        var result = ViewKeyResolver.ResolveViewKey(viewContext);

        // Assert
        Assert.Equal("Areas/Admin/Settings/Index", result);
    }

    [Fact]
    public void ResolveViewKey_ReturnsEmpty_WhenControllerMissing()
    {
        // Arrange
        var viewContext = CreateViewContext(controller: null, action: "Index", area: null);

        // Act
        var result = ViewKeyResolver.ResolveViewKey(viewContext);

        // Assert
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void ResolveViewKey_ReturnsEmpty_WhenActionMissing()
    {
        // Arrange
        var viewContext = CreateViewContext(controller: "Home", action: null, area: null);

        // Act
        var result = ViewKeyResolver.ResolveViewKey(viewContext);

        // Assert
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void ResolveViewKey_HandlesNestedArea()
    {
        // Arrange
        var viewContext = CreateViewContext(controller: "Users", action: "Edit", area: "Admin");

        // Act
        var result = ViewKeyResolver.ResolveViewKey(viewContext);

        // Assert
        Assert.Equal("Areas/Admin/Users/Edit", result);
    }

    [Fact]
    public void ResolveAreaName_ReturnsRoot_ForNonAreaRoute()
    {
        // Arrange
        var viewContext = CreateViewContext(controller: "Home", action: "Index", area: null);

        // Act
        var result = ViewKeyResolver.ResolveAreaName(viewContext);

        // Assert
        Assert.Equal("Root", result);
    }

    [Fact]
    public void ResolveAreaName_ReturnsAreaName_ForAreaRoute()
    {
        // Arrange
        var viewContext = CreateViewContext(controller: "Settings", action: "Index", area: "Admin");

        // Act
        var result = ViewKeyResolver.ResolveAreaName(viewContext);

        // Assert
        Assert.Equal("Admin", result);
    }

    private static ViewContext CreateViewContext(string? controller, string? action, string? area)
    {
        var routeData = new RouteData();
        if (controller != null)
            routeData.Values["controller"] = controller;
        if (action != null)
            routeData.Values["action"] = action;
        if (area != null)
            routeData.Values["area"] = area;

        var httpContext = new DefaultHttpContext();
        var actionContext = new ActionContext(httpContext, routeData, new ActionDescriptor());

        var viewContext = new ViewContext
        {
            RouteData = routeData
        };

        return viewContext;
    }
}
