@echo off
setlocal
title Uninstall VITEC Scoreboard

net session >nul 2>&1
if not "%errorlevel%"=="0" (
  powershell.exe -NoProfile -ExecutionPolicy Bypass -Command ^
    "Start-Process -FilePath '%~f0' -Verb RunAs"
  exit /b
)

set "SERVICENAME=VITECScoreboard"
set "INSTALLDIR=C:\Program Files\VITEC\Scoreboard"

echo Stopping service...
sc.exe stop "%SERVICENAME%" >nul 2>&1
timeout /t 2 /nobreak >nul

echo Removing service...
sc.exe delete "%SERVICENAME%" >nul 2>&1

echo Removing application files...
if exist "%INSTALLDIR%" rmdir /s /q "%INSTALLDIR%"

echo Removing firewall rule...
netsh advfirewall firewall delete rule name="VITEC Scoreboard TCP 5000" >nul 2>&1

echo.
echo VITEC Scoreboard was removed.
echo Historical PostgreSQL data and %%ProgramData%% settings were preserved.
pause
endlocal
