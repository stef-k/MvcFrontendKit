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

### 1. Install the NuGet package

```bash
dotnet add package MvcFrontendKit
```

This is the only package required for both **runtime** and **production builds**. It includes:
- Razor HTML helpers for your views
- MSBuild targets that automatically run esbuild during `dotnet publish -c Release`
- Platform-specific esbuild binaries (no Node.js required)

### 2. Install the CLI tool (optional)

The CLI provides diagnostic commands (`init`, `check`, `build --dry-run`) for development and CI workflows:

```bash
# Global install (available everywhere)
dotnet tool install --global MvcFrontendKit.Cli

# Or local install (per-project, tracked in .config/dotnet-tools.json)
dotnet new tool-manifest   # if you don't have one yet
dotnet tool install MvcFrontendKit.Cli
```

> **Note:** The CLI is **not required** for builds to work. Production bundling is handled by MSBuild targets in the main package. Install the CLI only if you want commands like `dotnet frontend check` or `dotnet frontend init`.

### 3. Generate configuration

```bash
# Creates frontend.config.yaml with sensible defaults
dotnet frontend init
```

If you don't have the CLI installed, you can copy the template from the [SPEC.md](SPEC.md#32-core-schema-overview) or let the MSBuild target auto-generate a default config on first build.

### 4. Register services in Program.cs

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

// Add MvcFrontendKit services
builder.Services.AddMvcFrontendKit();

var app = builder.Build();
// ... rest of your app configuration
```

### 5. Update your layout

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

### 6. Develop and build

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

- **TypeScript & SCSS Support** (zero-config)
  - TypeScript (`.ts`, `.tsx`) compiled automatically via esbuild
  - SCSS/Sass (`.scss`, `.sass`) compiled automatically via bundled Dart Sass
  - Just use the file extensions - no configuration needed

See **`SPEC.md`** for the full formal specification.

---

## HTML Helpers Reference

MvcFrontendKit provides HTML helpers for rendering script and link tags in your Razor views.

### Layout Helpers

Use these in your `_Layout.cshtml`:

```cshtml
@* Import map for bare module imports (Dev only) *@
@Html.FrontendImportMap()

@* Global CSS (from global.css in config) *@
@Html.FrontendGlobalStyles()

@* View-specific CSS (convention or override) *@
@Html.FrontendViewStyles()

@* Global JS (from global.js in config) *@
@Html.FrontendGlobalScripts()

@* View-specific JS (convention or override) *@
@Html.FrontendViewScripts()

@* Debug panel (renders only in Development) *@
@Html.FrontendDebugInfo()
```

### Component Helper

Use in views to load named components:

```cshtml
@* Load a component defined in frontend.config.yaml *@
@Html.FrontendComponent("datepicker")

@* Load multiple components *@
@Html.FrontendComponent("calendar")
@Html.FrontendComponent("modal")
```

Components are deduplicated per-request - calling `@Html.FrontendComponent("datepicker")` multiple times only renders the tags once.

### Area Helper

For area-specific bundles (when using `areas` mode):

```cshtml
@* In Areas/Admin/_ViewStart.cshtml or layout *@
@Html.FrontendAreaScripts("Admin")
@Html.FrontendAreaStyles("Admin")
```

### Helper Output

**Development:**
```html
<script type="module" src="/js/site.js?v=638123456789"></script>
<link rel="stylesheet" href="/css/site.css?v=638123456789">
```

**Production:**
```html
<script src="/dist/js/global.a1b2c3d4.js"></script>
<link rel="stylesheet" href="/dist/css/global.e5f6g7h8.css">
```

---

## TypeScript & SCSS Support

MvcFrontendKit automatically compiles TypeScript and SCSS files - no configuration required.

### TypeScript

Place `.ts` or `.tsx` files anywhere you would normally place `.js` files:

```text
wwwroot/
  js/
    site.ts              # Global TypeScript entry
    Home/
      Index.ts           # Per-view TypeScript
    components/
      calendar.tsx       # Component with JSX
```

The tool automatically:
- Detects `.ts`/`.tsx` extensions
- Applies esbuild's native TypeScript loader
- Compiles to JavaScript during bundling

**Example config:**
```yaml
global:
  js:
    - wwwroot/js/site.ts    # TypeScript works directly

views:
  overrides:
    "Views/Home/Index":
      js:
        - wwwroot/js/Home/Index.ts
```

### SCSS/Sass

Place `.scss` or `.sass` files anywhere you would normally place `.css` files:

```text
wwwroot/
  css/
    site.scss            # Global SCSS entry
    Home/
      Index.scss         # Per-view SCSS
    components/
      calendar.scss      # Component SCSS
```

The tool automatically:
- Detects `.scss`/`.sass` extensions
- Compiles to CSS using bundled Dart Sass (no Node.js required)
- Passes the compiled CSS to esbuild for bundling and minification

**Example config:**
```yaml
global:
  css:
    - wwwroot/css/site.scss    # SCSS works directly

views:
  overrides:
    "Areas/Admin/Settings/Index":
      css:
        - wwwroot/css/custom/admin-settings.scss
```

### Mixed Projects

You can mix JavaScript/TypeScript and CSS/SCSS freely:

```yaml
global:
  js:
    - wwwroot/js/vendor.js      # Plain JavaScript
    - wwwroot/js/app.ts         # TypeScript
  css:
    - wwwroot/css/reset.css     # Plain CSS
    - wwwroot/css/theme.scss    # SCSS
```

### Development Workflow

For development, use the `dev` command to compile TypeScript and SCSS files:

```bash
# One-time compilation
dotnet frontend dev

# Watch mode (recommended)
dotnet frontend dev --watch
```

This compiles your source files to `.js` and `.css` next to the originals, which are then served by the development helpers with cache-busting.

### Important Notes

- **Development vs Production**: Use `dotnet frontend dev` during development for fast compilation. Production builds (`dotnet publish -c Release`) handle bundling, minification, and fingerprinting automatically.
- **Compilation only**: This provides TS→JS and SCSS→CSS compilation. For editor features like IntelliSense and type checking, install appropriate tools (TypeScript language server, Sass extension, etc.)
- **No tsconfig.json required**: esbuild handles TypeScript compilation without a tsconfig. For strict type checking during development, you can add a tsconfig and run `tsc --noEmit` separately.
- **SCSS imports**: `@import` and `@use` statements in SCSS are resolved relative to the file, and the `cssRoot` directory is added to the load path.

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

The CLI tool provides diagnostics, build utilities, and development compilation:

```bash
# Initialize configuration
dotnet frontend init
dotnet frontend init --force    # Overwrite existing

# Compile TypeScript/SCSS for development
dotnet frontend dev             # One-time compilation
dotnet frontend dev --watch     # Watch mode with auto-recompile
dotnet frontend dev --verbose   # Show detailed output

# Validate configuration and assets
dotnet frontend check           # Basic check
dotnet frontend check --verbose # Detailed output
dotnet frontend check --all     # Check all discoverable views
dotnet frontend check --view "Areas/Admin/Settings/Index"  # Diagnose specific view

# Build preview (dry-run)
dotnet frontend build --dry-run # Preview bundles without building
```

### Development Compilation (`dev`)

The `dev` command compiles TypeScript and SCSS files to JavaScript and CSS for development. This enables you to use TypeScript and SCSS without running production builds during development.

```bash
# Compile all TypeScript/SCSS files from frontend.config.yaml
dotnet frontend dev

# Watch for changes and recompile automatically
dotnet frontend dev --watch

# Show compilation details
dotnet frontend dev --verbose
```

**How it works:**
- Reads `frontend.config.yaml` to find all TypeScript (`.ts`, `.tsx`) and SCSS (`.scss`, `.sass`) files
- Compiles TypeScript to JavaScript using esbuild (fast, native compilation)
- Compiles SCSS to CSS using Dart Sass (bundled, no Node.js required)
- Output files are placed next to source files (e.g., `site.ts` → `site.js`)
- Source maps are generated for debugging

**Watch mode:**
```bash
dotnet frontend dev --watch
```
- Monitors your `jsRoot` and `cssRoot` directories for changes
- Automatically recompiles when `.ts`, `.tsx`, `.scss`, or `.sass` files change
- Shows compilation results in real-time
- Press `Ctrl+C` to stop watching

**Example workflow:**
```bash
# Terminal 1: Start your ASP.NET app
dotnet watch run

# Terminal 2: Watch and compile frontend assets
dotnet frontend dev --watch
```

This gives you:
- Hot reload for C# code (via `dotnet watch`)
- Auto-compilation for TypeScript/SCSS (via `frontend dev --watch`)
- Browser refresh to see changes

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

## Configuration Reference

The `frontend.config.yaml` file controls all bundling behavior. Here's a complete reference:

### Core Settings

```yaml
# Schema version (for future migrations)
configVersion: 1

# Bundling mode: "single", "areas", or "views" (recommended)
mode: views

# Base path for URL generation (for sub-path deployments)
appBasePath: "/"

# Directory paths
webRoot: wwwroot           # Web root directory
jsRoot: wwwroot/js         # JavaScript source directory
cssRoot: wwwroot/css       # CSS source directory
libRoot: wwwroot/lib       # Third-party libraries

# Production output paths
distUrlRoot: /dist         # URL prefix for bundles
distJsSubPath: js          # Subdirectory for JS bundles
distCssSubPath: css        # Subdirectory for CSS bundles
```

### Global Assets

```yaml
global:
  js:
    - wwwroot/js/site.ts         # Global JS (TypeScript supported)
    - wwwroot/js/vendor.js       # Plain JS also works
  css:
    - wwwroot/css/site.scss      # Global CSS (SCSS supported)
    - wwwroot/css/reset.css      # Plain CSS also works
```

### Views Configuration

```yaml
views:
  # Auto-discovery by convention
  jsAutoLinkByConvention: true
  cssAutoLinkByConvention: true

  # JS file conventions (tried in order)
  conventions:
    - viewPattern: "Views/{controller}/{action}"
      scriptBasePattern: "wwwroot/js/{controller}/{action}"

  # CSS file conventions
  cssConventions:
    - viewPattern: "Views/{controller}/{action}"
      cssPattern: "wwwroot/css/{controller}/{action}"

  # Explicit overrides (bypass conventions)
  overrides:
    "Views/Home/Index":
      js:
        - wwwroot/js/home/custom-index.ts
      css:
        - wwwroot/css/home/custom-index.scss
```

### Components

```yaml
components:
  datepicker:
    js:
      - wwwroot/js/components/datepicker.ts
    css:
      - wwwroot/css/components/datepicker.scss
    depends:
      - calendar    # Load calendar component first

  calendar:
    js:
      - wwwroot/js/components/calendar.ts
    css:
      - wwwroot/css/components/calendar.scss
```

Use components in views with `@Html.FrontendComponent("datepicker")`.

### Areas

```yaml
areas:
  Admin:
    js:
      - wwwroot/js/Areas/Admin/admin.ts
    css:
      - wwwroot/css/Areas/Admin/admin.scss
```

### Import Maps (Dev)

```yaml
importMap:
  enabled: true
  prodStrategy: bundle    # "bundle" (default) or "external"
  entries:
    vue: /lib/vue/vue.esm-browser.js
    lodash: /lib/lodash-es/lodash.js
```

Allows bare imports in development:
```javascript
import { ref } from 'vue';
```

### CSS URL Policy

```yaml
cssUrlPolicy:
  # Fail build if relative URLs (../img) found in CSS
  allowRelative: false

  # Resolve @import statements
  resolveImports: true
```

### Output Options

```yaml
output:
  cleanDistOnBuild: true   # Remove dist folder before build
```

### Esbuild Options

```yaml
esbuild:
  jsTarget: es2020         # JavaScript target version
  jsFormat: iife           # "iife" (default) or "esm"
  jsSourcemap: true        # Generate source maps
  cssSourcemap: true       # Generate CSS source maps
```

**jsFormat options:**
- `iife` (default): Wraps bundle in `(function(){...})();` - works with regular `<script>` tags
- `esm`: Preserves ES module syntax - requires `type="module"` on script tags

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
