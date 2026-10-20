# pcsim — firmware simulated on PC

The firmware is built as a **DLL** with GCC and run by a simulator on the PC; eScope connects to it as to
a target: protocol V3 over TCP, with the same symbols and workspace as on hardware.

```
eScope ── TCP 127.0.0.1:47700 ──► PcSimHost (C#)                 firmware.dll (GCC)
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
- `Makefile` — builds the firmware DLL and the host into `build/`.
- `eScope.json` — workspace: device `PcSim`, `TCP:127.0.0.1,47700`, V3, **Addresses: 64 bits**, map
  `build\firmware.map`, sampling 1 ms.

## Build and run

From the **MSYS2 UCRT64** shell (packages `mingw-w64-ucrt-x86_64-gcc` and `make`; the host needs the
.NET 8 SDK):

```sh
make              # build/firmware.dll + build/firmware.map, build/PcSimHost.exe
make firmware     # the DLL only
./build/PcSimHost.exe        # [firmware.dll] [--port 47700] [--speed 1]
```

Then start eScope from this folder (it loads `eScope.json`), add channels (`motor_speed`, `motor_current`,
`led_state`) and record; `motor_cfg` can be written from the Watch.

## Points to know

- **Addresses**: the DLL is x64, its addresses are above 4 GB: the eScope device uses 64 bits addresses
  (V3 commands with `ADDR64_FLAG`). A device left in 32 bits gets `ERR_ADDRSIZE` (message in the log).
- **Relocation**: eScope sends the link addresses of the symbols; the DLL may be loaded elsewhere (ASLR).
  `ESCOPE_ADDR_TO_PTR` adds the offset between `__ImageBase` (loaded) and `SIM_LINK_BASE`, the
  `LINK_BASE` of the Makefile (given both to the linker and to the code).
- **Sanity check**: an access outside the DLL image is refused (`ERR_ADDR`) instead of crashing the
  simulator.
- **Symbols**: GNU map (`build/firmware.map`, with variable sizes), or GDB on `build/firmware.dll`
  (DWARF, any C expression).
- **Time**: no timestamp: eScope's time axis is the sampling cadence, in simulated time. With
  `--speed 5` one second of recording holds five seconds of simulation. A simulation paused (breakpoint)
  is reported as a communication loss (timeout), then resumes.
- **Determinism**: received bytes are delivered by the scheduler before the tick, in the same context as
  the other simulated interrupts: no critical section is needed (`eScopeDep.h`).

See `.claude/docs/pc-simulation.md` for the design.
