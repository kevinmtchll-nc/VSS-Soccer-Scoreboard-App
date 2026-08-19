VITEC Scoreboard v0.8.1 Live Data Fix

Fixes the incomplete fields seen during the live Phillies/Cardinals test.

SERIES / GAME CONTEXT
- Uses MLB schedule data for:
  - series description
  - game number in series
  - games in series
  - day/night
  - scheduled innings
  - doubleheader
  - scheduled start time
- Schedule context is cached for 6 hours so live refreshes do not repeatedly fetch it.
- Failure to load schedule context never breaks GameCenter.

PLAYER METADATA
- Reads full player data from gameData.players using batter/pitcher IDs.
- Position
- jersey number
- height
- weight
- handedness continues from matchup data.

BATTED BALL / EVENT DATA
- Existing structured LiveEvent / Statcast parsing remains.
- Home-run/scoring overlays continue to show exit velocity, launch angle and distance when MLB supplies hitData.

DEPLOYMENT
- Existing Windows Service registration is preserved.
- No Defender/service model changes.

Update:
  installer\Update-Live-Data-Fix.cmd
