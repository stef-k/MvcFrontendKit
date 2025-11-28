using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Routing;
using Moq;
using MvcFrontendKit.Utilities;

namespace MvcFrontendKit.Tests;

public class ViewKeyResolverTests
{
    #region ParseViewPath Tests (Core Path Parsing Logic)

    [Theory]
    [InlineData("/Views/Home/Index.cshtml", "Views/Home/Index")]
    [InlineData("~/Views/Home/Index.cshtml", "Views/Home/Index")]
    [InlineData("Views/Home/Index.cshtml", "Views/Home/Index")]
    [InlineData("/Views/Trip/Viewer.cshtml", "Views/Trip/Viewer")]
    public void ParseViewPath_ParsesStandardViewPaths(string input, string expected)
    {
        var result = ViewKeyResolver.ParseViewPath(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("/Areas/Admin/Views/Settings/Index.cshtml", "Areas/Admin/Settings/Index")]
    [InlineData("~/Areas/Admin/Views/Settings/Index.cshtml", "Areas/Admin/Settings/Index")]
    [InlineData("Areas/Admin/Views/Settings/Index.cshtml", "Areas/Admin/Settings/Index")]
    [InlineData("/Areas/Public/Views/Trips/Viewer.cshtml", "Areas/Public/Trips/Viewer")]
    public void ParseViewPath_ParsesAreaViewPaths(string input, string expected)
    {
        var result = ViewKeyResolver.ParseViewPath(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("\\Views\\Home\\Index.cshtml", "Views/Home/Index")]
    [InlineData("\\Areas\\Admin\\Views\\Settings\\Index.cshtml", "Areas/Admin/Settings/Index")]
    public void ParseViewPath_HandlesBackslashPaths(string input, string expected)
    {
        var result = ViewKeyResolver.ParseViewPath(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("/Shared/_Layout.cshtml", "")]
    [InlineData("/NotViews/Something.cshtml", "")]
    public void ParseViewPath_ReturnsEmptyForInvalidPaths(string? input, string expected)
    {
        var result = ViewKeyResolver.ParseViewPath(input!);
        Assert.Equal(expected, result);
    }

    #endregion

    #region ResolveViewKey with View Path (Primary Resolution)

    [Fact]
    public void ResolveViewKey_UsesViewPath_WhenAvailable()
    {
        // Arrange - View path takes precedence over route data
        var viewContext = CreateViewContextWithViewPath(
            viewPath: "/Views/Trip/Viewer.cshtml",
            controller: "Home",  // Different from view path
            action: "Index",     // Different from view path
            area: null);

        // Act
        var result = ViewKeyResolver.ResolveViewKey(viewContext);

        // Assert - Should use view path, not route data
        Assert.Equal("Views/Trip/Viewer", result);
    }

    [Fact]
    public void ResolveViewKey_UsesAreaFromViewPath_EvenWithoutAreaRouteData()
    {
        // This is the key scenario: Controller uses [Route] without [Area]
        // but is physically in Areas/Public folder
        var viewContext = CreateViewContextWithViewPath(
            viewPath: "/Areas/Public/Views/Trips/Viewer.cshtml",
            controller: "Trips",
            action: "Viewer",
            area: null);  // No area in route data!

        // Act
        var result = ViewKeyResolver.ResolveViewKey(viewContext);

        // Assert - Should detect area from view path
        Assert.Equal("Areas/Public/Trips/Viewer", result);
    }

    [Fact]
    public void ResolveViewKey_HandlesExplicitViewReturn()
    {
        // Scenario: return View("~/Views/Trip/Viewer.cshtml")
        var viewContext = CreateViewContextWithViewPath(
            viewPath: "~/Views/Trip/Viewer.cshtml",
            controller: "Different",
            action: "Action",
            area: null);

        // Act
        var result = ViewKeyResolver.ResolveViewKey(viewContext);

        // Assert
        Assert.Equal("Views/Trip/Viewer", result);
    }

    #endregion

    #region ResolveViewKey with Route Data (Fallback)

    [Fact]
    public void ResolveViewKey_FallsBackToRouteData_WhenViewPathNotAvailable()
    {
        // Arrange - No view path, only route data
        var viewContext = CreateViewContext(controller: "Home", action: "Index", area: null);

        // Act
        var result = ViewKeyResolver.ResolveViewKey(viewContext);

        // Assert
        Assert.Equal("Views/Home/Index", result);
    }

    [Fact]
    public void ResolveViewKey_ReturnsAreasFormat_ForAreaRouteData()
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

    #endregion

    #region ResolveAreaName Tests

    [Fact]
    public void ResolveAreaName_ExtractsAreaFromViewPath()
    {
        // Arrange
        var viewContext = CreateViewContextWithViewPath(
            viewPath: "/Areas/Admin/Views/Settings/Index.cshtml",
            controller: "Settings",
            action: "Index",
            area: null);  // No area in route data

        // Act
        var result = ViewKeyResolver.ResolveAreaName(viewContext);

        // Assert
        Assert.Equal("Admin", result);
    }

    [Fact]
    public void ResolveAreaName_ReturnsRoot_ForNonAreaView()
    {
        // Arrange
        var viewContext = CreateViewContextWithViewPath(
            viewPath: "/Views/Home/Index.cshtml",
            controller: "Home",
            action: "Index",
            area: null);

        // Act
        var result = ViewKeyResolver.ResolveAreaName(viewContext);

        // Assert
        Assert.Equal("Root", result);
    }

    [Fact]
    public void ResolveAreaName_FallsBackToRouteData_WhenViewPathNotAvailable()
    {
        // Arrange
        var viewContext = CreateViewContext(controller: "Settings", action: "Index", area: "Admin");

        // Act
        var result = ViewKeyResolver.ResolveAreaName(viewContext);

        // Assert
        Assert.Equal("Admin", result);
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

    #endregion

    #region Helper Methods

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

    private static ViewContext CreateViewContextWithViewPath(
        string viewPath,
        string? controller,
        string? action,
        string? area)
    {
        var routeData = new RouteData();
        if (controller != null)
            routeData.Values["controller"] = controller;
        if (action != null)
            routeData.Values["action"] = action;
        if (area != null)
            routeData.Values["area"] = area;

        var httpContext = new DefaultHttpContext();

        // Mock the IView with the specified path
        var mockView = new Mock<IView>();
        mockView.Setup(v => v.Path).Returns(viewPath);

        var viewContext = new ViewContext
        {
            RouteData = routeData,
            View = mockView.Object
        };

        return viewContext;
    }

    #endregion
}
