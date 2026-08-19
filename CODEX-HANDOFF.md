# VITEC Scoreboard — Codex Engineering Handoff

## Baseline

Start from **VITEC Scoreboard v0.8.10** in this package.

This is the current feature baseline after live MLB testing on August 12, 2026.

Do not replace this source tree with older v0.7.x or experimental v0.9 branches.

## First requirement: prove the baseline builds

Before changing installer or application behavior:

1. Run:
   `VERIFY-BASELINE-BUILD.cmd`
2. Fix only actual compile/build errors.
3. Confirm the untouched v0.8.10 feature baseline builds successfully.
4. Record any fixes in `BUILD_NOTES.md`.

Do not begin installer redesign before the baseline is reproducible.

## Working deployment model

The working Windows Service deployment is framework-dependent and uses Microsoft's installed .NET host:

`C:\Program Files\dotnet\dotnet.exe`
+
`C:\Program Files\VITEC\Scoreboard\VITEC.Scoreboard.dll`

This was used successfully after Windows Defender blocked an earlier unsigned custom self-contained application host with Service Error 225.

### Therefore

- Use .NET 8.
- Publish framework-dependent.
- Use `--self-contained false`.
- Use `-p:UseAppHost=false`.
- Do not depend on a generated custom `VITEC.Scoreboard.exe` for the Windows Service.
- Do not disable Windows Defender.
- Do not add Defender exclusions.

Use:
`installer\Publish-Defender-Safe.cmd`

as the known-safe publish reference.

## Product goal

Turn VITEC Scoreboard into a deployable Windows product for:

- Windows 10/11
- Windows Server 2022
- Windows Server 2025

Normal installation must not require the customer to use CMD or PowerShell.

## Installer objective

Produce an actual Windows installer artifact:

`VITEC-Scoreboard-Setup.exe`

or an MSI if the chosen toolchain makes MSI more appropriate.

The installer must support:

- fresh install
- upgrade
- repair
- uninstall

The final artifact must physically exist and its exact output path must be reported.

Do not report installer success until the installer EXE/MSI exists.

## Install location

Application files:

`C:\Program Files\VITEC\Scoreboard`

Writable configuration/data:

`C:\ProgramData\VITEC Scoreboard`

Do not place mutable data under Program Files.

## Windows Service

Internal service name:

`VITECScoreboard`

Display name:

`VITEC Scoreboard`

Required behavior:

- Automatic startup
- Defender-safe `dotnet.exe + VITEC.Scoreboard.dll` command line
- Service recovery configured to restart on failure
- Upgrade should stop/configure/restart the existing service
- Do not delete/recreate the service unnecessarily during an upgrade

## Networking

Default listen URL:

`http://0.0.0.0:5000`

Installer should create a Windows Firewall inbound TCP rule for port 5000.

The web UI should be reachable locally at:

`http://localhost:5000`

and from permitted LAN hosts using the server IP.

## Existing application features that must be preserved

### Main MLB Scoreboard

- daily MLB schedule
- date selector
- live/pre-game/final state
- team logos
- team W/L records
- current scores
- venue
- series context
- game number in series
- day/night
- configured scheduled start time
- link into GameCenter

### Configurable display time zone

Default:

`Server Local`

The application uses the Windows server's time zone automatically when this option is selected.

The System / Database page exposes a configurable Display Time Zone dropdown.

The setting is saved under:

`C:\ProgramData\VITEC Scoreboard\vssettings.json`

Changing the VS display time zone must not change the Windows operating-system time zone.

### GameCenter Live View

Preserve:

- team logos
- score
- inning/game state
- current batter
- current pitcher
- player headshots
- player metadata
- balls
- strikes
- outs
- base occupancy
- last play
- pitch type
- velocity
- pitch result
- pX / pZ strike-zone visualization
- recent action / recent pitches
- live automatic refresh

### Matchup visualizations

Preserve:

- Live Pitches layer
- At-Bat Heat Map layer
- Game Heat Map layer
- Live Pitches can overlay At-Bat Heat Map
- Live Pitches can overlay Game Heat Map
- heat map rendered beneath pitch dots
- strike-zone grid remains visible

### Big-event overlays

Preserve structured-event behavior for supported MLB events such as:

- home run
- grand slam
- scoring play
- double
- triple
- stolen base
- strikeout
- double play
- pitching change

When MLB provides batted-ball data, preserve display support for:

- exit velocity
- launch angle
- estimated distance

### MLB game weather

Preserve:

- temperature
- condition
- wind

Display as "Game Weather".

Weather absence must not break GameCenter.

### Standings

Preserve and verify:

- American League / National League tabs
- AL East / Central / West
- NL East / Central / West
- team logos
- W
- L
- PCT
- GB
- WCGB
- Last 10
- Streak
- Home record
- Away record
- Run Differential
- division-leader highlighting
- Wild Card standings
- top three Wild Card positions

Division-ID mapping currently used:

- 200 = AL West
- 201 = AL East
- 202 = AL Central
- 203 = NL West
- 204 = NL East
- 205 = NL Central

Verify this behavior against MLB API responses; do not silently regress to labels such as "Division 200".

## MLB API usage

The code currently uses MLB Stats API endpoints including:

- schedule
- live game feed
- standings

Audit exact endpoints in `VS.MLB\MlbStatsClient.cs` before modifying request behavior.

Do not introduce scraping when the MLB API already supplies the data.

## Images

Current UI uses MLB-hosted team/player images.

Image failure must remain non-fatal.

Do not allow remote-image problems to take down the live data UI.

## PostgreSQL status

PostgreSQL is **not currently reachable on the user's test machine**.

Observed test error:

`Failed to connect to 127.0.0.1:5432`

That indicates there is currently no PostgreSQL listener reachable on local TCP port 5432.

This is not yet an application-credential diagnosis because the TCP connection itself fails first.

### Current intended PostgreSQL defaults

Host:

`localhost`

Port:

`5432`

Database:

`vitec_scoreboard`

Application user:

`vsapp`

### Existing UI

System / Database currently exposes:

- Host
- Port
- Database
- Username
- Password
- Test Connection
- Save PostgreSQL Settings
- Initialize Database Schema
- status fields
- historical import

Preserve this UI.

## PostgreSQL installer requirements

PostgreSQL is optional for live MLB Scoreboard operation, but required for historical storage/analytics.

### Installer flow

If the user selects local integrated PostgreSQL:

1. Detect whether a compatible PostgreSQL installation/service exists.
2. If found, offer to use/configure it.
3. If not found, install a supported PostgreSQL release using an official/supported distribution mechanism.
4. Ensure the PostgreSQL Windows service is running.
5. Configure local access.
6. Create database:
   `vitec_scoreboard`
7. Create dedicated application account:
   `vsapp`
8. Generate or securely set a password.
9. Do not ship one hard-coded password used on every installation.
10. Save connection settings securely for VS.
11. Initialize the VS schema.
12. Test the connection.
13. Report database setup success/failure clearly.

### Existing / remote PostgreSQL

The installer must also allow customers to skip integrated PostgreSQL and configure:

- remote host
- custom port
- existing database
- existing user/password

### Failure behavior

PostgreSQL failure must never prevent:

- VITEC Scoreboard service startup
- main live scoreboard
- GameCenter
- standings
- weather
- live pitch data

Only historical/database-specific functions should degrade.

### Upgrade/uninstall

Do not delete the customer's PostgreSQL database during a normal VITEC Scoreboard upgrade.

Normal application uninstall should preserve PostgreSQL data by default.

## Settings/security

Do not embed:

- MLB developer credentials
- PostgreSQL passwords
- API keys
- user passwords

in source control, installer scripts, logs, or command lines when avoidable.

The current settings path is:

`C:\ProgramData\VITEC Scoreboard\vssettings.json`

Audit how secrets are stored and improve secret handling if needed without breaking existing upgrades.

## Versioning cleanup

Earlier updater scripts retained stale banners such as:

`v0.7.4 GameDay - Defender Safe`

even when later versions were being installed.

Centralize the version so these all agree:

- assembly/product version
- web UI
- System / Database page
- health API
- installer
- Programs & Features
- Windows file metadata
- logs
- service description if versioned

Do not maintain hard-coded version strings independently in many files.

## Installer UX

Use a normal graphical installer.

Suggested pages:

1. Welcome
2. License/intro if appropriate
3. Install directory
4. Service/network settings
5. PostgreSQL choice:
   - No historical database now
   - Configure existing PostgreSQL
   - Install/configure local PostgreSQL
6. Display time-zone choice
   - Server Local default
   - selectable Windows time zone
7. Ready to Install
8. Progress
9. Verification/results
10. Finish / Open VITEC Scoreboard

The installer should detect upgrades and preserve current settings.

## Required validation after install

Verify all of these:

1. Windows Services shows:
   - VITEC Scoreboard
   - Running
   - Automatic

2. Service command line uses:
   `dotnet.exe` + `VITEC.Scoreboard.dll`

3. Browser loads:
   `http://localhost:5000`

4. Main scoreboard retrieves current MLB schedule.

5. Opening an active live game does not stop/crash the service.

6. GameCenter live updates work.

7. Player/team images fail gracefully if unavailable.

8. Matchup overlays work.

9. Game Weather works when supplied.

10. Standings page shows real division names.

11. Wild Card standings render.

12. Display Time Zone saves and persists across service restart.

13. Upgrade preserves:
    `C:\ProgramData\VITEC Scoreboard`

14. PostgreSQL failure does not break live Scoreboard functionality.

15. If PostgreSQL is configured:
    - Test Connection succeeds
    - Initialize Schema succeeds
    - historical import works
    - game/pitch counts populate

## Do not do these

- Do not rewrite the working application from scratch.
- Do not remove features to simplify packaging.
- Do not switch back to the Defender-blocked self-contained custom apphost EXE.
- Do not require PowerShell/CMD from the end user.
- Do not claim the installer works without physically producing and testing the artifact.
- Do not delete customer historical data on upgrade.
- Do not hard-code a universal database password.

## Recommended Codex work order

1. Audit current source.
2. Build untouched v0.8.10.
3. Fix compile/test issues only.
4. Run app locally and verify live functionality.
5. Centralize versioning.
6. Harden configuration/secret handling.
7. Build installer.
8. Add optional PostgreSQL deployment/configuration.
9. Test clean install.
10. Test upgrade from current development installation.
11. Test repair.
12. Test uninstall.
13. Re-test live MLB behavior.
14. Produce final artifacts.

## Required Codex deliverables

Return:

1. Corrected source tree
2. `BUILD_NOTES.md`
3. Installer source/project
4. repeatable installer build script
5. final `VITEC-Scoreboard-Setup.exe` or `.msi`
6. exact absolute output path
7. clean-install test results
8. upgrade test results
9. uninstall test results
10. PostgreSQL test results
11. remaining known issues

Do not stop at documentation. Build and verify the actual installer artifact.
