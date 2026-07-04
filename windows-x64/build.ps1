# Builds TextPad for Windows x64 (64-bit)
# macOS source lives alongside at ..\macos\
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$project = Join-Path $root "TextPad\TextPad.csproj"
$outDir = Join-Path $root "dist\x64"

& (Join-Path $root "build_icon.ps1")

Write-Host "Building TextPad for win-x64..." -ForegroundColor Cyan
dotnet publish $project `
    -c Release `
    -r win-x64 `
    --self-contained false `
    -p:Platform=x64 `
    -o $outDir

if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host ""
Write-Host "Build complete:" -ForegroundColor Green
Write-Host "  $outDir\TextPad.exe"