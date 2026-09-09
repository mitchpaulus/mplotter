#!/bin/sh
# WSL wrapper: publishes the app and builds the NSIS installer via Windows PowerShell.
# Usage: ./build-installer.sh [version]
cd "$(dirname "$0")" || exit 1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File '.\build-installer.ps1' -Version "${1:-1.0.0}"
