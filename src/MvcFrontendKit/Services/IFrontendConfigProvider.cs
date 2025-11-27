using MvcFrontendKit.Configuration;

namespace MvcFrontendKit.Services;

public interface IFrontendConfigProvider
{
    FrontendConfig GetConfig();
    string GetConfigFilePath();
}
