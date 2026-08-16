$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
& (Join-Path $root "windows\build.ps1") @args
