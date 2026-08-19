VITEC Scoreboard v0.7.4 GameDay Defender-Safe

Why this build exists:
Windows Security returned Error 225 when trying to launch the unsigned/self-contained
VITEC.Scoreboard.exe as a Windows Service.

v0.7.4 removes the custom EXE from the service deployment.

Instead the Windows Service launches Microsoft's installed:
  C:\Program Files\dotnet\dotnet.exe

with:
  C:\Program Files\VITEC\Scoreboard\VITEC.Scoreboard.dll

This build does NOT require disabling Windows Defender or adding an antivirus exclusion.

Install:
  installer\Install-GameDay-Stable.cmd

The installer automatically deletes/replaces an older VITECScoreboard service and removes
the old custom VITEC.Scoreboard.exe if present.

Expected after installation:
  Services -> VITEC Scoreboard -> Running / Automatic
  http://localhost:5000

Logs:
  C:\ProgramData\VITEC Scoreboard\Logs\VITEC-Scoreboard.log
