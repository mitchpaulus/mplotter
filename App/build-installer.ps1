# Publishes the app and builds the NSIS installer.
# Usage: .\build-installer.ps1 [-Version 1.2.3] [-SkipPublish]
param(
    [string]$Version = "1.0.0",
    [switch]$SkipPublish
)
$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

if (-not $SkipPublish) {
    dotnet publish -r win-x64 -o publish -c Release --no-self-contained
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }
}

$makensis = Get-Command makensis -ErrorAction SilentlyContinue
if ($makensis) { $makensis = $makensis.Source }
else {
    $makensis = @("${env:ProgramFiles(x86)}\NSIS\makensis.exe", "$env:ProgramFiles\NSIS\makensis.exe") |
        Where-Object { Test-Path $_ } | Select-Object -First 1
}
if (-not $makensis) { throw "makensis.exe not found. Install NSIS: winget install NSIS.NSIS" }

New-Item -ItemType Directory -Force -Path installer\out | Out-Null
& $makensis "/DVERSION=$Version" "/DOUT_DIR=out" installer\mplotter.nsi
if ($LASTEXITCODE -ne 0) { throw "makensis failed" }
