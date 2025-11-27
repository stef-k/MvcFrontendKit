# Esbuild Binaries

This directory contains platform-specific esbuild binaries (v0.27.0).

Download from: https://github.com/evanw/esbuild/releases/tag/v0.27.0

Required files:
- `win-x64/native/esbuild.exe` - Windows x64
- `linux-x64/native/esbuild` - Linux x64
- `osx-x64/native/esbuild` - macOS x64 (Intel)
- `osx-arm64/native/esbuild` - macOS ARM64 (Apple Silicon)

These binaries must be included in the NuGet package with appropriate file permissions.
