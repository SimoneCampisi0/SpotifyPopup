$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
$env:DOTNET_CLI_HOME = Join-Path $PSScriptRoot '.dotnet-home'
$env:NUGET_PACKAGES = Join-Path $PSScriptRoot '.nuget\packages'
$env:APPDATA = Join-Path $PSScriptRoot '.local-appdata'
$env:LOCALAPPDATA = Join-Path $PSScriptRoot '.local-appdata'
$env:TEMP = Join-Path $PSScriptRoot '.temp'
$env:TMP = $env:TEMP
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE = 'true'
New-Item -ItemType Directory -Force -Path $env:TEMP | Out-Null
dotnet build SpotifyPopup\SpotifyPopup.csproj -p:NuGetAudit=false
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
