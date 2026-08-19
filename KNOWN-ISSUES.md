# Known Issues at Handoff

## PostgreSQL
Current test result:
`Failed to connect to 127.0.0.1:5432`

Interpretation:
No PostgreSQL listener is currently reachable on localhost port 5432.
Installer work should automate or guide PostgreSQL deployment/configuration.

## Installer/updater history
Earlier development scripts retained stale `v0.7.4 GameDay - Defender Safe` labels.
Centralized versioning is required.

## Current deployment model
Use framework-dependent .NET 8 with Microsoft `dotnet.exe` hosting `VITEC.Scoreboard.dll`.
Do not revert to the previously Defender-blocked custom self-contained apphost EXE.
