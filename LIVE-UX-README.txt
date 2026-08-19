VITEC Scoreboard v0.7.5 Live UX

Built from the stable v0.7.4 GameDay service branch.

Changes are intentionally focused on the browser UI:
- Follow Live Matchup toggle
- Automatic current pitcher/batter selection
- Last pitch readout
- Current pitcher pitch count
- Average / max velocity
- Pitch mix percentages
- Last successful update age
- Score-change alert
- Existing GameCenter / heat maps preserved

Safer updater:
  installer\Update-Live-UX.cmd

This updater does NOT delete or recreate the Windows Service.
It only stops the existing VITECScoreboard service, replaces application files,
and starts it again.
