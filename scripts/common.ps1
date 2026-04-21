Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-RepoRoot {
    param([string]$ScriptRoot)
    return (Resolve-Path (Join-Path $ScriptRoot '..')).Path
}

function Get-DefaultVersion {
    param([string]$RepoRoot)

    $propsPath = Join-Path $RepoRoot 'Directory.Build.props'
    if (-not (Test-Path $propsPath)) {
        throw "Directory.Build.props was not found at '$propsPath'."
    }

    $content = Get-Content -Path $propsPath -Encoding UTF8 -Raw
    $match = [regex]::Match($content, '<Version>\s*(?<version>[^<]+)\s*</Version>')
    if (-not $match.Success) {
        throw "Version was not found in '$propsPath'."
    }

    return $match.Groups['version'].Value.Trim()
}
