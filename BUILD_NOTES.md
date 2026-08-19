# Build notes

## 0.8.16

- Added the current inning number and inning state to every applicable main scoreboard game card.
- Live status is displayed in United States English, such as `In Progress · Top 5th` or `In Progress · Bottom 7th`.
- Pregame cards remain unchanged until MLB supplies a linescore.
- Added current season statistics for the active batter (AVG, HR, RBI) and pitcher (W-L, ERA, SO).
- Added a `Show Current Game Box Score` checkbox with live batting and pitching lines for both teams.
- Fixed upgrade error 1603 caused by attempting to configure or start the service while it was still stopping.
- Service installation now waits for `STOPPED`, retries startup, and verifies that the service reaches `RUNNING` before reporting success.
- Removed a trailing-backslash command-line ambiguity by deriving the install directory inside the elevated service configuration script.
- Fixed the elevated action to use 64-bit PowerShell and the 64-bit Program Files directory so it reliably finds `C:\Program Files\dotnet\dotnet.exe`.
- Replaced all PowerShell, `sc.exe`, and `netsh.exe` installer actions after Microsoft Defender blocked those behaviors.
- Service installation, service control, recovery, and the TCP 5000 firewall exception now use declarative WiX/Windows Installer tables.
- Bundled the official Microsoft ASP.NET Core Runtime 8.0.26 private runtime; its published SHA-512 matched and `dotnet.exe` has a valid Microsoft signature.

## 0.8.10 baseline

- Baseline verification completed on August 12, 2026 with `VERIFY-BASELINE-BUILD.cmd`.
- Result: succeeded with 0 errors.
- Two NU1900 warnings were emitted because the NuGet vulnerability service was not reachable from the build sandbox. Package restore and compilation both succeeded.
- No application source changes were required to establish the baseline.

## Windows installer

- Added a WiX Toolset 5 installer build.
- The application is published framework-dependent for `win-x64` with `UseAppHost=false`.
- The installer configures the `VITECScoreboard` service to use Microsoft's installed `dotnet.exe` and `VITEC.Scoreboard.dll`.
- Existing settings under `C:\ProgramData\VITEC Scoreboard` are preserved during upgrades and uninstall.
- A TCP 5000 Windows Firewall rule is configured during install and removed during a normal uninstall.
- PostgreSQL remains optional and is not installed or removed by this first installer milestone.
- Corrected the major-upgrade action sequence after field testing exposed Windows Installer error 2613.
- Removed WiX's placeholder Latin license page; installer UI is explicitly configured for United States English (language 1033).
