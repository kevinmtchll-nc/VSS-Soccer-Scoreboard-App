@echo off
setlocal EnableExtensions
cd /d "%~dp0"

echo ============================================================
echo   VITEC Scoreboard v0.8.10 - Baseline Build Verification
echo ============================================================
echo.

where dotnet >nul 2>&1
if errorlevel 1 (
  echo ERROR: .NET 8 SDK not found.
  exit /b 1
)

dotnet clean VITEC.Scoreboard.sln -c Release
if errorlevel 1 goto :fail

dotnet restore VITEC.Scoreboard.sln
if errorlevel 1 goto :fail

dotnet build VITEC.Scoreboard.sln -c Release --no-restore
if errorlevel 1 goto :fail

echo.
echo BASELINE BUILD PASSED.
exit /b 0

:fail
echo.
echo BASELINE BUILD FAILED.
exit /b 10
