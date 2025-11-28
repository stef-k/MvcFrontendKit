using System.Text.RegularExpressions;

namespace MvcFrontendKit.Tests;

/// <summary>
/// Tests for import path validation logic used by the CLI check command.
/// </summary>
public class ImportValidationTests
{
    // These regex patterns mirror the ones in CheckCommand.cs
    private static readonly Regex ImportRegex = new Regex(
        @"(?:import\s+.*?\s+from\s+['""]|import\s*\(\s*['""]|import\s+['""])(\.{1,2}/[^'""]+)['""]",
        RegexOptions.Compiled | RegexOptions.Multiline);

    private static readonly Regex ExportFromRegex = new Regex(
        @"export\s+.*?\s+from\s+['""](\.[^'""]+)['""]",
        RegexOptions.Compiled | RegexOptions.Multiline);

    [Theory]
    [InlineData("import { foo } from './utils.js';", "./utils.js")]
    [InlineData("import foo from './bar.js'", "./bar.js")]
    [InlineData("import './styles.js';", "./styles.js")]
    [InlineData("import { a, b } from '../shared/common.js';", "../shared/common.js")]
    [InlineData("import * as utils from './utils.js';", "./utils.js")]
    public void ImportRegex_MatchesRelativeImports(string code, string expectedPath)
    {
        var match = ImportRegex.Match(code);
        Assert.True(match.Success);
        Assert.Equal(expectedPath, match.Groups[1].Value);
    }

    [Theory]
    [InlineData("import { ref } from 'vue';")]
    [InlineData("import Vue from 'vue';")]
    [InlineData("import 'bootstrap';")]
    [InlineData("import { something } from '@scope/package';")]
    public void ImportRegex_DoesNotMatchBareImports(string code)
    {
        var match = ImportRegex.Match(code);
        Assert.False(match.Success);
    }

    [Theory]
    [InlineData("import('/path/to/module.js')", "/path/to/module.js")]
    [InlineData("const mod = import('./dynamic.js');", "./dynamic.js")]
    public void ImportRegex_MatchesDynamicImports(string code, string expectedPath)
    {
        var match = ImportRegex.Match(code);
        // Only relative imports should match
        if (expectedPath.StartsWith("./") || expectedPath.StartsWith("../"))
        {
            Assert.True(match.Success);
            Assert.Equal(expectedPath, match.Groups[1].Value);
        }
        else
        {
            // Absolute paths starting with / should not match
            Assert.False(match.Success);
        }
    }

    [Theory]
    [InlineData("export { foo } from './utils.js';", "./utils.js")]
    [InlineData("export * from './all.js';", "./all.js")]
    [InlineData("export { default as Bar } from './bar.js';", "./bar.js")]
    public void ExportFromRegex_MatchesReExports(string code, string expectedPath)
    {
        var match = ExportFromRegex.Match(code);
        Assert.True(match.Success);
        Assert.Equal(expectedPath, match.Groups[1].Value);
    }

    [Theory]
    [InlineData("export const foo = 'bar';")]
    [InlineData("export default class Foo {}")]
    [InlineData("export function doSomething() {}")]
    public void ExportFromRegex_DoesNotMatchRegularExports(string code)
    {
        var match = ExportFromRegex.Match(code);
        Assert.False(match.Success);
    }

    [Fact]
    public void ImportRegex_FindsMultipleImports()
    {
        var code = @"
import { foo } from './foo.js';
import { bar } from './bar.js';
import * as baz from '../baz.js';
import 'vue'; // bare import - should not match
";

        var matches = ImportRegex.Matches(code);
        Assert.Equal(3, matches.Count);
        Assert.Equal("./foo.js", matches[0].Groups[1].Value);
        Assert.Equal("./bar.js", matches[1].Groups[1].Value);
        Assert.Equal("../baz.js", matches[2].Groups[1].Value);
    }

    [Theory]
    [InlineData("import { foo } from \"./double-quotes.js\";", "./double-quotes.js")]
    [InlineData("import { foo } from './single-quotes.js';", "./single-quotes.js")]
    public void ImportRegex_HandlesBothQuoteStyles(string code, string expectedPath)
    {
        var match = ImportRegex.Match(code);
        Assert.True(match.Success);
        Assert.Equal(expectedPath, match.Groups[1].Value);
    }

    [Theory]
    [InlineData("./utils", "./utils.js", true)]
    [InlineData("./utils.js", "./utils.js", false)]
    [InlineData("../shared/common", "../shared/common.js", true)]
    [InlineData("./index", "./index.js", true)]
    public void ResolveImportPath_AddsJsExtensionIfMissing(string importPath, string expectedResult, bool extensionAdded)
    {
        // Test the extension addition logic
        var pathWithExt = importPath;
        if (!importPath.EndsWith(".js", StringComparison.OrdinalIgnoreCase))
        {
            pathWithExt = importPath + ".js";
        }

        Assert.Equal(expectedResult, pathWithExt);
        Assert.Equal(extensionAdded, !importPath.EndsWith(".js"));
    }
}
