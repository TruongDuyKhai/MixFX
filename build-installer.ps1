<#
.SYNOPSIS
    Builds artifacts/Soundboard-Setup-<version>.exe in one command (PROJECT_PROMPT.md §10).

.DESCRIPTION
    1. Reads the version from Directory.Build.props (the only place it is declared).
    2. Publishes the app self-contained, single-file, win-x64 into artifacts/publish.
    3. Compiles installer/Soundboard.iss with Inno Setup 6 (ISCC.exe).

.EXAMPLE
    ./build-installer.ps1
#>
[CmdletBinding()]
param(
    # Path to ISCC.exe when Inno Setup is installed somewhere unusual.
    [string]$Iscc
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = $PSScriptRoot
$artifacts = Join-Path $root 'artifacts'
$publishDir = Join-Path $artifacts 'publish'

# Version: single source of truth.
[xml]$props = Get-Content (Join-Path $root 'Directory.Build.props')
$version = ($props.Project.PropertyGroup | ForEach-Object { $_.Version } | Where-Object { $_ } | Select-Object -First 1)
if (-not $version) { throw 'No <Version> found in Directory.Build.props.' }

# Windows file versions must be numeric (1.2.3.0); drop any SemVer pre-release/build suffix for them.
$numericVersion = ($version -split '[-+]')[0]
if ($numericVersion.Split('.').Count -eq 3) { $numericVersion += '.0' }

# Locate ISCC.exe: parameter, PATH, then the per-user and per-machine default install folders.
if (-not $Iscc) {
    $candidates = @(
        (Get-Command 'ISCC.exe' -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source),
        (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'),
        (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
        (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe')
    ) | Where-Object { $_ -and (Test-Path $_) }
    $Iscc = $candidates | Select-Object -First 1
}
if (-not $Iscc) { throw 'ISCC.exe (Inno Setup 6) not found. Install it (winget install JRSoftware.InnoSetup) or pass -Iscc.' }

Write-Host "Soundboard $version" -ForegroundColor Cyan

Write-Host '> dotnet publish' -ForegroundColor Cyan
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
dotnet publish (Join-Path $root 'src/Soundboard.App') -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $publishDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed ($LASTEXITCODE)." }

Write-Host '> ISCC' -ForegroundColor Cyan
& $Iscc /Q `
    "/DAppVersion=$version" `
    "/DNumericVersion=$numericVersion" `
    "/DSourceDir=$publishDir" `
    "/DOutputDir=$artifacts" `
    (Join-Path $root 'installer/Soundboard.iss')
if ($LASTEXITCODE -ne 0) { throw "ISCC failed ($LASTEXITCODE)." }

$setup = Join-Path $artifacts "Soundboard-Setup-$version.exe"
Write-Host "Done: $setup" -ForegroundColor Green
