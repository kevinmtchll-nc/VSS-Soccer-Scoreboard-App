@echo off
set "CFG=%ProgramData%\VITEC Scoreboard\vssettings.json"
if not exist "%CFG%" (
  echo VS settings do not exist yet. Run Install-VITEC-Scoreboard.cmd first.
  pause
  exit /b 1
)
notepad "%CFG%"
