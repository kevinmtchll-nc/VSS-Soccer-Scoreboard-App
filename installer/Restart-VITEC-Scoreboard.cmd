@echo off
setlocal
title Restart VITEC Scoreboard

net session >nul 2>&1
if not "%errorlevel%"=="0" (
  powershell.exe -NoProfile -ExecutionPolicy Bypass -Command ^
    "Start-Process -FilePath '%~f0' -Verb RunAs"
  exit /b
)

sc.exe stop VITECScoreboard >nul 2>&1
timeout /t 2 /nobreak >nul
sc.exe start VITECScoreboard
echo.
echo VITEC Scoreboard restarted.
timeout /t 3 /nobreak >nul
endlocal
