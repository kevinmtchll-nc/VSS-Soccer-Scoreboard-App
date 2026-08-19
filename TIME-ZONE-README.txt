VITEC Scoreboard v0.8.8 Display Time Zone

Adds configurable game-time display.

DEFAULT
- Server Local
- Uses the Windows server's current time zone automatically.
- Example: a Windows server configured for US Eastern displays game start times in Eastern.

CONFIGURATION
System / Database -> Display Settings -> Display Time Zone

Options are populated from TimeZoneInfo.GetSystemTimeZones() on the actual server.

BEHAVIOR
- Main Scoreboard card context now includes scheduled start time.
- GameCenter scheduled-start context uses the same configured display time zone.
- Changing the display time zone does not change Windows itself.
- Setting is saved to:
  C:\ProgramData\VITEC Scoreboard\vssettings.json
- Existing settings in that file are preserved.

Example main scoreboard:
  Regular Season · Game 3 of 3 · 7:10 PM start · Night

Update:
  installer\Update-Time-Zone.cmd

Existing Windows Service registration and ProgramData are preserved.
