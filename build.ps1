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
$nestedPortableRoot = Join-Path $bridgeSdk 'portable'
$portableRoot = if (Test-Path -LiteralPath (Join-Path $nestedPortableRoot 'package-manifest.json') -PathType Leaf) {
    $nestedPortableRoot
} elseif (Test-Path -LiteralPath (Join-Path $bridgeSdk 'package-manifest.json') -PathType Leaf) {
    $bridgeSdk
} else {
    $null
}
$portableRuntime = if ($null -ne $portableRoot) {
    Join-Path $portableRoot 'runtime'
} else {
    $null
}
$portableNative = if ($null -ne $portableRuntime) {
    Join-Path $portableRuntime 'OcctNative.dll'
} else {
    $null
}

$hasFlatNative = Test-Path -LiteralPath $flatNative -PathType Leaf
$hasPortableNative =
    $null -ne $portableNative -and
    (Test-Path -LiteralPath $portableNative -PathType Leaf)
if (-not $hasFlatNative -and -not $hasPortableNative) {
    throw "Installed OcctCSharpBridge SDK contains neither flat OcctNative.dll nor portable/runtime/OcctNative.dll: $bridgeSdk"
}

$runtime = if ($hasPortableNative) {
    $portableRuntime
} else {
    $bridgeSdk
}
$runtimeCount = @(Get-ChildItem -LiteralPath $runtime -Filter '*.dll' -File -ErrorAction SilentlyContinue).Count
$runtimeLayout = if ($hasPortableNative) {
    'portable/runtime'
} else {
    'flat'
}
$resourceRoot = if ($null -ne $portableRoot) {
    Join-Path $portableRoot 'occt\resources'
} else {
    Join-Path $bridgeSdk 'occt\resources'
}
$hasResources = Test-Path -LiteralPath $resourceRoot -PathType Container

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
if ($hasPortableNative) {
    Write-Host '[build] Direct EXE:     portable runtime will be copied beside the application.'
}
else {
    Write-Host '[build] Direct EXE:     flat Bridge SDK; build is valid. Use .\run.ps1 -OcctRoot <path> or set OCCT_ROOT/CASROOT when the OCCT runtime is external.' -ForegroundColor Yellow
}

& dotnet build $solution -c $Configuration -p:Platform=x64 --nologo
if ($LASTEXITCODE -ne 0) {
    throw "OCCAD build failed. OCCAD source $occadSourceCommit; Bridge SDK source $bridgeSourceCommit. Exit code: $LASTEXITCODE."
}

Write-Host '[build] Completed.' -ForegroundColor Green
