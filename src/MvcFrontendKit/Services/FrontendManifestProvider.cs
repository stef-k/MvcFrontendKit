using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using MvcFrontendKit.Manifest;

namespace MvcFrontendKit.Services;

public class FrontendManifestProvider : IFrontendManifestProvider, IDisposable
{
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<FrontendManifestProvider> _logger;
    private FrontendManifest? _cachedManifest;
    private FileSystemWatcher? _watcher;
    private readonly object _lock = new();
    private readonly string _manifestFileName = "frontend.manifest.json";

    public FrontendManifestProvider(
        IWebHostEnvironment environment,
        ILogger<FrontendManifestProvider> logger)
    {
        _environment = environment;
        _logger = logger;

        if (IsProduction())
        {
            _cachedManifest = LoadManifest();

            if (_cachedManifest == null)
            {
                throw new InvalidOperationException(
                    $"Frontend manifest file not found at {GetManifestFilePath()}. " +
                    "Make sure the application was built/published in Release configuration.");
            }

            SetupFileWatcher();
        }
    }

    public FrontendManifest? GetManifest()
    {
        if (!IsProduction())
        {
            return null;
        }

        lock (_lock)
        {
            return _cachedManifest;
        }
    }

    public bool IsProduction()
    {
        return _environment.EnvironmentName != "Development";
    }

    private string GetManifestFilePath()
    {
        var webRoot = _environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot");
        return Path.Combine(webRoot, _manifestFileName);
    }

    private FrontendManifest? LoadManifest()
    {
        var manifestPath = GetManifestFilePath();

        if (!File.Exists(manifestPath))
        {
            _logger.LogError("Manifest file not found at {ManifestPath}", manifestPath);
            return null;
        }

        try
        {
            var json = File.ReadAllText(manifestPath);
            var manifest = JsonSerializer.Deserialize<FrontendManifest>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = false,
                ReadCommentHandling = JsonCommentHandling.Skip
            });

            _logger.LogInformation("Loaded frontend manifest from {ManifestPath}", manifestPath);
            return manifest;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse manifest file at {ManifestPath}", manifestPath);
            return null;
        }
    }

    private void SetupFileWatcher()
    {
        var webRoot = _environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot");

        if (!Directory.Exists(webRoot))
        {
            return;
        }

        try
        {
            _watcher = new FileSystemWatcher(webRoot, _manifestFileName)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
                EnableRaisingEvents = true
            };

            _watcher.Changed += OnManifestFileChanged;
            _watcher.Created += OnManifestFileChanged;

            _logger.LogDebug("File watcher set up for {ManifestFile}", _manifestFileName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not set up file watcher for manifest");
        }
    }

    private void OnManifestFileChanged(object sender, FileSystemEventArgs e)
    {
        Task.Delay(100).ContinueWith(_ =>
        {
            lock (_lock)
            {
                _logger.LogInformation("Manifest file changed, reloading...");
                _cachedManifest = LoadManifest();
            }
        });
    }

    public void Dispose()
    {
        _watcher?.Dispose();
    }
}
