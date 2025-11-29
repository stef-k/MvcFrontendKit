# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**MvcFrontendKit** is a Node-free frontend bundling toolkit for ASP.NET Core MVC / Razor applications. It wraps `esbuild` behind a simple `.NET + YAML` workflow without requiring Node.js or npm.

**Status:** Design/spec complete, implementation starting. Expect breaking changes until v1.0.

## Architecture

### Core Components

1. **Library package** (`MvcFrontendKit`) - Main class library containing:
   - Configuration and manifest loaders
   - Razor HTML helpers / tag helpers for emitting script/link tags
   - Runtime services (manifest cache, per-request component deduplication)
   - MSBuild targets that invoke esbuild during Release/Publish builds
   - **Auto-generates default `frontend.config.yaml` on package installation** if missing

2. **CLI package** (`MvcFrontendKit.Cli`) - Optional dotnet tool package:
   - `dotnet frontend init` - scaffolds `frontend.config.yaml`
   - `dotnet frontend check [--verbose]` - validates config and discovers assets

3. **Configuration** (`frontend.config.yaml`) - Single YAML file controlling:
   - Bundling mode: `single`, `areas`, or `views` (default)
   - Global JS/CSS assets
   - Per-view conventions and overrides
   - Named components (reusable JS/CSS chunks)
   - Import maps for bare module specifiers
   - CSS URL policy (root-relative vs relative)
   - Esbuild options

4. **Manifest** (`frontend.manifest.json`) - Generated during Prod builds:
   - Maps logical keys to fingerprinted bundle URLs
   - Key format: `global:js`, `global:css`, `view:Views/Home/Index`, `component:datepicker:js`, etc.
   - Keys are case-sensitive matching physical view paths

### Dev vs Prod Behavior

**Development:**
- Raw JS/CSS served from `wwwroot/js` and `wwwroot/css`
- Cache-busting via `?v={File.GetLastWriteTimeUtc(path).Ticks}`
- `type="module"` for JS
- Optional import map for bare imports
- No bundling, no minification

**Production:**
- Bundled + minified JS/CSS in `/dist/js` and `/dist/css`
- Fingerprinted filenames
- Helpers read manifest to emit bundle URLs
- Build fails if manifest is missing or invalid (no silent fallback)

### Bundling Modes

1. **single** - One global JS bundle for entire app
2. **areas** - Global bundle + one per Area (minimal in v1)
3. **views** (recommended) - Global bundle + per-view bundles driven by conventions/overrides

## Important Design Principles

### Config Auto-Generation
- **On NuGet package install:** If `frontend.config.yaml` doesn't exist, MSBuild targets automatically generate it with sensible defaults
- **CLI `init` command:** Optionally, devs can run `dotnet frontend init` to regenerate or use `--force` to overwrite
- **Default template:** Matches the full example in SPEC.md Section 3.2 with comprehensive comments
- Developers should NOT need to manually create this file - it appears automatically

### Error-First Philosophy
- Invalid YAML → build fails with line/column info
- Missing JS/CSS declared in config → build fails
- Invalid/missing manifest in Prod → app startup fails (no silent fallback)

### CSS URL Policy
- **Default (recommended):** Root-relative URLs only (`url("/img/foo.png")`)
- Build **fails** if relative URLs (`../`) detected in bundled CSS (unless `cssUrlPolicy.allowRelative: true`)
- `appBasePath` rewrites root-relative URLs for sub-path deployments (e.g., `/hr-app/img/foo.png`)

### View Key Resolution
- Logical keys are fully explicit with Action: `Views/Home/Index`, `Areas/Admin/Settings/Index`
- Keys are **case-sensitive** using physical view path casing (typically PascalCase)
- `/Admin/Settings` and `/Admin/Settings/Index` both resolve to same view key

### JS Candidate Naming (Dev Conventions)
For a base pattern like `wwwroot/js/{Controller}/{Action}`, attempts in order:
1. camelCase: `mapEditor.js`
2. lowercase: `mapeditor.js`
3. PascalCase: `MapEditor.js`
4. camelCase+Page: `mapEditorPage.js`
5. lowercase+Page: `mapeditorPage.js`

### Components
- Named reusable JS/CSS units (e.g., `datepicker`, `calendar`)
- Support dependency graph with cycle detection
- Per-request deduplication via `HttpContext.Items`
- Both JS and CSS are optional per component

## Key Files

- **SPEC.md** - Complete formal specification (read before making core behavior changes)
- **README.md** - User-facing documentation
- **frontend.config.yaml** - Single source of truth for bundling behavior (auto-generated on install)
- **frontend.manifest.json** - Generated in Prod builds, consumed by helpers at runtime

## Spec Compliance

**All implementation must strictly follow SPEC.md.** The spec defines:
- Config schema and validation rules
- Dev vs Prod behavior for each mode
- Manifest key namespace and structure
- Helper API contracts
- CSS URL policy enforcement
- Error handling requirements

When implementing features:
1. Read the relevant section of SPEC.md first
2. Implement exactly as specified
3. Do not add undocumented features or change core semantics
4. For new features, propose spec changes via issue first

## Preprocessor Support (TypeScript & SCSS)

MvcFrontendKit supports automatic compilation of TypeScript and SCSS files:

- **TypeScript** (`.ts`, `.tsx`): Auto-detected and compiled via esbuild's native TypeScript loader
- **SCSS/Sass** (`.scss`, `.sass`): Auto-detected and compiled via bundled Dart Sass compiler

This is zero-config - just use the file extensions and the tool handles compilation automatically.

## Reserved Extension Points

Do not use these config keys (reserved for future versions):
- `cdn` - for CDN hosting, SRI, etc.

## Browser Compatibility

Targets modern browsers with ES modules and import map support:
- Chrome 89+, Firefox 108+, Safari 16.4+, Edge 89+
- IE11 and older browsers require separate transpilation pipeline (out of scope)

## Package Structure

- `src/MvcFrontendKit/` - Core library (config, manifest, helpers)
- `src/MvcFrontendKit.Cli/` - CLI tool
- `tests/MvcFrontendKit.Tests/` - Test suite

## Implementation Details

### Config Lifecycle
1. **First install:** MSBuild targets check for `frontend.config.yaml` at project root
2. **If missing:** Generate from embedded template resource with all options commented
3. **If exists:** Leave untouched (user may have customized)
4. **If deleted:** Next build regenerates with warning logged

### Default Config Template
The auto-generated config must match SPEC.md Section 3.2 exactly:
- `mode: views` (recommended default)
- `webRoot: wwwroot`, `jsRoot: wwwroot/js`, `cssRoot: wwwroot/css`
- Sensible convention patterns for Views and Areas
- Example components (commented out)
- All options with inline comments explaining choices

### MSBuild Integration
- Targets only run for **Release/Publish** builds (not Debug)
- Config generation happens in early target (before build)
- esbuild invocation happens during publish
- Esbuild binaries are RID-specific, embedded in NuGet package

## Development Notes

- `configVersion` field enables schema evolution and migration detection
- Esbuild is shipped as RID-specific native binaries via NuGet (no Node/npm required)
- Root-relative URLs in CSS (`/img/*`, `/icons/*`) are treated as external by esbuild
- FileSystemWatcher only monitors `frontend.config.yaml` for changes (not individual JS/CSS files)
- Per-request state tracked via `HttpContext.Items` or scoped DI service
