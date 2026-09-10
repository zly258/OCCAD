param(
    [string]$OutputDirectory = '',
    [string]$OcctRoot = $env:OCCT_ROOT
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = $PSScriptRoot
$project = Join-Path $root 'src\OCCAD.Avalonia\OCCAD.Avalonia.csproj'
$launcherSource = Join-Path $root 'run.ps1'
$bridgeRoot = if (-not [string]::IsNullOrWhiteSpace($env:OCCTCSHARPBRIDGE_SDK)) {
    [System.IO.Path]::GetFullPath($env:OCCTCSHARPBRIDGE_SDK)
} else {
    Join-Path $env:ProgramFiles 'OcctCSharpBridge\SDK\3.0\win-x64'
}
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $root 'artifacts\publish\OCCAD'
}
$OutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)

foreach ($name in @('OcctNet.dll','OcctNet.Avalonia.dll','bridge-contract.json','bridge-manifest.json')) {
    if (-not (Test-Path -LiteralPath (Join-Path $bridgeRoot $name) -PathType Leaf)) {
        throw "Installed OcctCSharpBridge SDK is missing '$name' at '$bridgeRoot'. Run OcctCSharpBridge .\publish.ps1 from an elevated PowerShell session, or set OCCTCSHARPBRIDGE_SDK."
    }
}
if (-not (Test-Path -LiteralPath $launcherSource -PathType Leaf)) {
    throw "OCCAD Windows launcher is missing: $launcherSource"
}

$nestedPortableRoot = Join-Path $bridgeRoot 'portable'
$portableRoot = if (Test-Path -LiteralPath (Join-Path $nestedPortableRoot 'package-manifest.json') -PathType Leaf) {
    $nestedPortableRoot
} elseif (Test-Path -LiteralPath (Join-Path $bridgeRoot 'package-manifest.json') -PathType Leaf) {
    $bridgeRoot
} else {
    $null
}

$usesPortableRuntime = $null -ne $portableRoot
$runtimeDescription = 'flat'
if ($usesPortableRuntime) {
    $portableNative = Join-Path $portableRoot 'runtime\OcctNative.dll'
    $portableResources = Join-Path $portableRoot 'occt\resources'
    if (-not (Test-Path -LiteralPath $portableNative -PathType Leaf)) {
        throw "Installed portable Bridge SDK is missing runtime/OcctNative.dll: $portableRoot"
    }
    if (-not (Test-Path -LiteralPath $portableResources -PathType Container)) {
        throw "Installed portable Bridge SDK is missing OCCT resources: $portableResources"
    }
    $runtimeDescription = 'portable/runtime'
}
else {
    $flatNative = Join-Path $bridgeRoot 'OcctNative.dll'
    if (-not (Test-Path -LiteralPath $flatNative -PathType Leaf)) {
        throw "Installed flat Bridge SDK is missing OcctNative.dll: $bridgeRoot"
    }
}

if (-not [string]::IsNullOrWhiteSpace($OcctRoot)) {
    $env:OCCT_ROOT = [System.IO.Path]::GetFullPath($OcctRoot)
    $env:CASROOT = $env:OCCT_ROOT
}

$env:OCCTCSHARPBRIDGE_SDK = $bridgeRoot

Remove-Item -LiteralPath $OutputDirectory -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null

& dotnet publish $project -c Release -p:Platform=x64 -r win-x64 --self-contained false -o $OutputDirectory --nologo
if ($LASTEXITCODE -ne 0) {
    throw "OCCAD publish failed. Exit code: $LASTEXITCODE."
}

$appDll = Join-Path $OutputDirectory 'OCCAD.dll'
if (-not (Test-Path -LiteralPath $appDll -PathType Leaf)) {
    throw "Publish output is missing OCCAD.dll: $appDll"
}

if ($usesPortableRuntime) {
    foreach ($path in @(
        (Join-Path $OutputDirectory 'runtime\OcctNative.dll'),
        (Join-Path $OutputDirectory 'bridge-portable-manifest.json')
    )) {
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "Portable publish output is incomplete: $path"
        }
    }

    $outputResources = Join-Path $OutputDirectory 'occt\resources'
    if (-not (Test-Path -LiteralPath $outputResources -PathType Container)) {
        throw "Portable publish output is missing OCCT resources: $outputResources"
    }
}
else {
    $outputNative = Join-Path $OutputDirectory 'OcctNative.dll'
    if (-not (Test-Path -LiteralPath $outputNative -PathType Leaf)) {
        throw "Flat publish output is missing OcctNative.dll: $outputNative"
    }
}

$launcherDestination = Join-Path $OutputDirectory 'run.ps1'
Copy-Item -LiteralPath $launcherSource -Destination $launcherDestination -Force
if (-not (Test-Path -LiteralPath $launcherDestination -PathType Leaf)) {
    throw "Publish output is missing run.ps1: $launcherDestination"
}

Write-Host "[publish] Bridge SDK: $bridgeRoot"
Write-Host "[publish] Runtime:    $runtimeDescription"
Write-Host "[publish] Output:     $OutputDirectory"
Write-Host "[publish] Run:        $launcherDestination"
if (-not $usesPortableRuntime -and [string]::IsNullOrWhiteSpace($OcctRoot)) {
    Write-Host '[publish] Flat runtime requires external OCCT at launch. From the package, run .\run.ps1 -OcctRoot <path> or set OCCT_ROOT/CASROOT.' -ForegroundColor Yellow
}
