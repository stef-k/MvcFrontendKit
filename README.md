# MvcFrontendKit

**MvcFrontendKit** is a Node-free frontend bundling toolkit for ASP.NET Core MVC / Razor applications.

It wraps `esbuild` behind a simple `.NET + YAML` workflow:

- **Dev:** Serve raw JS/CSS from `wwwroot` with cache-busting query strings.
- **Prod:** Build fingerprinted bundles (`/dist/js`, `/dist/css`) via `esbuild`, driven by a single `frontend.config.yaml`.
- **No Node / npm required:** Uses a native `esbuild` binary under the hood.
- **Razor-friendly:** Provides HTML helpers / tag helpers for layouts, views, partials, and components.
- **Spec-driven:** Behavior is fully defined in `SPEC.md`.

This is for ASP.NET devs who want modern bundling without committing to the full Node toolchain.

> **Status:** Design/spec complete, implementation starting. Expect breaking changes until v1.0.

---

## Features (high level)

- **Dev vs Prod flow**
  - Dev:
    - Raw JS/CSS from `wwwroot/js` and `wwwroot/css`
    - `type="module"` for JS modules
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
