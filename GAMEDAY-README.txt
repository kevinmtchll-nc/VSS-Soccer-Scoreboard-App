VITEC Scoreboard v0.7.1 GAMEDAY

Purpose:
- Return to the v0.7 application branch that was visibly tested in the browser.
- Preserve flicker-free scoreboard refresh and staged GameCenter loading.
- Fix the malformed solution file found later in the v0.7-v0.9 source chain.
- Do NOT include the experimental Inno Setup installer work.

For immediate game testing:
1. Extract to a NEW folder.
2. Use the same Install-VITEC-Scoreboard.cmd workflow that successfully installed v0.7.
3. Existing PostgreSQL settings under ProgramData are preserved.

Important:
This environment cannot run the .NET SDK, so this package is not claimed as locally compiled here.
It is intentionally based on the browser-tested v0.7 branch rather than the later installer experiments.
