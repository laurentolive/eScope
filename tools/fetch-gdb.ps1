<#
.SYNOPSIS
    Downloads a portable gdb-multiarch (MSYS2 UCRT64 build) into eScope\tools\gdb
    so it can be shipped next to eScope.exe (no toolchain needed on the target PC).

.DESCRIPTION
    - Resolves gdb-multiarch and its runtime dependencies from repo.msys2.org
    - Keeps only what gdb needs at runtime: exe + DLLs, gdb data directory,
      a trimmed Python stdlib, and the licenses of every bundled package
    - Writes SOURCES.txt (exact package versions) for GPL compliance

    The build copies eScope\tools\gdb\** to <output>\tools\gdb\ automatically.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File tools\fetch-gdb.ps1
#>
param(
    [string]$Repo = "https://repo.msys2.org/mingw/ucrt64",
    [string]$OutDir = (Join-Path $PSScriptRoot "..\eScope\tools\gdb")
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"   # Invoke-WebRequest is very slow with the progress bar
$prefix = "mingw-w64-ucrt-x86_64-"
$tar = Join-Path $env:SystemRoot "System32\tar.exe"   # bsdtar: handles .zst

# Virtual packages ("provides") used in dependency lists
$aliases = @{ "cc-libs" = @("libgcc", "libstdc++") }
# Runtime deps gdb never loads (GUI / package-manager parts of Python)
$skip = @("tcl", "tk", "tzdata")

$work = Join-Path ([IO.Path]::GetTempPath()) "escope-fetch-gdb"
$stage = Join-Path $work "stage"
Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $stage | Out-Null

Write-Host "Reading package index..."
$index = (Invoke-WebRequest "$Repo/" -UseBasicParsing).Content
$files = [regex]::Matches($index, 'href="([^"]+\.pkg\.tar\.zst)"') | ForEach-Object { [uri]::UnescapeDataString($_.Groups[1].Value) } | Sort-Object -Unique

function VersionKey([string]$v) {
    # Natural sort key: pad every number so "10.1" sorts after "9.2"
    [regex]::Replace($v, '\d+', { param($m) $m.Value.PadLeft(10, '0') })
}

function Find-Package([string]$name) {
    $pattern = '^' + [regex]::Escape("$prefix$name") + '-(\d[^-]*-\d+)-any\.pkg\.tar\.zst$'
    $files | Where-Object { $_ -match $pattern } |
        Sort-Object { VersionKey ([regex]::Match($_, $pattern).Groups[1].Value) } |
        Select-Object -Last 1
}

$queue = New-Object System.Collections.Queue
$queue.Enqueue("gdb-multiarch")
$done = @{}
$fetched = @()

while ($queue.Count -gt 0) {
    $name = $queue.Dequeue()
    if ($aliases.ContainsKey($name)) { $aliases[$name] | ForEach-Object { $queue.Enqueue($_) }; continue }
    if ($done.ContainsKey($name) -or $skip -contains $name) { continue }
    $done[$name] = $true

    $file = Find-Package $name
    if (-not $file) { Write-Warning "Package not found: $name"; continue }

    Write-Host "  $file"
    $pkgPath = Join-Path $work $file
    Invoke-WebRequest ("$Repo/" + [uri]::EscapeDataString($file)) -OutFile $pkgPath -UseBasicParsing
    $pkgDir = Join-Path $work ($name + ".pkg")
    New-Item -ItemType Directory -Force $pkgDir | Out-Null
    & $tar -xf $pkgPath -C $pkgDir
    if ($LASTEXITCODE -ne 0) { throw "tar failed on $file" }
    $fetched += $file

    Get-Content (Join-Path $pkgDir ".PKGINFO") | Where-Object { $_ -match '^depend = ' } | ForEach-Object {
        $dep = ($_ -replace '^depend = ', '') -replace '[<>=].*$', ''
        $queue.Enqueue($dep.Substring($prefix.Length))
    }
    $content = Join-Path $pkgDir "ucrt64"
    if (Test-Path $content) { Copy-Item "$content\*" $stage -Recurse -Force }   # meta packages have no files
}

Write-Host "Assembling $OutDir ..."
Remove-Item $OutDir -Recurse -Force -ErrorAction SilentlyContinue
$bin = New-Item -ItemType Directory -Force (Join-Path $OutDir "bin")
Copy-Item (Join-Path $stage "bin\gdb-multiarch.exe") $bin
Copy-Item (Join-Path $stage "bin\*.dll") $bin

# gdb data directory (python helpers, syscalls) - located via ../share/gdb
New-Item -ItemType Directory -Force (Join-Path $OutDir "share") | Out-Null
Copy-Item (Join-Path $stage "share\gdb") (Join-Path $OutDir "share\gdb") -Recurse
Copy-Item (Join-Path $stage "share\licenses") (Join-Path $OutDir "share\licenses") -Recurse

# Python stdlib (libpython is mandatory for this gdb build) - trimmed
$pyLib = Get-ChildItem (Join-Path $stage "lib") -Directory -Filter "python3*" | Select-Object -First 1
$pyOut = Join-Path $OutDir ("lib\" + $pyLib.Name)
New-Item -ItemType Directory -Force $pyOut | Out-Null
$pyExclude = @("test", "idlelib", "tkinter", "turtledemo", "ensurepip", "site-packages", "lib2to3", "venv", "pydoc_data", "unittest")
Get-ChildItem $pyLib.FullName | Where-Object { $pyExclude -notcontains $_.Name } |
    ForEach-Object { Copy-Item $_.FullName $pyOut -Recurse }
Get-ChildItem $pyOut -Recurse -Directory -Filter "__pycache__" | Remove-Item -Recurse -Force
New-Item -ItemType Directory -Force (Join-Path $pyOut "site-packages") | Out-Null

$sources = @(
    "This folder contains gdb-multiarch and its runtime dependencies, unmodified binaries from MSYS2 (UCRT64).",
    "GDB is free software licensed under the GNU GPL v3 or later; see share\licenses\ for each package license.",
    "",
    "Corresponding source code:",
    "  Build recipes : https://github.com/msys2/MINGW-packages",
    "  Source tarballs: https://repo.msys2.org/mingw/sources/",
    "  GDB upstream  : https://sourceware.org/gdb/",
    "",
    "Bundled packages:"
) + ($fetched | ForEach-Object { "  $_" })
Set-Content (Join-Path $OutDir "SOURCES.txt") $sources -Encoding UTF8

Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue
$size = (Get-ChildItem $OutDir -Recurse -File | Measure-Object Length -Sum).Sum / 1MB
Write-Host ("Done: {0} ({1:N0} MB)" -f (Resolve-Path $OutDir), $size)
