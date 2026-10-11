"""
bridge_TcpRtt.py — RTT↔TCP bridge for eScope

Connects to any Cortex-M target via SWD (pyOCD), exposes RTT channel 0 as a
raw bidirectional TCP stream on localhost:<port>.  eScope's ComRtt connects to
that socket and uses it as a byte pipe — identical to a serial port from the
protocol layer's perspective.

If pylink-square is installed and the probe is a real SEGGER J-Link, the RTT
engine defaults to the J-Link's own native background RTT polling (--backend
jlink) instead of driving the ring buffer by hand over generic pyOCD SWD
reads — the J-Link firmware polls and buffers RTT data on-probe on its own,
which is why this is dramatically lower-latency than the pyOCD fallback.

Usage:
    python bridge_TcpRtt.py --target <pyocd-target-name> \\
                            [--probe <unique-id>] \\
                            [--port 9090] \\
                            [--freq 4000000] \\
                            [--backend {auto,jlink,pyocd}] \\
                            [--jlink-device <segger-device-name>] \\
                            [--jlink-serial <serial>] \\
                            [--sram-start 0x20000000] \\
                            [--sram-size 0x10000]

Examples:
    python bridge_TcpRtt.py --target stm32c051c8tx
    python bridge_TcpRtt.py --target stm32f103rb --probe 0672FF303230534343220043
    python bridge_TcpRtt.py --target rp2040 --sram-start 0x20000000 --sram-size 0x40000
    python bridge_TcpRtt.py --target r7fa4e10d --backend jlink --jlink-device R7FA4E10D

Setup:
    pip install pyocd
    pyocd pack install <target>   # first time only
    pip install pylink-square     # optional, for the fast J-Link native backend
"""

import argparse
import contextlib
import os
import select
import signal
import socket
import struct
import threading
import time

import pyocd.core.helpers as helpers

try:
    import pylink
    HAVE_PYLINK = True
except ImportError:
    HAVE_PYLINK = False

# ── RTT memory layout ────────────────────────────────────────────────────────
RTT_SIGNATURE  = b"SEGGER RTT\x00\x00\x00\x00\x00\x00"
DESC_FMT       = "<6I"        # sName, pBuffer, SizeOfBuffer, WrOff, RdOff, Flags
DESC_SIZE      = struct.calcsize(DESC_FMT)   # 24 bytes
CB_HEADER_FMT  = "<2I"        # MaxNumUpBuffers, MaxNumDownBuffers (follow acID[16])
CB_ID_SIZE     = 16
CB_HEADER_SIZE = CB_ID_SIZE + struct.calcsize(CB_HEADER_FMT)  # 24 bytes
OFF_PBUFFER    = 4            # pBuffer/SizeOfBuffer: fixed once RTT is initialised
STATIC_FMT     = "<2I"
OFF_WROFF      = 12           # WrOff/RdOff: the only fields that change per poll
OFF_RDOFF      = 16
WR_RD_FMT      = "<2I"
WR_RD_SIZE     = struct.calcsize(WR_RD_FMT)  # 8 bytes

# ── RTT helpers ──────────────────────────────────────────────────────────────

def locate_rtt_cb(read8, sram_start, sram_size, cb_addr_override):
    """Find the RTT control block address.

    If cb_addr_override is given (from a previous run's logged address), skip
    the full SRAM scan and just verify the signature is still there — much
    faster on every subsequent startup. Otherwise scan sram_start..+sram_size
    for the signature, as before.

    read8(addr, num_bytes) is the backend's raw memory-read call (pyOCD's
    target.read_memory_block8 or pylink's jlink.memory_read8 — same shape).
    """
    if cb_addr_override is not None:
        print(f"Using RTT control block @ 0x{cb_addr_override:08X} (skipping scan)…", flush=True)
        sig = bytes(read8(cb_addr_override, len(RTT_SIGNATURE)))
        if sig != RTT_SIGNATURE:
            raise RuntimeError(
                f"No RTT signature at --cb-addr 0x{cb_addr_override:08X} "
                f"(stale address? firmware relinked? omit --cb-addr to rescan)")
        return cb_addr_override

    print("Scanning SRAM for RTT control block…", flush=True)
    mem = bytes(read8(sram_start, sram_size))
    idx = mem.find(RTT_SIGNATURE)
    if idx < 0:
        raise RuntimeError("RTT control block not found — is the firmware running?")
    cb_addr = sram_start + idx
    print(f"  RTT CB @ 0x{cb_addr:08X}", flush=True)
    return cb_addr


def read_buffer_counts(target, cb_addr):
    """Read MaxNumUpBuffers / MaxNumDownBuffers from the control block header."""
    raw = bytes(target.read_memory_block8(cb_addr + CB_ID_SIZE, struct.calcsize(CB_HEADER_FMT)))
    return struct.unpack(CB_HEADER_FMT, raw)


def read_static_fields(target, desc_addr):
    """pBuffer/SizeOfBuffer never change after RTT init — read them once at
    startup instead of every poll, to halve the per-poll SWD traffic."""
    raw = bytes(target.read_memory_block8(desc_addr + OFF_PBUFFER, struct.calcsize(STATIC_FMT)))
    return struct.unpack(STATIC_FMT, raw)  # pBuffer, SizeOfBuffer


def _read_offsets(target, desc_addr):
    raw = bytes(target.read_memory_block8(desc_addr + OFF_WROFF, WR_RD_SIZE))
    return struct.unpack(WR_RD_FMT, raw)  # WrOff, RdOff


def rtt_read_up(target, desc_addr, pBuf, size):
    """Drain the UP ring buffer (Target→Host). Returns bytes."""
    wr, rd = _read_offsets(target, desc_addr)
    avail = (wr - rd + size) % size
    if avail == 0:
        return b""
    if rd + avail <= size:
        data = bytes(target.read_memory_block8(pBuf + rd, avail))
    else:
        tail  = size - rd
        data  = bytes(target.read_memory_block8(pBuf + rd, tail))
        data += bytes(target.read_memory_block8(pBuf, avail - tail))
    target.write32(desc_addr + OFF_RDOFF, (rd + avail) % size)
    return data


def rtt_write_down(target, desc_addr, data, pBuf, size):
    """Write bytes into the DOWN ring buffer (Host→Target). Returns bytes written."""
    wr, rd = _read_offsets(target, desc_addr)
    free     = (rd - 1 - wr + size) % size
    to_write = min(len(data), free)
    if to_write == 0:
        return 0
    chunk = data[:to_write]
    if wr + to_write <= size:
        target.write_memory_block8(pBuf + wr, list(chunk))
    else:
        tail = size - wr
        target.write_memory_block8(pBuf + wr, list(chunk[:tail]))
        target.write_memory_block8(pBuf,       list(chunk[tail:]))
    target.write32(desc_addr + OFF_WROFF, (wr + to_write) % size)
    return to_write


# ── J-Link native RTT backend ────────────────────────────────────────────────
# The J-Link probe's own firmware polls the RTT ring buffer in the background
# (see pylink's rtt_start() docstring) and buffers drained bytes on-probe, so
# the host just drains a queue instead of driving every single poll itself
# over the debug link. This is why e.g. e2 studio / J-Link RTT Viewer don't
# show the lag the generic pyOCD memory-polling backend below has.

def jlink_connect(args):
    """Connect via the J-Link's native RTT engine. Returns (read_up, write_down, cleanup)."""
    if not HAVE_PYLINK:
        raise RuntimeError("pylink-square not installed (pip install pylink-square)")

    device = args.jlink_device or args.target
    print(f"Connecting to {device} via J-Link (native RTT)…", flush=True)

    jlink = pylink.JLink()
    # jlink.open() requires an int-coercible serial; --probe may be a non-numeric
    # CMSIS-DAP-style UID meant for the pyocd backend, so only reuse it if it fits.
    serial = args.jlink_serial or (args.probe if args.probe and args.probe.isdigit() else None)
    jlink.open(serial_no=serial)
    jlink.set_tif(pylink.enums.JLinkInterfaces.SWD)
    speed_khz = min(12000, max(5, args.freq // 1000))
    jlink.connect(device, speed=speed_khz)

    # rtt_start()'s own blind auto-search only scans a limited default window and
    # can miss the control block entirely. We already know how to find it (same
    # signature scan the pyocd backend uses) — do it ourselves via J-Link's direct
    # memory-read API and hand rtt_start() the exact address instead of guessing.
    try:
        cb_addr = locate_rtt_cb(jlink.memory_read8, args.sram_start, args.sram_size, args.cb_addr)
    except Exception:
        try: jlink.close()
        except Exception: pass
        raise

    jlink.rtt_start(block_address=cb_addr)

    deadline = time.time() + 5.0
    status = jlink.rtt_get_status()
    while status.NumUpBuffers < 1 or status.NumDownBuffers < 1:
        if time.time() > deadline:
            try: jlink.rtt_stop()
            except Exception: pass
            try: jlink.close()
            except Exception: pass
            raise RuntimeError("J-Link could not find the RTT control block "
                                "(is SEGGER_RTT_Init() running on target?)")
        time.sleep(0.05)
        status = jlink.rtt_get_status()

    print(f"  RTT ready — {status.NumUpBuffers} up / {status.NumDownBuffers} down buffer(s) "
          f"@ {speed_khz/1000:.1f} MHz (J-Link native)", flush=True)

    lock = threading.Lock()

    def read_up():
        with lock:
            return bytes(jlink.rtt_read(0, 4096))

    def write_down(data):
        with lock:
            jlink.rtt_write(0, list(data))

    def cleanup():
        try: jlink.rtt_stop()
        except Exception: pass
        try: jlink.close()
        except Exception: pass

    return read_up, write_down, cleanup


# ── pyOCD generic SWD-polling backend ────────────────────────────────────────
# Fallback for probes without native RTT support (or when pylink isn't
# installed): drives the ring buffer by hand, reading/writing it as plain
# memory over the debug link. Works with any pyOCD-supported probe, but pays
# a host round-trip on every poll.

def pyocd_connect(args, exit_stack):
    """Connect via generic pyOCD SWD memory polling. Returns (read_up, write_down)."""
    print(f"Connecting to {args.target} via pyOCD…", flush=True)

    connect_kw = dict(
        target_override      = args.target,
        halt_on_connect      = False,
        resume_on_disconnect = True,
        frequency             = args.freq,
    )
    if args.probe:
        connect_kw["unique_id"] = args.probe

    session = helpers.ConnectHelper.session_with_chosen_probe(**connect_kw)
    exit_stack.enter_context(session)
    target = session.target
    target.resume()
    lock = threading.Lock()

    cb_addr = locate_rtt_cb(target.read_memory_block8, args.sram_start, args.sram_size, args.cb_addr)
    max_up, max_down = read_buffer_counts(target, cb_addr)
    if max_up < 1 or max_down < 1:
        raise RuntimeError(f"RTT control block reports MaxNumUpBuffers={max_up} "
                            f"MaxNumDownBuffers={max_down} — channel 0 not available")
    # aUp[MaxNumUpBuffers] immediately follows the header, aDown[MaxNumDownBuffers] follows aUp
    up_desc   = cb_addr + CB_HEADER_SIZE
    down_desc = cb_addr + CB_HEADER_SIZE + max_up * DESC_SIZE
    up_pbuf, up_size     = read_static_fields(target, up_desc)
    down_pbuf, down_size = read_static_fields(target, down_desc)
    print(f"  UP @ 0x{up_desc:08X} (buf 0x{up_pbuf:08X}, {up_size}B)  "
          f"DOWN @ 0x{down_desc:08X} (buf 0x{down_pbuf:08X}, {down_size}B)  "
          f"SWD @ {args.freq/1e6:.1f} MHz (pyOCD polling)", flush=True)

    def read_up():
        with lock:
            return rtt_read_up(target, up_desc, up_pbuf, up_size)

    def write_down(data):
        with lock:
            rtt_write_down(target, down_desc, data, down_pbuf, down_size)

    return read_up, write_down


# ── TCP server ────────────────────────────────────────────────────────────────
# Shared by both backends: bridges one TCP client to RTT via the read_up()/
# write_down() callables the chosen backend provides.

def serve_tcp(args, stop, read_up, write_down):
    server_sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    server_sock.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
    server_sock.bind((args.host, args.port))
    server_sock.listen(1)
    server_sock.settimeout(1.0)
    # This line is the ready signal read by ComRtt.Open()
    print(f"Listening on {args.host}:{args.port}", flush=True)

    try:
        while not stop.is_set():
            try:
                conn, addr = server_sock.accept()
            except socket.timeout:
                continue

            print(f"Client connected: {addr}", flush=True)
            conn.settimeout(0.01)

            # UP → TCP: reader thread forwards RTT bytes to the socket
            def _up_reader(c=conn):
                try:
                    while not stop.is_set():
                        data = read_up()
                        if data:
                            c.sendall(data)
                        else:
                            time.sleep(0.0005)
                except Exception as ex:
                    print(f"up-reader error: {ex}", flush=True)

            reader = threading.Thread(target=_up_reader, daemon=True, name="up-reader")
            reader.start()

            # TCP → DOWN: main loop receives from socket and writes to RTT
            try:
                while not stop.is_set():
                    ready, _, _ = select.select([conn], [], [], 0.01)
                    if ready:
                        data = conn.recv(256)
                        if not data:
                            break
                        write_down(data)
            except Exception as ex:
                print(f"down-writer error: {ex}", flush=True)

            print("Client disconnected", flush=True)
            try:
                conn.close()
            except Exception:
                pass
            reader.join(timeout=1.0)
    except KeyboardInterrupt:
        # Fallback in case Ctrl+C slips past the signal handler while
        # blocked in a C call (e.g. mid SWD transaction).
        stop.set()
    finally:
        server_sock.close()


# ── Main ─────────────────────────────────────────────────────────────────────

def main():
    parser = argparse.ArgumentParser(description="RTT<->TCP bridge for eScope")
    parser.add_argument("--target",     required=True,
                        help="pyOCD target name (e.g. stm32c051c8tx). "
                             "Run 'pyocd list --targets' for the full list.")
    parser.add_argument("--probe",      default=None,
                        help="Probe unique ID (leave blank to auto-select). "
                             "Run 'pyocd list --probes' to find IDs.")
    parser.add_argument("--host",       default="127.0.0.1",
                        help="IP address to bind the TCP server to (default 127.0.0.1)")
    parser.add_argument("--port",       type=int, default=9090,
                        help="TCP port to listen on (default 9090)")
    parser.add_argument("--sram-start", type=lambda x: int(x, 0), default=0x20000000,
                        help="SRAM start address for RTT scan (default 0x20000000)")
    parser.add_argument("--sram-size",  type=lambda x: int(x, 0), default=0x10000,
                        help="SRAM size in bytes for RTT scan (default 0x10000 = 64 KB)")
    parser.add_argument("--cb-addr",    type=lambda x: int(x, 0), default=None,
                        help="Known RTT control block address (from a previous run's "
                             "'RTT CB @ 0x...' log line). Skips the SRAM scan entirely and "
                             "just verifies the signature is still there — much faster "
                             "startup. Omit to scan --sram-start.. +--sram-size as before.")
    parser.add_argument("--freq",       type=lambda x: int(x, 0), default=4_000_000,
                        help="SWD/JTAG clock in Hz (default 4000000 = 4 MHz; "
                             "pyOCD itself defaults to a conservative 1 MHz)")
    parser.add_argument("--backend",    choices=["auto", "jlink", "pyocd"], default="auto",
                        help="RTT engine: 'jlink' uses the J-Link probe's native background "
                             "RTT polling (fast, requires a real SEGGER J-Link + pylink-square); "
                             "'pyocd' drives the ring buffer by hand over generic SWD memory "
                             "reads (works with any pyOCD probe, slower); 'auto' (default) "
                             "tries jlink first and falls back to pyocd")
    parser.add_argument("--jlink-device", default=None,
                        help="SEGGER device name for the jlink backend, if it differs from "
                             "--target (J-Link's device DB uses SEGGER's own naming). "
                             "Defaults to --target.")
    parser.add_argument("--jlink-serial", default=None,
                        help="J-Link probe serial number (leave blank to auto-select); "
                             "falls back to --probe if not given")
    args = parser.parse_args()

    stop = threading.Event()

    def _shutdown(*_):
        print("\nStopping…", flush=True)
        stop.set()
        return 1  # tell Windows the event was handled

    signal.signal(signal.SIGINT,  _shutdown)
    signal.signal(signal.SIGTERM, _shutdown)

    if os.name == "nt":
        # Ctrl+C/SIGTERM don't fire when the console window itself is closed —
        # Windows sends CTRL_CLOSE_EVENT instead, which the signal module never
        # sees. Catch it directly so the probe/socket still get released instead
        # of leaking an orphaned process that keeps holding the port and probe.
        import ctypes
        CTRL_CLOSE_EVENT    = 2
        CTRL_LOGOFF_EVENT   = 5
        CTRL_SHUTDOWN_EVENT = 6
        HANDLER_ROUTINE = ctypes.WINFUNCTYPE(ctypes.c_int, ctypes.c_uint)

        def _console_handler(ctrl_type):
            if ctrl_type in (CTRL_CLOSE_EVENT, CTRL_LOGOFF_EVENT, CTRL_SHUTDOWN_EVENT):
                return _shutdown()
            return 0

        # Must keep a reference — ctypes doesn't, and a GC'd callback crashes the process
        main._console_handler_ref = HANDLER_ROUTINE(_console_handler)
        ctypes.windll.kernel32.SetConsoleCtrlHandler(main._console_handler_ref, True)

    backend = args.backend
    if backend == "auto":
        backend = "jlink" if HAVE_PYLINK else "pyocd"

    with contextlib.ExitStack() as stack:
        read_up = write_down = None

        if backend == "jlink":
            try:
                read_up, write_down, jlink_cleanup = jlink_connect(args)
                stack.callback(jlink_cleanup)
            except Exception as ex:
                if args.backend != "auto":
                    raise
                print(f"J-Link native RTT unavailable ({ex}); falling back to pyOCD", flush=True)
                backend = "pyocd"

        if backend == "pyocd":
            read_up, write_down = pyocd_connect(args, stack)

        serve_tcp(args, stop, read_up, write_down)

    print("Bridge stopped.", flush=True)


if __name__ == "__main__":
    main()
