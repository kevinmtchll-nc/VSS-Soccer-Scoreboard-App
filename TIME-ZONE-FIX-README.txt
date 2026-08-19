VITEC Scoreboard v0.8.9 Time Zone Fix

Fixes v0.8.8 compile errors:
- Added missing using System.Text.Json;
- JsonDocument now resolves.
- JsonSerializerOptions now resolves.
- Normalized cached schedule/standings collections to remove nullable warning paths.

Keeps:
- Server Local default time zone
- configurable Display Time Zone
- scoreboard scheduled start time
- GameCenter display time
- standings
- weather
- matchup overlays
- all v0.8.x live features

Update:
  installer\Update-Time-Zone-Fix.cmd
