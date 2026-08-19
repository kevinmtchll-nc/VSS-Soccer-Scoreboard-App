VITEC Scoreboard v0.8.0 Pre-Codex Feature Build

Based on the live-tested v0.7.8 Big Events branch.

Adds as much useful MLB feed data as practical before installer handoff:

STRUCTURED EVENTS
- event
- eventType
- isScoringPlay
- hasOut
- hasReview
- captivatingIndex
- RBI count
- structured player IDs
- structured home-run / scoring detection

BATTED BALL / STATCAST
- exit velocity
- launch angle
- estimated distance
- trajectory
- hardness
- event overlay displays EV / launch / distance when available

PLAYERS
- batter / pitcher position
- jersey number
- height / weight
- existing MLB headshots retained

RUNNERS
- runner origin/end base
- scoring / RBI movement summary

GAME CONTEXT
- venue
- series description
- game number in series
- day/night
- scheduled innings
- doubleheader context

SCOREBOARD
- series/game context on cards

LIVE GAME
- structured big-event detection is preferred
- text-description detection remains as fallback
- no service-registration change

Update:
  installer\Update-Pre-Codex.cmd
