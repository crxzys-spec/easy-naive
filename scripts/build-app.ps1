[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [string]$RuntimeIdentifier = 'win-x64',

    [string]$Version
)

. (Join-Path $PSScriptRoot 'common.ps1')
$repoRoot = Get-RepoRoot -ScriptRoot $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = Get-DefaultVersion -RepoRoot $repoRoot
}
$publishRoot = Join-Path $repoRoot "artifacts\publish\$Configuration\$RuntimeIdentifier"
$appOutput = Join-Path $publishRoot 'EasyNaive.App'
$elevationOutput = Join-Path $publishRoot 'EasyNaive.Elevation'

$commonArgs = @(
    '-c', $Configuration,
    '-r', $RuntimeIdentifier,
    '--self-contained', 'false',
    "-p:Version=$Version"
)

& dotnet publish (Join-Path $repoRoot 'src\EasyNaive.App\EasyNaive.App.csproj') @commonArgs '-o' $appOutput
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed for EasyNaive.App." }

& dotnet publish (Join-Path $repoRoot 'src\EasyNaive.Elevation\EasyNaive.Elevation.csproj') @commonArgs '-o' $elevationOutput
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed for EasyNaive.Elevation." }

Write-Host "App publish output: $appOutput"
Write-Host "Elevation publish output: $elevationOutput"
