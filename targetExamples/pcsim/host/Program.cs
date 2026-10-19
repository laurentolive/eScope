using System.Diagnostics;
using System.Runtime.InteropServices;
using PcSimHost;

// Minimal firmware simulator: loads the firmware DLL, runs its 1 ms tick in simulated time, and relays its
// eScope UART to TCP (SimUart). The firmware code is the target code; only the board (simHw.c) is simulated.
//
//   PcSimHost [firmware.dll] [--port 47700] [--speed 1]
//   --speed: simulated time / real time (2 = twice as fast); eScope's time axis is the simulated time.

string dllPath = Path.Combine(AppContext.BaseDirectory, "firmware.dll");
int port = 47700;
double speed = 1.0;
for (int i = 0; i < args.Length; i++)
{
    if (args[i] == "--port") port = int.Parse(args[++i]);
    else if (args[i] == "--speed") speed = double.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture);
    else dllPath = Path.GetFullPath(args[i]);
}

var fw = new Firmware(dllPath);
using var uart = new SimUart(port);
const byte SIM_UART_ESCOPE = 0;

// keep the delegate alive: the firmware holds its pointer
var txBuf = new byte[ushort.MaxValue];
Firmware.UartTx onUartTx = (id, data, len) =>
{
    if (id != SIM_UART_ESCOPE) return;
    Marshal.Copy(data, txBuf, 0, len);
    uart.Transmit(txBuf.AsSpan(0, len));
};
fw.Init(onUartTx);
Console.WriteLine($"firmware {dllPath} loaded at 0x{fw.Base:X}, eScope UART on TCP 127.0.0.1:{uart.Port}, speed x{speed}");
Console.WriteLine("Ctrl+C to stop");

bool stop = false;
Console.CancelKeyPress += (_, e) => { e.Cancel = true; stop = true; };

// scheduler: one tick per simulated ms; the UART reception is delivered before the tick, in the same context
var clock = Stopwatch.StartNew();
ulong simMs = 0;
while (!stop)
{
    double due = clock.Elapsed.TotalMilliseconds * speed;
    while (simMs < due && !stop)
    {
        uart.Deliver(b => fw.UartRx(SIM_UART_ESCOPE, b));
        fw.Tick();
        simMs++;
    }
    Thread.Sleep(1);
}
GC.KeepAlive(onUartTx);

/// <summary>firmware DLL entry points (simHw.h)</summary>
sealed class Firmware
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void UartTx(byte id, IntPtr data, ushort len);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate void InitFn(UartTx pfUartTx);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate void TickFn();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate void UartRxFn(byte id, byte b);

    readonly InitFn init;
    readonly TickFn tick;
    readonly UartRxFn uartRx;
    public IntPtr Base { get; }

    public Firmware(string path)
    {
        Base = NativeLibrary.Load(path);
        init = Marshal.GetDelegateForFunctionPointer<InitFn>(NativeLibrary.GetExport(Base, "sim_Init"));
        tick = Marshal.GetDelegateForFunctionPointer<TickFn>(NativeLibrary.GetExport(Base, "sim_Tick"));
        uartRx = Marshal.GetDelegateForFunctionPointer<UartRxFn>(NativeLibrary.GetExport(Base, "sim_UartRx"));
    }

    public void Init(UartTx pfUartTx) => init(pfUartTx);
    public void Tick() => tick();
    public void UartRx(byte id, byte b) => uartRx(id, b);
}
