using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MvcFrontendKit.Build.Bundling;

/// <summary>
/// Runs the Dart Sass compiler to convert SCSS/Sass files to CSS.
/// Similar to EsbuildRunner, this invokes the sass binary as a subprocess.
/// </summary>
public class SassRunner
{
    private readonly ILogger _logger;

    public SassRunner(ILogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Compiles an SCSS/Sass file to CSS.
    /// </summary>
    /// <param name="inputPath">Absolute path to the .scss or .sass file</param>
    /// <param name="outputPath">Absolute path for the output .css file</param>
    /// <param name="options">Compilation options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result of the compilation</returns>
    public async Task<SassResult> CompileAsync(
        string inputPath,
        string outputPath,
        SassOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new SassOptions();

        var sassPath = GetSassPath();

        if (sassPath == null)
        {
            return new SassResult
            {
                Success = false,
                Error = "Sass binary not found. SCSS compilation is not available."
            };
        }

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            MakeExecutable(sassPath);
        }

        var arguments = BuildArguments(inputPath, outputPath, options);
        _logger.LogInformation("Running sass: {Path}", sassPath);
        _logger.LogInformation("  Arguments: {Arguments}", arguments);

        var startInfo = new ProcessStartInfo
        {
            FileName = sassPath,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(inputPath) ?? Directory.GetCurrentDirectory()
        };

        using var process = new Process { StartInfo = startInfo };
        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                outputBuilder.AppendLine(e.Data);
            }
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                errorBuilder.AppendLine(e.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await Task.Run(() => process.WaitForExit(), cancellationToken);

        var output = outputBuilder.ToString();
        var error = errorBuilder.ToString();

        if (process.ExitCode != 0)
        {
            _logger.LogError("Sass compilation failed with exit code {ExitCode}. Error: {Error}", process.ExitCode, error);
        }
        else
        {
            _logger.LogInformation("Sass compilation completed successfully: {Output}", inputPath);
        }

        return new SassResult
        {
            Success = process.ExitCode == 0,
            ExitCode = process.ExitCode,
            Output = output,
            Error = error,
            OutputPath = outputPath
        };
    }

    /// <summary>
    /// Checks if the Sass compiler is available.
    /// </summary>
    public bool IsAvailable()
    {
        return GetSassPath() != null;
    }

    private string? GetSassPath()
    {
        var rid = GetRuntimeIdentifier();
        var assemblyLocation = Assembly.GetExecutingAssembly().Location;
        var assemblyDir = Path.GetDirectoryName(assemblyLocation);

        if (string.IsNullOrEmpty(assemblyDir))
        {
            return null;
        }

        var fileName = rid.StartsWith("win") ? "sass.bat" : "sass";

        // Try multiple locations:
        // 1. Direct runtimes folder (development/local builds)
        // 2. NuGet package structure: tasks/net10.0/ -> ../../runtimes/ (package root)
        // 3. NuGet package structure: tasks/net472/ -> ../../runtimes/

        var searchPaths = new List<string>
        {
            // Direct (development)
            Path.Combine(assemblyDir, "runtimes", rid, "native", fileName),
            // NuGet package structure (assembly is in tasks/netX.X/)
            Path.Combine(assemblyDir, "..", "..", "runtimes", rid, "native", fileName),
            // Alternative: go up from tasks folder
            Path.Combine(assemblyDir, "..", "runtimes", rid, "native", fileName)
        };

        foreach (var path in searchPaths)
        {
            var normalizedPath = Path.GetFullPath(path);
            _logger.LogDebug("Checking sass path: {Path}", normalizedPath);
            if (File.Exists(normalizedPath))
            {
                _logger.LogInformation("Found sass at: {Path}", normalizedPath);
                return normalizedPath;
            }
        }

        _logger.LogWarning("Sass binary not found for {RID}. SCSS compilation will be skipped.", rid);
        return null;
    }

    private string GetRuntimeIdentifier()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.X64 => "win-x64",
                Architecture.Arm64 => "win-arm64",
                _ => throw new PlatformNotSupportedException($"Windows {RuntimeInformation.ProcessArchitecture} is not supported")
            };
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.X64 => "linux-x64",
                Architecture.Arm64 => "linux-arm64",
                _ => throw new PlatformNotSupportedException($"Linux {RuntimeInformation.ProcessArchitecture} is not supported")
            };
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.X64 => "osx-x64",
                Architecture.Arm64 => "osx-arm64",
                _ => throw new PlatformNotSupportedException($"macOS {RuntimeInformation.ProcessArchitecture} is not supported")
            };
        }

        throw new PlatformNotSupportedException($"Platform {RuntimeInformation.OSDescription} is not supported");
    }

    private void MakeExecutable(string filePath)
    {
        try
        {
            var chmod = Process.Start(new ProcessStartInfo
            {
                FileName = "chmod",
                Arguments = $"+x \"{filePath}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            });

            chmod?.WaitForExit();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not set executable permission on {Path}", filePath);
        }
    }

    private string BuildArguments(string inputPath, string outputPath, SassOptions options)
    {
        var args = new List<string>();

        // Input file
        args.Add($"\"{inputPath}\"");

        // Output file
        args.Add($"\"{outputPath}\"");

        // Style (compressed for production)
        if (options.Compressed)
        {
            args.Add("--style=compressed");
        }

        // Source maps
        if (options.SourceMap)
        {
            args.Add("--source-map");
        }
        else
        {
            args.Add("--no-source-map");
        }

        // Charset
        if (!options.Charset)
        {
            args.Add("--no-charset");
        }

        // Load paths for @import/@use resolution
        if (options.LoadPaths != null)
        {
            foreach (var loadPath in options.LoadPaths)
            {
                args.Add($"--load-path=\"{loadPath}\"");
            }
        }

        return string.Join(" ", args);
    }
}

/// <summary>
/// Options for Sass compilation.
/// </summary>
public class SassOptions
{
    /// <summary>
    /// Whether to compress (minify) the output CSS. Default: true for production.
    /// </summary>
    public bool Compressed { get; set; } = true;

    /// <summary>
    /// Whether to generate source maps. Default: false.
    /// </summary>
    public bool SourceMap { get; set; } = false;

    /// <summary>
    /// Whether to include @charset declaration. Default: false.
    /// </summary>
    public bool Charset { get; set; } = false;

    /// <summary>
    /// Additional paths to search for @import and @use.
    /// </summary>
    public List<string>? LoadPaths { get; set; }
}

/// <summary>
/// Result of a Sass compilation.
/// </summary>
public class SassResult
{
    public bool Success { get; set; }
    public int ExitCode { get; set; }
    public string Output { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
    public string? OutputPath { get; set; }
}
