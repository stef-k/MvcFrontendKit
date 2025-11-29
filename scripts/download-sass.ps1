# Download Dart Sass binaries for all supported platforms
# Run this script to update the Sass binaries in the runtimes folder

$ErrorActionPreference = "Stop"

$version = "1.94.2"
$baseUrl = "https://github.com/sass/dart-sass/releases/download/$version"
$runtimesDir = "$PSScriptRoot\..\src\MvcFrontendKit\runtimes"

$downloads = @(
    @{ RID = "win-x64"; Archive = "dart-sass-$version-windows-x64.zip"; Exe = "sass.bat" },
    @{ RID = "linux-x64"; Archive = "dart-sass-$version-linux-x64.tar.gz"; Exe = "sass" },
    @{ RID = "linux-arm64"; Archive = "dart-sass-$version-linux-arm64.tar.gz"; Exe = "sass" },
    @{ RID = "osx-x64"; Archive = "dart-sass-$version-macos-x64.tar.gz"; Exe = "sass" },
    @{ RID = "osx-arm64"; Archive = "dart-sass-$version-macos-arm64.tar.gz"; Exe = "sass" }
)

$tempDir = "$env:TEMP\dart-sass-download"
if (Test-Path $tempDir) {
    Remove-Item $tempDir -Recurse -Force
}
New-Item -ItemType Directory -Path $tempDir | Out-Null

foreach ($item in $downloads) {
    $rid = $item.RID
    $archive = $item.Archive
    $exe = $item.Exe
    $url = "$baseUrl/$archive"
    $archivePath = "$tempDir\$archive"
    $nativeDir = "$runtimesDir\$rid\native"

    Write-Host "Downloading Sass for $rid..." -ForegroundColor Cyan

    # Download
    Invoke-WebRequest -Uri $url -OutFile $archivePath -UseBasicParsing

    # Extract
    $extractDir = "$tempDir\$rid"
    if (Test-Path $extractDir) {
        Remove-Item $extractDir -Recurse -Force
    }
    New-Item -ItemType Directory -Path $extractDir | Out-Null

    if ($archive.EndsWith(".zip")) {
        Expand-Archive -Path $archivePath -DestinationPath $extractDir
    } else {
        # For tar.gz, use Windows native tar command explicitly
        $tarExe = "$env:SystemRoot\System32\tar.exe"
        if (Test-Path $tarExe) {
            Push-Location $extractDir
            try {
                & $tarExe -xzf $archivePath
            } finally {
                Pop-Location
            }
        } else {
            # Fallback: extract .gz first, then .tar using .NET
            Write-Host "  Using .NET extraction for tar.gz..." -ForegroundColor Yellow
            $gzPath = $archivePath
            $tarPath = $archivePath -replace '\.gz$', ''

            # Decompress gz
            $gzStream = [System.IO.File]::OpenRead($gzPath)
            $tarStream = [System.IO.File]::Create($tarPath)
            $decompressor = New-Object System.IO.Compression.GZipStream($gzStream, [System.IO.Compression.CompressionMode]::Decompress)
            $decompressor.CopyTo($tarStream)
            $decompressor.Close()
            $tarStream.Close()
            $gzStream.Close()

            # Use tar on decompressed file
            Push-Location $extractDir
            try {
                & tar -xf $tarPath
            } finally {
                Pop-Location
            }
        }
    }

    # Find the dart-sass folder (it extracts to dart-sass/)
    $sassDir = Get-ChildItem -Path $extractDir -Directory | Where-Object { $_.Name -like "dart-sass*" } | Select-Object -First 1

    if (-not $sassDir) {
        Write-Error "Could not find dart-sass directory in extracted archive for $rid"
        continue
    }

    # Create native directory if it doesn't exist
    if (-not (Test-Path $nativeDir)) {
        New-Item -ItemType Directory -Path $nativeDir | Out-Null
    }

    # Copy all contents of dart-sass folder to native directory
    Write-Host "  Copying to $nativeDir..." -ForegroundColor Gray
    Copy-Item -Path "$($sassDir.FullName)\*" -Destination $nativeDir -Recurse -Force

    Write-Host "  Done!" -ForegroundColor Green
}

# Cleanup
Remove-Item $tempDir -Recurse -Force

Write-Host "`nSass binaries downloaded successfully!" -ForegroundColor Green
Write-Host "Version: $version" -ForegroundColor Yellow
