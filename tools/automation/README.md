# eScope automation: AI agents (MCP) and scripts (REST)

**English** | [Français](README.fr.md)

eScope can be driven by an AI agent (Claude Code, Cursor, VS Code Copilot… through the
[Model Context Protocol](https://modelcontextprotocol.io/)) or by a script (plain HTTP + JSON):
acquisitions, channels (including computed ones), data, analysis, annotations, exports, and reads / writes of
the target memory. The agent drives the eScope window you have open: you see what it does, and you can undo
its changes with Ctrl+Z.

## Enable the server

**Tools > AI automation** in eScope:

- *Enable the automation server*: off by default. It listens on `http://localhost:<port>` only (default
  port 47653) and refuses requests from other machines and from web pages.
- *Token*: every request needs `Authorization: Bearer <token>`. Copy it, or regenerate it to revoke the
  configured clients.
- *Allow writing target memory*: off by default. Without it, `write_variable` / `write_memory` are refused
  (`WRITE_DISABLED`). With it, **any variable or address of the running target can be written**: the AI can
  change the behaviour of the equipment driven by the firmware.

The settings are per user (`%LocalAppData%\eScope\automation.json`), not in the workspace. While the server
runs, **AI** is shown at the right of the toolbar, with the last tool called.

## Connect an AI agent

The window gives the command for Claude Code:

```
claude mcp add --transport http escope http://localhost:47653/mcp --header "Authorization: Bearer <token>"
```

Other MCP clients take the same URL (Streamable HTTP) and header, for example:

```jsonc
// Cursor: .cursor/mcp.json
{ "mcpServers": { "escope": { "url": "http://localhost:47653/mcp",
                               "headers": { "Authorization": "Bearer <token>" } } } }

// VS Code: .vscode/mcp.json
{ "servers": { "escope": { "type": "http", "url": "http://localhost:47653/mcp",
                           "headers": { "Authorization": "Bearer <token>" } } } }
```

Then ask, for example: *"Record 2 s of motor_speed and motor_current, tell me the speed frequency and whether
the current oscillates, and annotate one period on the plot."*

## Scripts (REST)

Same tools, same arguments: `POST /api/<tool>` with a JSON object of arguments (send at least `{}`),
`GET /api/tools` for the list with the JSON schema of the arguments. Errors:
`{"error": {"code": "...", "message": "..."}}` with an HTTP status.

```bash
curl -H "Authorization: Bearer $TOKEN" -d '{"duration_ms": 2000, "clear": true}' http://localhost:47653/api/acquire
```

[`escope_api.py`](escope_api.py) is a small Python client (standard library only), usable as a library or
from the command line:

```bash
set ESCOPE_TOKEN=...
python escope_api.py tools                       # tools and arguments
python escope_api.py acquire "{\"duration_ms\": 2000}"
python escope_api.py screenshot > plot.png
```

Use `localhost`, not `127.0.0.1`: Windows (HTTP.sys) only accepts the host name the server registered.

## Tools

| Domain | Tools |
|---|---|
| Workspace | `get_workspace`, `list_traces`, `create_trace`, `select_trace`, `rename_trace`, `save_workspace`, `undo`, `redo` |
| Devices, symbols | `list_devices`, `device_status`, `search_symbols`, `resolve_symbol` |
| Acquisition | `acquire`, `start_recording`, `stop_recording`, `get_recording_state` |
| Channels | `list_channels`, `add_channel`, `add_computed_channel`, `configure_channel`, `remove_channel` |
| Data | `get_channel_data`, `get_value_at`, `delete_data`, `clear_data` |
| Analysis | `analyze` (statistics, frequency, duty cycle, FFT), `find_edges` |
| Plot | `list_annotations`, `add_marker`, `add_delta`, `add_annotation`, `add_data_marker`, `remove_annotation`, `set_view`, `zoom_fit`, `set_selection` |
| Target | `read_variable`, `read_variables`, `write_variable`, `read_memory`, `write_memory` |
| Files | `screenshot`, `export_csv`, `export_png`, `save_trace`, `load_trace` |

Good to know:

- Times are in **ms** on the trace X axis; `value` is the plotted (physical) value, `raw` the target value.
- The recording applies to the **current trace**, which cannot change while recording (`BUSY`).
- The target is read / written **with or without a recording**: the link to the device opens on demand and
  closes 3 s after the last access outside a recording (`NOT_CONNECTED` when the target does not answer).
- With GDB symbols (ELF), `add_channel` deduces the size and format from the C type, and `read_variable`
  accepts any C expression (pointers followed on the target).
- Model changes are undoable (`AI: ...` in Edit > Undo); `undo` uses the same history as the user.
- `get_channel_data` returns every sample of the range: for long recordings, restrict the range or use
  `export_csv` / `save_trace`.

Full specification (design document, French): [.claude/docs/automation.md](../../.claude/docs/automation.md).

## Try it without hardware: the V3 simulator

[`sim/sim_v3.py`](sim/sim_v3.py) is a device speaking the eScope protocol V3 over TCP (1 ms sampling): a
50 Hz sine (`motor_speed`), a 10 Hz square (`led_state`) and a current (`motor_current`) that depends on
a writable parameter (`motor_cfg`).

1. `python tools/automation/sim/sim_v3.py` (listens on 127.0.0.1:47700).
2. Start eScope **from the `tools/automation/sim` folder** (its `eScope.json` declares the `Sim` device and a
   `Motor` trace), e.g. `cd tools\automation\sim` then run `eScope.exe` from the build output.
3. Enable the automation server (and *Allow writing target memory* for the write part).
4. `python tools/automation/example_motor.py`: adds channels, acquires, analyses, raises `motor_cfg` and
   measures the current again, annotates one period, saves `out/motor.png` and `out/motor.csv`, then puts
   `motor_cfg` back.
