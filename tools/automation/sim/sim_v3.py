"""eScope protocol V3 device simulator (TCP server), 1 ms sampling: a target to try eScope and its automation
API without hardware. eScope device: transport "TCP:127.0.0.1,47700", protocol V3, map file sim.map,
sampling time 1 ms (sim.escope of this folder).

Periodic reads (CMD_READ_PERIODIC), one-shot reads (CMD_READ_APERIODIC), writes (CMD_WRITE_APERIODIC),
CMD_CLEAR and CMD_DEINIT are served.

RAM 0x20000000..0x2000FFFF. Variables (see sim.map):
  motor_speed   0x20000010 int16   1000 * sin(2 pi 50 Hz t)
  motor_current 0x20000014 int16   motor_cfg * 10 + 5 * sin(2 pi 5 Hz t)
  motor_cfg     0x20000020 uint32  written by the PC (default 7)
  led_state     0x20000030 uint16  10 Hz square, duty 30 % (0 / 100)
Addresses: 32 bits by default. --addr-size 8: 64 bits addresses (ADDR64_FLAG commands), RAM moved to
0x180020000 (sim64.map), as a firmware DLL simulated on a 64 bits PC; eScope device "Addresses: 64 bits".
Errors (sent instead of the data frames until CMD_CLEAR): ERR_ADDR for an access outside the RAM,
ERR_ADDRSIZE for a command whose address size is not the simulator's.
Usage: python sim_v3.py [port] [--addr-size 4|8]
"""
import math, socket, struct, sys, threading, time

args = sys.argv[1:]
ADDR_SIZE = 4
if "--addr-size" in args:
    i = args.index("--addr-size")
    ADDR_SIZE = int(args[i + 1])
    del args[i:i + 2]
if ADDR_SIZE not in (4, 8):
    sys.exit("--addr-size: 4 or 8")
PORT = int(args[0]) if args else 47700
BASE, SIZE = (0x180020000 if ADDR_SIZE == 8 else 0x20000000), 0x10000
CMD_SIZE = 2 + ADDR_SIZE
CMD_DEINIT, CMD_CLEAR, CMD_RD_PER, CMD_RD_APER, CMD_WR_APER = 0x01, 0x02, 0x43, 0x44, 0x25
ADDR64_FLAG = 0x10
ERR_ADDR, ERR_ADDRSIZE = 0x88, 0x8A

ram = bytearray(SIZE)
struct.pack_into("<I", ram, 0x20, 7)


def update(t_ms):
    cfg = struct.unpack_from("<I", ram, 0x20)[0]
    struct.pack_into("<h", ram, 0x10, int(round(1000 * math.sin(2 * math.pi * 50 * t_ms / 1000))))
    cur = int(round(cfg * 10 + 5 * math.sin(2 * math.pi * 5 * t_ms / 1000)))
    struct.pack_into("<h", ram, 0x14, max(-32768, min(32767, cur)))
    struct.pack_into("<H", ram, 0x30, 100 if (t_ms % 100) < 30 else 0)


def valid(addr, size):  # ESCOPE_ADDR_IS_VALID of the simulated firmware: RAM only
    off = addr - BASE
    return 0 <= off and off + size <= SIZE


def read(addr, size):
    off = addr - BASE
    return bytes(ram[off:off + size])


def serve(conn):
    periodic, oneshot = [], []  # (addr, size)
    error = [None]  # (code, payload): error frame sent instead of the data frames until CMD_CLEAR
    lock = threading.Lock()
    alive = [True]

    def rx():
        buf = b""
        while alive[0]:
            try:
                chunk = conn.recv(4096)
            except OSError:
                break
            if not chunk:
                break
            if chunk[0] == CMD_CLEAR:  # sent alone, the rest of the send is ignored
                with lock:
                    periodic.clear()
                    oneshot.clear()
                    error[0] = None
                buf = b""
                continue
            buf += chunk
            with lock:
                while buf:
                    cmd = buf[0] & ~ADDR64_FLAG
                    if error[0]:  # in error: everything dropped until CMD_CLEAR
                        buf = b""
                    elif cmd in (CMD_RD_PER, CMD_RD_APER, CMD_WR_APER):
                        if (buf[0] & ADDR64_FLAG) != (ADDR64_FLAG if ADDR_SIZE == 8 else 0):
                            error[0] = (ERR_ADDRSIZE, bytes([ADDR_SIZE]))
                            continue
                        if len(buf) < CMD_SIZE:
                            break
                        size = buf[1]
                        addr = int.from_bytes(buf[2:CMD_SIZE], "little")
                        if not valid(addr, size):
                            error[0] = (ERR_ADDR, buf[2:CMD_SIZE] + bytes([size]))
                            continue
                        if cmd == CMD_WR_APER:
                            if len(buf) < CMD_SIZE + size:
                                break
                            off = addr - BASE
                            ram[off:off + size] = buf[CMD_SIZE:CMD_SIZE + size]
                            buf = buf[CMD_SIZE + size:]
                        else:
                            (periodic if cmd == CMD_RD_PER else oneshot).append((addr, size))
                            buf = buf[CMD_SIZE:]
                    elif cmd == CMD_DEINIT:
                        periodic.clear()
                        oneshot.clear()
                        buf = buf[1:]
                    else:  # NOP / unknown: dropped
                        buf = buf[1:]
        alive[0] = False

    threading.Thread(target=rx, daemon=True).start()
    flow, tick = 0, 0
    t0 = time.perf_counter()
    while alive[0]:
        due = int((time.perf_counter() - t0) * 1000)
        out = bytearray()
        while tick < due:
            update(tick)
            with lock:
                reads = periodic + oneshot
                oneshot.clear()
                err = error[0]
            if err:
                flow = (flow + 1) & 0xFF
                out += bytes([0x55, flow, len(err[1]), err[0]]) + err[1] + b"\xAA"
            elif reads:
                payload = b"".join(read(a, s) for a, s in reads)
                flow = (flow + 1) & 0xFF
                out += bytes([0x55, flow, len(payload), CMD_RD_PER]) + payload + b"\xAA"
            tick += 1
        if out:
            try:
                conn.sendall(out)
            except OSError:
                break
        time.sleep(0.005)
    conn.close()


def main():
    srv = socket.socket()
    srv.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
    srv.bind(("127.0.0.1", PORT))
    srv.listen(1)
    print(f"V3 simulator on 127.0.0.1:{PORT}, {ADDR_SIZE * 8} bits addresses, RAM 0x{BASE:X}", flush=True)
    while True:
        conn, _ = srv.accept()
        conn.setsockopt(socket.IPPROTO_TCP, socket.TCP_NODELAY, 1)
        print("client connected", flush=True)
        serve(conn)
        print("client disconnected", flush=True)


if __name__ == "__main__":
    main()
