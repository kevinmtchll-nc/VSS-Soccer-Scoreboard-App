@echo off
set "LOG=%ProgramData%\VITEC Scoreboard\Logs\VITEC-Scoreboard.log"
if not exist "%LOG%" (
  echo Log file not created yet.
  pause
  exit /b 1
)
notepad "%LOG%"
