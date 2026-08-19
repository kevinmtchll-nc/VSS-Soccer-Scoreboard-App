@echo off
title VITEC Scoreboard Install Check
echo.
echo === VITEC Scoreboard Install Check ===
echo.

echo Windows service:
sc.exe query VITECScoreboard
echo.

echo Installed application:
if exist "C:\Program Files\VITEC\Scoreboard\VITEC.Scoreboard.exe" (
  echo FOUND: C:\Program Files\VITEC\Scoreboard\VITEC.Scoreboard.exe
) else (
  echo NOT FOUND
)
echo.

echo VS settings:
if exist "%ProgramData%\VITEC Scoreboard\vssettings.json" (
  echo FOUND: %ProgramData%\VITEC Scoreboard\vssettings.json
) else (
  echo NOT FOUND
)
echo.
pause
