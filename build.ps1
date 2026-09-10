param(
    [ValidateSet('Debug','Release')]
    [string]$Configuration = 'Release',

    [switch]$SyncBridge,
    [string]$BridgeBranch = 'main',
    [string]$OcctRoot = $env:OCCT_ROOT
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = $PSScriptRoot
$solution = Join-Path $root 'OCCAD.sln'
$bridgeCache = Join-Path $root 'artifacts\dependencies\OcctCSharpBridge'
$bridgeDestination = Join-Path $root 'external\OcctCSharpBridge\win-x64'

function Invoke-Checked {
    param(
        [Parameter(Mandatory = $true)][string]$Command,
        [Parameter(Mandatory = $true)][string[]]$Arguments,
        [Parameter(Mandatory = $true)][string]$ErrorMessage
    )

    & $Command @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$ErrorMessage Exit code: $LASTEXITCODE."
    }
}

function Sync-BridgeSdk {
    if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
        throw 'git was not found in PATH.'
    }

    $repository = 'https://github.com/zly258/OcctCSharpBridge.git'
    if (-not (Test-Path -LiteralPath (Join-Path $bridgeCache '.git'))) {
        New-Item -ItemType Directory -Path (Split-Path -Parent $bridgeCache) -Force | Out-Null
        Invoke-Checked 'git' @('clone','--quiet','--filter=blob:none',$repository,$bridgeCache) 'Unable to clone OcctCSharpBridge.'
    }

    Invoke-Checked 'git' @('-C',$bridgeCache,'fetch','--quiet','--prune','origin',$BridgeBranch) "Unable to fetch origin/$BridgeBranch."
    $commit = (& git -C $bridgeCache rev-parse FETCH_HEAD).Trim()
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($commit)) {
        throw "Unable to resolve origin/$BridgeBranch."
    }

    Invoke-Checked 'git' @('-C',$bridgeCache,'checkout','--quiet','--detach','--force',$commit) 'Unable to checkout Bridge source.'
    Invoke-Checked 'git' @('-C',$bridgeCache,'reset','--hard','--quiet',$commit) 'Unable to reset Bridge source.'
    Invoke-Checked 'git' @('-C',$bridgeCache,'clean','-ffdx','--quiet') 'Unable to clean Bridge source.'

    if (-not [string]::IsNullOrWhiteSpace($OcctRoot)) {
        $env:OCCT_ROOT = [System.IO.Path]::GetFullPath($OcctRoot)
    }

    & (Join-Path $bridgeCache 'build.ps1') dist Release
    if ($LASTEXITCODE -ne 0) {
        throw "Bridge Binary SDK build failed. Exit code: $LASTEXITCODE."
    }

    $source = Join-Path $bridgeCache 'dist\win-x64'
    foreach ($name in @('OcctNative.dll','OcctNet.dll','OcctNet.Wpf.dll','bridge-contract.json','bridge-manifest.json')) {
        if (-not (Test-Path -LiteralPath (Join-Path $source $name) -PathType Leaf)) {
            throw "Bridge Binary SDK is incomplete: $name"
        }
    }

    Remove-Item -LiteralPath $bridgeDestination -Recurse -Force -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Path $bridgeDestination -Force | Out-Null
    Copy-Item -Path (Join-Path $source '*') -Destination $bridgeDestination -Recurse -Force

    Write-Host "[build] Bridge branch: $BridgeBranch"
    Write-Host "[build] Bridge commit: $commit"
    Write-Host "[build] Binary SDK:    $bridgeDestination"
}

if ($SyncBridge) {
    Sync-BridgeSdk
}

foreach ($name in @('OcctNative.dll','OcctNet.dll','OcctNet.Wpf.dll','bridge-contract.json')) {
    if (-not (Test-Path -LiteralPath (Join-Path $bridgeDestination $name) -PathType Leaf)) {
        throw "CAD Bridge SDK is missing '$name'. Run '.\build.ps1 -SyncBridge -BridgeBranch main' once."
    }
}

Invoke-Checked 'dotnet' @('build',$solution,'-c',$Configuration,'-p:Platform=x64','--nologo') 'OCCAD build failed.'
