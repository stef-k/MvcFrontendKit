using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using MvcFrontendKit.Bundling;
using MvcFrontendKit.Configuration;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using MSBuildTask = Microsoft.Build.Utilities.Task;
using ExtensionsLogger = Microsoft.Extensions.Logging.ILogger;
using ExtensionsLogLevel = Microsoft.Extensions.Logging.LogLevel;
using ExtensionsEventId = Microsoft.Extensions.Logging.EventId;

namespace MvcFrontendKit.Build;

public class FrontendBundleTask : MSBuildTask
{
    [Required]
    public string? ProjectDirectory { get; set; }

    [Required]
    public string? ConfigPath { get; set; }

    public override bool Execute()
    {
        try
        {
            Log.LogMessage(MessageImportance.High, "MvcFrontendKit: Starting frontend bundling...");
            Log.LogMessage(MessageImportance.Normal, "  Project: {0}", ProjectDirectory);
            Log.LogMessage(MessageImportance.Normal, "  Config: {0}", ConfigPath);

            if (string.IsNullOrEmpty(ProjectDirectory) || string.IsNullOrEmpty(ConfigPath))
            {
                Log.LogError("ProjectDirectory and ConfigPath are required");
                return false;
            }

            if (!File.Exists(ConfigPath))
            {
                Log.LogError("Config file not found at: {0}", ConfigPath);
                Log.LogError("Run 'dotnet frontend init' to create it, or set MvcFrontendKitEnabled=false to disable bundling.");
                return false;
            }

            var config = LoadConfig(ConfigPath);
            if (config == null)
            {
                return false;
            }

            var logger = new MSBuildLogger(Log);
            var orchestrator = new BundleOrchestrator(config, ProjectDirectory, logger);

            var success = orchestrator.BuildBundlesAsync().GetAwaiter().GetResult();

            if (success)
            {
                Log.LogMessage(MessageImportance.High, "MvcFrontendKit: Bundling completed successfully");
            }
            else
            {
                Log.LogError("MvcFrontendKit: Bundling failed");
            }

            return success;
        }
        catch (Exception ex)
        {
            Log.LogErrorFromException(ex, showStackTrace: true);
            return false;
        }
    }

    private FrontendConfig? LoadConfig(string configPath)
    {
        try
        {
            var yaml = File.ReadAllText(configPath);
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();

            var config = deserializer.Deserialize<FrontendConfig>(yaml);
            Log.LogMessage(MessageImportance.Normal, "  Mode: {0}", config.Mode);
            Log.LogMessage(MessageImportance.Normal, "  Web Root: {0}", config.WebRoot);

            return config;
        }
        catch (Exception ex)
        {
            Log.LogError("Failed to parse config file: {0}", ex.Message);
            return null;
        }
    }

    private class MSBuildLogger : ExtensionsLogger
    {
        private readonly TaskLoggingHelper _log;

        public MSBuildLogger(TaskLoggingHelper log)
        {
            _log = log;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(ExtensionsLogLevel logLevel) => true;

        public void Log<TState>(
            ExtensionsLogLevel logLevel,
            ExtensionsEventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var message = formatter(state, exception);

            switch (logLevel)
            {
                case ExtensionsLogLevel.Critical:
                case ExtensionsLogLevel.Error:
                    _log.LogError(message);
                    break;
                case ExtensionsLogLevel.Warning:
                    _log.LogWarning(message);
                    break;
                case ExtensionsLogLevel.Information:
                    _log.LogMessage(MessageImportance.Normal, message);
                    break;
                case ExtensionsLogLevel.Debug:
                case ExtensionsLogLevel.Trace:
                    _log.LogMessage(MessageImportance.Low, message);
                    break;
            }
        }
    }
}
