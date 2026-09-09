; NSIS installer for MPlotter
;
; Build (from the App directory, after `dotnet publish` into ./publish):
;   makensis /DVERSION=1.2.3 installer\mplotter.nsi
;
; The app is published framework-dependent, so the installer checks for the
; .NET 8 Desktop Runtime and offers to open the download page if it is missing.

Unicode true
SetCompressor /SOLID lzma

!ifndef VERSION
  !define VERSION "0.0.0"
!endif
!ifndef PUBLISH_DIR
  !define PUBLISH_DIR "..\publish"
!endif
!ifndef OUT_DIR
  !define OUT_DIR "."
!endif

!define APP_NAME "MPlotter"
!define APP_EXE "csvplot.exe"
!define PUBLISHER "Command Commissioning"
!define UNINST_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}"
!define DOTNET_MAJOR "8"
!define DOTNET_URL "https://dotnet.microsoft.com/download/dotnet/8.0"

Name "${APP_NAME} ${VERSION}"
OutFile "${OUT_DIR}\${APP_NAME}-${VERSION}-setup.exe"
InstallDir "$PROGRAMFILES64\${APP_NAME}"
InstallDirRegKey HKLM "Software\${APP_NAME}" "InstallDir"
RequestExecutionLevel admin
BrandingText "${APP_NAME} ${VERSION}"

VIProductVersion "${VERSION}.0"
VIAddVersionKey "ProductName" "${APP_NAME}"
VIAddVersionKey "ProductVersion" "${VERSION}"
VIAddVersionKey "FileVersion" "${VERSION}"
VIAddVersionKey "CompanyName" "${PUBLISHER}"
VIAddVersionKey "FileDescription" "${APP_NAME} Installer"
VIAddVersionKey "LegalCopyright" "${PUBLISHER}"

;--------------------------------
; Modern UI

!include "MUI2.nsh"
!include "x64.nsh"
!include "LogicLib.nsh"
!include "FileFunc.nsh"

!define MUI_ICON "mplotter.ico"
!define MUI_UNICON "mplotter.ico"
!define MUI_ABORTWARNING
!define MUI_FINISHPAGE_RUN "$INSTDIR\${APP_EXE}"
!define MUI_FINISHPAGE_RUN_TEXT "Launch ${APP_NAME}"

!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES

!insertmacro MUI_LANGUAGE "English"

;--------------------------------
; .NET runtime check

; Sets $0 to "1" if a .NET ${DOTNET_MAJOR}.x Windows Desktop runtime is installed, else "0".
Function CheckDotNetDesktop
  StrCpy $0 "0"
  StrCpy $1 0
  ${DisableX64FSRedirection}
  loop:
    ; Shared runtimes live in HKLM\SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App
    EnumRegValue $2 HKLM "SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App" $1
    StrCmp $2 "" done
    StrCpy $3 $2 2 ; first two chars, e.g. "8."
    StrCmp $3 "${DOTNET_MAJOR}." found
    IntOp $1 $1 + 1
    Goto loop
  found:
    StrCpy $0 "1"
  done:
  ${EnableX64FSRedirection}
FunctionEnd

Function .onInit
  ${IfNot} ${RunningX64}
    MessageBox MB_OK|MB_ICONSTOP "${APP_NAME} requires 64-bit Windows."
    Abort
  ${EndIf}
  SetRegView 64

  Call CheckDotNetDesktop
  ${If} $0 == "0"
    MessageBox MB_YESNO|MB_ICONEXCLAMATION \
      "${APP_NAME} requires the .NET ${DOTNET_MAJOR} Desktop Runtime (x64), which was not found.$\r$\n$\r$\nOpen the download page now? Installation will continue either way." \
      IDNO skipDownload
    ExecShell "open" "${DOTNET_URL}"
    skipDownload:
  ${EndIf}
FunctionEnd

;--------------------------------
; Install

Section "Install"
  SetRegView 64
  SetOutPath "$INSTDIR"

  ; Remove files from a previous install so stale DLLs do not linger.
  Delete "$INSTDIR\*.dll"

  File /r /x "*.pdb" "${PUBLISH_DIR}\*.*"

  WriteRegStr HKLM "Software\${APP_NAME}" "InstallDir" "$INSTDIR"

  WriteUninstaller "$INSTDIR\uninstall.exe"

  ; Start menu
  CreateDirectory "$SMPROGRAMS\${APP_NAME}"
  CreateShortcut "$SMPROGRAMS\${APP_NAME}\${APP_NAME}.lnk" "$INSTDIR\${APP_EXE}"
  CreateShortcut "$SMPROGRAMS\${APP_NAME}\Uninstall ${APP_NAME}.lnk" "$INSTDIR\uninstall.exe"

  ; Add/Remove Programs
  WriteRegStr HKLM "${UNINST_KEY}" "DisplayName" "${APP_NAME}"
  WriteRegStr HKLM "${UNINST_KEY}" "DisplayVersion" "${VERSION}"
  WriteRegStr HKLM "${UNINST_KEY}" "Publisher" "${PUBLISHER}"
  WriteRegStr HKLM "${UNINST_KEY}" "DisplayIcon" "$INSTDIR\${APP_EXE}"
  WriteRegStr HKLM "${UNINST_KEY}" "InstallLocation" "$INSTDIR"
  WriteRegStr HKLM "${UNINST_KEY}" "UninstallString" '"$INSTDIR\uninstall.exe"'
  WriteRegStr HKLM "${UNINST_KEY}" "QuietUninstallString" '"$INSTDIR\uninstall.exe" /S'
  WriteRegDWORD HKLM "${UNINST_KEY}" "NoModify" 1
  WriteRegDWORD HKLM "${UNINST_KEY}" "NoRepair" 1

  ${GetSize} "$INSTDIR" "/S=0K" $0 $1 $2
  IntFmt $0 "0x%08X" $0
  WriteRegDWORD HKLM "${UNINST_KEY}" "EstimatedSize" "$0"
SectionEnd

;--------------------------------
; Uninstall

Section "Uninstall"
  SetRegView 64

  Delete "$SMPROGRAMS\${APP_NAME}\${APP_NAME}.lnk"
  Delete "$SMPROGRAMS\${APP_NAME}\Uninstall ${APP_NAME}.lnk"
  RMDir "$SMPROGRAMS\${APP_NAME}"

  RMDir /r "$INSTDIR"

  DeleteRegKey HKLM "${UNINST_KEY}"
  DeleteRegKey HKLM "Software\${APP_NAME}"
SectionEnd
