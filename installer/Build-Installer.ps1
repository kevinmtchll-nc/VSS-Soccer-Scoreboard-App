param(
    [ValidateSet('Release', 'Debug')]
    [string] $Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$publishDirectory = Join-Path $root 'artifacts\publish\win-x64'
$outputDirectory = Join-Path $root 'artifacts\installer'
$wix = Join-Path $root '.tools\wix.exe'
$productSource = Join-Path $PSScriptRoot 'wix\Product.wxs'
$outputMsi = Join-Path $outputDirectory 'VITEC-Soccer-Scoreboard-Setup-v0.2.2.msi'

if (-not (Test-Path -LiteralPath $wix)) {
    throw 'WiX is missing. Run: dotnet tool install wix --tool-path .tools --version 5.0.2'
}

if (Test-Path -LiteralPath $publishDirectory) {
    $resolvedPublish = [System.IO.Path]::GetFullPath($publishDirectory)
    $resolvedArtifacts = [System.IO.Path]::GetFullPath((Join-Path $root 'artifacts'))
    if (-not $resolvedPublish.StartsWith($resolvedArtifacts, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to clean publish directory outside artifacts: $resolvedPublish"
    }
    Remove-Item -LiteralPath $resolvedPublish -Recurse -Force
}

New-Item -ItemType Directory -Path $publishDirectory, $outputDirectory -Force | Out-Null

dotnet publish (Join-Path $root 'src\VS.Web\VS.Web.csproj') `
    -c $Configuration `
    -r win-x64 `
    --self-contained false `
    -p:UseAppHost=true `
    -o $publishDirectory
if ($LASTEXITCODE -ne 0) { throw 'Application publish failed.' }

dotnet publish (Join-Path $root 'src\VS.VideoOutput\VS.VideoOutput.csproj') `
    -c $Configuration `
    -r win-x64 `
    --self-contained false `
    -p:UseAppHost=true `
    -o $publishDirectory
if ($LASTEXITCODE -ne 0) { throw 'Video output helper publish failed.' }

Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'wix\Start-VideoOutput.ps1') `
    -Destination (Join-Path $publishDirectory 'Start-VideoOutput.ps1') -Force

$scoreboardAppHost = Join-Path $publishDirectory 'VITEC.SoccerScoreboard.exe'
$videoAppHost = Join-Path $publishDirectory 'VITEC.SoccerVideoOutput.exe'
if (-not (Test-Path -LiteralPath $scoreboardAppHost) -or -not (Test-Path -LiteralPath $videoAppHost)) {
    throw 'The branded VITEC application executables were not created.'
}

& $wix build $productSource `
    -arch x64 `
    -ext WixToolset.UI.wixext `
    -ext WixToolset.Firewall.wixext `
    -ext WixToolset.Util.wixext `
    -d "PublishDirectory=$publishDirectory" `
    -d "ProjectRoot=$root" `
    -o $outputMsi
if ($LASTEXITCODE -ne 0) { throw 'Installer build failed.' }

if (-not (Test-Path -LiteralPath $outputMsi)) {
    throw "Installer compiler reported success but did not create $outputMsi"
}

# Do not publish a package that merely exists while WiX is still writing it.
# Opening the Property table through Windows Installer verifies the compound
# file, MSI database, and embedded cabinet directory are readable.
$windowsInstaller = New-Object -ComObject WindowsInstaller.Installer
$database = $windowsInstaller.OpenDatabase($outputMsi, 0)
$view = $database.OpenView("SELECT `Value` FROM `Property` WHERE `Property` = 'ProductName'")
$view.Execute()
$record = $view.Fetch()
$productName = if ($null -ne $record) { $record.StringData(1) } else { '' }
if ($productName -ne 'VITEC Soccer Scoreboard') {
    throw "Installer validation failed. Expected VITEC Soccer Scoreboard but found '$productName'."
}

Write-Host "Installer created and validated: $outputMsi"
