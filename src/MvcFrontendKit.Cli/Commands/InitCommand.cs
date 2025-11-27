using System.Reflection;

namespace MvcFrontendKit.Cli.Commands;

public class InitCommand
{
    public static int Execute(bool force)
    {
        var configPath = Path.Combine(Directory.GetCurrentDirectory(), "frontend.config.yaml");

        if (File.Exists(configPath) && !force)
        {
            Console.WriteLine($"Config file already exists at: {configPath}");
            Console.WriteLine("Use --force to overwrite");
            return 1;
        }

        try
        {
            var template = GetEmbeddedTemplate();

            if (string.IsNullOrEmpty(template))
            {
                Console.Error.WriteLine("Error: Could not load config template");
                return 1;
            }

            File.WriteAllText(configPath, template);

            Console.WriteLine($"✓ Created frontend.config.yaml at: {configPath}");
            Console.WriteLine();
            Console.WriteLine("Next steps:");
            Console.WriteLine("  1. Edit frontend.config.yaml to match your project structure");
            Console.WriteLine("  2. Add services.AddMvcFrontendKit() in your Program.cs");
            Console.WriteLine("  3. Use @Html.FrontendGlobalScripts() and @Html.FrontendGlobalStyles() in your layout");
            Console.WriteLine("  4. Run 'dotnet frontend check' to validate your configuration");

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error creating config file: {ex.Message}");
            return 1;
        }
    }

    private static string? GetEmbeddedTemplate()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = "MvcFrontendKit.Cli.Templates.frontend.config.template.yaml";

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            Console.Error.WriteLine($"Error: Template resource not found: {resourceName}");
            var resources = assembly.GetManifestResourceNames();
            Console.Error.WriteLine("Available resources:");
            foreach (var res in resources)
            {
                Console.Error.WriteLine($"  - {res}");
            }
            return null;
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
