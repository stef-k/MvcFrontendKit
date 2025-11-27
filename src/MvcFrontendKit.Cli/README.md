# MvcFrontendKit CLI

Command-line tool for **MvcFrontendKit** - Node-free frontend bundling for ASP.NET Core MVC/Razor.

## Installation

```bash
dotnet tool install --global MvcFrontendKit.Cli
```

## Usage

### Initialize Configuration

Create a `frontend.config.yaml` with sensible defaults:

```bash
frontend init
```

Force overwrite existing config:

```bash
frontend init --force
```

### Validate Configuration

Check if your configuration is valid and all assets exist:

```bash
frontend check
```

Detailed validation output:

```bash
frontend check --verbose
```

### Help

```bash
frontend help
frontend version
```

## What's Next?

After running `frontend init`, you need to:

1. **Install the main library** in your ASP.NET Core project:
   ```bash
   dotnet add package MvcFrontendKit
   ```

2. **Register services** in `Program.cs`:
   ```csharp
   builder.Services.AddMvcFrontendKit();
   ```

3. **Add helpers** to your layout (`_Layout.cshtml`):
   ```cshtml
   <head>
       @Html.FrontendImportMap()
       @Html.FrontendGlobalStyles()
       @Html.FrontendViewStyles()
   </head>
   <body>
       @RenderBody()
       @Html.FrontendGlobalScripts()
       @Html.FrontendViewScripts()
   </body>
   ```

## Documentation

- Full documentation: https://github.com/stef-k/MvcFrontendKit
- Main package: https://www.nuget.org/packages/MvcFrontendKit

## License

MIT
