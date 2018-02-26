<#
.SYNOPSIS
    Resolves the version for an automated build.

.DESCRIPTION
    This script merges version information from the source code, branch name, and build counter.

    The resulting version is published as follows:
    * In Version.props, intended to set assembly and nupkg versions
    * As console output, intended to set the TeamCity build number

.NOTES
    Copyright (C) 2018 Jeffrey Sharp

    Permission to use, copy, modify, and distribute this software for any
    purpose with or without fee is hereby granted, provided that the above
    copyright notice and this permission notice appear in all copies.

    THE SOFTWARE IS PROVIDED "AS IS" AND THE AUTHOR DISCLAIMS ALL WARRANTIES
    WITH REGARD TO THIS SOFTWARE INCLUDING ALL IMPLIED WARRANTIES OF
    MERCHANTABILITY AND FITNESS. IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR
    ANY SPECIAL, DIRECT, INDIRECT, OR CONSEQUENTIAL DAMAGES OR ANY DAMAGES
    WHATSOEVER RESULTING FROM LOSS OF USE, DATA OR PROFITS, WHETHER IN AN
    ACTION OF CONTRACT, NEGLIGENCE OR OTHER TORTIOUS ACTION, ARISING OUT OF
    OR IN CONNECTION WITH THE USE OR PERFORMANCE OF THIS SOFTWARE.
#>
param (
    # Name of the branch or tag.  Default: "local".
    [Parameter(Position=1)]
    [ValidateNotNullOrEmpty()]
    [string] $Branch = "local",

    # Build counter.  Default: minutes since 2018-01-01 00:00:00 UTC.
    [Parameter(Position=2)]
    [ValidateRange(1, [long]::MaxValue)]
    [long] $Counter = ([DateTime]::Now - [DateTime]::new(2018, 1, 1)).TotalMinutes
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$PullRequestRegex = [regex] '(?nx)
    ^
    ( pulls/ )?
    (?<Number> [1-9][0-9]* )
    $
'

# Reference:
# https://github.com/semver/semver/blob/master/semver.md
$VersionRegex = [regex] '(?nx)
    ^
    ( release/ )?
    (?<Version>
        (?<Numbers>
            ( 0 | [1-9][0-9]* )
            (
                \.
                ( 0 | [1-9][0-9]* )
            ){2}
        )
        # Pre-release tag
        (
            -
            ( 0 | [1-9][0-9]* | [0-9]*[a-zA-Z-][0-9a-zA-Z-]* )
            (
                \.
                ( 0 | [1-9][0-9]* | [0-9]*[a-zA-Z-][0-9a-zA-Z-]* )
            )*
        )?
        # Build metadata
        (
            \+
            [0-9a-zA-Z-]+
            (
                \.
                [0-9a-zA-Z-]+
            )*
        )?
    )
    $
'

# Get code's version string
$PropsPath = Join-Path $PSScriptRoot Version.props
$PropsXml  = [xml] (Get-Content -LiteralPath $PropsPath -Raw -Encoding UTF8)
$Props     = $PropsXml.Project.PropertyGroup

# Parse code's version string
if ($Props.Version -match $VersionRegex) {
    $Version     = [version] $Matches.Numbers # 1.2.3
    $VersionFull = [string]  $Matches.Version # 1.2.3-pre+123
}
else {
    throw "Version.props <Version> content is not a SemVer 2.0 version."
}

# File version components are 16-bit numbers; just wrap counter to 17 bits
$Counter = $Counter -band 0xFFFF

# Merge branch and build counter
if ($Branch -match $VersionRegex) {
    # Branch name contains a usable version string (ex: a release scenario)
    $BranchVersion     = [version] $Matches.Numbers # 1.2.3
    $BranchVersionFull = [string]  $Matches.Version # 1.2.3-pre+123

    # Verify branch/code versions have same numbers
    if ($BranchVersion -ne $Version) {
        throw "Branch version ($BranchVersion) does not match code version ($Version)."
    }

    # Use branch version verbatim
    $VersionFull = $BranchVersionFull
}
elseif ($Branch -match $PullRequestRegex) {
    # Branch name contains a pull request number
    $VersionFull = "{0}-pr{1}-b{2}" -f $Version, $Matches.Number, $Counter
}
else {
    # Branch name not recognized
    $VersionFull = "{0}-{1}-b{2}" -f $Version, $Branch, $Counter
}

# Apply version number to code.
$Props.Version     = $VersionFull         # => nupkg, assembly, informational/product versions
$Props.FileVersion = "$Version.$Counter"  # => file version
$PropsXml.Save($PropsPath)

# Tell TeamCity the new build number
Write-Output "##teamcity[buildNumber '$VersionFull']"
