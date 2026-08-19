param(
    [string]$HostName = "localhost",
    [int]$Port = 5432,
    [string]$Database = "vitec_scoreboard",
    [string]$Username = "vsapp",
    [Parameter(Mandatory = $true)]
    [string]$Password
)

$ErrorActionPreference = "Stop"

$env:ConnectionStrings__VSPostgres = "Host=$HostName;Port=$Port;Database=$Database;Username=$Username;Password=$Password;Pooling=true;Minimum Pool Size=0;Maximum Pool Size=50;Timeout=10;Command Timeout=30"

Write-Host ""
Write-Host "VITEC Scoreboard PostgreSQL connection configured for this PowerShell process."
Write-Host "Host: $HostName"
Write-Host "Port: $Port"
Write-Host "Database: $Database"
Write-Host "Username: $Username"
Write-Host ""
Write-Host "Starting VS v0.4..."
Write-Host ""

dotnet run --project ".\src\VS.Web\VS.Web.csproj"
