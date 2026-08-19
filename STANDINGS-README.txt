VITEC Scoreboard v0.8.7 MLB Standings

Adds:
- top-level Standings page
- American League / National League tabs
- three divisions per league
- team logos
- W / L
- winning percentage
- games back
- Wild Card games back
- Last 10
- streak
- home / away records
- runs scored / allowed-derived run differential
- division leader highlighting
- Wild Card rank label
- clinch indicator when MLB supplies it

Data source:
  MLB Stats API /api/v1/standings
  leagues 103 (AL) and 104 (NL)
  regularSeason standings

The VS API caches standings for 60 seconds.

Open:
  http://localhost:5000/standings.html

Update:
  installer\Update-Standings.cmd
