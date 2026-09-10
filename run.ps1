param(
    [ValidateSet('Debug','Release')]
    [string]$Configuration = 'Release',
    [string]$OcctRoot = $env:OCCT_ROOT
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = $PSScriptRoot

function Test-OcctRoot {
    param([string]$Path)

    if ([string]::IsNullOrWhiteSpace($Path)) {
        return $false
    }

    $candidate = [System.IO.Path]::GetFullPath($Path)
    $bin = Join-Path $candidate 'win64\vc14\bin'
    return (Test-Path -LiteralPath (Join-Path $bin 'TKernel.dll') -PathType Leaf)
}

function Resolve-OcctRoot {
    param([string]$RequestedRoot)

    foreach ($candidate in @($RequestedRoot, $env:OCCT_ROOT, $env:CASROOT)) {
        if (Test-OcctRoot $candidate) {
            return [System.IO.Path]::GetFullPath($candidate)
        }
    }

    foreach ($entry in ($env:PATH -split [System.IO.Path]::PathSeparator)) {
        $directory = $entry.Trim().Trim('"')
        if ([string]::IsNullOrWhiteSpace($directory)) {
            continue
        }

        $kernel = Join-Path $directory 'TKernel.dll'
        if (-not (Test-Path -LiteralPath $kernel -PathType Leaf)) {
            continue
        }

        $vc14 = Split-Path -Parent $directory
        $win64 = Split-Path -Parent $vc14
        $candidate = Split-Path -Parent $win64
        if (Test-OcctRoot $candidate) {
            return [System.IO.Path]::GetFullPath($candidate)
        }
    }

    throw 'OCCT runtime was not found. Pass -OcctRoot once or set OCCT_ROOT/CASROOT.'
}

$resolvedOcctRoot = Resolve-OcctRoot $OcctRoot
$appDirectory = Join-Path $root "src\OCCAD.Avalonia\bin\x64\$Configuration\net10.0"
$executable = Join-Path $appDirectory 'OCCAD.exe'
if (-not (Test-Path -LiteralPath $executable -PathType Leaf)) {
    throw "OCCAD executable was not found: $executable. Run '.\build.ps1' first."
}

$occtBin = Join-Path $resolvedOcctRoot 'win64\vc14\bin'
if (-not (Test-Path -LiteralPath $occtBin -PathType Container)) {
    throw "OCCT runtime directory was not found: $occtBin"
}

$env:OCCT_ROOT = $resolvedOcctRoot
$env:CASROOT = $resolvedOcctRoot
$env:OCCT_BRIDGE_NATIVE_DIR = $appDirectory
$env:PATH = "$appDirectory;$occtBin;$env:PATH"

$thirdParty = Join-Path $resolvedOcctRoot '3rdparty-vc14-64'
if (Test-Path -LiteralPath $thirdParty -PathType Container) {
    foreach ($directory in Get-ChildItem -LiteralPath $thirdParty -Directory) {
        foreach ($relative in @('bin','bin\win64','bin\x64')) {
            $path = Join-Path $directory.FullName $relative
            if (Test-Path -LiteralPath $path -PathType Container) {
                $env:PATH = "$path;$env:PATH"
            }
        }
    }
}

Write-Host "[run] Application: $executable"
Write-Host "[run] OCCT root:   $resolvedOcctRoot"
& $executable
if ($LASTEXITCODE -ne 0) {
    throw "OCCAD exited with code $LASTEXITCODE."
}
