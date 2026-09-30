; NSIS installer for MPlotter
;
; Build (from the App directory, after `dotnet publish` into ./publish):
;   makensis installer\mplotter.nsi
;
; The version is read from the published csvplot.exe, which is stamped by
; `dotnet publish -p:Version=...` (see build-installer.msh). It is never passed
; in here, so the installer always matches the app it contains. An exe built
; without a version (0.0.0-dev) produces a "dev" installer.
;
; The app is published framework-dependent, so the installer checks for the
; .NET 8 Desktop Runtime and offers to open the download page if it is missing.
;
; Installs per-user (%LOCALAPPDATA%\Programs, HKCU) so no administrator rights
; are needed. The .NET runtime itself is installed separately by the user.

Unicode true
SetCompressor /SOLID lzma

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

!getdllversion "${PUBLISH_DIR}\${APP_EXE}" EXEVER_
!if "${EXEVER_1}.${EXEVER_2}.${EXEVER_3}" == "0.0.0"
  !define VERSION "dev"
!else
  !define VERSION "${EXEVER_1}.${EXEVER_2}.${EXEVER_3}"
!endif

Name "${APP_NAME} ${VERSION}"
OutFile "${OUT_DIR}\${APP_NAME}-${VERSION}-setup.exe"
InstallDir "$LOCALAPPDATA\Programs\${APP_NAME}"
InstallDirRegKey HKCU "Software\${APP_NAME}" "InstallDir"
RequestExecutionLevel user
BrandingText "${APP_NAME} ${VERSION}"

VIProductVersion "${EXEVER_1}.${EXEVER_2}.${EXEVER_3}.${EXEVER_4}"
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
  ; The .NET installers write InstalledVersions to the 32-bit registry view (WOW6432Node),
  ; even for x64 runtimes, so read it there rather than in the 64-bit view set in .onInit.
  SetRegView 32
  loop:
    ; Shared runtimes live in HKLM\SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App
    EnumRegValue $2 HKLM "SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App" $1
    StrCmp $2 "" done
    StrLen $4 "${DOTNET_MAJOR}."
    StrCpy $3 $2 $4 ; major version prefix, e.g. "8."
    StrCmp $3 "${DOTNET_MAJOR}." found
    IntOp $1 $1 + 1
    Goto loop
  found:
    StrCpy $0 "1"
  done:
  SetRegView 64
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
  SetShellVarContext current
  SetRegView 64
  SetOutPath "$INSTDIR"

  ; Remove files from a previous install so stale DLLs do not linger.
  Delete "$INSTDIR\*.dll"

  File /r /x "*.pdb" "${PUBLISH_DIR}\*.*"

  WriteRegStr HKCU "Software\${APP_NAME}" "InstallDir" "$INSTDIR"

  WriteUninstaller "$INSTDIR\uninstall.exe"

  ; Start menu
  CreateDirectory "$SMPROGRAMS\${APP_NAME}"
  CreateShortcut "$SMPROGRAMS\${APP_NAME}\${APP_NAME}.lnk" "$INSTDIR\${APP_EXE}"
  CreateShortcut "$SMPROGRAMS\${APP_NAME}\Uninstall ${APP_NAME}.lnk" "$INSTDIR\uninstall.exe"

  ; Add/Remove Programs
  WriteRegStr HKCU "${UNINST_KEY}" "DisplayName" "${APP_NAME}"
  WriteRegStr HKCU "${UNINST_KEY}" "DisplayVersion" "${VERSION}"
  WriteRegStr HKCU "${UNINST_KEY}" "Publisher" "${PUBLISHER}"
  WriteRegStr HKCU "${UNINST_KEY}" "DisplayIcon" "$INSTDIR\${APP_EXE}"
  WriteRegStr HKCU "${UNINST_KEY}" "InstallLocation" "$INSTDIR"
  WriteRegStr HKCU "${UNINST_KEY}" "UninstallString" '"$INSTDIR\uninstall.exe"'
  WriteRegStr HKCU "${UNINST_KEY}" "QuietUninstallString" '"$INSTDIR\uninstall.exe" /S'
  WriteRegDWORD HKCU "${UNINST_KEY}" "NoModify" 1
  WriteRegDWORD HKCU "${UNINST_KEY}" "NoRepair" 1

  ${GetSize} "$INSTDIR" "/S=0K" $0 $1 $2
  IntFmt $0 "0x%08X" $0
  WriteRegDWORD HKCU "${UNINST_KEY}" "EstimatedSize" "$0"
SectionEnd

;--------------------------------
; Uninstall

Section "Uninstall"
  SetShellVarContext current
  SetRegView 64

  Delete "$SMPROGRAMS\${APP_NAME}\${APP_NAME}.lnk"
  Delete "$SMPROGRAMS\${APP_NAME}\Uninstall ${APP_NAME}.lnk"
  RMDir "$SMPROGRAMS\${APP_NAME}"

  RMDir /r "$INSTDIR"

  DeleteRegKey HKCU "${UNINST_KEY}"
  DeleteRegKey HKCU "Software\${APP_NAME}"
SectionEnd
