VITEC Scoreboard v0.7.7 Images

Adds:
- MLB team logos on scoreboard cards
- Larger team logos in GameCenter score header
- current batter headshot
- current pitcher headshot

Image failures are non-fatal:
- missing/broken remote images automatically hide
- GameCenter and live data continue working

The image URLs are isolated in small frontend helper functions so VS can later
replace MLB image URL patterns or add local caching without rewriting the UI.

Update:
  installer\Update-Images.cmd

This updater preserves the existing Windows Service registration.
