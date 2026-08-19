@echo off
setlocal
title Cleanup Old VITEC Scoreboard Service

net session >nul 2>&1
if not "%errorlevel%"=="0" (
  powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
  exit /b
)

sc.exe stop VITECScoreboard >nul 2>&1
timeout /t 1 /nobreak >nul
sc.exe delete VITECScoreboard >nul 2>&1

if exist "C:\Program Files\VITEC\Scoreboard\VITEC.Scoreboard.exe" (
  del /f /q "C:\Program Files\VITEC\Scoreboard\VITEC.Scoreboard.exe" >nul 2>&1
)

echo Old service registration/custom EXE cleanup complete.
pause
endlocal
