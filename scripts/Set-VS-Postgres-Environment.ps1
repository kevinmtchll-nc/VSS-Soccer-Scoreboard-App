param(
    [string]$HostName = "localhost",
    [int]$Port = 5432,
    [string]$Database = "vitec_scoreboard",
    [string]$Username = "vsapp",
    [Parameter(Mandatory = $true)]
    [string]$Password,
    [switch]$PersistForUser
)

$connection = "Host=$HostName;Port=$Port;Database=$Database;Username=$Username;Password=$Password;Pooling=true;Minimum Pool Size=0;Maximum Pool Size=50;Timeout=10;Command Timeout=30"

$env:ConnectionStrings__VSPostgres = $connection
Write-Host "Configured PostgreSQL for the current PowerShell process."

if ($PersistForUser) {
    [Environment]::SetEnvironmentVariable(
        "ConnectionStrings__VSPostgres",
        $connection,
        [EnvironmentVariableTarget]::User
    )
    Write-Host "Also saved ConnectionStrings__VSPostgres to your Windows user environment."
    Write-Host "Open a new PowerShell window before starting VS."
}
