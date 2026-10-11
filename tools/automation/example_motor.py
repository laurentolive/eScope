"""End-to-end example of the eScope automation API, on the V3 simulator (see README.md).

Adds channels, acquires, analyses, changes a firmware parameter on the target, measures again, annotates the
plot, saves a screenshot and a CSV export to out/, then puts the parameter back.
Usage: ESCOPE_TOKEN=... python example_motor.py
"""
import os
import sys

from escope_api import EScope, EScopeError

TRACE = "Motor"


def channel(scope, name, symbol, unit):
    channels = {c["name"] for c in scope.call("list_channels", trace=TRACE)}
    if name not in channels:
        # sim.map gives no size: 16-bit variables
        scope.call("add_channel", trace=TRACE, symbol=symbol, name=name, unit=unit, size_bits=16)


def measure(scope, label):
    summary = scope.call("acquire", trace=TRACE, duration_ms=1000, clear=True)
    for w in summary["warnings"]:
        print(f"  warning: {w}")
    speed, current = scope.call("analyze", trace=TRACE, channels=["speed", "current"])
    print(f"{label}: speed {speed['frequency_hz']} Hz, {speed['peak_to_peak']} rpm p-p | "
          f"current mean {current['mean']:.1f} mA")
    return current["mean"]


def write_cfg(scope, value):
    """motor_cfg written on the running target (the link opens on demand, no recording needed)."""
    return scope.call("write_variable", expression="motor_cfg", value=value, size_bits=32)


def main():
    scope = EScope()
    ws = scope.call("get_workspace")
    if "Sim" not in [d["name"] for d in ws["devices"]]:
        sys.exit("device 'Sim' not found: start eScope in tools/automation/sim (see README.md)")
    if TRACE not in [t["name"] for t in ws["traces"]]:
        scope.call("create_trace", name=TRACE)

    channel(scope, "speed", "motor_speed", "rpm")
    channel(scope, "current", "motor_current", "mA")

    before = measure(scope, "before")

    # firmware parameter changed on the running target (needs "Allow writing target memory")
    original = None
    try:
        original = int(scope.call("read_variable", expression="motor_cfg", size_bits=32)["value"])
        write_cfg(scope, original + 5)
        after = measure(scope, f"motor_cfg {original} -> {original + 5}")
        print(f"current change: {after - before:+.1f} mA")
    except EScopeError as e:
        if e.code != "WRITE_DISABLED":
            raise
        original = None
        print("write skipped: enable 'Allow writing target memory' in eScope > Tools > AI automation")

    # one period of the speed between two rising edges, annotated on the plot
    edges = scope.call("find_edges", trace=TRACE, channel="speed", direction="rising")["rising_ms"]
    if len(edges) >= 2:
        scope.call("add_delta", trace=TRACE, from_ms=edges[0], to_ms=edges[1], name="period")
        scope.call("add_annotation", trace=TRACE, channel="speed", time_ms=edges[0],
                   text=f"period {edges[1] - edges[0]:.1f} ms")
    scope.call("set_view", trace=TRACE, from_ms=0, to_ms=200)

    out = os.path.join(os.path.dirname(os.path.abspath(__file__)), "out")
    os.makedirs(out, exist_ok=True)
    with open(os.path.join(out, "motor.png"), "wb") as f:
        f.write(scope.screenshot(TRACE))
    csv = scope.call("export_csv", trace=TRACE, path=os.path.join(out, "motor.csv"), overwrite=True)
    print(f"saved {os.path.join(out, 'motor.png')} and {csv['path']} ({csv['rows']} rows)")

    if original is not None:
        write_cfg(scope, original)  # target left as found
        print(f"motor_cfg restored to {original}")


if __name__ == "__main__":
    main()
