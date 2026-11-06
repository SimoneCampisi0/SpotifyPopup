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

dotnet publish core\SpotifyPopup.csproj -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=none -p:DebugSymbols=false -p:NuGetAudit=false `
    -o core\bin\publish\win-x64
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

