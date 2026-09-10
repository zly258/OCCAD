param(
    [string]$OutputDirectory = '',
    [string]$OcctRoot = $env:OCCT_ROOT
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = $PSScriptRoot
$project = Join-Path $root 'src\OCCAD.Wpf\OCCAD.Wpf.csproj'
$bridgeRoot = Join-Path $root 'external\OcctCSharpBridge\win-x64'
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $root 'artifacts\publish\OCCAD'
}
$OutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)

foreach ($name in @('OcctNative.dll','OcctNet.dll','OcctNet.Wpf.dll','bridge-contract.json')) {
    if (-not (Test-Path -LiteralPath (Join-Path $bridgeRoot $name) -PathType Leaf)) {
        throw "CAD Bridge SDK is missing '$name'. Run '.\build.ps1 -SyncBridge -BridgeBranch main' once."
    }
}

if (-not [string]::IsNullOrWhiteSpace($OcctRoot)) {
    $env:OCCT_ROOT = [System.IO.Path]::GetFullPath($OcctRoot)
}

Remove-Item -LiteralPath $OutputDirectory -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null

& dotnet publish $project -c Release -p:Platform=x64 -r win-x64 --self-contained false -o $OutputDirectory --nologo
if ($LASTEXITCODE -ne 0) {
    throw "OCCAD publish failed. Exit code: $LASTEXITCODE."
}

$nativeBridge = Join-Path $bridgeRoot 'OcctNative.dll'
Copy-Item -LiteralPath $nativeBridge -Destination (Join-Path $OutputDirectory 'OcctNative.dll') -Force

Write-Host "[publish] Output: $OutputDirectory"
