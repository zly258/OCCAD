param(
    [string]$OutputDirectory = '',
    [string]$OcctRoot = $env:OCCT_ROOT
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = $PSScriptRoot
$project = Join-Path $root 'src\OCCAD.Avalonia\OCCAD.Avalonia.csproj'
$bridgeRoot = if (-not [string]::IsNullOrWhiteSpace($env:OCCTCSHARPBRIDGE_SDK)) {
    [System.IO.Path]::GetFullPath($env:OCCTCSHARPBRIDGE_SDK)
} else {
    Join-Path $env:ProgramFiles 'OcctCSharpBridge\SDK\3.0\win-x64'
}
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $root 'artifacts\publish\OCCAD'
}
$OutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)

foreach ($name in @('OcctNative.dll','OcctNet.dll','OcctNet.Avalonia.dll','bridge-contract.json')) {
    if (-not (Test-Path -LiteralPath (Join-Path $bridgeRoot $name) -PathType Leaf)) {
        throw "Installed OcctCSharpBridge SDK is missing '$name' at '$bridgeRoot'. Run OcctCSharpBridge .\publish.ps1 from an elevated PowerShell session, or set OCCTCSHARPBRIDGE_SDK."
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

Copy-Item -LiteralPath (Join-Path $bridgeRoot 'OcctNative.dll') -Destination (Join-Path $OutputDirectory 'OcctNative.dll') -Force

Write-Host "[publish] Output: $OutputDirectory"
