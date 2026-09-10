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

foreach ($name in @('OcctNet.dll','OcctNet.Avalonia.dll','bridge-contract.json','bridge-manifest.json')) {
    if (-not (Test-Path -LiteralPath (Join-Path $bridgeSdk $name) -PathType Leaf)) {
        throw "Installed OcctCSharpBridge SDK is missing '$name' at '$bridgeSdk'. Run OcctCSharpBridge .\publish.ps1 from an elevated PowerShell session, or set OCCTCSHARPBRIDGE_SDK."
    }
}

$flatNative = Join-Path $bridgeSdk 'OcctNative.dll'
$runtimeDirectory = Join-Path $bridgeSdk 'runtime'
$runtimeNative = Join-Path $runtimeDirectory 'OcctNative.dll'
if (-not (Test-Path -LiteralPath $flatNative -PathType Leaf) -and
    -not (Test-Path -LiteralPath $runtimeNative -PathType Leaf)) {
    throw "Installed OcctCSharpBridge SDK contains neither flat nor portable OcctNative.dll: $bridgeSdk"
}

$runtime = if (Test-Path -LiteralPath $runtimeNative -PathType Leaf) {
    $runtimeDirectory
}
else {
    $bridgeSdk
}
$runtimeCount = @(Get-ChildItem -LiteralPath $runtime -Filter '*.dll' -File -ErrorAction SilentlyContinue).Count
$runtimeLayout = if ([string]::Equals($runtime, $bridgeSdk, [StringComparison]::OrdinalIgnoreCase)) {
    'flat'
}
else {
    'runtime-subdir'
}
$hasResources = Test-Path -LiteralPath (Join-Path $bridgeSdk 'occt\resources') -PathType Container

$bridgeManifestPath = Join-Path $bridgeSdk 'bridge-manifest.json'
$bridgeManifest = Get-Content -LiteralPath $bridgeManifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
$bridgeSourceCommit = [string]$bridgeManifest.sourceCommit
if ([string]::IsNullOrWhiteSpace($bridgeSourceCommit)) {
    throw "Installed OcctCSharpBridge SDK manifest does not contain sourceCommit: $bridgeManifestPath"
}

$occadSourceCommit = (& git -C $root rev-parse HEAD 2>$null)
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($occadSourceCommit)) {
    $occadSourceCommit = 'unknown'
} else {
    $occadSourceCommit = $occadSourceCommit.Trim()
}

Write-Host "[build] OCCAD source:   $occadSourceCommit"
Write-Host "[build] Bridge SDK:     $bridgeSdk"
Write-Host "[build] Bridge source:  $bridgeSourceCommit"
Write-Host "[build] Bridge runtime: $runtime ($runtimeCount DLLs, $runtimeLayout)"
Write-Host "[build] OCCT resources: $hasResources"
if ($runtimeLayout -eq 'runtime-subdir') {
    Write-Host '[build] Direct EXE:     portable runtime will be copied beside the application.'
}
else {
    Write-Host '[build] Direct EXE:     external OCCT_ROOT/CASROOT is required unless the SDK is switched to a portable layout.' -ForegroundColor Yellow
}

& dotnet build $solution -c $Configuration -p:Platform=x64 --nologo
if ($LASTEXITCODE -ne 0) {
    throw "OCCAD build failed. OCCAD source $occadSourceCommit; Bridge SDK source $bridgeSourceCommit. Exit code: $LASTEXITCODE."
}

Write-Host '[build] Completed.' -ForegroundColor Green
