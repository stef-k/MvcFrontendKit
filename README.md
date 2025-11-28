# MvcFrontendKit

[![CI](https://github.com/stef-k/MvcFrontendKit/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/stef-k/MvcFrontendKit/actions/workflows/ci.yml)

[![NuGet - MvcFrontendKit](https://img.shields.io/nuget/v/MvcFrontendKit.svg?label=MvcFrontendKit)](https://www.nuget.org/packages/MvcFrontendKit/)
[![NuGet - MvcFrontendKit Downloads](https://img.shields.io/nuget/dt/MvcFrontendKit.svg?label=downloads)](https://www.nuget.org/packages/MvcFrontendKit/)

[![NuGet - MvcFrontendKit.Cli](https://img.shields.io/nuget/v/MvcFrontendKit.Cli.svg?label=MvcFrontendKit.Cli)](https://www.nuget.org/packages/MvcFrontendKit.Cli/)
[![NuGet - MvcFrontendKit.Cli Downloads](https://img.shields.io/nuget/dt/MvcFrontendKit.Cli.svg?label=downloads)](https://www.nuget.org/packages/MvcFrontendKit.Cli/)

**MvcFrontendKit** is a Node.js-free frontend bundling toolkit for ASP.NET Core MVC / Razor applications.

It wraps `esbuild` behind a simple `.NET + YAML` workflow:

- **Dev:** Serve raw JS/CSS from `wwwroot` with cache-busting query strings.
- **Prod:** Build fingerprinted bundles (`/dist/js`, `/dist/css`) via `esbuild`, driven by a single `frontend.config.yaml`.
- **No Node / npm required:** Uses a native `esbuild` binary under the hood.
- **Razor-friendly:** Provides HTML helpers / tag helpers for layouts, views, partials, and components.
- **Spec-driven:** Behavior is fully defined in `SPEC.md`.

This is for ASP.NET devs who want modern bundling without committing to the full Node toolchain.

> **Status:** v1.0.0-preview.x - Core implementation complete. Production-ready for early adopters. Expect breaking changes until v1.0.

## Installation

### 1. Install the NuGet packages

```bash
# Install the main library
dotnet add package MvcFrontendKit

# Install the CLI tool (optional but recommended)
dotnet tool install --global MvcFrontendKit.Cli
```

### 2. Generate configuration

```bash
# Creates frontend.config.yaml with sensible defaults
dotnet frontend init
```

### 3. Register services in Program.cs

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

// Add MvcFrontendKit services
builder.Services.AddMvcFrontendKit();

var app = builder.Build();
// ... rest of your app configuration
```

### 4. Update your layout

Add helpers to your `_Layout.cshtml` or equivalent:

```cshtml
<head>
    <meta charset="utf-8" />
    <title>@ViewData["Title"] - MyApp</title>

    @* Dev: import map for bare imports, Prod: bundled *@
    @Html.FrontendImportMap()

    @* Global + view-specific CSS *@
    @Html.FrontendGlobalStyles()
    @Html.FrontendViewStyles()
</head>
<body>
    @RenderBody()

    @* Global + view-specific JS *@
    @Html.FrontendGlobalScripts()
    @Html.FrontendViewScripts()

    @RenderSection("Scripts", required: false)
</body>
```

### 5. Develop and build

- **Development:** Run `dotnet run` or `dotnet watch`. Raw files served from `wwwroot` with cache-busting.
- **Production:** Run `dotnet publish -c Release`. Bundles built automatically to `wwwroot/dist` with `frontend.manifest.json`.

---

## Features (high level)

- **Dev vs Prod flow**
  - Dev:
    - Raw JS/CSS from `wwwroot/js` and `wwwroot/css`
    - `type="module"` for JS (use `window.myFunc = myFunc` to expose globals)
    - `?v={File.GetLastWriteTimeUtc(path).Ticks` cache-busting
  - Prod:
    - Bundled + minified JS/CSS into `/dist/js` and `/dist/css`
    - Fingerprinted filenames and a JSON manifest
    - Helpers emit `<script>` / `<link>` tags pointing at bundles

- **Modes**
  - `single` mode — one global JS + one global CSS bundle
  - `areas` mode — one global bundle plus one bundle per Area (intentionally minimal in v1)
  - `views` mode — per-view bundles driven by conventions + overrides (recommended)

- **Config-driven (YAML)**
  - `frontend.config.yaml` controls:
    - `mode`, `webRoot`, `appBasePath`
    - `global.js` / `global.css`
    - `views.conventions` and `views.overrides`
    - `components` (named reusable JS/CSS chunks)
    - `cssUrlPolicy` (relative vs root-relative URLs, `@import` handling)
    - `importMap` (Dev import map, Prod strategy: `bundle` vs `external`)
    - `cleanDistOnBuild`, bundle size warning thresholds, etc.

- **Components**
  - Named components (e.g. `datepicker`) with optional JS and/or CSS
  - Dependency graph with cycle detection
  - Per-request deduplication — a component used multiple times renders its tags only once

- **Import maps for Dev**
  - Support for `import { ref } from 'vue'` style bare imports during Development
  - Production strategy is explicit (`prodStrategy: bundle` or `external`)

- **CSS handling**
  - Global CSS bundle built via a virtual entry file with ordered `@import` statements
  - Default policy enforces safe, root-relative URLs like `/img/foo.png`
  - Optional `allowRelative` mode for advanced layouts
  - `@import` resolution (and failure) handled via `esbuild`

- **Error-first behavior**
  - Invalid YAML → build fails with line/column info
  - Missing JS/CSS declared in config → build fails
  - Invalid or missing manifest in Prod → app startup fails (no silent fallback to Dev mode)

See **`SPEC.md`** for the full formal specification.

---

## Quick Start

1. Add MvcFrontendKit to your web project (NuGet package) and the CLI tool.

2. Generate a default config:

   ```bash
   dotnet frontend init
   ```

   This creates a commented `frontend.config.yaml` at the project root.

3. Minimal example config:

   ```yaml
   mode: single

   webRoot: wwwroot
   appBasePath: "/"

   global:
     js:
       - wwwroot/js/site.js
     css:
       - wwwroot/css/site.css
   ```

4. Use helpers in your layout:

   ```cshtml
   <head>
       <meta charset="utf-8" />
       <title>@ViewData["Title"] - MyApp</title>

       @* Global CSS (Dev: /css, Prod: /dist/css) *@
       @Html.FrontendGlobalStyles()
   </head>
   <body>
       @RenderBody()

       @* Global JS (Dev: /js, Prod: /dist/js) *@
       @Html.FrontendGlobalScripts()

       @RenderSection("Scripts", required: false)
   </body>
   ```

5. In Development:

   - Helpers emit tags for raw files:
     - `/css/site.css?v=...`
     - `/js/site.js?v=...`

6. In Production:

   ```bash
   dotnet frontend check   # diagnostic
   dotnet publish -c Release
   ```

   - `esbuild` runs under the hood and builds `/dist/js/...` and `/dist/css/...`
   - A `frontend.manifest.json` is generated
   - Helpers read the manifest and switch to bundle URLs.

---

## CLI Commands

The CLI tool provides diagnostics and build utilities:

```bash
# Initialize configuration
dotnet frontend init
dotnet frontend init --force    # Overwrite existing

# Validate configuration and assets
dotnet frontend check           # Basic check
dotnet frontend check --verbose # Detailed output
dotnet frontend check --all     # Check all discoverable views
dotnet frontend check --view "Areas/Admin/Settings/Index"  # Diagnose specific view

# Build preview (dry-run)
dotnet frontend build --dry-run # Preview bundles without building
```

### View Diagnostics (`--view` and `--all`)

When a view's JS/CSS isn't loading, use diagnostics to understand why:

```bash
dotnet frontend check --view "Areas/Admin/Settings/Index"
```

Output shows:
- Resolution method (explicit override vs convention)
- Matched convention pattern
- Files found/expected
- Import validation results
- What would be bundled in production

### Import Path Validation

The check command automatically validates relative imports in JS files:

```javascript
// These imports are validated:
import { helper } from './utils.js';
import shared from '../shared/common.js';

// Broken imports are reported:
//   ✗ Broken import in index.js: ./missing-file.js
```

Use `--skip-imports` to disable import validation.

### Build Preview (`--dry-run`)

Preview what will be bundled without actually building:

```bash
dotnet frontend build --dry-run
```

Shows:
- All bundles that would be created
- Input files and sizes
- Estimated output sizes after minification

---

## Debugging

MvcFrontendKit provides two debugging mechanisms to help troubleshoot asset resolution issues during development.

### HTML Debug Comments (automatic)

In the **Development** environment, all HTML helpers automatically emit HTML comments showing resolution details:

```html
<!-- MvcFrontendKit:FrontendViewScripts - Development mode | View: Views/Home/Index | Resolution: Convention | 1 file(s) -->
<!--   wwwroot/js/Home/Index.js -->
<script type="module" src="/js/Home/Index.js?v=638123456789"></script>
```

These comments show:
- **Helper name**: Which helper generated the output
- **Mode**: Development (raw files) or Production (manifest)
- **View key**: The resolved view key (e.g., `Views/Home/Index`)
- **Resolution method**: Override (from config) or Convention (auto-discovered)
- **File list**: All files being loaded

**Note:** Debug comments are automatically suppressed in Production—no configuration needed.

### Debug Panel (`@Html.FrontendDebugInfo()`)

For a visual debug overlay, add `@Html.FrontendDebugInfo()` to your layout:

```cshtml
<body>
    @RenderBody()

    @Html.FrontendGlobalScripts()
    @Html.FrontendViewScripts()

    @* Shows debug panel in Development environment only *@
    @Html.FrontendDebugInfo()
</body>
```

The debug panel displays:
- Current view key
- Manifest key
- Resolved JS/CSS files
- Whether using production manifest or development mode

**Note:** The helper renders nothing in Production environment, so it's safe to leave in your layout.

---

## Upgrading

MvcFrontendKit automatically handles most upgrade scenarios. When you update to a new version:

1. **Version marker detection**: The tool writes a `.mvcfrontendkit-version` file to `wwwroot/dist/`. On each build, it checks if the version has changed and automatically performs a clean build if needed.

2. **SDK cache cleanup**: The build process automatically cleans the ASP.NET SDK's static web assets compression cache (`obj/**/compressed/`) to prevent stale reference errors.

### Manual clean (if needed)

In rare cases where you encounter build errors about missing fingerprinted files, run:

```bash
# Full clean
dotnet clean -c Release
rm -rf wwwroot/dist
rm -rf obj/*/compressed

# Then rebuild
dotnet publish -c Release
```

### gitignore recommendations

Add the version marker to your `.gitignore`:

```
wwwroot/dist/
wwwroot/frontend.manifest.json
```

---

## Repository layout

```text
MvcFrontendKit/
  LICENSE
  README.md
  SPEC.md               # this repo’s internal design spec
  .gitignore
  src/
    MvcFrontendKit/     # core library (config + manifest + helpers)
    MvcFrontendKit.Cli/ # CLI: 'dotnet frontend'
  tests/
    MvcFrontendKit.Tests/
```

---

## Contributing

The behavior of this project is defined in `SPEC.md`.

- Please read `SPEC.md` before proposing changes to core behavior.
- For new features, open an issue and describe:
  - Your scenario
  - How it fits into existing modes (`single`, `areas`, `views`)
  - Any config changes you propose

Pull requests should:

- Keep public APIs consistent with the spec
- Include tests where it makes sense
