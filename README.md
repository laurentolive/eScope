# eScope

**English** | [Français](README.fr.md)

A software oscilloscope for embedded developers: eScope reads variables straight from a
microcontroller's RAM while it runs and plots them in real time, without halting the program
or reaching for a hardware scope.

Windows application (WinForms, .NET 8). Tested targets: Arduino (AVR), STM32, Renesas RA, and
more generally any MCU that can embed the small firmware module provided.

## How it works

```
 Symbol file (.elf / .map)               Microcontroller
          │                                    ▲
          ▼                                    │ addresses + sizes
   eScope: variable name ──► address ──► eScope protocol (serial, TCP, RTT)
          ▲                                    │
          └──────── raw values ◄───────────────┘
```

The firmware is passive: it does not know which variables are being watched. eScope resolves
names to addresses from the symbol file and sends the list to the MCU, which periodically sends
back the raw content of those memory areas.

## Features

### Acquisition
- **Multiple devices** at once, each with its own transport and symbol file.
- **Transports**: serial / Bluetooth serial, TCP, SEGGER RTT through an SWD probe
  (`bridge_TcpRtt.py` bridge, pyOCD or J-Link).
- **Data loss detection**: missing frames and device silences are marked on the plot.
- **Hot reload** of the symbol file after a rebuild.

### Symbols
- **`.map` file**: format detected (GNU ld map with sizes, address / name lines), or a custom regular expression.
- **ELF through GDB** (`gdb-multiarch` bundled in `tools/gdb`): any C expression, such as
  `ctx.cfg.gain`, `tab[3].x`, `*(uint16_t*)&reg` or `uart->Instance->BRR`.
  - Pointers are read **on the target** at resolution time: GDB is connected to a built-in
    gdbserver that goes through the eScope protocol (V3).
  - Completion of variable names and struct fields.

### Plotting (trace tabs)
- Colored curves, one Y axis per channel, gain / offset, linked channels that move together.
- Zoom, pan, zoom to selection, zoom to fit; resizable legend.
- **Markers**, **deltas** (interval measurement), text **annotations**, watch line.
- **Computed channels** defined by a C# script over the other channels.
- Cut / copy / paste of data (including from Excel), undo / redo.
- Trace saving (compressed), **CSV** and **PNG** export.

### Analysis (A key)
On the selection, for each channel: min, max, peak-to-peak, mean, RMS, standard deviation,
frequency, period, duty cycle (edge detection with hysteresis) and **FFT** spectrum
(linear or dB).

### Watch (W key)
A window of current values refreshed at 1 Hz, with a display format (decimal, hex, float…) and
**writing** of variables on the target. With GDB symbols, each row is a C expression evaluated
again on every refresh: pointers are followed live, and an expression without an address
(`a * 2 + b`, a struct, a bitfield) is computed by GDB.

### AI automation (Tools > AI automation)
A local server, off by default, lets an AI agent (Claude Code, Cursor, VS Code… through **MCP**) or a script
(**REST**) drive the open eScope window: add channels, acquire, analyze (statistics, frequency, FFT, edges),
annotate, take screenshots, export, read and write target variables. Changes are undoable (Ctrl+Z), writes to
the target need an explicit option, and requests need a token. Guide, Python client, a protocol V3 device
simulator and an end-to-end example: [tools/automation](tools/automation/README.md).

## Protocols

| Version | Use |
|---|---|
| V1 | Legacy: fixed channel count and data size |
| V2 | Address and size per channel (maintenance only) |
| **V3** | Recommended: periodic reads, one-shot reads and writes, clear |

The wire format is described in [.claude/docs/api.md](.claude/docs/api.md).

## Adding eScope to a firmware

Reference code lives in [targetExamples/](targetExamples/):

- `eScopeProtocol/`: protocol (V1, V2, V3, V3Id);
- `eScopeTransport/`: transport (UART, TCP, RTT).

Both are selected at compile time in a project-specific `eScopeCfg.h`:

```c
#define ESCOPE_PROTOCOL_V3
#define ESCOPE_TRANSPORT_UART
#define ESCOPE_PROTOCOL_BAUDRATE      115200
#define ESCOPE_PROTOCOL_CHAN_MAX      (8U)
#define ESCOPE_PROTOCOL_ADDRESS_SIZE  (4U)
#define ESCOPE_PROTOCOL_DATASIZE_SIZE (4U)
#define ESCOPE_PROTOCOL_TX_SIZE       (128U)
```

Then call `eScopeProtocol_Init()` at startup and `eScopeProtocol_Process()` at a fixed rate
(that rate is the sampling period). Complete examples:

- `arduino/`: ready-to-use `DebugScope` library;
- `stm32/`: STM32CubeIDE project (NUCLEO-C051C8), UART (ST-LINK virtual COM port or USART1 bridge) or RTT;
- `ra/`: Renesas RA4E1, RTT or UART (SCI9);
- `pcsim/`: firmware simulated on PC: built as a DLL with GCC, run by a simulator whose UART is relayed
  to TCP (64 bits addresses).

Wireless links go through an ESP32 serial bridge: [`BluetoothSerialBridge/`](BluetoothSerialBridge/)
(Bluetooth serial port) and [`WifiTcpBridge/`](WifiTcpBridge/) (Wi-Fi TCP).

Step-by-step tutorials, with wiring diagrams and screenshots, for each board and link:
[docs/tutorials](docs/tutorials/README.md).

## Build and run

```powershell
dotnet build eScope.sln
dotnet run --project eScope/eScope.csproj
```

The bundled GDB is downloaded with `powershell -ExecutionPolicy Bypass -File tools\fetch-gdb.ps1`
(portable MSYS2 build, copied next to `eScope.exe`).

## Quick start

1. Build the firmware with the eScope module and get the `.elf` (or `.map`) file.
2. In eScope, declare a **device**: transport, protocol, symbol file.
3. Add **channels** by typing variable names (or expressions).
4. **Record** (Ctrl+Space): the curves scroll in real time.
5. Measure with markers, deltas and the analyzer; follow values in the Watch window.
6. The workspace is saved to a `.escope` file (*Files > Open...* or drop it on the window to reopen it).

## License

The eScope PC application is source-available under the [PolyForm Noncommercial License 1.0.0](LICENSE.md):

- **Free for noncommercial use**: personal projects, hobby, study, research, and use by educational, public research, charitable or government institutions.
- **Commercial use requires a Professional license** (work, business, client projects): buy it from the *About > License* menu of eScope.

The firmware side is **example code under the [MIT License](targetExamples/LICENSE.md)**: `targetExamples/` (eScopeProtocol, eScopeTransport, Arduino library, integration code of the example projects) and [`BluetoothSerialBridge/`](BluetoothSerialBridge/LICENSE.md). Copy it into your firmware and ship it in your products, commercial ones included, without any eScope license.

Third-party files keep their own license: the SEGGER RTT sources and the ST / Renesas drivers in `targetExamples/`, and GDB in `eScope/tools/gdb` (GPL, see `SOURCES.txt`).

## Documentation

- [Tutorials](docs/tutorials/README.md): deploying and using eScope on Arduino Nano, STM32 NUCLEO-C051C8 and
  Renesas FPB-RA4E1 over serial, RTT, Bluetooth and Wi-Fi / TCP, and firmware simulated on PC
- [AI / script automation](tools/automation/README.md): automation API (MCP + REST)

Design documents (French):

- [Product](.claude/docs/product.md): vision, features, editions
- [Architecture](.claude/docs/architecture.md): threading, data flow, symbols
- [API](.claude/docs/api.md): internal interfaces and wire format
- [Constraints](.claude/docs/constraints.md): limits and deliberate choices
- [Automation](.claude/docs/automation.md): full specification of the MCP + REST API
