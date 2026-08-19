@echo off
setlocal EnableExtensions
cd /d "%~dp0.."

set "PUBLISHDIR=%CD%\publish"

echo ============================================================
echo   VITEC Scoreboard - Branded Windows Publish
echo ============================================================
echo.

if exist "%PUBLISHDIR%" rd /s /q "%PUBLISHDIR%"

dotnet publish src\VS.Web\VS.Web.csproj ^
  -c Release ^
  -r win-x64 ^
  --self-contained true ^
  -p:UseAppHost=true ^
  -o "%PUBLISHDIR%"

if errorlevel 1 (
  echo ERROR: Publish failed.
  exit /b 1
)

if not exist "%PUBLISHDIR%\VITEC.Scoreboard.exe" (
  echo ERROR: VITEC.Scoreboard.exe was not produced.
  exit /b 2
)

echo.
echo Publish verified:
echo   %PUBLISHDIR%\VITEC.Scoreboard.exe
exit /b 0
