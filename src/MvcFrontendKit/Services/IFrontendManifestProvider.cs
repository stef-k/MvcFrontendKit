using MvcFrontendKit.Manifest;

namespace MvcFrontendKit.Services;

public interface IFrontendManifestProvider
{
    FrontendManifest? GetManifest();
    bool IsProduction();
}
