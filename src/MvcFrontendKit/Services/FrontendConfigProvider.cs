using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MvcFrontendKit.Configuration;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace MvcFrontendKit.Services;

public class FrontendConfigProvider : IFrontendConfigProvider, IDisposable
{
    private readonly IHostEnvironment _environment;
    private readonly ILogger<FrontendConfigProvider> _logger;
    private FrontendConfig? _cachedConfig;
    private FileSystemWatcher? _watcher;
    private readonly object _lock = new();
    private readonly string _configFileName = "frontend.config.yaml";

    public FrontendConfigProvider(
        IHostEnvironment environment,
        ILogger<FrontendConfigProvider> logger)
    {
        _environment = environment;
        _logger = logger;

        if (_environment.IsDevelopment())
        {
            SetupFileWatcher();
        }
    }

    public FrontendConfig GetConfig()
    {
        lock (_lock)
        {
            if (_cachedConfig == null)
            {
                _cachedConfig = LoadConfig();
            }
            return _cachedConfig;
        }
    }

    public string GetConfigFilePath()
    {
        return Path.Combine(_environment.ContentRootPath, _configFileName);
    }

    private FrontendConfig LoadConfig()
    {
        var configPath = GetConfigFilePath();

        if (!File.Exists(configPath))
        {
            _logger.LogWarning(
                "Frontend config file not found at {ConfigPath}. Using default configuration. " +
                "Run 'dotnet frontend init' to generate a config file.",
                configPath);
            return new FrontendConfig();
        }

        try
        {
            var yaml = File.ReadAllText(configPath);
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();

            var config = deserializer.Deserialize<FrontendConfig>(yaml);
            _logger.LogInformation("Loaded frontend config from {ConfigPath}", configPath);
            return config ?? new FrontendConfig();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse frontend config at {ConfigPath}", configPath);

            if (!_environment.IsDevelopment())
            {
                throw new InvalidOperationException(
                    $"Failed to parse frontend.config.yaml: {ex.Message}", ex);
            }

            _logger.LogWarning("Using previously loaded config or default config in Development");
            return _cachedConfig ?? new FrontendConfig();
        }
    }

    private void SetupFileWatcher()
    {
        var configDir = _environment.ContentRootPath;

        try
        {
            _watcher = new FileSystemWatcher(configDir, _configFileName)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
                EnableRaisingEvents = true
            };

            _watcher.Changed += OnConfigFileChanged;
            _watcher.Created += OnConfigFileChanged;
            _watcher.Renamed += OnConfigFileRenamed;

            _logger.LogDebug("File watcher set up for {ConfigFile}", _configFileName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not set up file watcher for config");
        }
    }

    private void OnConfigFileChanged(object sender, FileSystemEventArgs e)
    {
        Task.Delay(100).ContinueWith(_ =>
        {
            lock (_lock)
            {
                _logger.LogInformation("Frontend config file changed, reloading...");
                _cachedConfig = null;
            }
        });
    }

    private void OnConfigFileRenamed(object sender, RenamedEventArgs e)
    {
        if (e.Name == _configFileName)
        {
            OnConfigFileChanged(sender, e);
        }
    }

    public void Dispose()
    {
        _watcher?.Dispose();
    }
}
