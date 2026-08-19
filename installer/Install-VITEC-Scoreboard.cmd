@echo off
setlocal EnableExtensions EnableDelayedExpansion
title VITEC Scoreboard v0.7 Clean Installer

net session >nul 2>&1
if not "%errorlevel%"=="0" (
  echo Requesting Administrator rights...
  powershell.exe -NoProfile -ExecutionPolicy Bypass -Command ^
    "Start-Process -FilePath '%~f0' -Verb RunAs"
  exit /b
)

cd /d "%~dp0.."

set "INSTALLDIR=C:\Program Files\VITEC\Scoreboard"
set "DATADIR=%ProgramData%\VITEC Scoreboard"
set "SERVICENAME=VITECScoreboard"
set "PUBLISHDIR=%TEMP%\VS-Clean-Publish"

echo.
echo ============================================================
echo   VITEC Scoreboard v0.7 - Clean Windows Service Installer
echo ============================================================
echo.
echo This installer safely replaces an older VS application/service.
echo PostgreSQL data and ProgramData settings are preserved.
echo.

where dotnet >nul 2>&1
if not "%errorlevel%"=="0" (
  echo ERROR: .NET 8 SDK was not found.
  echo Install the .NET 8 SDK, then run this installer again.
  pause
  exit /b 1
)

echo [1/7] Stopping any existing VITEC Scoreboard service...
sc.exe query "%SERVICENAME%" >nul 2>&1
if "%errorlevel%"=="0" (
  sc.exe stop "%SERVICENAME%" >nul 2>&1
  timeout /t 3 /nobreak >nul
)

echo [2/7] Removing previous build output...
dotnet clean ".\VITEC.Scoreboard.sln" -c Release >nul 2>&1
for /d /r "." %%D in (bin,obj) do @if exist "%%D" rd /s /q "%%D" >nul 2>&1
if exist "%PUBLISHDIR%" rd /s /q "%PUBLISHDIR%"

echo [3/7] Restoring packages...
dotnet restore ".\VITEC.Scoreboard.sln"
if not "%errorlevel%"=="0" (
  echo.
  echo ERROR: Package restore failed.
  pause
  exit /b 1
)

echo [4/7] Publishing self-contained Windows x64 application...
dotnet publish ".\src\VS.Web\VS.Web.csproj" ^
  -c Release ^
  -r win-x64 ^
  --self-contained true ^
  --no-restore ^
  -o "%PUBLISHDIR%"

if not "%errorlevel%"=="0" (
  echo.
  echo ERROR: VS publish failed.
  echo No existing service/application files were deleted.
  pause
  exit /b 1
)

echo [5/7] Replacing application files...
if exist "%INSTALLDIR%" rd /s /q "%INSTALLDIR%"
mkdir "%INSTALLDIR%" >nul 2>&1
xcopy "%PUBLISHDIR%\*" "%INSTALLDIR%\" /E /I /Y /Q >nul

if not exist "%DATADIR%" mkdir "%DATADIR%"

if not exist "%DATADIR%\vssettings.json" (
  copy /Y ".\config\vssettings.template.json" "%DATADIR%\vssettings.json" >nul
  echo Created new VS settings file:
  echo   %DATADIR%\vssettings.json
)

echo [6/7] Configuring service and firewall...
netsh advfirewall firewall delete rule name="VITEC Scoreboard TCP 5000" >nul 2>&1
netsh advfirewall firewall add rule name="VITEC Scoreboard TCP 5000" dir=in action=allow protocol=TCP localport=5000 >nul

sc.exe query "%SERVICENAME%" >nul 2>&1
if "%errorlevel%"=="0" (
  sc.exe delete "%SERVICENAME%" >nul 2>&1
  timeout /t 2 /nobreak >nul
)

sc.exe create "%SERVICENAME%" ^
  binPath= "\"%INSTALLDIR%\VITEC.Scoreboard.exe\"" ^
  start= auto ^
  DisplayName= "VITEC Scoreboard"

if not "%errorlevel%"=="0" (
  echo.
  echo ERROR: Unable to create Windows Service.
  pause
  exit /b 1
)

sc.exe description "%SERVICENAME%" "VITEC Scoreboard live MLB scoring and analytics service."
sc.exe failure "%SERVICENAME%" reset= 86400 actions= restart/5000/restart/10000/restart/30000

echo [7/7] Starting VITEC Scoreboard...
sc.exe start "%SERVICENAME%"

echo.
echo ============================================================
echo   VITEC Scoreboard v0.7 installation complete
echo ============================================================
echo.
echo Application:
echo   %INSTALLDIR%
echo.
echo Settings:
echo   %DATADIR%\vssettings.json
echo.
echo Web:
echo   http://localhost:5000
echo.
echo Service:
echo   VITEC Scoreboard
echo.
echo NOTE:
echo PostgreSQL databases and existing VS settings were NOT deleted.
echo ============================================================
echo.

start "" "http://localhost:5000"
pause
endlocal
