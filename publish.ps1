param(
    [string]$OutputDirectory = '',
    [string]$BridgePortableRoot = '',
    [string]$OcctRoot = $env:OCCT_ROOT,
    [switch]$NoArchive
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = $PSScriptRoot
$project = Join-Path $root 'src\OCCAD.Avalonia\OCCAD.Avalonia.csproj'
$launcherSource = Join-Path $root 'run.ps1'
$defaultBridgeSdk = Join-Path $env:ProgramFiles 'OcctCSharpBridge\SDK\3.0\win-x64'
$binaryBridgeRoot = if (-not [string]::IsNullOrWhiteSpace($env:OCCTCSHARPBRIDGE_SDK)) {
    [System.IO.Path]::GetFullPath($env:OCCTCSHARPBRIDGE_SDK)
} else {
    $defaultBridgeSdk
}

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $root 'artifacts\publish\OCCAD'
}
$OutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)
$outputParent = Split-Path -Parent $OutputDirectory
$packageName = Split-Path -Leaf $OutputDirectory
$archivePath = Join-Path $outputParent "$packageName-win-x64.zip"

function Assert-File {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [string]$Message = ''
    )

    if (Test-Path -LiteralPath $Path -PathType Leaf) {
        return
    }

    if ([string]::IsNullOrWhiteSpace($Message)) {
        throw "Required file was not found: $Path"
    }

    throw $Message
}

function Assert-Directory {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [string]$Message = ''
    )

    if (Test-Path -LiteralPath $Path -PathType Container) {
        return
    }

    if ([string]::IsNullOrWhiteSpace($Message)) {
        throw "Required directory was not found: $Path"
    }

    throw $Message
}

function Test-PortableBridgeRoot {
    param([Parameter(Mandatory = $true)][string]$Path)

    if (-not (Test-Path -LiteralPath $Path -PathType Container)) {
        return $false
    }

    foreach ($relative in @(
        'package-manifest.json',
        'bridge-contract.json',
        'bridge-manifest.json',
        'OcctNet.dll',
        'OcctNet.Avalonia.dll',
        'runtime\OcctNative.dll'
    )) {
        if (-not (Test-Path -LiteralPath (Join-Path $Path $relative) -PathType Leaf)) {
            return $false
        }
    }

    return Test-Path -LiteralPath (Join-Path $Path 'occt\resources') -PathType Container
}

function Resolve-PortableBridgeRoot {
    param(
        [Parameter(Mandatory = $true)][string]$BinaryRoot,
        [string]$ExplicitRoot = ''
    )

    if (-not [string]::IsNullOrWhiteSpace($ExplicitRoot)) {
        $candidate = [System.IO.Path]::GetFullPath($ExplicitRoot)
        if (-not (Test-PortableBridgeRoot $candidate)) {
            throw "Bridge Portable SDK is incomplete: $candidate"
        }
        return $candidate
    }

    if (Test-PortableBridgeRoot $BinaryRoot) {
        return [System.IO.Path]::GetFullPath($BinaryRoot)
    }

    $nestedPortable = Join-Path $BinaryRoot 'portable'
    if (Test-PortableBridgeRoot $nestedPortable) {
        return [System.IO.Path]::GetFullPath($nestedPortable)
    }

    $contractPath = Join-Path $BinaryRoot 'bridge-contract.json'
    Assert-File $contractPath "Installed OcctCSharpBridge Binary SDK is missing bridge-contract.json: $BinaryRoot"

    $contract = Get-Content -LiteralPath $contractPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $bridgeVersion = [string]$contract.bridgeVersion
    if ([string]::IsNullOrWhiteSpace($bridgeVersion)) {
        throw "OcctCSharpBridge bridge-contract.json does not contain bridgeVersion: $contractPath"
    }

    $workspaceRoot = Split-Path -Parent $root
    $siblingPortable = Join-Path $workspaceRoot "OcctCSharpBridge\artifacts\publish\OcctCSharpBridge-$bridgeVersion-win-x64-portable"
    if (Test-PortableBridgeRoot $siblingPortable) {
        return [System.IO.Path]::GetFullPath($siblingPortable)
    }

    throw @"
A complete OcctCSharpBridge Portable SDK was not found.

Binary SDK:
  $BinaryRoot

Expected portable artifact:
  $siblingPortable

OCCAD release packages are intentionally environment-independent and will not fall back to a flat Binary SDK.
Run OcctCSharpBridge .\publish.ps1 first so its artifacts\publish\OcctCSharpBridge-$bridgeVersion-win-x64-portable package exists,
or pass -BridgePortableRoot <path> explicitly.
"@
}

function Assert-PortableBridgeMatchesBinary {
    param(
        [Parameter(Mandatory = $true)][string]$BinaryRoot,
        [Parameter(Mandatory = $true)][string]$PortableRoot
    )

    $binaryContractPath = Join-Path $BinaryRoot 'bridge-contract.json'
    $binaryManifestPath = Join-Path $BinaryRoot 'bridge-manifest.json'
    $portableContractPath = Join-Path $PortableRoot 'bridge-contract.json'
    $portableManifestPath = Join-Path $PortableRoot 'bridge-manifest.json'
    $portablePackageManifestPath = Join-Path $PortableRoot 'package-manifest.json'

    foreach ($path in @(
        $binaryContractPath,
        $binaryManifestPath,
        $portableContractPath,
        $portableManifestPath,
        $portablePackageManifestPath
    )) {
        Assert-File $path
    }

    $binaryContract = Get-Content -LiteralPath $binaryContractPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $binaryManifest = Get-Content -LiteralPath $binaryManifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $portableContract = Get-Content -LiteralPath $portableContractPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $portableManifest = Get-Content -LiteralPath $portableManifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $portablePackageManifest = Get-Content -LiteralPath $portablePackageManifestPath -Raw -Encoding UTF8 | ConvertFrom-Json

    if ([string]$portableContract.bridgeVersion -ne [string]$binaryContract.bridgeVersion) {
        throw "Bridge Portable SDK version does not match the installed Binary SDK."
    }
    if ([string]$portableManifest.sourceCommit -ne [string]$binaryManifest.sourceCommit) {
        throw "Bridge Portable SDK source commit does not match the installed Binary SDK. Re-publish OcctCSharpBridge before publishing OCCAD."
    }
    if ([string]$portablePackageManifest.product -ne 'OcctCSharpBridge Portable SDK' -or
        [string]$portablePackageManifest.platform -ne 'win-x64' -or
        -not [bool]$portablePackageManifest.portableRuntime) {
        throw "Bridge Portable SDK package-manifest.json is not a valid Windows x64 portable runtime package."
    }
    if ([string]$portablePackageManifest.bridgeVersion -ne [string]$binaryContract.bridgeVersion -or
        [string]$portablePackageManifest.bridgeSourceCommit -ne [string]$binaryManifest.sourceCommit) {
        throw "Bridge Portable SDK package manifest does not match the installed Binary SDK."
    }

    foreach ($entry in @($portablePackageManifest.files)) {
        $relative = ([string]$entry.name).Replace('/', [System.IO.Path]::DirectorySeparatorChar)
        $path = Join-Path $PortableRoot $relative
        Assert-File $path "Bridge Portable SDK manifest file is missing: $relative"

        $actualHash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($actualHash -ne ([string]$entry.sha256).ToLowerInvariant()) {
            throw "Bridge Portable SDK hash mismatch: $relative"
        }
    }
}

function Assert-SelfContainedRuntime {
    param([Parameter(Mandatory = $true)][string]$ApplicationRoot)

    foreach ($name in @(
        'hostfxr.dll',
        'hostpolicy.dll',
        'coreclr.dll',
        'System.Private.CoreLib.dll'
    )) {
        Assert-File (Join-Path $ApplicationRoot $name) "Self-contained .NET runtime file is missing from the OCCAD package: $name"
    }

    $runtimeConfigPath = Join-Path $ApplicationRoot 'OCCAD.runtimeconfig.json'
    Assert-File $runtimeConfigPath

    $runtimeConfig = Get-Content -LiteralPath $runtimeConfigPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $includedFrameworks = @($runtimeConfig.runtimeOptions.includedFrameworks)
    if ($includedFrameworks.Count -eq 0 -or
        -not ($includedFrameworks | Where-Object { $_.name -eq 'Microsoft.NETCore.App' })) {
        throw 'OCCAD.runtimeconfig.json is not a valid self-contained .NET runtime configuration.'
    }
}

foreach ($name in @(
    'OcctNet.dll',
    'OcctNet.Avalonia.dll',
    'bridge-contract.json',
    'bridge-manifest.json'
)) {
    Assert-File (Join-Path $binaryBridgeRoot $name) "Installed OcctCSharpBridge Binary SDK is missing '$name' at '$binaryBridgeRoot'. Run OcctCSharpBridge .\publish.ps1 first or set OCCTCSHARPBRIDGE_SDK to the validated SDK root."
}

Assert-File $launcherSource "OCCAD Windows launcher is missing: $launcherSource"

$portableRoot = Resolve-PortableBridgeRoot -BinaryRoot $binaryBridgeRoot -ExplicitRoot $BridgePortableRoot
Assert-PortableBridgeMatchesBinary -BinaryRoot $binaryBridgeRoot -PortableRoot $portableRoot

$portableRuntime = Join-Path $portableRoot 'runtime'
$portableResources = Join-Path $portableRoot 'occt\resources'
Assert-File (Join-Path $portableRuntime 'OcctNative.dll')
Assert-Directory $portableResources

$portableRuntimeDllCount = @(Get-ChildItem -LiteralPath $portableRuntime -Filter '*.dll' -File -ErrorAction Stop).Count
if ($portableRuntimeDllCount -lt 10) {
    throw "Bridge Portable SDK contains unexpectedly few native runtime DLLs: $portableRuntimeDllCount"
}

# The publish input is always the complete portable SDK. The installed Program Files SDK remains
# the validated Binary SDK identity, while the portable artifact supplies the native dependency
# closure and OCCT resources that must travel with the application.
$env:OCCTCSHARPBRIDGE_SDK = $portableRoot

Remove-Item -LiteralPath $OutputDirectory -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null

$publishArguments = @(
    'publish',
    $project,
    '-c', 'Release',
    '-p:Platform=x64',
    '-r', 'win-x64',
    '--self-contained', 'true',
    '-p:PublishSingleFile=false',
    '-p:PublishTrimmed=false',
    '--nologo',
    '-o', $OutputDirectory
)

& dotnet @publishArguments
if ($LASTEXITCODE -ne 0) {
    throw "OCCAD publish failed. Exit code: $LASTEXITCODE."
}

foreach ($name in @(
    'OCCAD.exe',
    'OCCAD.dll',
    'OcctNet.dll',
    'OcctNet.Avalonia.dll',
    'bridge-contract.json',
    'bridge-manifest.json',
    'bridge-portable-manifest.json'
)) {
    Assert-File (Join-Path $OutputDirectory $name) "Publish output is missing required file '$name'."
}

Assert-SelfContainedRuntime -ApplicationRoot $OutputDirectory
Assert-File (Join-Path $OutputDirectory 'runtime\OcctNative.dll') 'Portable publish output is missing runtime\OcctNative.dll.'
Assert-Directory (Join-Path $OutputDirectory 'occt\resources') 'Portable publish output is missing OCCT resources.'

$outputRuntimeDllCount = @(Get-ChildItem -LiteralPath (Join-Path $OutputDirectory 'runtime') -Filter '*.dll' -File -ErrorAction Stop).Count
if ($outputRuntimeDllCount -ne $portableRuntimeDllCount) {
    throw "Published native runtime DLL count differs from the validated Bridge Portable SDK. Expected $portableRuntimeDllCount, found $outputRuntimeDllCount."
}

# OcctRuntime.Configure() probes <app>\runtime and <app>\occt automatically, so OCCAD.exe is the
# primary release entry point. Keep run.ps1 only as a diagnostic launcher.
$launcherDestination = Join-Path $OutputDirectory 'run.ps1'
Copy-Item -LiteralPath $launcherSource -Destination $launcherDestination -Force
Assert-File $launcherDestination

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
    Assert-File $source "Release documentation is missing from the repository: $source"
    Copy-Item -LiteralPath $source -Destination (Join-Path $OutputDirectory $name) -Force
}

$docsSource = Join-Path $root 'docs'
$docsDestination = Join-Path $OutputDirectory 'docs'
Assert-Directory $docsSource "Release documentation directory is missing: $docsSource"
Copy-Item -LiteralPath $docsSource -Destination $docsDestination -Recurse -Force
Assert-File (Join-Path $docsDestination 'README.md') "Publish output is missing offline documentation: $docsDestination"

if (-not $NoArchive) {
    Remove-Item -LiteralPath $archivePath -Force -ErrorAction SilentlyContinue
    Compress-Archive -LiteralPath $OutputDirectory -DestinationPath $archivePath -CompressionLevel Optimal
    Assert-File $archivePath "Windows release archive was not created: $archivePath"
}

Write-Host "[publish] Binary SDK:       $binaryBridgeRoot"
Write-Host "[publish] Portable SDK:     $portableRoot"
Write-Host "[publish] .NET deployment:  self-contained win-x64"
Write-Host "[publish] Native runtime:   $outputRuntimeDllCount DLLs"
Write-Host "[publish] Output:           $OutputDirectory"
Write-Host "[publish] Primary launch:   $(Join-Path $OutputDirectory 'OCCAD.exe')"
Write-Host "[publish] Diagnostic launch:$launcherDestination"
Write-Host "[publish] Docs:             $docsDestination"
if (-not $NoArchive) {
    Write-Host "[publish] Archive:          $archivePath"
}
Write-Host '[publish] Target machine does not require a machine-wide .NET runtime, OcctCSharpBridge SDK, or OCCT installation.' -ForegroundColor Green
