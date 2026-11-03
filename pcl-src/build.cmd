@echo off
rem ============================================================================
rem  Rebuild PCL (Plain Craft Launcher 2) - open-source edition, with the
rem  Minecraft download entry restored (ModSecret.vb: RestrictMcDownload).
rem
rem  Usage:
rem    build.cmd               rebuild, stamp a new version, archive the output
rem    build.cmd -NoArchive    rebuild, skip the versions\ archive
rem    build.cmd -NoRestore    rebuild without NuGet restore. Needed when the
rem                            process cannot read machine-level environment
rem                            values (NuGet then dies with "Value cannot be
rem                            null. (Parameter 'path1')" in NuGet.targets),
rem                            and for offline builds. Requires obj\*.assets.json
rem                            to already exist.
rem
rem  Flags can be combined, in any order.
rem
rem  Optional environment variable PCL_TREE=<dir> builds another source tree
rem  (used to smoke-test a tree produced by sync.cmd).
rem
rem  Version stamping (tools\version-apply.ps1):
rem    ModBase.vb       VersionBaseName  -> e.g. 2026.10.25-custom.1
rem                     BuildTypeDisplay -> the localized "testing build" tag
rem    AssemblyInfo.vb  AssemblyVersion / AssemblyFileVersion -> 26.10.25.1
rem  The pristine copies are kept as *.verorig and restored after the build.
rem
rem  Requirements already satisfied in this workspace:
rem    ..\_tools\dotnet          .NET SDK 8.0.404 (portable, no install needed)
rem    ..\_tools\nuget           NuGet package cache (net48 reference assemblies)
rem
rem  Output:
rem    ..\PCL-custom\Plain Craft Launcher 2.exe
rem    ..\PCL-custom\versions\<label>\...        (unless -NoArchive)
rem
rem  Keep this file ASCII-only. cmd.exe decodes .cmd bytes with the OEM code
rem  page (GBK on a Chinese Windows); a UTF-8 Chinese full stop is 3 bytes, and it ends
rem  with byte 0x82, which pairs with the trailing 0x0D as one double-byte
rem  character and eats the CR. The next line then joins the comment and gets
rem  executed as a command ("'...' is not recognized"). ASCII has no such risk.
rem ============================================================================
setlocal EnableExtensions EnableDelayedExpansion
set ROOT=%~dp0
rem Always call PowerShell by absolute path, never by bare name: on a trimmed
rem PATH (some IDE, service and sandbox sessions) "powershell" is not found.
rem Inside for /f the call MUST carry the "call" prefix - otherwise cmd /c
rem strips the leading quote of the quoted path and the whole line fails with
rem "'...powershell.exe" -NoProfile ...' is not recognized".
if not defined PS_EXE set "PS_EXE=%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe"
rem Layout: gongtong\pcl-src (source), gongtong\_tools (toolchain),
rem gongtong\PCL-custom (output). All three are siblings, so the workspace
rem root is the parent directory. WB_ROOT overrides it when already set.
if defined WB_ROOT (
    set WB=%WB_ROOT%
) else (
    for %%i in ("%ROOT%..") do set WB=%%~fi
)
set DOTNET=%WB%\_tools\dotnet\dotnet.exe
set DOTNET_ROOT=%WB%\_tools\dotnet
set NUGET_PACKAGES=%WB%\_tools\nuget
set DOTNET_CLI_HOME=%WB%\_tools\dotnet-home
set DOTNET_CLI_TELEMETRY_OPTOUT=1
set DOTNET_NOLOGO=1
set RFP=%NUGET_PACKAGES%\microsoft.netframework.referenceassemblies.net48\1.0.3\build

set "NOARCHIVE=0"
set "NORESTORE=0"
for %%a in (%*) do (
    if /i "%%~a"=="-NoArchive" set "NOARCHIVE=1"
    if /i "%%~a"=="-NoRestore" set "NORESTORE=1"
)
rem Empty by default, so a plain "build.cmd" keeps restoring packages as before.
set "RESTOREOPT="
if "%NORESTORE%"=="1" set "RESTOREOPT=--no-restore"

if not exist "%DOTNET%" (
    echo [ERROR] dotnet SDK not found: %DOTNET%
    echo         See docs\BUILD.md for how the toolchain was set up.
    exit /b 1
)
if not exist "%RFP%\.NETFramework\v4.8\mscorlib.dll" (
    echo [ERROR] net48 reference assemblies not found under %RFP%
    exit /b 1
)

rem  Optional: build another source tree (used to smoke-test a freshly synced one)
set "TREE=%ROOT%build"
if defined PCL_TREE set "TREE=%PCL_TREE%"

cd /d "%TREE%" || exit /b 1

echo [0/5] Stamping version
set "STAMP="
set "SEQOPT="
if "%NOARCHIVE%"=="1" set "SEQOPT=-ReuseSeq"
rem Keep only lines containing "-custom.": Windows PowerShell 5.1 sometimes
rem prints a PS7 promo line to stdout; findstr filters it so the version stamp
rem stays clean.
set "STAMP="
for /f "usebackq delims=" %%v in (`call "%PS_EXE%" -NoProfile -ExecutionPolicy Bypass -File "%ROOT%tools\version-apply.ps1" -BuildRoot "%TREE%" %SEQOPT% ^| findstr /r /c:"-custom\."`) do set "STAMP=%%v"
if not defined STAMP (
    echo [ERROR] Version stamp failed.
    echo         Run this to see the reason:
    echo         "%PS_EXE%" -NoProfile -ExecutionPolicy Bypass -File "%ROOT%tools\version-apply.ps1"
    exit /b 1
)
for /f "tokens=1,2 delims=|" %%a in ("%STAMP%") do (
    set "VER_LABEL=%%a"
    set "VER_NUM=%%b"
)
rem Hard check: the label must match YYYY.MM.DD-custom.N. Fail rather than let a
rem dirty value create an archive directory.
echo %VER_LABEL%| findstr /r "^[0-9][0-9][0-9][0-9]\.[0-9][0-9]\.[0-9][0-9]-custom\.[0-9]*$" >nul
if errorlevel 1 (
    echo [ERROR] Version stamp looks wrong: %VER_LABEL%
    exit /b 1
)
echo        label=%VER_LABEL%  assembly=%VER_NUM%

echo [1/5] MeloongCore (shared)
"%DOTNET%" build %RESTOREOPT% "MeloongCore\Shared\MeloongCore.csproj" -c Release -v q -p:TargetFrameworkRootPath="%RFP%" || goto :fail

echo [2/5] MeloongCore.Wpf
"%DOTNET%" build %RESTOREOPT% "MeloongCore\Wpf\MeloongCore.Wpf.csproj" -c Release -v q -p:TargetFrameworkRootPath="%RFP%" || goto :fail

echo [3/5] PCLCS
"%DOTNET%" build %RESTOREOPT% "PCLCS\PCLCS.csproj" -c Release -v q -p:TargetFrameworkRootPath="%RFP%" || goto :fail

echo [4/5] Plain Craft Launcher 2
"%DOTNET%" build %RESTOREOPT% "Plain Craft Launcher 2\Plain Craft Launcher 2.vbproj" -c Release -v m -p:TargetFrameworkRootPath="%RFP%" -p:IncludePackageReferencesDuringMarkupCompilation=false || goto :fail

echo [5/5] Collect output
if not exist "%WB%\PCL-custom" mkdir "%WB%\PCL-custom"
copy /y "Plain Craft Launcher 2\bin\Plain Craft Launcher 2.exe"        "%WB%\PCL-custom\" >nul || goto :fail
copy /y "Plain Craft Launcher 2\bin\Plain Craft Launcher 2.exe.config" "%WB%\PCL-custom\" >nul || goto :fail

if "%NOARCHIVE%"=="0" (
    set "VDIR=%WB%\PCL-custom\versions\!VER_LABEL!"
    if not exist "!VDIR!" mkdir "!VDIR!"
    copy /y "Plain Craft Launcher 2\bin\Plain Craft Launcher 2.exe"        "!VDIR!\" >nul || goto :fail
    copy /y "Plain Craft Launcher 2\bin\Plain Craft Launcher 2.exe.config" "!VDIR!\" >nul || goto :fail
    rem SHA256 of the archived exe, so an archive can be verified later.
    rem certutil prints: "<localized intro line>" / "<64 hex chars>" / "<localized footer>".
    rem skip=1 drops the intro; the first remaining line is the hash.
    rem certutil rejects relative paths containing "..", so force an absolute one.
    for %%i in ("!VDIR!") do set "VABS=%%~fi"
    set "SHA256="
    for /f "skip=1 delims=" %%h in ('certutil -hashfile "!VABS!\Plain Craft Launcher 2.exe" SHA256') do if not defined SHA256 set "SHA256=%%h"
    if not defined SHA256 (
        echo [WARN] certutil failed to compute SHA256, recording "unknown".
        set "SHA256=unknown"
    )
    rem Timestamp is written as ASCII ISO 8601 only. %DATE%/%TIME% follow the
    rem regional settings: on a Chinese Windows they carry GBK weekday bytes,
    rem which read as mojibake in any UTF-8 reader and cannot be sorted. Take
    rem the value from PowerShell instead; findstr drops the PS7 promo line.
    set "BUILT="
    for /f "usebackq delims=" %%t in (`call "%PS_EXE%" -NoProfile -ExecutionPolicy Bypass -Command "Get-Date -Format yyyy-MM-ddTHH:mm:sszzz" ^| findstr /r "^[0-9][0-9][0-9][0-9]-[0-9][0-9]-[0-9][0-9]T"`) do set "BUILT=%%t"
    if not defined BUILT (
        echo [WARN] Could not read an ISO timestamp, recording "unknown".
        set "BUILT=unknown"
    )
    (
        echo label=!VER_LABEL!
        echo assembly=!VER_NUM!
        echo built=!BUILT!
        echo source=Plain Craft Launcher 2 open-source edition
        echo note=RestrictMcDownload=False, keys injected via secrets.env
        echo sha256=!SHA256!
    ) > "!VDIR!\build-info.txt"
)

call :restore
echo.
echo [OK] Build finished.
echo      Version: %VER_LABEL%
echo      Output : %WB%\PCL-custom\Plain Craft Launcher 2.exe
if "%NOARCHIVE%"=="0" echo      Archive: %VDIR%
exit /b 0

:fail
echo.
echo [FAIL] Build stopped. See the messages above.
call :restore
exit /b 1

:restore
"%PS_EXE%" -NoProfile -ExecutionPolicy Bypass -File "%ROOT%tools\version-apply.ps1" -BuildRoot "%TREE%" -Restore >nul 2>&1
goto :eof
