# Builds the firmware DLL with GCC (MSYS2 UCRT64): ..\build\firmware.dll, ..\build\firmware.map (symbols for
# eScope: GNU map, or the DLL itself with GDB, DWARF -g).
# Link base fixed (--image-base) = SIM_LINK_BASE of eScopeCfg.h; the DLL stays relocatable (ASLR).
param(
    [string]$Msys = "C:\Dev\Tools\msys64"
)
$ErrorActionPreference = "Stop"
$gcc = Join-Path $Msys "ucrt64\bin\gcc.exe"
if (-not (Test-Path $gcc)) { throw "gcc not found: $gcc (MSYS2: pacman -S mingw-w64-ucrt-x86_64-gcc)" }
$te = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$out = Join-Path $PSScriptRoot "..\build"   # next to PcSimHost
New-Item -ItemType Directory -Force $out | Out-Null

$env:PATH = (Split-Path $gcc) + ";" + $env:PATH
& $gcc -std=c99 -Wall -Wextra -O1 -g -fdata-sections -shared `
    "-I$PSScriptRoot" "-I$te\eScopeProtocol" "-I$te\eScopeTransport" `
    "$PSScriptRoot\app.c" "$PSScriptRoot\simHw.c" "$te\eScopeProtocol\eScopeProtocolV3.c" `
    -o "$out\firmware.dll" "-Wl,-Map=$out\firmware.map" "-Wl,--image-base=0x180000000" `
    -static-libgcc
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Write-Host "built $out\firmware.dll"
