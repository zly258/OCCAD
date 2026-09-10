param(
    [ValidateSet('Debug','Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = $PSScriptRoot
$solution = Join-Path $root 'OCCAD.sln'
$bridgeSdk = if (-not [string]::IsNullOrWhiteSpace($env:OCCTCSHARPBRIDGE_SDK)) {
    [System.IO.Path]::GetFullPath($env:OCCTCSHARPBRIDGE_SDK)
} else {
    Join-Path $env:ProgramFiles 'OcctCSharpBridge\SDK\3.0\win-x64'
}

foreach ($name in @('OcctNative.dll','OcctNet.dll','OcctNet.Avalonia.dll','bridge-contract.json','bridge-manifest.json')) {
    if (-not (Test-Path -LiteralPath (Join-Path $bridgeSdk $name) -PathType Leaf)) {
        throw "Installed OcctCSharpBridge SDK is missing '$name' at '$bridgeSdk'. Run OcctCSharpBridge .\publish.ps1 from an elevated PowerShell session, or set OCCTCSHARPBRIDGE_SDK."
    }
}

$bridgeManifestPath = Join-Path $bridgeSdk 'bridge-manifest.json'
$bridgeManifest = Get-Content -LiteralPath $bridgeManifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
$bridgeSourceCommit = [string]$bridgeManifest.sourceCommit
if ([string]::IsNullOrWhiteSpace($bridgeSourceCommit)) {
    throw "Installed OcctCSharpBridge SDK manifest does not contain sourceCommit: $bridgeManifestPath"
}

Write-Host "[build] Bridge SDK:    $bridgeSdk"
Write-Host "[build] Bridge source: $bridgeSourceCommit"

& dotnet build $solution -c $Configuration -p:Platform=x64 --nologo
if ($LASTEXITCODE -ne 0) {
    throw "OCCAD build failed against Bridge SDK source $bridgeSourceCommit. Exit code: $LASTEXITCODE."
}

Write-Host '[build] Completed.' -ForegroundColor Green
