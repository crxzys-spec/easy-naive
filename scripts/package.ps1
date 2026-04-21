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
$timestamp = (Get-Date).ToUniversalTime().ToString('yyyyMMdd-HHmmss-fff')
$packageRoot = Join-Path $repoRoot "artifacts\package\$Configuration\$RuntimeIdentifier"
$layoutRoot = Join-Path $packageRoot "EasyNaive-$Version-$timestamp"
$zipPath = Join-Path $packageRoot "EasyNaive-$Version-$RuntimeIdentifier-$timestamp.zip"

& (Join-Path $PSScriptRoot 'build-app.ps1') -Configuration $Configuration -RuntimeIdentifier $RuntimeIdentifier -Version $Version
if ($LASTEXITCODE -ne 0) { throw "build-app.ps1 failed." }

$publishRoot = Join-Path $repoRoot "artifacts\publish\$Configuration\$RuntimeIdentifier"
$appPublish = Join-Path $publishRoot 'EasyNaive.App'
$elevationPublish = Join-Path $publishRoot 'EasyNaive.Elevation'
$servicePublish = Join-Path $publishRoot 'EasyNaive.Service'
$singBoxSource = Join-Path $repoRoot 'bin\sing-box'
$singBoxTarget = Join-Path $layoutRoot 'sing-box'
$docsTarget = Join-Path $layoutRoot 'docs'

function Assert-RequiredFile {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Required package file is missing: $Path"
    }
}

New-Item -ItemType Directory -Path $layoutRoot -Force | Out-Null
New-Item -ItemType Directory -Path $singBoxTarget -Force | Out-Null
New-Item -ItemType Directory -Path $docsTarget -Force | Out-Null

Copy-Item -Path (Join-Path $appPublish '*') -Destination $layoutRoot -Recurse -Force
Copy-Item -Path (Join-Path $elevationPublish '*') -Destination $layoutRoot -Recurse -Force
Copy-Item -Path (Join-Path $servicePublish '*') -Destination $layoutRoot -Recurse -Force

foreach ($fileName in 'sing-box.exe', 'libcronet.dll', 'LICENSE')
{
    $sourcePath = Join-Path $singBoxSource $fileName
    if (Test-Path $sourcePath)
    {
        Copy-Item -Path $sourcePath -Destination (Join-Path $singBoxTarget $fileName) -Force
    }
}

$assetsSource = Join-Path $repoRoot 'assets'
if (Test-Path $assetsSource)
{
    Copy-Item -Path $assetsSource -Destination (Join-Path $layoutRoot 'assets') -Recurse -Force
}

foreach ($fileName in 'LICENSE', 'README.md', 'SECURITY.md', 'THIRD_PARTY_NOTICES.md')
{
    Copy-Item -Path (Join-Path $repoRoot $fileName) -Destination (Join-Path $docsTarget $fileName) -Force
}

$requiredFiles = @(
    'EasyNaiveTray.exe',
    'EasyNaiveTray.dll',
    'EasyNaiveTray.deps.json',
    'EasyNaiveTray.runtimeconfig.json',
    'EasyNaive.Core.dll',
    'EasyNaive.Platform.Windows.dll',
    'EasyNaive.SingBox.dll',
    'EasyNaive.Elevation.exe',
    'EasyNaive.Elevation.dll',
    'EasyNaive.Elevation.deps.json',
    'EasyNaive.Elevation.runtimeconfig.json',
    'EasyNaive.Service.exe',
    'EasyNaive.Service.dll',
    'EasyNaive.Service.deps.json',
    'EasyNaive.Service.runtimeconfig.json',
    'System.Diagnostics.EventLog.dll',
    'System.Diagnostics.EventLog.Messages.dll',
    'System.ServiceProcess.ServiceController.dll',
    'Assets\App.ico',
    'Assets\TrayConnected.ico',
    'Assets\TrayError.ico',
    'Assets\TrayStopped.ico',
    'docs\LICENSE',
    'docs\README.md',
    'docs\SECURITY.md',
    'docs\THIRD_PARTY_NOTICES.md',
    'sing-box\sing-box.exe',
    'sing-box\libcronet.dll'
)

foreach ($relativePath in $requiredFiles)
{
    Assert-RequiredFile -Path (Join-Path $layoutRoot $relativePath)
}

$manifest = [ordered]@{
    version = $Version
    configuration = $Configuration
    runtimeIdentifier = $RuntimeIdentifier
    generatedAtUtc = (Get-Date).ToUniversalTime().ToString('o')
    packageLayout = Split-Path -Leaf $layoutRoot
    entryPoint = 'EasyNaiveTray.exe'
    userDataRoot = '%LocalAppData%\EasyNaive'
    requiredDesktopRuntime = 'Microsoft .NET Desktop Runtime 8 x64'
    requiredFiles = $requiredFiles
}

$manifest | ConvertTo-Json | Set-Content -Path (Join-Path $layoutRoot 'package-manifest.json') -Encoding UTF8

if (Test-Path -LiteralPath $zipPath) {
    throw "Package archive already exists: $zipPath"
}

Compress-Archive -Path $layoutRoot -DestinationPath $zipPath -CompressionLevel Optimal

Write-Host "Package layout: $layoutRoot"
Write-Host "Package archive: $zipPath"
