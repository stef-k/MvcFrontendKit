namespace MvcFrontendKit.Services;

public interface IFrontendComponentRegistry
{
    /// <summary>
    /// Attempts to register a component for the current request.
    /// Returns true if the component was newly registered, false if it was already registered.
    /// </summary>
    bool TryRegister(string componentKey);

    /// <summary>
    /// Checks if a component has already been registered for the current request.
    /// </summary>
    bool IsRegistered(string componentKey);
}
