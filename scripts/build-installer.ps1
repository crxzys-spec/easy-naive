[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [string]$RuntimeIdentifier = 'win-x64',

    [string]$Version
)

. (Join-Path $PSScriptRoot 'common.ps1')

if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = Get-DefaultVersion -RepoRoot (Get-RepoRoot -ScriptRoot $PSScriptRoot)
}

function Get-InstallerVersion {
    param([string]$InputVersion)

    $match = [regex]::Match($InputVersion, '^(?<major>\d+)\.(?<minor>\d+)\.(?<patch>\d+)')
    if (-not $match.Success) {
        throw "Version '$InputVersion' does not start with a valid semantic version."
    }

    return "{0}.{1}.{2}" -f $match.Groups['major'].Value, $match.Groups['minor'].Value, $match.Groups['patch'].Value
}

$repoRoot = Get-RepoRoot -ScriptRoot $PSScriptRoot
$installerVersion = Get-InstallerVersion -InputVersion $Version
$packageRoot = Join-Path $repoRoot "artifacts\package\$Configuration\$RuntimeIdentifier"
$installerOutput = Join-Path $repoRoot "artifacts\installer\$Configuration\$RuntimeIdentifier"
$wixProject = Join-Path $repoRoot 'packaging\wix\EasyNaive.Setup.wixproj'

& (Join-Path $PSScriptRoot 'package.ps1') -Configuration $Configuration -RuntimeIdentifier $RuntimeIdentifier -Version $Version
if ($LASTEXITCODE -ne 0) { throw "package.ps1 failed." }

$latestLayout = Get-ChildItem -Path $packageRoot -Directory |
    Sort-Object LastWriteTimeUtc -Descending |
    Select-Object -First 1

if ($null -eq $latestLayout) {
    throw "No packaged layout was found under '$packageRoot'."
}

New-Item -ItemType Directory -Path $installerOutput -Force | Out-Null

& dotnet build $wixProject `
    -c $Configuration `
    "-p:PackageSourceRoot=$($latestLayout.FullName)" `
    "-p:InstallerProductVersion=$installerVersion" `
    "-p:OutputPath=$installerOutput\"

if ($LASTEXITCODE -ne 0) {
    throw "WiX installer build failed."
}

Write-Host "Installer layout source: $($latestLayout.FullName)"
Write-Host "Installer output: $installerOutput"
