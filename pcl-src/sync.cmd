@echo off
rem ============================================================================
rem  sync.cmd  --  pull an upstream PCL tag and prepare it for local building
rem
rem  All arguments are forwarded to tools\sync.ps1. Typical use:
rem
rem    sync.cmd -Tag 2.99.0.0
rem        download the tag from codeload, fetch the MeloongCore submodule
rem        by commit, unpack to staging\<tag>\tree, apply local patches
rem
rem    sync.cmd -Tag 2.99.0.0 -From pcl-2.99.0.0.tar.gz -Core core.tar.gz
rem        same, but use local tarballs (offline / avoid the slow mirror)
rem
rem    sync.cmd -Tag 2.99.0.0 -Force
rem        additionally replace build\ with the synced tree
rem        (the old tree is kept as build.bak-<timestamp>)
rem
rem  Exit: 0 ok / 1 failed
rem ============================================================================
setlocal
set ROOT=%~dp0
if not defined PS_EXE set "PS_EXE=%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe"

if "%~1"=="" (
    echo Usage: sync.cmd -Tag ^<version^> [-From ^<local.tar.gz^>] [-Core ^<core.tar.gz^>] [-Force] [-SkipPatch]
    echo Example: sync.cmd -Tag 2.99.0.0
    exit /b 1
)

"%PS_EXE%" -NoProfile -File "%ROOT%tools\sync.ps1" %*
if errorlevel 1 (
    echo.
    echo [FAIL] Sync stopped. See the messages above.
    exit /b 1
)

echo.
echo [OK] Sync finished. Next: run build.cmd to compile.
exit /b 0
