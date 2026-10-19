# pcsim — firmware simulated on PC

The firmware is built as a **DLL** and run by a simulator on the PC; eScope connects to it as to a
target: protocol V3 over TCP, with the same symbols and workspace as on hardware.

```
eScope ── TCP 127.0.0.1:47700 ──► PcSimHost (C#)                 firmware.dll (GCC or MSVC)
                                   SimUart: UART driver whose  ⇄  simHw.c   simulated board
                                   "wire" is the TCP socket       app.c     application (target code)
                                   scheduler: 1 tick = 1 ms       eScopeProtocolV3.c + eScopeCfg.h hooks
                                   of simulated time
```

- `firmware/` — `app.c` (the application, as on the target), `simHw.c` (simulated board: DLL entry points
  `sim_Init` / `sim_Tick` / `sim_UartRx`, UART driver of `eScopeTransport`, memory sanity check),
  `eScopeCfg.h` (64 bits addresses, `ESCOPE_ADDR_TO_PTR` relocation, `ESCOPE_ADDR_IS_VALID`).
- `host/` — `PcSimHost`, a minimal simulator: loads the DLL, runs its tick in simulated time, relays its
  eScope UART to TCP. In your own simulator, only the UART driver (`SimUart`) is needed.
- `eScope.json` — workspace: device `PcSim`, `TCP:127.0.0.1,47700`, V3, **Addresses: 64 bits**, map
  `build\firmware.map`, sampling 1 ms.

## Build and run

```powershell
# firmware DLL, GCC (MSYS2 UCRT64: pacman -S mingw-w64-ucrt-x86_64-gcc)
.\firmware\build_gcc.ps1                    # -Msys C:\path\to\msys64 if not C:\Dev\Tools\msys64
# or MSVC, from a "x64 Native Tools Command Prompt"
firmware\build_msvc.bat

# simulator
dotnet build host -c Release -o build
.\build\PcSimHost.exe                        # [firmware.dll] [--port 47700] [--speed 1]
```

Then start eScope from this folder (it loads `eScope.json`), add channels (`motor_speed`, `motor_current`,
`led_state`) and record; `motor_cfg` can be written from the Watch.

## Points to know

- **Addresses**: the DLL is x64, its addresses are above 4 GB: the eScope device uses 64 bits addresses
  (V3 commands with `ADDR64_FLAG`). A device left in 32 bits gets `ERR_ADDRSIZE` (message in the log).
- **Relocation**: eScope sends the link addresses of the symbols; the DLL may be loaded elsewhere (ASLR).
  `ESCOPE_ADDR_TO_PTR` adds the offset between `__ImageBase` (loaded) and `SIM_LINK_BASE` (the
  `--image-base` / `/BASE` of the build scripts: keep them equal).
- **Sanity check**: an access outside the DLL image is refused (`ERR_ADDR`) instead of crashing the
  simulator.
- **Symbols**: GCC: GNU map (`build\firmware.map`) or GDB on `build\firmware.dll` (DWARF, any C
  expression). MSVC: MSVC map, without sizes (set the size of each channel).
- **Time**: no timestamp: eScope's time axis is the sampling cadence, in simulated time. With
  `--speed 5` one second of recording holds five seconds of simulation. A simulation paused (breakpoint)
  is reported as a communication loss (timeout), then resumes.
- **Determinism**: received bytes are delivered by the scheduler before the tick, in the same context as
  the other simulated interrupts: no critical section is needed (`eScopeDep.h`).

See `.claude/docs/pc-simulation.md` for the design.
