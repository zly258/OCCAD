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

    return $null
}

function Show-LogTail {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return
    }

    Write-Host ''
    Write-Host '[run] Latest OCCAD log:' -ForegroundColor Yellow
    Get-Content -LiteralPath $Path -Tail 100
}

$appDirectory = Join-Path $root "src\OCCAD.Avalonia\bin\x64\$Configuration\net10.0"
$executable = Join-Path $appDirectory 'OCCAD.exe'
if (-not (Test-Path -LiteralPath $executable -PathType Leaf)) {
    throw "OCCAD executable was not found: $executable. Run '.\build.ps1' first."
}

$portableRuntime = Join-Path $appDirectory 'runtime'
$portableNative = Join-Path $portableRuntime 'OcctNative.dll'
$portableOcct = Join-Path $appDirectory 'occt'
$hasPortableRuntime = Test-Path -LiteralPath $portableNative -PathType Leaf

$resolvedOcctRoot = $null
if ($hasPortableRuntime) {
    $env:OCCT_BRIDGE_NATIVE_DIR = $portableRuntime
    $env:PATH = "$portableRuntime;$appDirectory;$env:PATH"

    if (-not (Test-Path -LiteralPath $portableOcct -PathType Container)) {
        $resolvedOcctRoot = Resolve-OcctRoot $OcctRoot
    }
}
else {
    $resolvedOcctRoot = Resolve-OcctRoot $OcctRoot
    if ([string]::IsNullOrWhiteSpace($resolvedOcctRoot)) {
        throw 'OCCT runtime was not found. Use a Portable Bridge SDK, pass -OcctRoot once, or set OCCT_ROOT/CASROOT.'
    }

    $env:OCCT_BRIDGE_NATIVE_DIR = $appDirectory
    $env:PATH = "$appDirectory;$env:PATH"
}

if (-not [string]::IsNullOrWhiteSpace($resolvedOcctRoot)) {
    $occtBin = Join-Path $resolvedOcctRoot 'win64\vc14\bin'
    if (-not (Test-Path -LiteralPath $occtBin -PathType Container)) {
        throw "OCCT runtime directory was not found: $occtBin"
    }

    $env:OCCT_ROOT = $resolvedOcctRoot
    $env:CASROOT = $resolvedOcctRoot
    $env:PATH = "$occtBin;$env:PATH"

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
}

$localAppData = [Environment]::GetFolderPath(
    [Environment+SpecialFolder]::LocalApplicationData)
$logPath = if ([string]::IsNullOrWhiteSpace($localAppData)) {
    Join-Path ([System.IO.Path]::GetTempPath()) 'OCCAD\Logs\OCCAD.App.log'
}
else {
    Join-Path $localAppData 'OCCAD\Logs\OCCAD.App.log'
}

Write-Host "[run] Application: $executable"
Write-Host "[run] Working dir: $appDirectory"
Write-Host "[run] Runtime:     $(if ($hasPortableRuntime) { $portableRuntime } else { $resolvedOcctRoot })"
Write-Host "[run] Log:         $logPath"

$process = Start-Process -FilePath $executable -WorkingDirectory $appDirectory -PassThru
if ($null -eq $process) {
    throw 'Failed to create the OCCAD process.'
}

Write-Host "[run] Process started. PID=$($process.Id)"

$startupDeadline = [DateTime]::UtcNow.AddSeconds(15)
do {
    Start-Sleep -Milliseconds 250
    $process.Refresh()
} while (
    -not $process.HasExited -and
    $process.MainWindowHandle -eq 0 -and
    [DateTime]::UtcNow -lt $startupDeadline)

if (-not $process.HasExited) {
    Start-Sleep -Seconds 1
    $process.Refresh()
}

if (-not $process.HasExited) {
    if ($process.MainWindowHandle -eq 0) {
        Show-LogTail $logPath
        throw "OCCAD is running (PID=$($process.Id)), but no main window appeared within 15 seconds. See: $logPath"
    }

    Write-Host "[run] Main window is visible: $($process.MainWindowTitle)"
    exit 0
}

$exitCode = $process.ExitCode
Write-Host "[run] OCCAD exited during startup with code $exitCode." -ForegroundColor Red
Show-LogTail $logPath
exit $(if ($exitCode -eq 0) { 1 } else { $exitCode })
