# ASP.NET Frontend Bundler Tool — Design Spec v1

---

## 1. Purpose & Goals

A small, .NET-native helper tool named **MvcFrontendKit** that:

- Uses **esbuild** under the hood (no npm, no Node.js required on the dev machine or server).
- Is configured via a single, human-editable **YAML config file**.
- Provides a minimal set of **Razor helpers/tag helpers** to output `<script>` and `<link>` tags.
- Supports three bundling strategies **for JS**:
  1. **single** – one JS bundle for the whole app.
  2. **areas** – one JS bundle per Area, plus a site-wide global bundle.
  3. **views** – per-view JS bundles (plus a site-wide bundle).
- Treats CSS with the **same Dev/Prod philosophy** as JS:
  - **Dev:** raw CSS files from `wwwroot/css` (no bundling, no minify).
  - **Prod:** CSS is **bundled + minified** under `/dist/css`, with strict deterministic order.
- Has **sensible default conventions** that work for a typical ASP.NET MVC app with Areas.
- Allows **explicit overrides** in config; explicit always wins over convention.
- Provides an **optional import-map mechanism** so bare module imports (`import { ref } from 'vue'`) work in Dev and Prod consistently.
- Adds **components** as reusable JS/CSS units for partials, with runtime deduplication.
- Plays nicely with `dotnet run` / `dotnet watch` without introducing HMR or SPA-level complexity.

Out of scope (for this version):

- Full SPA-style integration for Vue/React (dev servers, HMR).
- TypeScript / SCSS / Less as first-class upstream source languages (tool operates on JS/CSS).
- CDN hosting, SRI hashes, and advanced incremental build caching (reserved as future extensions).

> **Important clarification**
> All naming/casing logic in this spec applies to **frontend asset paths** (JS/CSS files under `wwwroot`, and references to static resources like `/img` or `/icons`).
> It uses route data (`Area`, `Controller`, `Action`) and **physical view paths** (`*.cshtml`) as input.
> It does **not** rename C# classes or `.cshtml` files; it only derives asset paths and bundles.

---

## 2. High-Level Architecture

### 2.1 Components

1. **Config file**: `frontend.config.yaml`
   - Authored by the developer or generated automatically by the tool.
   - Describes:
     - JS bundling mode (`single`, `areas`, `views`).
     - Physical roots (webRoot, jsRoot, cssRoot, libRoot).
     - Optional `appBasePath` for apps hosted under a sub-path.
     - Conventions for mapping views/areas to JS and CSS assets.
     - Explicit overrides for JS/CSS per view.
     - **Components** (shared bundles used by multiple views/partials).
     - **importMap** entries for bare module imports and Prod strategy.
     - CSS URL policy (root-relative vs relative, import handling).
     - Optional esbuild tuning (targets, sourcemaps).
     - Optional output cleaning behavior.

2. **Build integration** (MSBuild target + .NET host)
   - Runs only for **Release / Production** builds.
   - Reads the YAML config.
   - Discovers:
     - JS entry files (global, per-area, per-view, components).
     - CSS entry files (global, per-view, components).
   - Generates **virtual entry files** for CSS to control concatenation and order.
   - Cleans the `/dist/js` and `/dist/css` output directories (configurable).
   - Invokes **esbuild** via embedded platform-specific binaries:
     - JS: bundles + minifies to `/dist/js/...` with source maps.
     - CSS: bundles + minifies to `/dist/css/...` with source maps.
     - Applies CSS URL policy and import resolution.
   - Writes a **manifest file** mapping logical keys to JS/CSS bundle URLs.

3. **Manifest file**: `frontend.manifest.json`
   - Generated at build/publish (Prod only).
   - Uses explicit prefixes to avoid collisions:
     - `global:js`, `global:css`
     - `view:Views/Home/Index`
     - `view:Areas/Admin/Settings/Index`
     - `area:Root`, `area:Admin`
     - `component:datepicker:js`, `component:datepicker:css`
   - Keys are **case-sensitive** and match the physical view/component names.

4. **Runtime helpers** (HTML helpers / tag helpers)
   - Called from `_Layout.cshtml`, views, and partials.
   - In **Dev**:
     - Output `<script type="module">` tags pointing to raw JS under `/js` with cache-busting query string `?v={lastWriteTimeTicks}`.
     - Output `<link rel="stylesheet">` tags to raw CSS under `/css` with `?v={lastWriteTimeTicks}`.
     - Optionally output `<script type="importmap">` from config so bare imports work.
   - In **Prod**:
     - Use the manifest to output `<script>` and `<link>` tags pointing to hashed bundles under `/dist/js` and `/dist/css`.
     - On startup, if manifest is missing or invalid JSON → application initialization fails with a clear error message (no fallback to Dev-mode behavior).

   - Runtime **deduplication** of components:
     - Per-request, track which components have already been included.
     - Subsequent calls to `FrontendInclude("...")` for the same component emit nothing.

5. **CLI helper** (optional but recommended)
   - `dotnet frontend init`
   - `dotnet frontend check [--verbose]`

> **No HMR**
> This tool does not provide hot module replacement. JS/CSS changes require a normal browser refresh. It is compatible with `dotnet watch` for Razor recompilation, but does not hook into ASP.NET hot reload semantics.

---

## 3. Config File (`frontend.config.yaml`)

### 3.1 Format & lifecycle

- **File name:** `frontend.config.yaml` in the project root.
- If it doesn’t exist:
  - `dotnet frontend init` creates it from a default template, or
  - The build target creates it on first run with a warning.
- If it is deleted:
  - The next `init`/build regenerates the default template and logs a warning.
- YAML parsing errors:
  - Build fails with a clear message including approximate line/column (as provided by the YAML parser).

The FileSystemWatcher only monitors `frontend.config.yaml` for changes. JS/CSS source files themselves are not watched; in Development, changes to JS/CSS are picked up on the next HTTP request because helpers always read the current files from disk and append `?v={File.GetLastWriteTimeUtc(path).Ticks}` for cache busting.

### 3.2 Core schema (overview)

Below is a **full example** configuration. This is also what `dotnet frontend init` generates:
every setting has a short comment describing what it does and, when relevant, what options are available.

```yaml
# Schema version for future migration / breaking changes
configVersion: 1

# JS bundling mode:
# - single : single global JS bundle
# - areas  : per-area JS bundles + site-wide bundle
# - views  : per-view JS bundles + site-wide bundle (default)
mode: views

# Application base path for sub-path deployments:
# - "/"        : app hosted at e.g. https://example.com/
# - "/hr-app"  : app hosted at e.g. https://example.com/hr-app/
# Used mainly when rewriting root-relative URLs in CSS in production.
appBasePath: "/"

# Root physical directories (relative to project root).
# These are used for resolving paths in this config and for validation.
webRoot: wwwroot    # ASP.NET static web root
jsRoot: wwwroot/js  # Application JS source (ES modules)
cssRoot: wwwroot/css  # Application CSS source
libRoot: wwwroot/lib  # 3rd-party libraries (left untouched unless imported/mapped)

# Dist URL structure used in generated HTML and manifest.
# These are virtual URL paths (as seen by the browser), typically under webRoot/dist.
distUrlRoot: /dist       # Base URL for all built assets
distJsSubPath: js        # JS bundles live under /dist/js
distCssSubPath: css      # CSS bundles live under /dist/css

# Output cleaning strategy for dist folder(s).
output:
  # When true (recommended), dist/js and dist/css are cleared before writing new bundles.
  # Prevents orphaned bundles from older builds accumulating over time.
  cleanDistOnBuild: true

# CSS URL & @import policy for files that participate in bundling.
cssUrlPolicy:
  # URL style in CSS:
  #
  # - Root-relative URLs (RECOMMENDED):
  #     url("/object/something.png")
  #   With the default config:
  #     webRoot: wwwroot
  #     appBasePath: "/"
  #   this maps to:
  #     wwwroot/object/something.png
  #
  #   If you later set:
  #     appBasePath: "/myapp"
  #   the bundler rewrites to:
  #     url("/myapp/object/something.png")
  #   but the physical file is STILL wwwroot/object/something.png.
  #
  # - Relative URLs (NOT allowed by default):
  #     url("object/something.png")    # relative to the CSS file itself
  #     url("../img/something.png")    # walks up directories
  #
  #   These break easily after bundling, because the CSS file moves from
  #   e.g. /css/site.css to /dist/css/global.xxxxx.css and the relative
  #   path no longer points to the same place.
  #
  # If false (default), ANY URL starting with "../" in a bundled CSS file
  # causes the build to fail with a clear error. This enforces the safer
  # pattern: always use root-relative URLs (/img, /object, /icons, etc.)
  # for assets that live under wwwroot.
  allowRelative: false

  # Handling of @import in CSS:
  #
  # If true (default):
  #   - esbuild resolves @import statements inline.
  #   - If an imported file is missing, the build fails.
  #
  # If false:
  #   - @import lines are kept as-is in the bundled CSS.
  #   - Use sparingly; this can re-introduce relative-path pitfalls.
  resolveImports: true

# Dev-time import map for bare module specifiers like "vue".
importMap:
  # Enables rendering of a <script type="importmap"> in Development.
  enabled: true

  # Strategy for handling bare module specifiers in Production:
  # - "bundle"   : esbuild bundles these modules into your JS bundles
  #                (default; no runtime import map needed in Prod).
  # - "external" : esbuild treats them as external; a runtime import map
  #                or CDN-provided modules must be available in Prod as well.
  prodStrategy: "bundle"

  # Map bare-module-name -> URL that exists under libRoot (or other static path).
  # These entries are used ONLY for Dev; Prod behavior is controlled by prodStrategy.
  entries:
    # vue: "/lib/vue/vue.esm-browser.js"
    # "vue-router": "/lib/vue-router/vue-router.esm-browser.js"

# Global site assets (always included on every page via FrontendGlobalScripts/Styles).
# NOTE: In Dev mode, JS is loaded with type="module". To expose functions globally:
#   window.myFunc = myFunc;  // Makes myFunc available everywhere
# This is the recommended modern pattern and works in both Dev and Prod.
global:
  # Global JS entry files (in order). Dev uses these as-is; Prod bundles them into one JS bundle.
  js:
    - wwwroot/js/site.js

  # Global CSS files (in order). Dev emits one <link> per file; Prod bundles them into one CSS file.
  css:
    - wwwroot/css/site.css
    # - wwwroot/css/layout.css
    # - wwwroot/css/app-theme.css

# Per-view conventions and explicit overrides for JS & CSS.
views:
  # If true, JS entry files can be discovered by naming conventions (Section 4.4)
  # instead of requiring explicit overrides for every view.
  jsAutoLinkByConvention: true

  # If true, CSS files can be discovered by naming conventions instead of requiring overrides.
  cssAutoLinkByConvention: true

  # JS conventions: how to map a view key to a JS "base path".
  # View keys are generated as: "Views/{Controller}/{Action}" or "Areas/{Area}/{Controller}/{Action}"
  # Conventions are evaluated in order; first match wins.
  conventions:
    # Example: Views/Home/Index -> wwwroot/js/Home/Index*.js (various case attempts)
    - viewPattern: "Views/{Controller}/{Action}"
      scriptBasePattern: "wwwroot/js/{Controller}/{Action}"

    # Example: Areas/Admin/Settings/Index -> wwwroot/js/Areas/Admin/Settings/Index*.js
    - viewPattern: "Areas/{Area}/{Controller}/{Action}"
      scriptBasePattern: "wwwroot/js/Areas/{Area}/{Controller}/{Action}"

  # CSS conventions: how to map a view key to a CSS file.
  # Conventions are evaluated in order; first match wins.
  cssConventions:
    # Example: Views/Home/Index -> wwwroot/css/Home/Index.css
    - viewPattern: "Views/{Controller}/{Action}"
      cssPattern: "wwwroot/css/{Controller}/{Action}.css"

    # Example: Areas/Admin/Settings/Index -> wwwroot/css/Areas/Admin/Settings/Index.css
    - viewPattern: "Areas/{Area}/{Controller}/{Action}"
      cssPattern: "wwwroot/css/Areas/{Area}/{Controller}/{Action}.css"

  # Explicit per-view overrides (JS & CSS), keyed by logical view key.
  # Key format: "Views/Controller/Action" or "Areas/Area/Controller/Action"
  overrides:
    # Example override: Areas/Admin/Views/Settings/Index.cshtml
    "Areas/Admin/Settings/Index":
      # JS files to include for this view (in order). Dev loads directly; Prod bundles.
      js:
        - "wwwroot/js/custom/admin-settings-entry.js"
        - "wwwroot/js/custom/admin-helpers.js"
      # CSS files to include for this view (in order). Dev links directly; Prod bundles.
      css:
        - "wwwroot/css/custom/admin-settings.css"

# Shared components usable from any view/partial.
# These are EXAMPLES; each project should define its own components.
components:
  # A component that has both JS and CSS.
  calendar:
    js:
      - "wwwroot/js/components/calendar.js"
    css:
      - "wwwroot/css/components/calendar.css"

  # A component that depends on another component; both JS and CSS.
  datepicker:
    js:
      - "wwwroot/js/components/datepicker.js"
    css:
      - "wwwroot/css/components/datepicker.css"
    # Dependencies are other component names. They are included first,
    # and deduplicated per request to avoid duplicate tags.
    depends:
      - "calendar"

  # A CSS-only component (no JS).
  utilityCssOnly:
    css:
      - "wwwroot/css/components/utility.css"
    # js: []   # optional, if needed later

# Esbuild tuning (optional). Most projects can keep these defaults.
esbuild:
  # JS target passed to esbuild (e.g., es2017, es2020, esnext).
  jsTarget: "es2020"

  # Whether to generate JS sourcemaps in Prod (external .map files).
  jsSourcemap: true

  # Whether to generate CSS sourcemaps in Prod.
  cssSourcemap: true

  # Reserved for future options (e.g., cdnBaseUrl, bundle size thresholds).
  # cdnBaseUrl: null
```

#### 3.2.1 View keys & case rules

- Logical keys are always **fully explicit**, including `Action`:
  - `Views/Home/Index.cshtml` → `view:Views/Home/Index`
  - `Areas/Admin/Views/Settings/Index.cshtml` → `view:Areas/Admin/Settings/Index`
- URLs like `/Admin/Settings` and `/Admin/Settings/Index` both resolve to the same underlying view key.
- Keys are **case-sensitive** and use the case from the **physical view path** (typically PascalCase for MVC controllers/actions).

---

## 4. JS Modes (single / areas / views)

The tool must support three JS bundling modes, selectable via `mode` in config.

### 4.1 `single` mode

- One global JS bundle used for all pages.
- Dev:
  - Helpers emit `<script type="module">` tags for each `global.js` entry (raw files under `/js`).
- Prod:
  - Tool uses a virtual JS entry that imports each `global.js` in order.
  - esbuild bundles this into a single `global` JS bundle.
  - Manifest key: `global:js` (array of bundle URLs, typically one entry).

### 4.2 `areas` mode

- One global JS bundle (`global:js`) plus one JS bundle per Area.
In v1, `areas` mode is intentionally minimal and provided mainly for simpler applications; for most projects, `views` mode is the recommended and most fully featured configuration.

- Dev:
  - Global assets behave as in `single` mode.
  - Area assets may be wired using a convention or explicit future extensions (this version can keep area-mode minimal).
- Prod:
  - `area:<AreaName>` keys in manifest hold bundle URLs for each area.

### 4.3 `views` mode (default & recommended)

- Global JS + per-view JS.
- Dev:
  - Global: `FrontendGlobalScripts()` emits `<script type="module">` tags for `global.js`.
  - Per-view: `FrontendViewScripts()`:
    - Uses `views.overrides[*].js` when present.
    - Else, attempts to resolve via `views.conventions` and candidate naming.
- Prod:
  - Tool generates a global JS bundle (from `global.js`).
  - Tool generates one per-view JS bundle per view that has non-empty JS (from overrides or conventions).
  - Manifest keys:
    - `global:js`: JS bundle(s) for global scripts.
    - `view:<LogicalViewKey>`: object with `js` array for that view.

All JS files resolved for a view (whether via `views.overrides[*].js` or conventions) are bundled together into a single per-view JS bundle.

### 4.4 JS candidate naming (Dev conventions)

Given a `scriptBasePattern` like `wwwroot/js/Areas/{Area}/{Controller}/{Action}`, the tool:

1. Substitutes `{Area}`, `{Controller}`, `{Action}` using route/view info (case from file path).
2. From this base, attempts filenames in this order (first existing file wins):

   1. **camelCase** action: `mapEditor.js`
   2. **all-lowercase** action: `mapeditor.js`
   3. **PascalCase** action: `MapEditor.js`
   4. **camelCase + Page suffix**: `mapEditorPage.js`
   5. **lowercase + Page suffix**: `mapeditorPage.js`

3. If no candidate exists:
   - Dev: Log a debug warning; view gets only global JS.
   - Prod: No per-view JS bundle generated for that view; build still succeeds.

---

## 5. CSS Behavior

### 5.1 Dev (raw CSS with optional auto-link)

- `FrontendGlobalStyles()`:
  - For each `global.css` entry:
    - Emits `<link rel="stylesheet" href="~/css/...?.v={ticks}">`.
    - Order is exactly the order listed in `global.css`.
- `FrontendViewStyles()`:
  - For the current view key:
    - If `views.overrides[key].css` exists:
      - Emit `<link>` per file, in order.
    - Else, if `cssAutoLinkByConvention == true`:
      - Apply `cssConventions` in order; for the first pattern whose `cssPattern` maps to an existing file, emit `<link>` for that file.
  - View-specific CSS is output **after** global CSS so it can override global styles via cascade.

### 5.2 Prod (bundled + minified CSS)

#### 5.2.1 Global CSS bundles

- Tool creates a virtual CSS entry file with `@import` lines for each `global.css` file, in order:

  ```css
  /* obj/frontend/global-entry.css */
  @import "../wwwroot/css/site.css";
  @import "../wwwroot/css/layout.css";
  @import "../wwwroot/css/app-theme.css";
  ```

- esbuild bundles this into a single global CSS bundle (minified + sourcemap).
- Manifest entry: `global:css` with the resulting bundle URL.

#### 5.2.2 Per-view CSS bundles

- For each view key:

  1. Build a list of CSS files:
     - From `views.overrides[key].css` if present.
     - If not, and `cssAutoLinkByConvention == true`, from `cssConventions` (first pattern that yields an existing CSS file).
  2. If the list is non-empty:
     - Generate a per-view virtual CSS entry file, with `@import` lines in that order.
     - Bundle via esbuild into a **single** per-view CSS bundle.
     - Manifest entry: `view:<Key>.css` array with that bundle URL.

#### 5.2.3 CSS URL policy & sub-path deployments

- `appBasePath` defines the base path of the application (`"/"` or e.g. `"/hr-app"`).
- Root-relative URLs in CSS (e.g., `url("/img/logo.png")`, `url('/icons/foo.svg')`) that appear in any CSS file being bundled:
  - If `appBasePath == "/"` → left unchanged.
  - If `appBasePath != "/"` → rewritten during bundling to prefix `appBasePath`:
    - `url("/img/logo.png")` → `url("/hr-app/img/logo.png")`.
- `cssUrlPolicy.allowRelative`:
  - If `false` (default):
    - Any URL that starts with `"../"` in CSS being bundled (including imported CSS via `@import`) causes build failure, with a clear error.
  - If `true`:
    - Relative URLs are allowed; behavior depends on how directory structures align between `wwwroot/css` and `/dist/css`. This is considered an advanced scenario.
- `cssUrlPolicy.resolveImports`:
  - If `true` (default):
    - esbuild resolves `@import` statements inline.
    - Missing targets cause build failure.
  - If `false`:
    - `@import` statements remain in the bundle as-is; use sparingly.

---

## 6. Manifest: `frontend.manifest.json`

### 6.1 Key namespace

To avoid collisions and make lookups explicit, manifest keys follow this schema:

- `global:js` – array of JS bundle URLs (global scripts).
- `global:css` – array of CSS bundle URLs (global styles).
- `view:<LogicalViewKey>` – object with optional `js` and `css` arrays.
  - `view:Views/Home/Index`
  - `view:Areas/Admin/Settings/Index`
- `area:<AreaName>` – object with optional `js` array (for areas mode).
- `component:<ComponentName>:js` – array of JS bundle URLs for that component.
- `component:<ComponentName>:css` – array of CSS bundle URLs for that component.

### 6.2 Example manifest (illustrative)

```json
{
  "global:js": [
    "/dist/js/site.abcd1234.js"
  ],
  "global:css": [
    "/dist/css/global.5555eeee.css"
  ],

  "view:Views/Home/Index": {
    "js": ["/dist/js/home-index.1111aaaa.js"],
    "css": ["/dist/css/home-index.9999bbbb.css"]
  },

  "view:Areas/Manager/Groups/Map": {
    "js": ["/dist/js/manager-groups-map.2222cccc.js"],
    "css": ["/dist/css/manager-groups-map.7777dddd.css"]
  },

  "area:Root": {
    "js": ["/dist/js/root.aaaa1111.js"]
  },
  "area:Admin": {
    "js": ["/dist/js/admin.bbbb2222.js"]
  },

  "component:calendar:js": [
    "/dist/js/component-calendar.1234abcd.js"
  ],
  "component:calendar:css": [
    "/dist/css/component-calendar.5678efef.css"
  ],

  "component:datepicker:js": [
    "/dist/js/component-datepicker.9999aaaa.js"
  ],
  "component:datepicker:css": [
    "/dist/css/component-datepicker.dddd3333.css"
  ]
}

Each component produces its own JS and/or CSS bundle; components are not automatically merged into shared bundles. If multiple components share underlying code, esbuild may emit internal shared chunks, but the manifest still exposes one logical bundle per component.
```

---

## 7. Runtime Helper API

Helpers can be implemented as HTML helpers and/or tag helpers.

### 7.1 Typical layout usage

Example `_Layout.cshtml` wiring:

```cshtml
<head>
    ...
    @Html.FrontendImportMap()       <!-- Dev only if enabled -->
    @Html.FrontendGlobalStyles()
    @Html.FrontendViewStyles()
    @RenderSection("Styles", required: false)
</head>
<body>
    @RenderBody()

    @Html.FrontendGlobalScripts()
    @Html.FrontendViewScripts()
    @RenderSection("Scripts", required: false)
</body>
```

### 7.2 `FrontendImportMap()`

- Dev:
  - If `importMap.enabled == true`, emits:

    ```html
    <script type="importmap">
    {
      "imports": {
        "vue": "/lib/vue/vue.esm-browser.js",
        "vue-router": "/lib/vue-router/vue-router.esm-browser.js"
      }
    }
    </script>
    ```

    (using actual entries from config).
- Prod:
  - By default, emits nothing.
  - How bare imports are handled in Prod:
    - If `prodStrategy: "bundle"`:
      - esbuild bundles those dependencies into JS bundles; no import map needed at runtime.
    - If `prodStrategy: "external"`:
      - esbuild treats them as external; the application is responsible for injecting an appropriate `<script type="importmap">` in Prod (e.g., via a custom layout partial) or relying on CDN modules.

### 7.3 `FrontendGlobalStyles()`

- Dev:
  - Emits `<link rel="stylesheet" href="~/css/...?.v={ticks}">` for each `global.css` entry, in config order.
- Prod:
  - Looks up `global:css` in manifest; emits `<link>` tags to the bundled CSS.
  - If manifest is missing or `global:css` absent when it should exist, this should have been caught as a build/startup error.

### 7.4 `FrontendViewStyles()`

- Dev:
  - Resolves view key (`Views/Controller/Action` or `Areas/Area/Controller/Action`).
  - Uses `views.overrides[*].css` or `cssConventions` (as described in Section 5).
- Prod:
  - Looks up `view:<Key>` in manifest; if `css` array exists, emits `<link>` tags for those bundles.
  - If no `css` array → no view-specific CSS is emitted.

### 7.5 `FrontendGlobalScripts()`

- Dev:
  - Emits `<script type="module" src="~/js/...?.v={ticks}"></script>` for each `global.js` entry.
- Prod:
  - Uses `global:js` from manifest to emit `<script src="..."></script>` tags.

### 7.6 `FrontendViewScripts()`

- Dev:
  - Resolves view key.
  - Uses `views.overrides[*].js` or JS conventions + candidate naming (Section 4).
  - Emits `<script type="module" src="~/js/...?.v={ticks}"></script>` tags.
- Prod:
  - Uses `view:<Key>` from manifest.
  - If `js` exists, emits `<script src="..."></script>` for each.

### 7.7 Components helper & deduplication

`@Html.FrontendInclude("datepicker")`

- Components are optional building blocks; config example is illustrative only.
- Both JS and CSS are optional for each component.

#### 7.7.1 Dedup behavior

- For each HTTP request, helper keeps a per-request set of included component names (via `HttpContext.Items` or a scoped service).
- If `"datepicker"` is already in that set:
  - Helper returns an empty `IHtmlContent` (no tags).
- If not:
  - It is added to the set.
  - Dependencies (if any) are included first.
  - Then JS/CSS for the component are emitted.

This guarantees that components used in multiple partials don’t cause duplicate `<script>`/`<link>` tags on a single page.

#### 7.7.2 Dependencies

- If a component defines `depends: ["calendar", "otherWidget"]`:
  - Helper ensures `FrontendInclude("calendar")` and `FrontendInclude("otherWidget")` are processed first.
  - Dedup logic prevents cycles from causing infinite recursion, but:
    - `dotnet frontend check --verbose` should analyze component dependencies and warn about cycles.

---

## 8. Dev vs Prod Behavior (recap)

### Dev (“raw mode”)

- No bundling, no minify.
- Raw JS loaded from `/js`.
- Raw CSS loaded from `/css`.
- `?v={File.GetLastWriteTimeUtc(path).Ticks}` appended to avoid stale caching.
- Optional import map for bare imports.
- Config changes and new files are seen on the next request.

### Prod (Release builds)

- Build/publish:
  - Reads YAML config.
  - Validates config and file existence.
  - Enforces CSS URL policy and import resolution rules.
  - Cleans `dist/js` and `dist/css` (unless disabled).
  - Builds JS and CSS bundles with esbuild (bundle + minify + sourcemap).
  - Writes bundles to `/dist/js` and `/dist/css`.
  - Writes `frontend.manifest.json`.
- Application startup:
  - Loads manifest.
  - If manifest is missing, invalid JSON, or obviously mismatched to config → startup fails with a clear error.

---

## 9. Esbuild Integration & Distribution

### 9.1 Esbuild version & configuration

- Tool ships with a pinned stable esbuild version via NuGet, with RID-specific native binaries.
- .NET host selects correct binary based on RuntimeIdentifier; no Node/npm required.
- Default JS target is `es2020` (modern browsers); can be overridden via `esbuild.jsTarget`.

### 9.2 Browser compatibility

- ES modules and import maps require reasonably modern browsers.
- README should note approximate minima:
  - Chrome 89+, Firefox 108+, Safari 16.4+, Edge 89+.
- For older browser support (e.g., IE11), a separate transpilation pipeline (e.g. TS + Babel) is required and considered out-of-scope.

### 9.3 External assets config

- esbuild is configured so that root-relative `/img/*` and `/icons/*` URLs in CSS are treated as external and preserved (subject to `appBasePath` rewriting).
- This keeps static resources decoupled from the bundle content.

---

## 10. CLI Commands

### 10.1 `dotnet frontend init`

- Creates `frontend.config.yaml` if missing, using a template identical in structure and comments to Section 3.2.
- Does not overwrite existing config unless `--force` is specified.

### 10.2 `dotnet frontend check [--verbose]`

- Loads YAML config and validates:
  - `global.js` / `global.css` paths.
  - `views.overrides[*].js` / `css` paths.
  - `components[*].js` / `css` paths.
- Checks CSS files in bundling scope:
  - Warns or errors on disallowed `../` URLs based on `cssUrlPolicy`.
- With `--verbose`, additionally:
  - Prints mapping of view keys to Dev assets and Prod bundle keys.
  - Prints component graph (including `depends`) and warns about cycles.
  - May optionally inspect esbuild’s module graph to warn about circular JS imports (while still allowing them).
  - Optionally warns if any predicted bundle exceeds a configurable size threshold (e.g., 500KB).

---

## 11. Migration Guidance (summary)

Projects migrating from:

- **Plain `<script>`/`<link>` tags**
  - Start by moving global assets into `global.js` and `global.css`.
  - Wire `_Layout` to use `FrontendGlobalScripts()` and `FrontendGlobalStyles()`.
  - Gradually move per-view scripts into:
    - Either `views.conventions`, or
    - `views.overrides` for special cases.
- **ASP.NET Bundling & Minification / WebOptimizer**
  - Translate existing bundles into the YAML schema (`global`, `views`, `components`).
  - Use `dotnet frontend check --verbose` to verify asset-to-bundle mapping.
  - Remove old bundling configuration once new pipeline is validated.

---

## 12. Reserved Extension Points

To keep room for future evolution:

- `configVersion` — for evolving config schema.
- Reserved top-level keys:
  - `preprocessors` — for TypeScript/SCSS integration later.
  - `cdn` — for CDN hosting, SRI, and related features.

These keys must not be reused for other semantics in this version, so they can be safely added in future versions.

---

This spec describes a general-purpose .NET-centric frontend bundling workflow that can be applied to a wide variety of ASP.NET MVC/Razor projects, not tied to any particular application domain. All examples (views, components like `calendar`/`datepicker`) are illustrative and are meant to be adapted or replaced per project.

## 13. Packaging & Distribution

The tool ships from v1 as two NuGet packages:

1. **Library package**: `MvcFrontendKit`
   - Type: class library (TFMs such as `net8.0` / `netstandard2.1` to be finalized at implementation time).
   - Referenced by ASP.NET Core MVC / Razor apps via:
     - `dotnet add package MvcFrontendKit`
   - Contains:
     - Configuration and manifest loaders.
     - Razor HTML helpers / tag helpers (`FrontendGlobalScripts`, `FrontendViewScripts`, `FrontendGlobalStyles`, `FrontendViewStyles`, `FrontendImportMap`, `FrontendInclude`, etc.).
     - Runtime services (manifest cache, per-request component deduplication, config access).
     - MSBuild `.targets` / `.props` that:
       - Hook into `dotnet publish` / Release builds.
       - Invoke the internal .NET host that runs `esbuild` according to `frontend.config.yaml`.
   - Required for applications to use MvcFrontendKit at runtime.

2. **CLI NuGet package (dotnet tool)**: `MvcFrontendKit.Cli`
   - Type: `DotNetTool` package.
   - Optional for runtime: web apps do **not** need this package to run in Production, only the library is required.
   - Strongly recommended for development and CI workflows.
   - Exposes the `frontend` command:
     - `dotnet frontend init` — scaffold a fully commented `frontend.config.yaml`.
     - `dotnet frontend check [--verbose]` — validate config, discover assets, and report problems before a build/publish.
   - Shares the same core parsing / validation logic as the library (via a shared project or internal package) so semantics stay in sync.

Versioning & compatibility:

- The `configVersion` field in `frontend.config.yaml` is used to guide schema evolution and detect breaking changes.
- NuGet package versions follow semantic versioning:
  - `1.x` corresponds to the behavior specified in this v1 spec.
  - `2.x` may introduce breaking changes to the config schema or core behavior and must be reflected by:
    - Updating this spec.
    - Updating the generated config template.
    - Providing migration notes in the README.
- The CLI and library packages should keep major versions aligned (e.g., `MvcFrontendKit` 1.x works with `MvcFrontendKit.Cli` 1.x).
