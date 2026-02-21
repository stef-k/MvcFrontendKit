using System.Xml.Linq;

namespace MvcFrontendKit.Cli.Helpers;

public static class CsprojHelper
{
    private const string ConfigFileName = "frontend.config.yaml";

    /// <summary>
    /// Finds a single .csproj file in the given directory.
    /// Returns null if zero or more than one .csproj is found.
    /// </summary>
    public static string? FindCsprojFile(string directory)
    {
        var csprojFiles = Directory.GetFiles(directory, "*.csproj");

        if (csprojFiles.Length == 0)
        {
            return null;
        }

        if (csprojFiles.Length > 1)
        {
            Console.WriteLine($"  Multiple .csproj files found in {directory}. Skipping .csproj patching.");
            return null;
        }

        return csprojFiles[0];
    }

    /// <summary>
    /// Checks whether the XDocument already contains a Content item for frontend.config.yaml.
    /// Case-insensitive comparison on the Include attribute.
    /// </summary>
    public static bool HasConfigContentItem(XDocument doc)
    {
        var ns = doc.Root?.Name.Namespace ?? XNamespace.None;

        return doc.Descendants(ns + "Content")
            .Any(el => string.Equals(
                el.Attribute("Include")?.Value,
                ConfigFileName,
                StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Adds a Content item for frontend.config.yaml with CopyToPublishDirectory="PreserveNewest".
    /// Returns true if the item was added, false if it already exists.
    /// </summary>
    public static bool AddConfigContentItem(XDocument doc)
    {
        if (HasConfigContentItem(doc))
        {
            return false;
        }

        var ns = doc.Root?.Name.Namespace ?? XNamespace.None;

        var itemGroup = new XElement(ns + "ItemGroup",
            new XElement(ns + "Content",
                new XAttribute("Include", ConfigFileName),
                new XAttribute("CopyToPublishDirectory", "PreserveNewest")));

        doc.Root!.Add(itemGroup);
        return true;
    }

    /// <summary>
    /// Returns the XML snippet for manual .csproj patching instructions.
    /// </summary>
    public static string GetManualXmlSnippet()
    {
        return """
            <ItemGroup>
              <Content Include="frontend.config.yaml" CopyToPublishDirectory="PreserveNewest" />
            </ItemGroup>
            """;
    }
}
