# RTT Transport — Implementation Spec

> **Status: implemented.** This is the original spec; the implementation differs on these points:
> - Connection string `RTT:<target>:<probeUID>:<host>:<port>:<cbAddr>` (see below).
> - The bridge is started only when the host is local (`127.0.0.1`, `localhost`, `::1`); otherwise
>   ComRtt connects to a bridge already running on another machine.
> - Python is located by `PythonLauncher` (`python` / `python3`), the script is looked up next to
>   `eScope.exe` (`AppContext.BaseDirectory/bridge_TcpRtt.py`).
> - `cbAddr`: address of `_SEGGER_RTT`, resolved from the symbol file in DeviceEdit and passed as
>   `--cb-addr` so the bridge skips the SRAM scan.
> - Bridge backends: `--backend auto|jlink|pyocd` (native J-Link RTT when pylink-square is installed
>   and the probe is a J-Link), plus `--freq`, `--jlink-device`, `--jlink-serial`, `--host`.
> - DeviceEdit has Host / Port text boxes in the RTT panel; the TCP radio is implemented (`TCP:host,port`).
> - Firmware side: `targetExamples/eScopeTransport/eScopeTransportRtt.c` (`ESCOPE_TRANSPORT_RTT`).

## Goal

Add a `ComRtt` class to eScope (implementing `ICom`) that transports data over Segger RTT via SWD,
using **any target and probe supported by pyOCD**. The user selects target and probe in DeviceEdit;
eScope spawns `bridge_TcpRtt.py` as a subprocess and connects to it via TCP on localhost:9090.

---

## Architecture

```
firmware (any Cortex-M target)
  │  SEGGER_RTT_Write / SEGGER_RTT_Read
  │
Debug probe (ST-Link, J-Link, CMSIS-DAP…)
  │  SWD / JTAG
  │
bridge_TcpRtt.py --target <name> [--probe <uid>]
  │  pyOCD reads/writes RTT ring buffers in target SRAM
  │  TCP server on localhost:9090
  │
ComRtt.cs (C# TcpClient)
  │  spawns bridge_TcpRtt.py in Open(), kills it in Close()
  │  connects TCP after bridge prints "Listening on 127.0.0.1:9090"
  │
ICom interface — same contract used by ComSerial
  │
Protocol / Device / Plot — unchanged
```

### Connection string format

```
RTT:<target>:<probeUID>:<host>:<port>:<cbAddr>
```

| Part | Example | Meaning |
|---|---|---|
| `target` | `stm32c051c8tx` | pyOCD target name from `pyocd list --targets` |
| `probeUID` | `0672FF303230534343220043` | Probe unique ID from `pyocd list --probes`; empty → auto-select |
| `host` | `127.0.0.1` | Bridge host (default `127.0.0.1`; local → bridge started by eScope) |
| `port` | `9090` | Bridge TCP port (default 9090) |
| `cbAddr` | `20000400` | `_SEGGER_RTT` address in hex, optional (empty → SRAM scan) |

Examples:
```
RTT:stm32c051c8tx:0672FF303230534343220043:127.0.0.1:9090:20000400
RTT:stm32f103rb::127.0.0.1:9090:            ← empty probe = auto-select, no control block address
```

Parsed in `ICom.Get()`: missing trailing parts take their default value.

---

## pyOCD target and probe discovery

### List all supported targets

```powershell
python -m pyocd list --targets
```

Output (truncated):
```
  Name                          Vendor              Part Numbers          Source
──────────────────────────────────────────────────────────────────────────────────
  stm32c051c8tx                 STMicroelectronics  STM32C051C8Tx         pack
  stm32f103rb                   STMicroelectronics  STM32F103RB           builtin
  rp2040                        Raspberry Pi        RP2040                pack
  ...
```

First token on each data line is the target name passed to `--target`.

### List connected probes

```powershell
python -m pyocd list --probes
```

Output:
```
  #   Probe/Board                         Unique ID               Target
──────────────────────────────────────────────────────────────────────────
  0   STMicroelectronics ST-Link V3 MINIE  0672FF303230534343220043  n/a
  1   SEGGER J-Link OB                     000788123456             n/a
```

`Unique ID` column is the value passed to `--probe`. If omitted, pyOCD prompts interactively
(bridge runs non-interactively, so always pass a UID when multiple probes are connected).

---

## Files to create / modify

| Action | File |
|---|---|
| **Create** | `eScope/Com/ComRtt.cs` |
| **Modify** | `eScope/Com/bridge_TcpRtt.py` — add TCP server + CLI args |
| **Modify** | `eScope/Com/ICom.cs` — add RTT branch in `Get()` |
| **Modify** | `eScope/DeviceEdit.Designer.cs` — add RTT/SWD radio + panelRTT |
| **Modify** | `eScope/DeviceEdit.cs` — RTT type handling |

---

## 1 — ComRtt.cs (new file)

Constructor: `ComRtt(string target, string probeUid)`

### Open()

1. Locate `bridge_TcpRtt.py` relative to the executable: `AppDomain.CurrentDomain.BaseDirectory/Com/bridge_TcpRtt.py`
2. Spawn `python bridge_TcpRtt.py --target <target> [--probe <probeUid>]`  
   with `RedirectStandardOutput = true`, `CreateNoWindow = true`
3. Read stdout lines until a line containing `"Listening on"` appears (bridge is ready)
4. Connect `TcpClient` to `127.0.0.1:9090`
5. Get `NetworkStream`, set `ReadTimeout = 10 ms`, `WriteTimeout = 500 ms`
6. Start background reader thread (see below)

### Close() / Dispose()

- `_cts.Cancel()` → stops reader thread
- Close `NetworkStream` and `TcpClient`
- Kill bridge process (`Process.Kill()`)

### Read(int n) / ReadAll()

Non-blocking. Use a 4096-byte ring buffer (`_rxBuf`, head/tail) filled by background reader.  
`Read(n)` returns `Array.Empty<byte>()` if fewer than `n` bytes available.  
`ReadAll()` drains everything in the buffer.  
Both are thread-safe via `lock(_rxLock)`.

### ReadAsync(CancellationToken)

Await `SemaphoreSlim(0,1)` released by the reader thread when new data arrives. Then call `ReadAll()`.

### Write(byte[])

Write directly to `NetworkStream`. Catch all exceptions, log with `LogMsg.Info()`, never throw.

### Flush()

Reset head/tail to 0 (discard buffered RX data).

### Background reader thread

```
while not cancelled:
    read up to 256 bytes from NetworkStream (blocking, ReadTimeout = 10 ms)
    if bytes received: append to _rxBuf, release semaphore if count == 0
    if IOException (timeout): continue
    if other exception: exit thread
```

---

## 2 — ICom.cs — extend Get()

```csharp
else if (comPrm[0].Equals("RTT"))
{
    string target  = comPrm.Length > 1 ? comPrm[1] : "";
    string probeUid = comPrm.Length > 2 ? comPrm[2] : "";
    return new ComRtt(target, probeUid);
}
```

---

## 3 — bridge_TcpRtt.py — TCP server + CLI args

### CLI

```
python bridge_TcpRtt.py --target <name> [--probe <uid>] [--port 9090]
                        [--sram-start 0x20000000] [--sram-size 0x10000]
```

`--target` is required. `--probe` is optional (pyOCD auto-selects if omitted).  
`--sram-start` / `--sram-size` override the SRAM scan range for unusual targets.

### Behaviour

1. Connect to the target via pyOCD (`ConnectHelper.session_with_chosen_probe`, pass `unique_id` if `--probe` given)
2. Scan SRAM for the RTT control block signature
3. Open TCP server on `localhost:<port>`, print `Listening on 127.0.0.1:<port>` (triggers ComRtt.Open)
4. Accept one client at a time:
   - **UP → TCP**: reader thread drains RTT UP buffer, calls `conn.sendall()`
   - **TCP → DOWN**: main loop uses `select.select` to receive from socket, writes to RTT DOWN buffer
5. On client disconnect, wait for next client (loop) — do not exit

### Key pyOCD call

```python
helpers.ConnectHelper.session_with_chosen_probe(
    target_override      = args.target,
    halt_on_connect      = False,
    resume_on_disconnect = True,
    **({"unique_id": args.probe} if args.probe else {})
)
```

---

## 4 — DeviceEdit — RTT/SWD communication type

Replace the unused **USB** radio button with **RTT/SWD**.

### groupBox1 "Communication"

| Radio | Value | Notes |
|---|---|---|
| Serial | `Serial:COM3,115200,8,N,1,1` | unchanged |
| TCP | `TCP:host,port` | implemented since |
| **RTT/SWD** | `RTT:<target>:<probeUID>:<host>:<port>:<cbAddr>` | new |

### panelRTT (shown when RTT/SWD selected)

Positioned in the same area as panelSerial (hidden when RTT is active).

| Control | Type | Purpose |
|---|---|---|
| `comboBoxRttTarget` | ComboBox (DropDown) | pyOCD target name; autocomplete from `pyocd list --targets` |
| `comboBoxRttProbe` | ComboBox (DropDownList) | Connected probe; items from `pyocd list --probes`; shows "Description [UID]" |
| `buttonRefreshProbes` | Button | Re-runs `pyocd list --probes` and repopulates the combo |

Target list is fetched once when the RTT/SWD radio is first selected (on `DeviceEdit.Load` if RTT is preselected).  
Probe list is fetched on panel activation and on Refresh click.

### Connection string built in OK handler

```csharp
string probeUid = ExtractProbeUid(comboBoxRttProbe.Text); // parses "Desc [UID]" → UID
string cbAddr = LookupSeggerRttAddress();                  // _SEGGER_RTT from the symbol file
comText = $"RTT:{target}:{probeUid}:{host}:{port}:{cbAddr}";
```

`IsValid()` requires `comboBoxRttTarget.Text` non-empty; probe may be empty (auto-select).

---

## Key constraints

- **Do not modify** `Protocol/`, `Device.cs`, or any plot code.
- `ComRtt` compiles against `.NET 8` (`net8.0-windows7.0`). No extra NuGet packages — `System.Net.Sockets` and `System.Diagnostics` are in the BCL.
- Match the defensive exception handling of `ComSerial`: catch all exceptions, log, never throw to caller.
- `ReadTimeout = 10 ms` on `NetworkStream` is intentional — mirrors ComSerial.
- `python` must be on `PATH` (needed for both the bridge subprocess and `python -m pyocd` queries). If not found, `ComRtt.Open()` returns `false`.

---

## How to test

1. Install pyOCD and the target pack:
   ```powershell
   pip install pyocd
   pyocd pack install stm32c051c8tx   # or your target
   ```
2. Connect a probe to the target board.
3. Verify probe is detected:
   ```powershell
   python -m pyocd list --probes
   ```
4. In eScope DeviceEdit, select **RTT/SWD**, choose target and probe, click OK.
5. eScope spawns the bridge — check console/log for `Listening on 127.0.0.1:9090`.
6. Device connects and data flows at RTT speed (≫ UART).

To test the bridge manually:
```powershell
cd eScope/Com
python bridge_TcpRtt.py --target stm32c051c8tx --probe 0672FF303230534343220043
# prints "Listening on 127.0.0.1:9090" when ready
```

---

## Reference files

- [Com/ICom.cs](../../eScope/Com/ICom.cs) — interface to implement
- [Com/ComSerial.cs](../../eScope/Com/ComSerial.cs) — style and error-handling model
- [Com/bridge_TcpRtt.py](../../eScope/Com/bridge_TcpRtt.py) — Python bridge
- [CLAUDE.md](../../CLAUDE.md) — project overview and build instructions
