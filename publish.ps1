param(
    [string]$OutputDirectory = '',
    [string]$OcctRoot = $env:OCCT_ROOT,
    [switch]$NoArchive
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
$outputParent = Split-Path -Parent $OutputDirectory
$packageName = Split-Path -Leaf $OutputDirectory
$archivePath = Join-Path $outputParent "$packageName-win-x64.zip"

foreach ($name in @('OcctNet.dll','OcctNet.Avalonia.dll','bridge-contract.json','bridge-manifest.json')) {
    if (-not (Test-Path -LiteralPath (Join-Path $bridgeRoot $name) -PathType Leaf)) {
        throw "Installed OcctCSharpBridge SDK is missing '$name' at '$bridgeRoot'. Run OcctCSharpBridge .\publish.ps1 with a writable -InstallRoot, or set OCCTCSHARPBRIDGE_SDK to a valid SDK path. Administrator rights are only needed when the chosen install directory itself requires them."
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

foreach ($name in @(
    'OCCAD.exe',
    'OCCAD.dll',
    'OcctNet.dll',
    'OcctNet.Avalonia.dll',
    'bridge-contract.json',
    'bridge-manifest.json'
)) {
    $path = Join-Path $OutputDirectory $name
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Publish output is missing required file '$name': $path"
    }
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

# Ship release/legal documentation with the executable package so a copied or
# archived delivery remains self-describing without requiring the source tree.
foreach ($name in @(
    'README.md',
    'README.zh-CN.md',
    'CHANGELOG.md',
    'LICENSE',
    'LICENSE_LGPL_21.txt',
    'OcctCSharpBridge_LGPL_EXCEPTION.txt',
    'THIRD_PARTY_NOTICES.md'
)) {
    $source = Join-Path $root $name
    if (-not (Test-Path -LiteralPath $source -PathType Leaf)) {
        throw "Release documentation is missing from the repository: $source"
    }
    Copy-Item -LiteralPath $source -Destination (Join-Path $OutputDirectory $name) -Force
}

$docsSource = Join-Path $root 'docs'
$docsDestination = Join-Path $OutputDirectory 'docs'
if (-not (Test-Path -LiteralPath $docsSource -PathType Container)) {
    throw "Release documentation directory is missing: $docsSource"
}
Copy-Item -LiteralPath $docsSource -Destination $docsDestination -Recurse -Force
if (-not (Test-Path -LiteralPath (Join-Path $docsDestination 'README.md') -PathType Leaf)) {
    throw "Publish output is missing offline documentation: $docsDestination"
}

if (-not $NoArchive) {
    Remove-Item -LiteralPath $archivePath -Force -ErrorAction SilentlyContinue
    Compress-Archive -LiteralPath $OutputDirectory -DestinationPath $archivePath -CompressionLevel Optimal
    if (-not (Test-Path -LiteralPath $archivePath -PathType Leaf)) {
        throw "Windows release archive was not created: $archivePath"
    }
}

Write-Host "[publish] Bridge SDK: $bridgeRoot"
Write-Host "[publish] Runtime:    $runtimeDescription"
Write-Host "[publish] Output:     $OutputDirectory"
Write-Host "[publish] Run:        $launcherDestination"
Write-Host "[publish] Docs:       $docsDestination"
if (-not $NoArchive) {
    Write-Host "[publish] Archive:    $archivePath"
}
if (-not $usesPortableRuntime -and [string]::IsNullOrWhiteSpace($OcctRoot)) {
    Write-Host '[publish] Flat runtime requires external OCCT at launch. From the package, run .\run.ps1 -OcctRoot <path> or set OCCT_ROOT/CASROOT.' -ForegroundColor Yellow
}
