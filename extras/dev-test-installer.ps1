# dev-test-installer.ps1
#
# Developer-only harness for testing a LOCAL Tweaker.Installer.exe with the
# same silent-install arguments that Tweaker.ps1 / Tweaker-Beta.ps1 use in
# production. Lets you validate the installer (especially issue #649 silent-
# mode behaviour) without uploading the build to GitHub releases first.
#
# Usage:
#   .\extras\dev-test-installer.ps1 -LocalInstaller C:\path\to\Tweaker.Installer.exe -Mode Normal
#   .\extras\dev-test-installer.ps1 -LocalInstaller C:\path\to\Tweaker.Installer.exe -Mode Portable
#
# Mirrors Tweaker.ps1 lines 66 and 77 exactly. After the installer exits, it
# probes the expected post-install location (Program Files for Normal,
# ~\Desktop\Tweaker for Portable) and reports whether Tweaker.exe landed
# where Tweaker.ps1's launch logic would look for it.
#
# Not for end users. Not invoked by any release artifact.

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateScript({
        if (-not (Test-Path -LiteralPath $_ -PathType Leaf)) {
            throw "LocalInstaller path does not exist or is not a file: $_"
        }
        $true
    })]
    [string]$LocalInstaller,

    [Parameter(Mandatory = $true)]
    [ValidateSet('Normal', 'Portable')]
    [string]$Mode
)

$ErrorActionPreference = 'Stop'

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "   Tweaker dev-test-installer ($Mode)" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Installer: $LocalInstaller" -ForegroundColor White
Write-Host ""

if ($Mode -eq 'Normal') {
    # Mirrors Tweaker.ps1:66 verbatim.
    $argList = '/SILENT /SUPPRESSMSGBOXES /MERGETASKS="regularinstall\desktopicon,regularinstall\startmenuicon"'
    $expectedPath = Join-Path $env:ProgramFiles "Tweaker\Tweaker.exe"
} else {
    # Mirrors Tweaker.ps1:77 verbatim.
    $argList = '/SILENT /SUPPRESSMSGBOXES /TASKS="portableinstall"'
    $expectedPath = Join-Path ([System.Environment]::GetFolderPath('Desktop')) "Tweaker\Tweaker.exe"
}

Write-Host "Args: $argList" -ForegroundColor DarkGray
Write-Host ""
Write-Host "Running installer..." -ForegroundColor Cyan

$proc = Start-Process -FilePath $LocalInstaller -ArgumentList $argList -Wait -PassThru
$exit = $proc.ExitCode

Write-Host ""
Write-Host "Installer exit code: $exit" -ForegroundColor $(if ($exit -eq 0) { 'Green' } else { 'Yellow' })
Write-Host ""
Write-Host "Expected install location ({app} per Tweaker.ps1's launch logic):" -ForegroundColor White
Write-Host "  $expectedPath" -ForegroundColor White
Write-Host ""

if (Test-Path -LiteralPath $expectedPath -PathType Leaf) {
    Write-Host "PASS: Tweaker.exe is at the expected location." -ForegroundColor Green

    # For Portable, also confirm the portable.marker landed.
    if ($Mode -eq 'Portable') {
        $markerPath = Join-Path ([System.IO.Path]::GetDirectoryName($expectedPath)) "portable.marker"
        if (Test-Path -LiteralPath $markerPath -PathType Leaf) {
            Write-Host "PASS: portable.marker is present." -ForegroundColor Green
        } else {
            Write-Host "FAIL: portable.marker missing at $markerPath" -ForegroundColor Red
        }
    }
} else {
    Write-Host "FAIL: Tweaker.exe NOT found at expected location." -ForegroundColor Red
    Write-Host ""
    Write-Host "Searching common alternate locations..." -ForegroundColor Yellow
    $candidates = @(
        (Join-Path $env:ProgramFiles "Tweaker\Tweaker.exe"),
        (Join-Path ${env:ProgramFiles(x86)} "Tweaker\Tweaker.exe"),
        (Join-Path ([System.Environment]::GetFolderPath('Desktop')) "Tweaker\Tweaker.exe"),
        (Join-Path $env:LOCALAPPDATA "Programs\Tweaker\Tweaker.exe")
    )
    foreach ($c in $candidates) {
        if ($c -and (Test-Path -LiteralPath $c -PathType Leaf)) {
            Write-Host "  Found at: $c" -ForegroundColor Yellow
        }
    }
}

Write-Host ""
