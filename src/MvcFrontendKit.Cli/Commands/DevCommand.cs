using MvcFrontendKit.Configuration;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace MvcFrontendKit.Cli.Commands;

/// <summary>
/// Compiles TypeScript and SCSS files for development.
/// Creates .js files alongside .ts files, and .css files alongside .scss/.sass files.
/// This allows the development mode helpers to serve the compiled files.
/// </summary>
public class DevCommand
{
    public static int Execute(bool verbose, bool watch)
    {
        var configPath = Path.Combine(Directory.GetCurrentDirectory(), "frontend.config.yaml");

        if (!File.Exists(configPath))
        {
            Console.Error.WriteLine($"Error: Config file not found at: {configPath}");
            Console.Error.WriteLine("Run 'dotnet frontend init' to create it");
            return 1;
        }

        try
        {
            var yaml = File.ReadAllText(configPath);
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();

            var config = deserializer.Deserialize<FrontendConfig>(yaml);
            var projectRoot = Directory.GetCurrentDirectory();

            if (watch)
            {
                return ExecuteWatch(config, projectRoot, verbose);
            }
            else
            {
                return ExecuteCompile(config, projectRoot, verbose);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            if (verbose)
            {
                Console.Error.WriteLine(ex.StackTrace);
            }
            return 1;
        }
    }

    private static int ExecuteCompile(FrontendConfig config, string projectRoot, bool verbose)
    {
        Console.WriteLine("Compiling TypeScript and SCSS for development...");
        Console.WriteLine();

        var tsFiles = new List<string>();
        var scssFiles = new List<string>();

        // Collect all files from config
        CollectFilesFromConfig(config, tsFiles, scssFiles);

        // Also scan directories for any additional files
        var jsRoot = Path.Combine(projectRoot, config.JsRoot ?? "wwwroot/js");
        var cssRoot = Path.Combine(projectRoot, config.CssRoot ?? "wwwroot/css");

        if (Directory.Exists(jsRoot))
        {
            tsFiles.AddRange(Directory.GetFiles(jsRoot, "*.ts", SearchOption.AllDirectories)
                .Where(f => !f.EndsWith(".d.ts", StringComparison.OrdinalIgnoreCase)));
            tsFiles.AddRange(Directory.GetFiles(jsRoot, "*.tsx", SearchOption.AllDirectories));
        }

        if (Directory.Exists(cssRoot))
        {
            scssFiles.AddRange(Directory.GetFiles(cssRoot, "*.scss", SearchOption.AllDirectories));
            scssFiles.AddRange(Directory.GetFiles(cssRoot, "*.sass", SearchOption.AllDirectories));
        }

        // Remove duplicates (normalize path separators for proper comparison)
        tsFiles = tsFiles
            .Select(f => Path.GetFullPath(Path.IsPathRooted(f) ? f : Path.Combine(projectRoot, f)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        scssFiles = scssFiles
            .Select(f => Path.GetFullPath(Path.IsPathRooted(f) ? f : Path.Combine(projectRoot, f)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var tsCompiled = 0;
        var scssCompiled = 0;
        var errors = 0;

        // Compile TypeScript files
        if (tsFiles.Any())
        {
            Console.WriteLine($"TypeScript files ({tsFiles.Count}):");
            foreach (var tsFile in tsFiles)
            {
                var absolutePath = Path.IsPathRooted(tsFile) ? tsFile : Path.Combine(projectRoot, tsFile);
                if (!File.Exists(absolutePath))
                {
                    if (verbose)
                    {
                        Console.WriteLine($"  [SKIP] {tsFile} (not found)");
                    }
                    continue;
                }

                var jsFile = GetOutputPath(absolutePath, ".js");
                if (verbose)
                {
                    Console.WriteLine($"  {GetRelativePath(projectRoot, absolutePath)} -> {GetRelativePath(projectRoot, jsFile)}");
                }

                var success = CompileTypeScript(absolutePath, jsFile, projectRoot, verbose);
                if (success)
                {
                    tsCompiled++;
                    Console.WriteLine($"  [OK] {GetRelativePath(projectRoot, absolutePath)}");
                }
                else
                {
                    errors++;
                    Console.WriteLine($"  [FAIL] {GetRelativePath(projectRoot, absolutePath)}");
                }
            }
            Console.WriteLine();
        }

        // Compile SCSS/Sass files (batched for performance - single Dart VM startup)
        if (scssFiles.Any())
        {
            Console.WriteLine($"SCSS/Sass files ({scssFiles.Count}):");

            // Build list of input:output pairs for batch compilation
            var compilationPairs = new List<(string input, string output)>();
            foreach (var scssFile in scssFiles)
            {
                var absolutePath = Path.IsPathRooted(scssFile) ? scssFile : Path.Combine(projectRoot, scssFile);
                if (!File.Exists(absolutePath))
                {
                    if (verbose)
                    {
                        Console.WriteLine($"  [SKIP] {scssFile} (not found)");
                    }
                    continue;
                }

                var cssFile = GetOutputPath(absolutePath, ".css");
                compilationPairs.Add((absolutePath, cssFile));

                if (verbose)
                {
                    Console.WriteLine($"  {GetRelativePath(projectRoot, absolutePath)} -> {GetRelativePath(projectRoot, cssFile)}");
                }
            }

            if (compilationPairs.Count > 0)
            {
                var result = CompileScssBatch(compilationPairs, projectRoot, verbose);
                scssCompiled = result.successCount;
                errors += result.errorCount;

                // Report results
                foreach (var pair in compilationPairs)
                {
                    var success = File.Exists(pair.output) &&
                        File.GetLastWriteTimeUtc(pair.output) >= File.GetLastWriteTimeUtc(pair.input).AddSeconds(-1);
                    if (success)
                    {
                        Console.WriteLine($"  [OK] {GetRelativePath(projectRoot, pair.input)}");
                    }
                    else
                    {
                        Console.WriteLine($"  [FAIL] {GetRelativePath(projectRoot, pair.input)}");
                    }
                }
            }
            Console.WriteLine();
        }

        // Summary
        Console.WriteLine(new string('-', 50));
        Console.WriteLine($"Compiled: {tsCompiled} TypeScript, {scssCompiled} SCSS/Sass");
        if (errors > 0)
        {
            Console.WriteLine($"Errors: {errors}");
            return 1;
        }

        if (tsCompiled == 0 && scssCompiled == 0)
        {
            Console.WriteLine("No TypeScript or SCSS files found to compile.");
            Console.WriteLine("Place .ts/.tsx files in your jsRoot and .scss/.sass files in your cssRoot.");
        }
        else
        {
            Console.WriteLine();
            Console.WriteLine("Development files ready. Run your app with:");
            Console.WriteLine("  dotnet run");
            Console.WriteLine();
            Console.WriteLine("Tip: Add these patterns to .gitignore to exclude dev-compiled files:");
            Console.WriteLine("  wwwroot/js/**/*.js");
            Console.WriteLine("  wwwroot/css/**/*.css");
            Console.WriteLine("  !wwwroot/js/**/*.min.js  # keep vendor files");
            Console.WriteLine("  !wwwroot/lib/**/*        # keep library files");
        }

        return 0;
    }

    private static int ExecuteWatch(FrontendConfig config, string projectRoot, bool verbose)
    {
        var jsRoot = Path.Combine(projectRoot, config.JsRoot ?? "wwwroot/js");
        var cssRoot = Path.Combine(projectRoot, config.CssRoot ?? "wwwroot/css");

        // Initial compilation
        Console.WriteLine("Performing initial compilation...");
        Console.WriteLine();
        ExecuteCompile(config, projectRoot, verbose);
        Console.WriteLine();

        Console.WriteLine("Watch mode started. Monitoring for changes...");
        Console.WriteLine($"  JS root:  {jsRoot}");
        Console.WriteLine($"  CSS root: {cssRoot}");
        Console.WriteLine();
        Console.WriteLine("Press Ctrl+C to stop watching.");
        Console.WriteLine(new string('-', 50));
        Console.WriteLine();

        // Debounce tracking - avoid compiling same file multiple times in quick succession
        var pendingChanges = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        var debounceMs = 300;
        var lockObj = new object();

        // Create watchers
        var watchers = new List<FileSystemWatcher>();

        if (Directory.Exists(jsRoot))
        {
            var jsWatcher = new FileSystemWatcher(jsRoot)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
                EnableRaisingEvents = true
            };
            jsWatcher.Changed += (s, e) => QueueChange(e.FullPath, pendingChanges, lockObj);
            jsWatcher.Created += (s, e) => QueueChange(e.FullPath, pendingChanges, lockObj);
            jsWatcher.Renamed += (s, e) => QueueChange(e.FullPath, pendingChanges, lockObj);
            watchers.Add(jsWatcher);
        }

        if (Directory.Exists(cssRoot))
        {
            var cssWatcher = new FileSystemWatcher(cssRoot)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
                EnableRaisingEvents = true
            };
            cssWatcher.Changed += (s, e) => QueueChange(e.FullPath, pendingChanges, lockObj);
            cssWatcher.Created += (s, e) => QueueChange(e.FullPath, pendingChanges, lockObj);
            cssWatcher.Renamed += (s, e) => QueueChange(e.FullPath, pendingChanges, lockObj);
            watchers.Add(cssWatcher);
        }

        // Handle Ctrl+C
        var exitEvent = new ManualResetEvent(false);
        Console.CancelKeyPress += (s, e) =>
        {
            e.Cancel = true;
            exitEvent.Set();
        };

        // Processing loop
        while (!exitEvent.WaitOne(100))
        {
            List<string> filesToProcess;
            lock (lockObj)
            {
                var now = DateTime.UtcNow;
                filesToProcess = pendingChanges
                    .Where(kv => (now - kv.Value).TotalMilliseconds >= debounceMs)
                    .Select(kv => kv.Key)
                    .ToList();

                foreach (var file in filesToProcess)
                {
                    pendingChanges.Remove(file);
                }
            }

            foreach (var file in filesToProcess)
            {
                ProcessChangedFile(file, projectRoot, verbose);
            }
        }

        // Cleanup
        foreach (var watcher in watchers)
        {
            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
        }

        Console.WriteLine();
        Console.WriteLine("Watch mode stopped.");
        return 0;
    }

    private static void QueueChange(string filePath, Dictionary<string, DateTime> pendingChanges, object lockObj)
    {
        // Only process TypeScript and SCSS files
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        if (ext != ".ts" && ext != ".tsx" && ext != ".scss" && ext != ".sass")
            return;

        // Skip .d.ts files
        if (filePath.EndsWith(".d.ts", StringComparison.OrdinalIgnoreCase))
            return;

        lock (lockObj)
        {
            pendingChanges[filePath] = DateTime.UtcNow;
        }
    }

    private static void ProcessChangedFile(string filePath, string projectRoot, bool verbose)
    {
        if (!File.Exists(filePath))
            return;

        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        var relativePath = GetRelativePath(projectRoot, filePath);
        var timestamp = DateTime.Now.ToString("HH:mm:ss");

        if (ext == ".ts" || ext == ".tsx")
        {
            Console.Write($"[{timestamp}] Compiling {relativePath}... ");
            var outputPath = GetOutputPath(filePath, ".js");
            var success = CompileTypeScript(filePath, outputPath, projectRoot, verbose);
            Console.WriteLine(success ? "OK" : "FAILED");
        }
        else if (ext == ".scss" || ext == ".sass")
        {
            Console.Write($"[{timestamp}] Compiling {relativePath}... ");
            var outputPath = GetOutputPath(filePath, ".css");
            var files = new List<(string input, string output)> { (filePath, outputPath) };
            var result = CompileScssBatch(files, projectRoot, verbose);
            Console.WriteLine(result.successCount > 0 ? "OK" : "FAILED");
        }
    }

    private static void CollectFilesFromConfig(FrontendConfig config, List<string> tsFiles, List<string> scssFiles)
    {
        // Global assets
        if (config.Global?.Js != null)
        {
            tsFiles.AddRange(config.Global.Js.Where(f =>
                f.EndsWith(".ts", StringComparison.OrdinalIgnoreCase) ||
                f.EndsWith(".tsx", StringComparison.OrdinalIgnoreCase)));
        }

        if (config.Global?.Css != null)
        {
            scssFiles.AddRange(config.Global.Css.Where(f =>
                f.EndsWith(".scss", StringComparison.OrdinalIgnoreCase) ||
                f.EndsWith(".sass", StringComparison.OrdinalIgnoreCase)));
        }

        // View overrides
        if (config.Views?.Overrides != null)
        {
            foreach (var viewOverride in config.Views.Overrides.Values)
            {
                if (viewOverride?.Js != null)
                {
                    tsFiles.AddRange(viewOverride.Js.Where(f =>
                        f.EndsWith(".ts", StringComparison.OrdinalIgnoreCase) ||
                        f.EndsWith(".tsx", StringComparison.OrdinalIgnoreCase)));
                }

                if (viewOverride?.Css != null)
                {
                    scssFiles.AddRange(viewOverride.Css.Where(f =>
                        f.EndsWith(".scss", StringComparison.OrdinalIgnoreCase) ||
                        f.EndsWith(".sass", StringComparison.OrdinalIgnoreCase)));
                }
            }
        }

        // Components
        if (config.Components != null)
        {
            foreach (var component in config.Components.Values)
            {
                if (component?.Js != null)
                {
                    tsFiles.AddRange(component.Js.Where(f =>
                        f.EndsWith(".ts", StringComparison.OrdinalIgnoreCase) ||
                        f.EndsWith(".tsx", StringComparison.OrdinalIgnoreCase)));
                }

                if (component?.Css != null)
                {
                    scssFiles.AddRange(component.Css.Where(f =>
                        f.EndsWith(".scss", StringComparison.OrdinalIgnoreCase) ||
                        f.EndsWith(".sass", StringComparison.OrdinalIgnoreCase)));
                }
            }
        }

        // Areas
        if (config.Areas != null)
        {
            foreach (var area in config.Areas.Values)
            {
                if (area?.Js != null)
                {
                    tsFiles.AddRange(area.Js.Where(f =>
                        f.EndsWith(".ts", StringComparison.OrdinalIgnoreCase) ||
                        f.EndsWith(".tsx", StringComparison.OrdinalIgnoreCase)));
                }

                if (area?.Css != null)
                {
                    scssFiles.AddRange(area.Css.Where(f =>
                        f.EndsWith(".scss", StringComparison.OrdinalIgnoreCase) ||
                        f.EndsWith(".sass", StringComparison.OrdinalIgnoreCase)));
                }
            }
        }
    }

    private static string GetOutputPath(string inputPath, string newExtension)
    {
        var dir = Path.GetDirectoryName(inputPath) ?? "";
        var nameWithoutExt = Path.GetFileNameWithoutExtension(inputPath);
        return Path.Combine(dir, nameWithoutExt + newExtension);
    }

    private static bool CompileTypeScript(string inputPath, string outputPath, string projectRoot, bool verbose)
    {
        try
        {
            var esbuildPath = FindEsbuild(projectRoot);
            if (esbuildPath == null)
            {
                Console.Error.WriteLine("    Error: esbuild not found");
                return false;
            }

            var args = new StringBuilder();
            args.Append($"\"{inputPath}\"");
            args.Append($" --outfile=\"{outputPath}\"");
            args.Append(" --format=esm");
            args.Append(" --sourcemap");

            // Add loader based on extension
            if (inputPath.EndsWith(".tsx", StringComparison.OrdinalIgnoreCase))
            {
                args.Append(" --loader:.tsx=tsx");
            }
            else
            {
                args.Append(" --loader:.ts=ts");
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = esbuildPath,
                Arguments = args.ToString(),
                WorkingDirectory = projectRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null) return false;

            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                if (verbose || !string.IsNullOrWhiteSpace(stderr))
                {
                    Console.Error.WriteLine($"    {stderr}");
                }
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            if (verbose)
            {
                Console.Error.WriteLine($"    Exception: {ex.Message}");
            }
            return false;
        }
    }

    /// <summary>
    /// Compiles all SCSS files in a single batch to avoid multiple Dart VM startups.
    /// Uses sass many-to-many syntax: sass input1.scss:output1.css input2.scss:output2.css
    /// Note: Uses relative paths to avoid Windows absolute path colon conflicts.
    /// </summary>
    private static (int successCount, int errorCount) CompileScssBatch(
        List<(string input, string output)> files,
        string projectRoot,
        bool verbose)
    {
        if (files.Count == 0)
            return (0, 0);

        try
        {
            var sassPath = FindSass(projectRoot);
            if (sassPath == null)
            {
                Console.Error.WriteLine("    Error: sass compiler not found");
                return (0, files.Count);
            }

            var args = new StringBuilder();

            // Build many-to-many args using relative paths to avoid Windows C: colon conflict
            // sass uses input:output syntax, but C:\path has a colon too
            foreach (var file in files)
            {
                if (args.Length > 0)
                    args.Append(' ');

                // Convert to relative paths from projectRoot to avoid C: colon issues
                var relInput = Path.GetRelativePath(projectRoot, file.input).Replace('\\', '/');
                var relOutput = Path.GetRelativePath(projectRoot, file.output).Replace('\\', '/');

                args.Append($"\"{relInput}:{relOutput}\"");
            }

            args.Append(" --no-source-map");
            args.Append(" --no-charset");

            // Collect unique directories for load paths (use relative paths)
            var loadPaths = files
                .Select(f => Path.GetDirectoryName(f.input))
                .Where(d => !string.IsNullOrEmpty(d))
                .Select(d => Path.GetRelativePath(projectRoot, d!).Replace('\\', '/'))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var loadPath in loadPaths)
            {
                args.Append($" --load-path=\"{loadPath}\"");
            }

            if (verbose)
            {
                Console.WriteLine($"  [BATCH] Compiling {files.Count} files in single sass invocation");
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = sassPath,
                Arguments = args.ToString(),
                WorkingDirectory = projectRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null) return (0, files.Count);

            // Read both streams asynchronously to avoid deadlock when stderr buffer fills
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();

            process.WaitForExit();

            var stdout = stdoutTask.Result;
            var stderr = stderrTask.Result;

            if (process.ExitCode != 0)
            {
                if (verbose || !string.IsNullOrWhiteSpace(stderr))
                {
                    Console.Error.WriteLine($"    {stderr}");
                }
                // Count how many actually got compiled despite error
                var successCount = files.Count(f => File.Exists(f.output));
                return (successCount, files.Count - successCount);
            }

            return (files.Count, 0);
        }
        catch (Exception ex)
        {
            if (verbose)
            {
                Console.Error.WriteLine($"    Exception: {ex.Message}");
            }
            return (0, files.Count);
        }
    }

    private static string? FindEsbuild(string projectRoot)
    {
        var rid = GetRuntimeIdentifier();
        var execName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "esbuild.exe" : "esbuild";

        // Look in common locations
        var searchPaths = new[]
        {
            // NuGet package location (when using MvcFrontendKit package)
            Path.Combine(projectRoot, "bin", "Debug", "net10.0", "runtimes", rid, "native", execName),
            Path.Combine(projectRoot, "bin", "Release", "net10.0", "runtimes", rid, "native", execName),
            // Development location (when using project reference)
            Path.Combine(projectRoot, "..", "MvcFrontendKit", "src", "MvcFrontendKit", "runtimes", rid, "native", execName),
            // Fallback: look for esbuild in PATH
        };

        foreach (var path in searchPaths)
        {
            if (File.Exists(path))
            {
                return path;
            }
        }

        // Try to find in PATH
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
        var pathDirs = pathEnv.Split(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ';' : ':');
        foreach (var dir in pathDirs)
        {
            var fullPath = Path.Combine(dir, execName);
            if (File.Exists(fullPath))
            {
                return fullPath;
            }
        }

        return null;
    }

    private static string? FindSass(string projectRoot)
    {
        var rid = GetRuntimeIdentifier();
        var execName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "sass.bat" : "sass";

        // Look in common locations
        var searchPaths = new[]
        {
            // NuGet package location (when using MvcFrontendKit package)
            Path.Combine(projectRoot, "bin", "Debug", "net10.0", "runtimes", rid, "native", execName),
            Path.Combine(projectRoot, "bin", "Release", "net10.0", "runtimes", rid, "native", execName),
            // Development location (when using project reference)
            Path.Combine(projectRoot, "..", "MvcFrontendKit", "src", "MvcFrontendKit", "runtimes", rid, "native", execName),
        };

        foreach (var path in searchPaths)
        {
            if (File.Exists(path))
            {
                return path;
            }
        }

        // Try to find in PATH
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
        var pathDirs = pathEnv.Split(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ';' : ':');
        foreach (var dir in pathDirs)
        {
            var fullPath = Path.Combine(dir, execName);
            if (File.Exists(fullPath))
            {
                return fullPath;
            }
        }

        return null;
    }

    private static string GetRuntimeIdentifier()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return "win-x64";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "linux-arm64" : "linux-x64";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "osx-arm64" : "osx-x64";
        }
        return "win-x64";
    }

    private static string GetRelativePath(string basePath, string fullPath)
    {
        if (fullPath.StartsWith(basePath, StringComparison.OrdinalIgnoreCase))
        {
            return fullPath.Substring(basePath.Length).TrimStart(Path.DirectorySeparatorChar, '/');
        }
        return fullPath;
    }
}
