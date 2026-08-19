VITEC Scoreboard v0.8.5 Overlay Visuals

Changes the Matchup Visual controls from mutually exclusive views to layers.

Independent Live Pitch layer:
- Live Pitches ON/OFF

Heat-map layer:
- At-Bat Heat Map
- Game Heat Map
- At-Bat and Game heat maps are mutually exclusive to avoid two heat fields obscuring each other.

Supported combinations:
- Live Pitches only
- At-Bat Heat Map only
- Game Heat Map only
- Live Pitches + At-Bat Heat Map
- Live Pitches + Game Heat Map
- Strike zone only

Rendering order:
1. Heat map
2. Strike-zone grid
3. Live pitch dots

This means live pitch dots remain clearly visible on top of the heat-map layer.

Update:
  installer\Update-Overlay-Visuals.cmd

Windows Service registration and backend are unchanged.
