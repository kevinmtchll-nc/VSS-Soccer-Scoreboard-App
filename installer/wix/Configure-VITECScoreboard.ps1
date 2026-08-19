param(
    [Parameter(Mandatory = $true)]
    [string] $SettingsTemplate
)

$ErrorActionPreference = 'Stop'

$serviceName = 'VITECSoccerScoreboard'
$displayName = 'VITEC Soccer Scoreboard'
$firewallRule = 'VITEC Soccer Scoreboard TCP 5100'
$InstallDirectory = Split-Path -Parent $PSScriptRoot
$dataDirectory = Join-Path $env:ProgramData 'VITEC Soccer Scoreboard'
$settingsPath = Join-Path $dataDirectory 'vssettings.json'
$applicationExe = Join-Path $InstallDirectory 'VITEC.Scoreboard.exe'

function Get-ServiceState {
    $output = & sc.exe query $serviceName 2>&1
    if ($LASTEXITCODE -ne 0) { return 'NOT_FOUND' }
    $stateLine = $output | Where-Object { $_ -match '^\s*STATE\s*:' } | Select-Object -First 1
    if ($stateLine -match '\b(RUNNING|STOPPED|START_PENDING|STOP_PENDING|PAUSED|PAUSE_PENDING|CONTINUE_PENDING)\b') {
        return $Matches[1]
    }
    return 'UNKNOWN'
}

function Wait-ServiceState([string] $DesiredState, [int] $TimeoutSeconds = 30) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    do {
        $state = Get-ServiceState
        if ($state -eq $DesiredState) { return $true }
        Start-Sleep -Milliseconds 500
    } while ((Get-Date) -lt $deadline)
    return $false
}

if (-not (Test-Path -LiteralPath $applicationExe)) {
    throw "The application payload is incomplete: $applicationExe was not found."
}

New-Item -ItemType Directory -Path $dataDirectory -Force | Out-Null
if (-not (Test-Path -LiteralPath $settingsPath)) {
    Copy-Item -LiteralPath $SettingsTemplate -Destination $settingsPath
}

& sc.exe query $serviceName *> $null
$serviceExists = $LASTEXITCODE -eq 0
if ($serviceExists) {
    $state = Get-ServiceState
    if ($state -ne 'STOPPED') {
        & sc.exe stop $serviceName *> $null
        if (-not (Wait-ServiceState 'STOPPED' 30)) {
            throw 'The existing VITEC Scoreboard service did not stop within 30 seconds.'
        }
    }
}

$serviceCommand = '"{0}" --windows-service' -f $applicationExe
if ($serviceExists) {
    & sc.exe config $serviceName "binPath= $serviceCommand" 'start= auto' "DisplayName= $displayName"
} else {
    & sc.exe create $serviceName "binPath= $serviceCommand" 'start= auto' "DisplayName= $displayName"
}
if ($LASTEXITCODE -ne 0) {
    throw 'Windows could not create or update the VITEC Scoreboard service.'
}

& sc.exe description $serviceName 'VITEC Soccer Scoreboard MLS live scoring and MatchCenter service.' | Out-Null
& sc.exe failure $serviceName 'reset= 86400' 'actions= restart/5000/restart/10000/restart/30000' | Out-Null
& sc.exe failureflag $serviceName 1 | Out-Null

& netsh.exe advfirewall firewall delete rule "name=$firewallRule" *> $null
& netsh.exe advfirewall firewall add rule "name=$firewallRule" dir=in action=allow protocol=TCP localport=5100 | Out-Null
if ($LASTEXITCODE -ne 0) {
    throw 'Windows could not create the inbound firewall rule for TCP port 5100.'
}

for ($attempt = 1; $attempt -le 3; $attempt++) {
    & sc.exe start $serviceName *> $null
    if ((Wait-ServiceState 'RUNNING' 20)) { break }
    Start-Sleep -Seconds 1
}
if ((Get-ServiceState) -ne 'RUNNING') {
    throw 'The VITEC Scoreboard service was installed but did not reach the Running state. Check the application log under ProgramData.'
}
