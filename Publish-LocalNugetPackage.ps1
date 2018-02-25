<#
.SYNOPSIS
    Publishes a NuGet package to a local package repository.

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
    # Paths of NuGet package (.nupkg) files.
    [Parameter(Mandatory, Position=1)]
    [string[]] $Package,

    # Path of the local repository.
    [Parameter(Position=2)]
    [string] $Repository = (Join-Path $HOME .nuget\local)
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

# Install NuGet

$NuGetUrl = "https://dist.nuget.org/win-x86-commandline/latest/nuget.exe"
$NuGetDir = Join-Path $PSScriptRoot .nuget
$NuGetExe = Join-Path $NuGetDir nuget.exe

if (-not (Test-Path $NuGetExe)) {
    Write-Host "Downloading NuGet..."
    New-Item $NuGetDir -ItemType Directory -Force > $null
    Invoke-WebRequest $NuGetUrl -OutFile $NuGetExe -UseBasicParsing
}

# Update NuGet

$NuGetUp   = Join-Path $NuGetDir nuget.up
$Updated   = Get-Item $NuGetUp -ErrorAction SilentlyContinue | % LastWriteTimeUtc
$Yesterday = [DateTime]::UtcNow.AddDays(-1)

if (-not $Updated -or $Updated -lt $Yesterday) {
    & $NuGetExe update -self
    if ($LASTEXITCODE) { throw "NuGet exited with error code $LASTEXITCODE." }

    Set-Content $NuGetUp ""
}

# Publish Packages

$RepositoryPath = New-Item $Repository -ItemType Directory -Force | % FullName

foreach ($P in $Package) {
    $PackagePath = Convert-Path $P

    & $NuGetExe add $PackagePath -Source $RepositoryPath
    if ($LASTEXITCODE) { throw "NuGet exited with error code $LASTEXITCODE." }
}
