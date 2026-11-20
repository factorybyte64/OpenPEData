<#
.SYNOPSIS
    Publishes OpenPEData.Viewer (with the .NET parser OpenPEData.dll) and creates a silent Velopack Setup.exe (no auto-updates).

.DESCRIPTION
    1) dotnet publish OpenPEData.Viewer to a staging folder; the parser is the library OpenPEData.dll
       (dotnet\src\OpenPEData) next to the executable. The script checks that it is there and that no Python parser
       (parser\) is in the package.
    2) vpk pack -> embedded Velopack payload. The setup installs the .NET 10 Desktop Runtime if it is missing
       (--framework net10-x64-desktop). Python is not needed.
    3) OpenPEData.SetupLauncher wraps the payload and always runs Setup with --silent
       (reinstall over existing install without repair/already-installed dialogs).

.PARAMETER Configuration
    Debug or Release (default: Release).

.PARAMETER Version
    Velopack pack version (default: <Version> of OpenPEData.Viewer.csproj).

.PARAMETER SignParams
    Optional signtool.exe arguments without the file name, e.g.
    '/sha1 <certificate thumbprint> /fd sha256 /tr http://timestamp.digicert.com /td sha256'.
    Velopack signs the application binaries and its setup with them, and the script signs the final
    OpenPEData-Setup-V*.exe. Default: environment variable OPENPEDATA_SIGN_PARAMS; without both the setup stays unsigned
    (Windows SmartScreen then shows "unknown publisher").

.PARAMETER SignToolPath
    Optional path to signtool.exe for the final setup; default signtool on PATH or the newest Windows SDK copy.

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File .\ui\scripts\Pack-OpenPEData.ps1
#>

param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [string]$Version = '',
    [string]$SignParams = $env:OPENPEDATA_SIGN_PARAMS,
    [string]$SignToolPath = ''
)

$ErrorActionPreference = 'Stop'

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot = (Resolve-Path (Join-Path $ScriptDir '..')).Path
$UiProject = Join-Path $RepoRoot 'OpenPEData.Viewer\OpenPEData.Viewer.csproj'
$LauncherProject = Join-Path $RepoRoot 'OpenPEData.SetupLauncher\OpenPEData.SetupLauncher.csproj'
$PublishDir = Join-Path $RepoRoot "artifacts\publish\$Configuration"
$VpkStagingDir = Join-Path $RepoRoot 'artifacts\_vpk-staging'
$ReleasesDir = Join-Path $RepoRoot 'artifacts\Releases'
$PayloadPath = Join-Path $RepoRoot 'OpenPEData.SetupLauncher\Embedded\SetupPayload.exe'
$IconPath = Join-Path $RepoRoot 'OpenPEData.Viewer\Assets\app.ico'
$VpkVersion = '1.2.0'
$Framework = 'net10-x64-desktop'

function Ensure-VpkTool {
    param([string]$ExpectedVersion)
    $installed = dotnet tool list -g 2>$null | Select-String -Pattern '^\s*vpk\s+' -CaseSensitive:$false
    if ($installed -match "vpk\s+$([regex]::Escape($ExpectedVersion))") {
        return
    }
    Write-Output "==> Installing vpk global tool $ExpectedVersion ..."
    dotnet tool install -g vpk --version $ExpectedVersion
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to install vpk $ExpectedVersion"
    }
}

function Find-SignTool {
    param([string]$ExplicitPath)
    if (-not [string]::IsNullOrWhiteSpace($ExplicitPath)) {
        if (-not (Test-Path -LiteralPath $ExplicitPath)) {
            throw "signtool.exe not found: $ExplicitPath"
        }
        return $ExplicitPath
    }
    $onPath = Get-Command signtool.exe -ErrorAction SilentlyContinue
    if ($onPath) {
        return $onPath.Source
    }
    $kits = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\bin'
    $found = Get-ChildItem -Path $kits -Filter signtool.exe -Recurse -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match '\\x64\\' } |
        Sort-Object { $_.VersionInfo.FileVersionRaw } -Descending |
        Select-Object -First 1
    if (-not $found) {
        throw 'signtool.exe not found; install the Windows SDK or pass -SignToolPath.'
    }
    return $found.FullName
}

function Get-UiProjectVersion {
    param([string]$ProjectPath)
    [xml]$proj = Get-Content -LiteralPath $ProjectPath -ErrorAction Stop
    $version = $proj.Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
    if ([string]::IsNullOrWhiteSpace($version)) {
        throw "No <Version> found in $ProjectPath"
    }
    return $version.Trim()
}

if (-not (Test-Path -LiteralPath $UiProject)) {
    throw "UI project not found: $UiProject"
}
if (-not (Test-Path -LiteralPath $LauncherProject)) {
    throw "Setup launcher project not found: $LauncherProject"
}
if (-not (Test-Path -LiteralPath $IconPath)) {
    throw "Icon not found: $IconPath"
}

if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = Get-UiProjectVersion -ProjectPath $UiProject
}

$VersionLabel = 'V' + ($Version -replace '\.', '-')
$Sign = -not [string]::IsNullOrWhiteSpace($SignParams)
if ($Sign) {
    $SignTool = Find-SignTool -ExplicitPath $SignToolPath
    Write-Output "==> Code signing on (signtool: $SignTool)"
}
else {
    Write-Warning 'Code signing off: pass -SignParams or set OPENPEDATA_SIGN_PARAMS to sign the setup.'
}

Ensure-VpkTool -ExpectedVersion $VpkVersion

Write-Output "==> Publishing OpenPEData.Viewer ($Configuration) -> $PublishDir"
if (Test-Path -LiteralPath $PublishDir) {
    Remove-Item -LiteralPath $PublishDir -Recurse -Force
}
dotnet publish $UiProject -c $Configuration -o $PublishDir --nologo -p:PackSetup=false
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

$mainExe = Join-Path $PublishDir 'OpenPEData.Viewer.exe'
if (-not (Test-Path -LiteralPath $mainExe)) {
    throw "Publish did not produce: $mainExe"
}

# the parser is the .NET library next to the executable; the setup carries no Python parser any more
if (-not (Test-Path -LiteralPath (Join-Path $PublishDir 'OpenPEData.dll'))) {
    throw "Parser library OpenPEData.dll missing in the publish output"
}
if (Test-Path -LiteralPath (Join-Path $PublishDir 'parser')) {
    throw "Python parser found in the publish output (parser\), the setup must not carry it"
}

Write-Output "==> Creating Velopack payload $Version -> $VpkStagingDir"
if (Test-Path -LiteralPath $VpkStagingDir) {
    Remove-Item -LiteralPath $VpkStagingDir -Recurse -Force
}
New-Item -ItemType Directory -Path $VpkStagingDir -Force | Out-Null

$vpkArgs = @(
    'pack',
    '-u', 'OpenPEData',
    '-v', $Version,
    '-p', $PublishDir,
    '-e', 'OpenPEData.Viewer.exe',
    '--packTitle', 'OpenPEData Viewer',
    '--packAuthors', 'factorybyte',
    '--icon', $IconPath,
    '--delta', 'none',
    '--noPortable',
    '--framework', $Framework,
    '-o', $VpkStagingDir
)
if ($Sign) {
    $vpkArgs += @('--signParams', $SignParams)
}
& vpk @vpkArgs

if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

$vpkSetupExe = Join-Path $VpkStagingDir 'OpenPEData-win-Setup.exe'
if (-not (Test-Path -LiteralPath $vpkSetupExe)) {
    $found = Get-ChildItem -LiteralPath $VpkStagingDir -Filter '*-Setup.exe' -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($found) {
        $vpkSetupExe = $found.FullName
    }
}
if (-not (Test-Path -LiteralPath $vpkSetupExe)) {
    throw "Velopack did not produce a *-Setup.exe in $VpkStagingDir"
}

$payloadDir = Split-Path -Parent $PayloadPath
if (-not (Test-Path -LiteralPath $payloadDir)) {
    New-Item -ItemType Directory -Path $payloadDir -Force | Out-Null
}
Copy-Item -LiteralPath $vpkSetupExe -Destination $PayloadPath -Force

Write-Output "==> Building silent setup launcher -> $ReleasesDir"
if (Test-Path -LiteralPath $ReleasesDir) {
    Remove-Item -LiteralPath $ReleasesDir -Recurse -Force
}
New-Item -ItemType Directory -Path $ReleasesDir -Force | Out-Null

dotnet publish $LauncherProject -c $Configuration -o $ReleasesDir --nologo
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

$builtSetup = Join-Path $ReleasesDir 'OpenPEData-Setup.exe'
if (-not (Test-Path -LiteralPath $builtSetup)) {
    throw "Setup launcher did not produce: $builtSetup"
}

$setupExe = Join-Path $ReleasesDir "OpenPEData-Setup-$VersionLabel.exe"
if (Test-Path -LiteralPath $setupExe) {
    Remove-Item -LiteralPath $setupExe -Force
}
Rename-Item -LiteralPath $builtSetup -NewName (Split-Path -Leaf $setupExe)

if ($Sign) {
    Write-Output "==> Signing $setupExe"
    $signArgs = @('sign') + @($SignParams -split '\s+' | Where-Object { $_ }) + @($setupExe)
    & $SignTool @signArgs
    if ($LASTEXITCODE -ne 0) {
        throw "signtool failed with exit code $LASTEXITCODE"
    }
    $signature = Get-AuthenticodeSignature -LiteralPath $setupExe
    if ($signature.Status -ne 'Valid') {
        throw "Setup signature is $($signature.Status): $($signature.StatusMessage)"
    }
}

Write-Output ""
Write-Output "==> Done."
Write-Output "    Setup:  $setupExe"
Write-Output "    Version: $VersionLabel"
Write-Output ("    Signed:  " + $(if ($Sign) { 'yes' } else { 'no' }))
Write-Output "    Behavior: silent reinstall (no already-installed dialog)"
Write-Output "    Install: per-user under %LocalAppData%\OpenPEData\"
Write-Output "    Uninstall: Windows Apps & features, or Update.exe uninstall"
exit 0
