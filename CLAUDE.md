# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Spec docs

- [.claude/docs/product.md](.claude/docs/product.md) — vision, features, éditions
- [.claude/docs/architecture.md](.claude/docs/architecture.md) — décisions techniques, threading, data flow
- [.claude/docs/api.md](.claude/docs/api.md) — contrats IProtocol / ICom / ISymbol, format filaire
- [.claude/docs/constraints.md](.claude/docs/constraints.md) — ce qu'on ne fait pas, protocoles dépréciés
- [.claude/docs/rtt-transport.md](.claude/docs/rtt-transport.md) — transport SEGGER RTT (ComRtt + bridge Python)
- [.claude/docs/automation.md](.claude/docs/automation.md) — pilotage par IA / script (serveur MCP + REST)

## Project Overview

eScope is a Windows Forms oscilloscope/data acquisition application written in C# (.NET 8). It connects to embedded devices (microcontrollers) via serial/Bluetooth, TCP or SEGGER RTT, reads variable values from memory, and visualizes them in real-time plots. The target audience is embedded developers who want to visualize live variable data like a hardware oscilloscope.

A Linux port (WinForms → Avalonia, with a platform-agnostic `eScope.Core`) is in progress on the `worktree-linux-port-core` branch; `master` stays Windows-only.

## Build & Run

```powershell
# Build
dotnet build eScope.sln

# Run from project directory
dotnet run --project eScope/eScope.csproj
```

The project targets `net8.0-windows7.0` — Windows only, no cross-platform builds. Open `eScope.sln` in Visual Studio for full IDE support.

The bundled GDB (`eScope/tools/gdb`, copied next to the exe) is fetched with `tools/fetch-gdb.ps1`.

`tools/automation/` holds the automation API client (`escope_api.py`), an end-to-end example and a protocol V3
device simulator over TCP (`sim/sim_v3.py` + a ready workspace): run eScope from `tools/automation/sim` to try
acquisitions and target reads / writes without hardware.

There are no automated tests; verification is done by running the application (the V3 simulator helps).

## Architecture

### Data Flow

1. Workspace loaded from `eScope.json` (devices, traces, channels, layout)
2. Each `Device` loads symbols (variable name or C expression → memory address), connects via a `Protocol`
3. A single 50 ms WinForms timer calls `DeviceCtrl.Process()` for each device (protocol frames → `FrameComplete` → channel buffers) then `TraceCtrl.Refresh()` on the current trace
4. User interactions create `ICommand` objects (undo/redo via `commandMgr`)

### Key Abstractions

**`Device.cs`** — Top-level unit: owns a `Protocol`, a `Symbol` resolver, and a set of registered channels. Supports multiple protocol versions (V1/V2/V3). Watches the symbol file for hot-reload (reload done in `Process()`). With GDB symbols it serves the GDB memory requests and re-resolves channels when the device connects.

**`Protocol/` (`IProtocol`)** — Communication with the embedded device. Factory via `IProtocol.New()`. Three implementations (V1/V2/V3) handle different wire formats. Core operations: `Process()`, `Start()`/`Stop()`, `WriteMem()`/`ReadMem()` (one-shot, V3 only), `ReadMemRegister()`/`ReadMemUnregister()` (periodic, values delivered by the `FrameComplete` event keyed by token).

**`Symbol/` (`ISymbol`)** — Maps variable names to memory addresses. Two implementations: `SymbolMap` (`.map` file: GNU ld map, `FormatGNU` or address/name lines detected, or a custom regex) and `SymbolGDB` (GDB/MI on the ELF, any C expression; GDB reads pointers on the target through `Symbol/Gdb/GdbRemoteBridge`, a gdbserver backed by the V3 one-shot reads). Factory via `ISymbol.New()`.

**`Com/` (`ICom`)** — Transport layer. `ComSerial` wraps `SerialPortStream`; `ComTcp` is a raw TCP client; `ComRtt` connects to `bridge_TcpRtt.py` (RTT over SWD with pyOCD or J-Link), started locally through `PythonLauncher`.

**`Plot/`** — ScottPlot 5.0 WinForms integration. `eScopePlot` is the core engine: pan/zoom/scroll, channel color management, markers, deltas, annotations, legend, dark mode. Computed channels are C# scripts compiled with Roslyn (`Channel.CompileScript`).

**`Trace.cs`** — Data model for a single plot tab: owns collections of `Channel`, `Marker`, `Delta`, `Annotation` and `Watch.Data` objects. `TraceCtrl` is the controller that bridges data to the plot. A trace can be saved alone as `.etrace` (gzipped JSON).

**`UI/`** — Look of the main window: `Theme` (light / dark palettes, fonts, ToolStrip renderer, icons from Segoe Fluent Icons), `ThemeApplier` (themes every designer form when activated, CBT hook), `SidePanel` (per trace: channels, markers, selection measures, embedded watch), `TraceTabStrip` + `PlotCard` (tabs above the plot), `UiSettings` (per-user `%LOCALAPPDATA%\eScope\ui.json`: dark mode, panel width), `DialogLayout` (common layout of the edit dialogs, built in code over the designer controls: their designer positions are not used). Layout of the main form in `eScope.Layout.cs`.

**`Analyzer/`** — Analysis window on the selection: statistics, frequency / period / duty cycle, FFT.

**`Watch/`** — Floating window that shows live variable values (independent of the plot). With GDB symbols each row is a C expression evaluated again on every refresh.

**`Automation/`** — Local server (HttpListener, `localhost`, Bearer token, off by default: Tools > AI automation) exposing the same tool catalog over MCP (`/mcp`, `ModelContextProtocol.Core`, stateless) and REST (`/api/<tool>`). Tools are the `[McpServerTool]` methods of `Automation/Tools/`; they go through `AutomationService`, which runs every operation on the UI thread. See [.claude/docs/automation.md](.claude/docs/automation.md).

**`Command/`** — Undo/redo. All user mutations (add/delete channel, marker, etc.) go through `ICommand` implementations collected by `commandMgr`.

### Persistence

Workspace state serialized to `eScope.json` with `System.Text.Json`. The file is created in the working directory on first save and loaded at startup.

### Key Dependencies

| Package | Purpose |
|---|---|
| ScottPlot.WinForms 5.0 | Real-time plotting |
| SerialPortStream 2.4 | Reliable serial I/O (preferred over `System.IO.Ports`) |
| Microsoft.CodeAnalysis.CSharp.Scripting | Computed channel scripts |
| System.IO.Pipelines | V3 frame extraction |
| ModelContextProtocol.Core | MCP server of the automation API (no ASP.NET Core: the release is framework-dependent) |
| Serilog | Logging |
| Newtonsoft.Json | Legacy: `[JsonIgnore]` attributes and `JObject` helpers only (persistence uses `System.Text.Json`) |
| IronPython 3.4 | Referenced but not used |

### Target-Side Code

`targetExamples/` contains reference firmware organized into reusable modules:

- `arduino/` — Arduino library `DebugScope.cpp/h` (self-contained V3 port) and example sketch
- `stm32/` — STM32CubeIDE project (NUCLEO-C051C8) with HAL drivers
- `ra/` — Renesas RA4E1 (FPB) project, V3Id over RTT
- `SEGGER_RTT/` — SEGGER RTT sources used by the RTT transport

`BluetoothSerialBridge/` and `WifiTcpBridge/` (repository root) — ESP32 sketches bridging a target UART to Bluetooth SPP / to a Wi-Fi TCP server (port 3333).

`docs/tutorials/` (English) and `docs/tutoriels/` (French) — user tutorials per board and link, kept in sync (same numbering, language link at the top of each page). Screenshots and SVG wiring diagrams are shared in `docs/img/` (`*.svg` French, `*.en.svg` English). User-facing READMEs exist in both languages (`README.md` / `README.fr.md`).

**`eScopeProtocol/`** — Protocol implementations (compile-time selection via `eScopeProtocol.h`).  
Define exactly one of the following in `eScopeCfg.h` (project-supplied):

| Define | Description |
|---|---|
| `ESCOPE_PROTOCOL_V1` | Minimal — fixed channel count and data size |
| `ESCOPE_PROTOCOL_V2` | Per-channel address + data size |
| `ESCOPE_PROTOCOL_V3` | Full command set (read/write/clear) |
| `ESCOPE_PROTOCOL_V3ID` | V3 variant for ID-based UART drivers; the whole config (sizes included) lives in the caller's `eScopeProtocolCfg_t` |

**`eScopeTransport/`** — Transport implementations (compile-time selection via `eScopeTransport.h`).  
Define exactly one of the following in `eScopeCfg.h`:

| Define | Description |
|---|---|
| `ESCOPE_TRANSPORT_UART` | Generic `hwUart` driver |
| `ESCOPE_TRANSPORT_RTT` | SEGGER RTT (poll from the main loop before `eScopeProtocol_Process()`) |
| `ESCOPE_TRANSPORT_TCP` | TCP (stub) |

Both layers share the same integration pattern: include only the selector header (`eScopeProtocol.h` / `eScopeTransport.h`), which resolves concrete types and APIs via preprocessor aliases — no runtime indirection.
