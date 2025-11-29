# Native Binaries

This directory contains platform-specific native binaries for esbuild and Dart Sass.

## Esbuild (v0.27.0)

Download from: https://github.com/evanw/esbuild/releases/tag/v0.27.0

Required files:
- `win-x64/native/esbuild.exe` - Windows x64
- `linux-x64/native/esbuild` - Linux x64
- `linux-arm64/native/esbuild` - Linux ARM64
- `osx-x64/native/esbuild` - macOS x64 (Intel)
- `osx-arm64/native/esbuild` - macOS ARM64 (Apple Silicon)

## Dart Sass (v1.94.2)

Download from: https://github.com/sass/dart-sass/releases/tag/1.94.2

To update Dart Sass binaries, run:
```powershell
.\scripts\download-sass.ps1
```

Required files:
- `win-x64/native/sass.bat` - Windows x64 (batch script + src/dart.exe + src/sass.snapshot)
- `linux-x64/native/sass` - Linux x64 (shell script + src/dart + src/sass.snapshot)
- `linux-arm64/native/sass` - Linux ARM64 (shell script + src/dart + src/sass.snapshot)
- `osx-x64/native/sass` - macOS x64 (shell script + src/dart + src/sass.snapshot)
- `osx-arm64/native/sass` - macOS ARM64 (shell script + src/dart + src/sass.snapshot)

## Notes

- These binaries must be included in the NuGet package with appropriate file permissions
- On Unix systems (Linux/macOS), the binaries need executable permissions (`chmod +x`)
- The Dart Sass binaries include a `src/` subdirectory with the Dart runtime and snapshot
