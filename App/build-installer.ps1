# Publishes the app and builds the NSIS installer. Used by CI (release.yml), which passes the
# version from the pushed tag. For local builds use build-installer.msh, which reads the tag itself.
# Usage: .\build-installer.ps1 -Version 1.2.3 [-SkipPublish]
param(
    [Parameter(Mandatory = $true)][string]$Version,
    [switch]$SkipPublish
)
$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

if (-not $SkipPublish) {
    # The version is stamped into csvplot.exe here; the installer reads it back from the exe.
    dotnet publish -r win-x64 -o publish -c Release --no-self-contained "-p:Version=$Version"
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
& $makensis "/DOUT_DIR=out" installer\mplotter.nsi
if ($LASTEXITCODE -ne 0) { throw "makensis failed" }
