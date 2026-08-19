@echo off
setlocal EnableExtensions
title VITEC Scoreboard Branded Windows Installer

net session >nul 2>&1
if not "%errorlevel%"=="0" (
  powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
  exit /b
)

cd /d "%~dp0.."

set "INSTALLDIR=C:\Program Files\VITEC\Scoreboard"
set "SERVICENAME=VITECScoreboard"
set "PUBLISH=%TEMP%\VS-GameDay-Publish"

echo ============================================================
echo   VITEC Scoreboard - Branded Windows Executable
echo ============================================================
echo.

where dotnet >nul 2>&1
if errorlevel 1 (
  echo ERROR: .NET SDK not found.
  pause
  exit /b 1
)

echo [1/6] Publishing self-contained application...
if exist "%PUBLISH%" rd /s /q "%PUBLISH%"

dotnet publish ".\src\VS.Web\VS.Web.csproj" ^
  -c Release ^
  -r win-x64 ^
  --self-contained true ^
  -p:UseAppHost=true ^
  -o "%PUBLISH%"

if errorlevel 1 (
  echo ERROR: Publish failed.
  pause
  exit /b 2
)

if not exist "%PUBLISH%\VITEC.Scoreboard.exe" (
  echo ERROR: Expected VITEC.Scoreboard.exe was not produced.
  pause
  exit /b 3
)

echo [2/6] Removing previous VITEC Scoreboard service...
sc.exe stop "%SERVICENAME%" >nul 2>&1
timeout /t 2 /nobreak >nul
sc.exe delete "%SERVICENAME%" >nul 2>&1
timeout /t 2 /nobreak >nul

echo [3/6] Replacing application files...
if exist "%INSTALLDIR%" rd /s /q "%INSTALLDIR%"
mkdir "%INSTALLDIR%" >nul 2>&1
xcopy "%PUBLISH%\*" "%INSTALLDIR%\" /E /I /Y /Q >nul

echo [4/6] Creating branded VITEC Scoreboard Windows Service...
sc.exe create "%SERVICENAME%" ^
  binPath= "\"%INSTALLDIR%\VITEC.Scoreboard.exe\" --windows-service" ^
  start= auto ^
  DisplayName= "VITEC Scoreboard"

if errorlevel 1 (
  echo ERROR: Unable to create Windows Service.
  pause
  exit /b 4
)

sc.exe description "%SERVICENAME%" "VITEC Scoreboard MLB live scoring and GameCenter service."
sc.exe failure "%SERVICENAME%" reset= 86400 actions= restart/5000/restart/10000/restart/30000
sc.exe failureflag "%SERVICENAME%" 1 >nul 2>&1

echo [5/6] Configuring firewall...
netsh advfirewall firewall delete rule name="VITEC Scoreboard TCP 5000" >nul 2>&1
netsh advfirewall firewall add rule name="VITEC Scoreboard TCP 5000" dir=in action=allow protocol=TCP localport=5000 >nul

echo [6/6] Starting service...
sc.exe start "%SERVICENAME%"
if errorlevel 1 (
  echo.
  echo ERROR: Windows could not start VITEC Scoreboard.
  echo.
  echo Service configuration:
  sc.exe qc "%SERVICENAME%"
  echo.
  echo Check log if present:
  echo   C:\ProgramData\VITEC Scoreboard\Logs\VITEC-Scoreboard.log
  pause
  exit /b 5
)

timeout /t 3 /nobreak >nul

echo.
echo ============================================================
echo   INSTALLATION SUCCESSFUL
echo ============================================================
echo.
echo VITEC Scoreboard executable:
echo   %INSTALLDIR%\VITEC.Scoreboard.exe
echo.
echo Web:
echo   http://localhost:5000
echo.
echo Service:
sc.exe query "%SERVICENAME%"
echo.
start "" "http://localhost:5000"
pause
endlocal
