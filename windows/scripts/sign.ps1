function Resolve-SignTool {
    $candidates = @(
        ${env:TEXT_PAD_SIGNTOOL},
        ${env:SIGNTOOL_PATH}
    )

    $searchRoots = @(
        "${env:ProgramFiles(x86)}\Windows Kits\10\bin",
        "${env:ProgramFiles}\Windows Kits\10\bin"
    )

    foreach ($root in $searchRoots) {
        if (-not (Test-Path $root)) { continue }
        $match = Get-ChildItem -Path $root -Recurse -Filter signtool.exe -ErrorAction SilentlyContinue |
            Sort-Object FullName -Descending |
            Select-Object -First 1
        if ($match) {
            $candidates += $match.FullName
        }
    }

    foreach ($candidate in $candidates) {
        if (-not [string]::IsNullOrWhiteSpace($candidate) -and (Test-Path $candidate)) {
            return $candidate
        }
    }

    return $null
}

function Resolve-SigningCertificate {
    if (-not [string]::IsNullOrWhiteSpace($env:TEXTPAD_SIGN_PFX)) {
        $securePassword = if ($env:TEXTPAD_SIGN_PASSWORD) {
            ConvertTo-SecureString -String $env:TEXTPAD_SIGN_PASSWORD -AsPlainText -Force
        }
        else {
            $null
        }

        return Get-PfxCertificate -FilePath $env:TEXTPAD_SIGN_PFX -Password $securePassword -ErrorAction Stop
    }

    if (-not [string]::IsNullOrWhiteSpace($env:TEXTPAD_SIGN_THUMBPRINT)) {
        $thumbprint = $env:TEXTPAD_SIGN_THUMBPRINT.Replace(" ", "").ToUpperInvariant()
        $cert = Get-ChildItem Cert:\CurrentUser\My, Cert:\LocalMachine\My -ErrorAction SilentlyContinue |
            Where-Object { $_.Thumbprint -eq $thumbprint } |
            Select-Object -First 1
        if ($cert) {
            return $cert
        }
        throw "No certificate found for thumbprint $thumbprint"
    }

    $existing = Get-ChildItem Cert:\CurrentUser\My -ErrorAction SilentlyContinue |
        Where-Object {
            $_.HasPrivateKey -and (
                $_.Subject -match 'TextPad Code Signing' -or
                ($_.EnhancedKeyUsageList.FriendlyName -contains 'Code Signing')
            )
        } |
        Sort-Object NotAfter -Descending |
        Select-Object -First 1

    if ($existing) {
        return $existing
    }

    Write-Host "No signing certificate configured. Creating a local TextPad dev certificate..." -ForegroundColor Yellow
    return New-SelfSignedCertificate `
        -Type CodeSigningCert `
        -Subject "CN=TextPad Code Signing" `
        -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3") `
        -CertStoreLocation Cert:\CurrentUser\My `
        -KeyExportPolicy Exportable `
        -NotAfter ((Get-Date).AddYears(5))
}

function Sign-AuthenticodeFile {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [string]$Description = "TextPad",
        [string]$TimestampServer = $(if ($env:TEXTPAD_SIGN_TIMESTAMP) { $env:TEXTPAD_SIGN_TIMESTAMP } else { "http://timestamp.digicert.com" })
    )

    if (-not (Test-Path $Path)) {
        throw "Cannot sign missing file: $Path"
    }

    $certificate = Resolve-SigningCertificate
    $signTool = Resolve-SignTool

    if ($signTool) {
        $args = @("sign", "/fd", "sha256", "/td", "sha256", "/tr", $TimestampServer, "/d", $Description, "/sha1", $certificate.Thumbprint, $Path)
        if (-not [string]::IsNullOrWhiteSpace($env:TEXTPAD_SIGN_PFX)) {
            $args = @(
                "sign",
                "/fd", "sha256",
                "/td", "sha256",
                "/tr", $TimestampServer,
                "/d", $Description,
                "/f", $env:TEXTPAD_SIGN_PFX
            )
            if ($env:TEXTPAD_SIGN_PASSWORD) {
                $args += @("/p", $env:TEXTPAD_SIGN_PASSWORD)
            }
            $args += $Path
        }

        & $signTool @args
        if ($LASTEXITCODE -ne 0) {
            throw "signtool failed for $Path (exit $LASTEXITCODE)"
        }
    }
    else {
        Set-AuthenticodeSignature `
            -FilePath $Path `
            -Certificate $certificate `
            -TimestampServer $TimestampServer `
            -HashAlgorithm SHA256 | Out-Null
    }

    $signature = Get-AuthenticodeSignature -FilePath $Path
    if ($null -eq $signature.SignerCertificate -or $signature.Status -eq "NotSigned") {
        throw "Signature status for $Path is $($signature.Status)"
    }

    Write-Host "Signed $Path" -ForegroundColor Green
    Write-Host "  Certificate: $($certificate.Subject)" -ForegroundColor DarkGray
}

function Sign-TextPadPayload {
    param([Parameter(Mandatory = $true)][string]$PayloadDir)

    $targets = @(
        Join-Path $PayloadDir "TextPad.exe"
    )

    foreach ($target in $targets) {
        Sign-AuthenticodeFile -Path $target
    }
}

function Sign-TextPadInstaller {
    param([Parameter(Mandatory = $true)][string]$InstallerPath)

    Sign-AuthenticodeFile -Path $InstallerPath -Description "TextPad Setup"
}