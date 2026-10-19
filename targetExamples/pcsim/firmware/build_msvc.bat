@echo off
rem Builds the firmware DLL with MSVC (run from a "x64 Native Tools Command Prompt for VS"):
rem ..\build\firmware.dll, ..\build\firmware.map (symbols for eScope: MSVC map, no size, no GDB).
rem Link base fixed (/BASE) = SIM_LINK_BASE of eScopeCfg.h; the DLL stays relocatable (/DYNAMICBASE).
setlocal
set TE=%~dp0..\..
set OUT=%~dp0..\build
if not exist "%OUT%" mkdir "%OUT%"
cl /nologo /W4 /O1 /LD /I"%~dp0." /I"%TE%\eScopeProtocol" /I"%TE%\eScopeTransport" ^
   "%~dp0app.c" "%~dp0simHw.c" "%TE%\eScopeProtocol\eScopeProtocolV3.c" ^
   /Fo"%OUT%\\" /Fe"%OUT%\firmware.dll" ^
   /link /MAP:"%OUT%\firmware.map" /BASE:0x180000000 /DYNAMICBASE
endlocal
