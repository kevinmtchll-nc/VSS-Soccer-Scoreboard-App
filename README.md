# VITEC Soccer Scoreboard

VITEC Soccer Scoreboard is the soccer counterpart to VITEC Scoreboard. It is a separate Windows application so both products can run on one computer without sharing a service name, configuration directory, or web port.

## Current implementation

- MLS daily schedule and live scores
- MatchCenter lineups, formations, match timeline, and team statistics
- Goals, shots, shot locations, xG, cards, substitutions, fouls, offsides, corners, possession, passing, and saves
- Automatic 30-second updates
- Existing VITEC foundation retained for PostgreSQL, configurable workspaces, JSON/XML feeds, advertising, and multicast/SRT output while soccer-specific screens and schemas are completed
- Default development web port: `5100`

## Product separation

- Product: `VITEC Soccer Scoreboard`
- Executable: `VITEC.SoccerScoreboard.exe`
- Windows service: `VITEC Soccer Scoreboard`
- Configuration: `%ProgramData%\VITEC Soccer Scoreboard`

## Data-source notice

The initial adapter reads publicly reachable JSON used by the MLS website. Public reachability does not grant redistribution or commercial-use rights. Obtain written permission from MLS or replace the adapter with a licensed sports-data provider before commercial deployment.

## Planned parity with VITEC Scoreboard

The implementation roadmap includes the complete feature set of the baseball product adapted to soccer: standings, daily leaders, alerts, draggable MatchCenter tiles, saved output templates, historical PostgreSQL analytics, JSON/XML schemas, advertising layouts, 1080p/4K scenes, and multicast/SRT encoding.
