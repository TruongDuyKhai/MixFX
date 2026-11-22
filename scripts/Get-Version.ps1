<#
.SYNOPSIS
    Prints the app version from Directory.Build.props, the single place it is declared (PROJECT_PROMPT.md §9).
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

[xml]$props = Get-Content (Join-Path $PSScriptRoot '..\Directory.Build.props')
$version = $props.Project.PropertyGroup | ForEach-Object { $_.Version } | Where-Object { $_ } | Select-Object -First 1
if (-not $version) { throw 'No <Version> found in Directory.Build.props.' }
$version
