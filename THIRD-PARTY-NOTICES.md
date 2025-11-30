# Third-Party Notices

MvcFrontendKit bundles the following open-source projects. We are grateful to their authors and contributors for making their work available.

## Bundled Dependencies

### esbuild

- **Project:** https://github.com/evanw/esbuild
- **Website:** https://esbuild.github.io/
- **Author:** Evan Wallace
- **License:** MIT
- **Description:** An extremely fast bundler for the web

MvcFrontendKit uses esbuild for JavaScript/TypeScript bundling and minification.

Full license text: [src/MvcFrontendKit/runtimes/*/native/LICENSE-esbuild](src/MvcFrontendKit/runtimes/win-x64/native/LICENSE-esbuild)

---

### Dart Sass

- **Project:** https://github.com/sass/dart-sass
- **Website:** https://sass-lang.com/
- **Author:** Google Inc. and the Sass team
- **License:** MIT
- **Description:** The reference implementation of Sass, written in Dart

MvcFrontendKit uses Dart Sass for SCSS/Sass compilation.

Full license text (including all transitive dependencies): [src/MvcFrontendKit/runtimes/*/native/src/LICENSE](src/MvcFrontendKit/runtimes/win-x64/native/src/LICENSE)

---

## NuGet Dependencies

MvcFrontendKit also uses the following NuGet packages (installed via standard package restore):

### YamlDotNet

- **Project:** https://github.com/aaubry/YamlDotNet
- **Website:** https://github.com/aaubry/YamlDotNet
- **Author:** Antoine Aubry
- **License:** MIT
- **Description:** A .NET library for YAML

Used for parsing `frontend.config.yaml` configuration files.

---

## License Compliance

All bundled tools are distributed under permissive open-source licenses (MIT). The full license texts are included in the NuGet package under the `runtimes/` directory structure.

If you have any questions about licensing or attribution, please open an issue at https://github.com/stef-k/MvcFrontendKit/issues.
