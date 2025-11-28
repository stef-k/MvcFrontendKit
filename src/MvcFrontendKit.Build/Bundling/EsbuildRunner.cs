using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MvcFrontendKit.Build.Bundling;

public class EsbuildRunner
{
    private readonly ILogger _logger;

    public EsbuildRunner(ILogger logger)
    {
        _logger = logger;
    }

    public async Task<EsbuildResult> RunAsync(EsbuildOptions options, CancellationToken cancellationToken = default)
    {
        var esbuildPath = GetEsbuildPath();

        if (!File.Exists(esbuildPath))
        {
            throw new FileNotFoundException(
                $"Esbuild binary not found at {esbuildPath}. " +
                "Make sure the MvcFrontendKit NuGet package is properly installed with all runtime dependencies.");
        }

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            MakeExecutable(esbuildPath);
        }

        var arguments = BuildArguments(options);
        _logger.LogDebug("Running esbuild: {Path} {Arguments}", esbuildPath, arguments);

        var startInfo = new ProcessStartInfo
        {
            FileName = esbuildPath,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = options.WorkingDirectory ?? Directory.GetCurrentDirectory()
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

        // WaitForExitAsync is not available in netstandard2.0, use sync version
        await Task.Run(() => process.WaitForExit(), cancellationToken);

        var output = outputBuilder.ToString();
        var error = errorBuilder.ToString();

        if (process.ExitCode != 0)
        {
            _logger.LogError("Esbuild failed with exit code {ExitCode}. Error: {Error}", process.ExitCode, error);
        }
        else
        {
            _logger.LogInformation("Esbuild completed successfully");
        }

        return new EsbuildResult
        {
            Success = process.ExitCode == 0,
            ExitCode = process.ExitCode,
            Output = output,
            Error = error
        };
    }

    private string GetEsbuildPath()
    {
        var rid = GetRuntimeIdentifier();
        var assemblyLocation = Assembly.GetExecutingAssembly().Location;
        var assemblyDir = Path.GetDirectoryName(assemblyLocation);

        if (string.IsNullOrEmpty(assemblyDir))
        {
            throw new InvalidOperationException("Could not determine assembly directory");
        }

        var fileName = rid.StartsWith("win") ? "esbuild.exe" : "esbuild";
        var esbuildPath = Path.Combine(assemblyDir, "runtimes", rid, "native", fileName);

        return esbuildPath;
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

    private string BuildArguments(EsbuildOptions options)
    {
        var args = new List<string>();

        foreach (var entryPoint in options.EntryPoints)
        {
            args.Add($"\"{entryPoint}\"");
        }

        if (!string.IsNullOrEmpty(options.OutDir))
        {
            args.Add($"--outdir=\"{options.OutDir}\"");
        }

        if (!string.IsNullOrEmpty(options.OutFile))
        {
            args.Add($"--outfile=\"{options.OutFile}\"");
        }

        args.Add("--bundle");

        if (options.Minify)
        {
            args.Add("--minify");
        }

        if (options.Sourcemap)
        {
            args.Add("--sourcemap");
        }

        if (!string.IsNullOrEmpty(options.Target))
        {
            args.Add($"--target={options.Target}");
        }

        if (!string.IsNullOrEmpty(options.Format))
        {
            args.Add($"--format={options.Format}");
        }

        if (options.Splitting)
        {
            args.Add("--splitting");
        }

        if (options.EntryNames != null)
        {
            args.Add($"--entry-names=\"{options.EntryNames}\"");
        }

        if (options.ChunkNames != null)
        {
            args.Add($"--chunk-names=\"{options.ChunkNames}\"");
        }

        if (options.AssetNames != null)
        {
            args.Add($"--asset-names=\"{options.AssetNames}\"");
        }

        if (options.Loader != null)
        {
            foreach (var kvp in options.Loader)
            {
                args.Add($"--loader:{kvp.Key}={kvp.Value}");
            }
        }

        if (options.External != null && options.External.Any())
        {
            foreach (var external in options.External)
            {
                args.Add($"--external:{external}");
            }
        }

        if (options.Define != null)
        {
            foreach (var kvp in options.Define)
            {
                args.Add($"--define:{kvp.Key}={kvp.Value}");
            }
        }

        if (options.Platform != null)
        {
            args.Add($"--platform={options.Platform}");
        }

        if (options.LogLevel != null)
        {
            args.Add($"--log-level={options.LogLevel}");
        }

        if (options.Metafile)
        {
            args.Add("--metafile");
        }

        if (options.AdditionalArgs != null && options.AdditionalArgs.Any())
        {
            args.AddRange(options.AdditionalArgs);
        }

        return string.Join(" ", args);
    }
}

public class EsbuildOptions
{
    public List<string> EntryPoints { get; set; } = new List<string>();
    public string? OutDir { get; set; }
    public string? OutFile { get; set; }
    public bool Minify { get; set; } = true;
    public bool Sourcemap { get; set; } = true;
    public string? Target { get; set; }
    public string? Format { get; set; }
    public bool Splitting { get; set; }
    public string? EntryNames { get; set; }
    public string? ChunkNames { get; set; }
    public string? AssetNames { get; set; }
    public Dictionary<string, string>? Loader { get; set; }
    public List<string>? External { get; set; }
    public Dictionary<string, string>? Define { get; set; }
    public string? Platform { get; set; }
    public string? LogLevel { get; set; }
    public bool Metafile { get; set; }
    public List<string>? AdditionalArgs { get; set; }
    public string? WorkingDirectory { get; set; }
}

public class EsbuildResult
{
    public bool Success { get; set; }
    public int ExitCode { get; set; }
    public string Output { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
}
