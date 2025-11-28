using Microsoft.AspNetCore.Http;
using Moq;
using MvcFrontendKit.Services;

namespace MvcFrontendKit.Tests;

public class ComponentRegistryTests
{
    [Fact]
    public void TryRegister_FirstCall_ReturnsTrue()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        var contextAccessor = new Mock<IHttpContextAccessor>();
        contextAccessor.Setup(a => a.HttpContext).Returns(httpContext);
        var registry = new FrontendComponentRegistry(contextAccessor.Object);

        // Act
        var result = registry.TryRegister("datepicker");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void TryRegister_DuplicateCall_ReturnsFalse()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        var contextAccessor = new Mock<IHttpContextAccessor>();
        contextAccessor.Setup(a => a.HttpContext).Returns(httpContext);
        var registry = new FrontendComponentRegistry(contextAccessor.Object);

        // Act
        var firstCall = registry.TryRegister("datepicker");
        var secondCall = registry.TryRegister("datepicker");

        // Assert
        Assert.True(firstCall);
        Assert.False(secondCall);
    }

    [Fact]
    public void TryRegister_DifferentComponents_BothReturnTrue()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        var contextAccessor = new Mock<IHttpContextAccessor>();
        contextAccessor.Setup(a => a.HttpContext).Returns(httpContext);
        var registry = new FrontendComponentRegistry(contextAccessor.Object);

        // Act
        var datepicker = registry.TryRegister("datepicker");
        var calendar = registry.TryRegister("calendar");

        // Assert
        Assert.True(datepicker);
        Assert.True(calendar);
    }

    [Fact]
    public void IsRegistered_WhenRegistered_ReturnsTrue()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        var contextAccessor = new Mock<IHttpContextAccessor>();
        contextAccessor.Setup(a => a.HttpContext).Returns(httpContext);
        var registry = new FrontendComponentRegistry(contextAccessor.Object);

        // Act
        registry.TryRegister("datepicker");
        var result = registry.IsRegistered("datepicker");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsRegistered_WhenNotRegistered_ReturnsFalse()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        var contextAccessor = new Mock<IHttpContextAccessor>();
        contextAccessor.Setup(a => a.HttpContext).Returns(httpContext);
        var registry = new FrontendComponentRegistry(contextAccessor.Object);

        // Act
        var result = registry.IsRegistered("datepicker");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void MultipleComponents_CanBeRegistered()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        var contextAccessor = new Mock<IHttpContextAccessor>();
        contextAccessor.Setup(a => a.HttpContext).Returns(httpContext);
        var registry = new FrontendComponentRegistry(contextAccessor.Object);

        // Act
        var datepicker = registry.TryRegister("datepicker");
        var calendar = registry.TryRegister("calendar");
        var chart = registry.TryRegister("chart");

        // Assert
        Assert.True(datepicker);
        Assert.True(calendar);
        Assert.True(chart);
        Assert.True(registry.IsRegistered("datepicker"));
        Assert.True(registry.IsRegistered("calendar"));
        Assert.True(registry.IsRegistered("chart"));
    }

    [Fact]
    public void Registry_IsolatedPerRequest()
    {
        // Arrange
        var httpContext1 = new DefaultHttpContext();
        var httpContext2 = new DefaultHttpContext();

        var contextAccessor1 = new Mock<IHttpContextAccessor>();
        contextAccessor1.Setup(a => a.HttpContext).Returns(httpContext1);

        var contextAccessor2 = new Mock<IHttpContextAccessor>();
        contextAccessor2.Setup(a => a.HttpContext).Returns(httpContext2);

        var registry1 = new FrontendComponentRegistry(contextAccessor1.Object);
        var registry2 = new FrontendComponentRegistry(contextAccessor2.Object);

        // Act
        registry1.TryRegister("datepicker");
        var registered1 = registry1.IsRegistered("datepicker");
        var registered2 = registry2.IsRegistered("datepicker");

        // Assert
        Assert.True(registered1);
        Assert.False(registered2); // Different request context
    }

    [Fact]
    public void TryRegister_NullHttpContext_ThrowsInvalidOperationException()
    {
        // Arrange
        var contextAccessor = new Mock<IHttpContextAccessor>();
        contextAccessor.Setup(a => a.HttpContext).Returns((HttpContext?)null);
        var registry = new FrontendComponentRegistry(contextAccessor.Object);

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
            registry.TryRegister("datepicker"));

        Assert.Contains("HttpContext", ex.Message);
    }

    [Fact]
    public void TryRegister_IsCaseInsensitive()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        var contextAccessor = new Mock<IHttpContextAccessor>();
        contextAccessor.Setup(a => a.HttpContext).Returns(httpContext);
        var registry = new FrontendComponentRegistry(contextAccessor.Object);

        // Act
        var lower = registry.TryRegister("datepicker");
        var upper = registry.TryRegister("Datepicker");

        // Assert
        Assert.True(lower);
        Assert.False(upper); // Same component name (case-insensitive)
    }
}
