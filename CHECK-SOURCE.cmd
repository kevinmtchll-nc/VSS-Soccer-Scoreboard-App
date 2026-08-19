@echo off
setlocal EnableExtensions
cd /d "%~dp0"
echo Checking Program.cs for duplicate commonData declarations...
for /f %%C in ('find /c "var commonData =" ^< "src\VS.Web\Program.cs"') do set COUNT=%%C
echo commonData declarations: %COUNT%
if not "%COUNT%"=="1" (
  echo ERROR: Program.cs contains duplicate commonData declarations.
  exit /b 1
)
echo Source sanity check passed.
exit /b 0
