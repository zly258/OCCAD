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

& dotnet build $solution -c $Configuration -p:Platform=x64 --nologo
if ($LASTEXITCODE -ne 0) {
    throw "OCCAD build failed. Exit code: $LASTEXITCODE."
}

Write-Host "[build] Bridge SDK: $bridgeSdk"
Write-Host '[build] Completed.' -ForegroundColor Green
