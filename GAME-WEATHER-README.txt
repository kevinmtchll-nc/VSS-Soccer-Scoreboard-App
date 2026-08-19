VITEC Scoreboard v0.8.2 Game Weather

Adds MLB game-weather data from gameData.weather:

- temperature (F)
- condition
- wind

Displayed in the GameCenter context strip as:

  Game Weather: 82°F · Partly Cloudy · Wind 8 mph, Out To RF

If MLB omits weather for a game, VS displays:

  Game Weather: —

This does not call an external weather service and does not change the Windows Service registration.

Update:
  installer\Update-Game-Weather.cmd
