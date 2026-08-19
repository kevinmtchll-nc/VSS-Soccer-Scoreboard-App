$ErrorActionPreference = 'Stop'

$helper = Join-Path $PSScriptRoot 'VITEC.SoccerVideoOutput.exe'

if (Test-Path -LiteralPath $helper) {
    Start-Process -FilePath $helper `
        -WorkingDirectory $PSScriptRoot `
        -WindowStyle Hidden
}
