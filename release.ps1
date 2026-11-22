<#
.SYNOPSIS
    Cuts a release: bumps the version, dates the changelog, commits and tags (PROJECT_PROMPT.md §9).

.DESCRIPTION
    1. Checks the version is SemVer, the working tree is clean, you are on an up-to-date `main`,
       and the tag does not exist yet.
    2. Writes the version into Directory.Build.props (the single source of truth).
    3. Turns the "## [Unreleased]" changelog section into "## [<version>] - <today>" and opens a new,
       empty Unreleased section above it.
    4. Builds with warnings as errors and runs the tests.
    5. Commits "chore(release): v<version>" and creates the annotated tag v<version>.
    6. With -Push, pushes main and the tag; the Release workflow then builds the installer and
       publishes the GitHub Release.

.EXAMPLE
    ./release.ps1 1.1.0 -Push
.EXAMPLE
    ./release.ps1 1.2.0-rc.1        # pre-release; review, then: git push origin main v1.2.0-rc.1
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory, Position = 0)][string]$Version,
    [switch]$Push,
    [switch]$SkipTests
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = $PSScriptRoot
$tag = "v$Version"
$propsPath = Join-Path $root 'Directory.Build.props'
$changelogPath = Join-Path $root 'CHANGELOG.md'

function Invoke-Git {
    $output = & git @args 2>&1
    if ($LASTEXITCODE -ne 0) { throw "git $($args -join ' ') failed: $output" }
    $output
}

# 1. Preconditions.
if ($Version -notmatch '^\d+\.\d+\.\d+(-[0-9A-Za-z][0-9A-Za-z.-]*)?$') {
    throw "'$Version' is not a SemVer version such as 1.2.3 or 1.2.3-rc.1."
}

Push-Location $root
try {
    if (Invoke-Git status --porcelain) { throw 'The working tree has uncommitted changes. Commit or stash them first.' }
    if ((Invoke-Git branch --show-current) -ne 'main') { throw 'Releases are cut from main.' }
    Invoke-Git fetch --quiet --tags origin | Out-Null
    if ((Invoke-Git rev-parse HEAD) -ne (Invoke-Git rev-parse origin/main)) { throw 'main differs from origin/main. Pull or push first.' }
    if (Invoke-Git tag --list $tag) { throw "Tag $tag already exists." }

    $current = & (Join-Path $root 'scripts\Get-Version.ps1')
    Write-Host "Releasing $current -> $Version" -ForegroundColor Cyan

    # 2. Version.
    $props = Get-Content $propsPath -Raw
    $props = [regex]::Replace($props, '<Version>[^<]*</Version>', "<Version>$Version</Version>")
    [IO.File]::WriteAllText($propsPath, $props, (New-Object Text.UTF8Encoding($false)))

    # 3. Changelog.
    $changelog = Get-Content $changelogPath -Raw -Encoding utf8
    $heading = [regex]::Match($changelog, '(?m)^## \[Unreleased\][^\r\n]*')
    if (-not $heading.Success) { throw 'CHANGELOG.md has no "## [Unreleased]" section.' }
    $afterHeading = $changelog.Substring($heading.Index + $heading.Length)
    $nextSection = [regex]::Match($afterHeading, '(?m)^## \[')
    $unreleasedBody = if ($nextSection.Success) { $afterHeading.Substring(0, $nextSection.Index) } else { $afterHeading }
    if (-not $unreleasedBody.Trim()) { throw 'The Unreleased section of CHANGELOG.md is empty: nothing to release.' }

    $date = Get-Date -Format 'yyyy-MM-dd'
    $newline = if ($changelog.Contains("`r`n")) { "`r`n" } else { "`n" }
    $replacement = "## [Unreleased]$newline$newline## [$Version] - $date"
    $changelog = $changelog.Substring(0, $heading.Index) + $replacement + $afterHeading
    [IO.File]::WriteAllText($changelogPath, $changelog, (New-Object Text.UTF8Encoding($false)))

    # 4. Build and test what is about to be tagged.
    if (-not $SkipTests) {
        dotnet build Soundboard.sln -c Release -warnaserror
        if ($LASTEXITCODE -ne 0) { throw 'Build failed; nothing was committed. Revert with: git checkout -- .' }
        dotnet test Soundboard.sln -c Release --no-build
        if ($LASTEXITCODE -ne 0) { throw 'Tests failed; nothing was committed. Revert with: git checkout -- .' }
    }

    # 5. Commit and tag.
    Invoke-Git add $propsPath $changelogPath | Out-Null
    Invoke-Git commit --quiet -m "chore(release): $tag" | Out-Null
    Invoke-Git tag -a $tag -m "Soundboard $Version" | Out-Null
    Write-Host "Created commit and tag $tag." -ForegroundColor Green

    # 6. Publish.
    if ($Push) {
        Invoke-Git push --quiet origin main | Out-Null
        Invoke-Git push --quiet origin $tag | Out-Null
        Write-Host "Pushed. The Release workflow now builds and publishes $tag." -ForegroundColor Green
    }
    else {
        Write-Host "Review, then publish with: git push origin main $tag" -ForegroundColor Yellow
    }
}
finally {
    Pop-Location
}
