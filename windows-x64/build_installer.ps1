# Builds a signed self-contained 64-bit TextPad installer for Windows.
param(
    [switch]$SkipSign
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$project = Join-Path $root "TextPad\TextPad.csproj"
$installerScript = Join-Path $root "installer\TextPad.iss"
$toolsDir = Join-Path $root "tools"
$innoDir = Join-Path $toolsDir "InnoSetup6"
$iscc = Join-Path $innoDir "ISCC.exe"
$signScript = Join-Path $root "scripts\sign.ps1"

. $signScript

function Get-ProjectVersion {
    param([string]$ProjectPath)
    [xml]$xml = Get-Content $ProjectPath
    $version = $xml.Project.PropertyGroup.Version
    if ([string]::IsNullOrWhiteSpace($version)) {
        throw "Could not read <Version> from $ProjectPath"
    }
    return $version
}

function Resolve-InnoSetupCompiler {
    param([string]$CompilerPath)

    $candidates = @(
        $CompilerPath,
        "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
        "C:\Program Files\Inno Setup 6\ISCC.exe"
    )
    foreach ($candidate in $candidates) {
        if (Test-Path $candidate) {
            return $candidate
        }
    }

    Write-Host "Inno Setup not found. Downloading compiler..." -ForegroundColor Yellow
    New-Item -ItemType Directory -Force -Path $toolsDir | Out-Null

    $installerPath = Join-Path $toolsDir "innosetup-installer.exe"
    $installerUrls = @(
        "https://github.com/jrsoftware/issrc/releases/download/is-6_7_3/innosetup-6.7.3.exe",
        "https://files.jrsoftware.org/is/6/innosetup-6.3.3.exe"
    )

    $downloaded = $false
    foreach ($installerUrl in $installerUrls) {
        try {
            Invoke-WebRequest -Uri $installerUrl -OutFile $installerPath
            $downloaded = $true
            break
        }
        catch {
            Write-Host "Download failed: $installerUrl" -ForegroundColor DarkYellow
        }
    }

    if ($downloaded) {
        Start-Process -FilePath $installerPath -ArgumentList @(
            "/VERYSILENT",
            "/SUPPRESSMSGBOXES",
            "/DIR=$innoDir",
            "/NORESTART"
        ) -Wait
    }
    elseif (Get-Command winget -ErrorAction SilentlyContinue) {
        Write-Host "Falling back to winget for Inno Setup..." -ForegroundColor Yellow
        winget install --id JRSoftware.InnoSetup -e --silent --accept-package-agreements --accept-source-agreements | Out-Host
    }

    if (-not (Test-Path $CompilerPath)) {
        throw "Inno Setup installation failed. Expected compiler at $CompilerPath"
    }

    return $CompilerPath
}

function Build-Installer {
    param(
        [string]$Compiler,
        [string]$Version
    )

    $payloadDir = Join-Path $root "dist\x64-installer"
    $outputBase = "TextPad-$Version-win-x64-Setup"

    Write-Host "Publishing self-contained TextPad for win-x64..." -ForegroundColor Cyan

    if (Test-Path $payloadDir) {
        Remove-Item -Recurse -Force $payloadDir
    }

    dotnet publish $project `
        -c Release `
        -r win-x64 `
        --self-contained true `
        -p:Platform=x64 `
        -p:DebugType=None `
        -p:DebugSymbols=false `
        -o $payloadDir

    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }

    if (-not $SkipSign) {
        Write-Host "Signing win-x64 payload..." -ForegroundColor Cyan
        Sign-TextPadPayload -PayloadDir $payloadDir
    }

    Write-Host "Building win-x64 installer..." -ForegroundColor Cyan
    & $Compiler `
        "/DMyAppVersion=$Version" `
        "/DMyAppSource=$payloadDir" `
        $installerScript

    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }

    $setupPath = Join-Path $root "dist\$outputBase.exe"
    if (-not $SkipSign) {
        Write-Host "Signing installer..." -ForegroundColor Cyan
        Sign-TextPadInstaller -InstallerPath $setupPath
    }

    return $setupPath
}

$version = Get-ProjectVersion -ProjectPath $project
& (Join-Path $root "build_icon.ps1")
$iscc = Resolve-InnoSetupCompiler -CompilerPath $iscc

$setupPath = Build-Installer -Compiler $iscc -Version $version

Write-Host ""
Write-Host "Installer build complete:" -ForegroundColor Green
Write-Host "  $setupPath"

if (-not $SkipSign) {
    if ($env:TEXTPAD_SIGN_PFX -or $env:TEXTPAD_SIGN_THUMBPRINT) {
        Write-Host ""
        Write-Host "Signed with configured release certificate." -ForegroundColor Green
    }
    else {
        Write-Host ""
        Write-Host "Signed with a local dev certificate (SmartScreen will still warn until you use a trusted CA cert)." -ForegroundColor Yellow
        Write-Host "For release signing, set TEXTPAD_SIGN_PFX + TEXTPAD_SIGN_PASSWORD or TEXTPAD_SIGN_THUMBPRINT and rebuild." -ForegroundColor Yellow
    }
}