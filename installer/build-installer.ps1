param(
    [string]$Version = '1.0.0'
)

$ErrorActionPreference = 'Stop'

$Version = $Version -replace '^[vV]', ''
if ($Version -notmatch '^\d+\.\d+\.\d+$') {
    throw 'Version must use a stable semantic version such as 1.0.0 or v1.0.0.'
}

$project = Join-Path $PSScriptRoot '..\BatteryGuard.csproj'
$project = [IO.Path]::GetFullPath($project)
$installerScript = Join-Path $PSScriptRoot 'BatteryGuard.iss'
$dotnetCommand = Get-Command dotnet.exe -ErrorAction SilentlyContinue
$dotnetExe = if ($dotnetCommand) { $dotnetCommand.Source } else { 'C:\Program Files\dotnet\dotnet.exe' }
if (-not (Test-Path -LiteralPath $dotnetExe)) {
    throw '.NET 8 SDK was not found. Install the SDK, reopen PowerShell, and rerun this script.'
}

# Keep the CLI's first-run files inside this checkout, which also works in
# restricted environments where the current user's profile is read-only.
$env:DOTNET_CLI_HOME = Join-Path $PSScriptRoot '.dotnet-cli-home'
$env:APPDATA = Join-Path $PSScriptRoot '.build-appdata'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'
$nugetConfig = Join-Path $PSScriptRoot 'NuGet.Config'
New-Item -ItemType Directory -Force -Path (Join-Path $env:APPDATA 'NuGet') | Out-Null

& $dotnetExe restore $project -r win-x64 --configfile $nugetConfig -p:NuGetAudit=false
if ($LASTEXITCODE -ne 0) { throw 'Restoring the app packages failed.' }

& $dotnetExe publish $project -c Release -r win-x64 --self-contained true --no-restore `
    "-p:Version=$Version" `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true
if ($LASTEXITCODE -ne 0) { throw 'The self-contained app publish failed.' }

$isccCommand = Get-Command iscc.exe -ErrorAction SilentlyContinue
$compilerCandidates = @()
if ($isccCommand) { $compilerCandidates += $isccCommand.Source }
$compilerCandidates += @(
    (Join-Path $env:ProgramFiles 'Inno Setup 7\ISCC.exe'),
    (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 7\ISCC.exe'),
    (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
    (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe')
) | Where-Object { $_ -and (Test-Path -LiteralPath $_) }

if (-not $compilerCandidates) {
    throw 'Inno Setup 6 or 7 was not found. Install Inno Setup, then rerun this script.'
}

& $compilerCandidates[0] "/DMyAppVersion=$Version" $installerScript
if ($LASTEXITCODE -ne 0) { throw 'The installer build failed.' }

$installerPath = Join-Path (Split-Path $PSScriptRoot) "artifacts\BatteryGuard-Setup-$Version-win-x64.exe"
Write-Host "Installer created: $installerPath"
