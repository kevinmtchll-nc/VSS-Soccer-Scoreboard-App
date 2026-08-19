$ErrorActionPreference = 'Stop'

$serviceName = 'VITECSoccerScoreboard'
$firewallRule = 'VITEC Soccer Scoreboard TCP 5100'

& sc.exe query $serviceName *> $null
if ($LASTEXITCODE -eq 0) {
    & sc.exe stop $serviceName *> $null
    $deadline = (Get-Date).AddSeconds(30)
    do {
        $query = & sc.exe query $serviceName 2>&1
        if ($query -match '\bSTOPPED\b') { break }
        Start-Sleep -Milliseconds 500
    } while ((Get-Date) -lt $deadline)
    & sc.exe delete $serviceName | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw 'Windows could not remove the VITEC Scoreboard service.'
    }
}

& netsh.exe advfirewall firewall delete rule "name=$firewallRule" *> $null

# Customer settings and PostgreSQL data under ProgramData are intentionally preserved.
