<#
.SYNOPSIS
    Builds the GitHub Release text for one version from CHANGELOG.md.

.DESCRIPTION
    Takes the "## [<Version>] - <date>" section of the changelog and appends install notes and the
    installer's SHA-256, so the changelog stays the only place release notes are written.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Version,
    [string]$Sha256
)

$ErrorActionPreference = 'Stop'

$lines = Get-Content (Join-Path $PSScriptRoot '..\CHANGELOG.md') -Encoding utf8
$start = -1
for ($i = 0; $i -lt $lines.Count; $i++) {
    if ($lines[$i] -match "^## \[$([regex]::Escape($Version))\]") { $start = $i + 1; break }
}
if ($start -lt 0) { throw "CHANGELOG.md has no section for [$Version]. Run ./release.ps1 to create it." }

$end = $lines.Count
for ($i = $start; $i -lt $lines.Count; $i++) {
    if ($lines[$i] -match '^## \[') { $end = $i; break }
}

$notes = ($lines[$start..($end - 1)] -join "`n").Trim()
if (-not $notes) { throw "The [$Version] section of CHANGELOG.md is empty." }

@"
$notes

---

**Cài đặt:** tải ``Soundboard-Setup-$Version.exe`` bên dưới và chạy (Windows 10/11 64-bit, không cần cài .NET). Soundboard cần [VB-Audio Virtual Cable](https://vb-audio.com/Cable/) (miễn phí, cài riêng); xem hướng dẫn trong [README](https://github.com/TruongDuyKhai/MixFX#cài-đặt-và-dùng).
"@

if ($Sha256) {
    @"

**SHA-256:** ``$Sha256``
"@
}
