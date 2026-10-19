using eScope.Protocol;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace eScope.Symbol.Gdb
{
    /// <summary>
    /// Minimal GDB remote serial protocol server (gdbserver) on the loopback: gives GDB
    /// access to the target memory through the device protocol one shot read / write,
    /// so that GDB evaluates expressions with pointers on live data.
    /// The target is never halted: registers are reported as zero, run requests stop at once.
    /// Memory requests are forwarded by Service(), called on the device Process() thread.
    /// </summary>
    public sealed class GdbRemoteBridge : IDisposable
    {
        class MemRequest
        {
            public bool Write;
            public UInt64 Address;
            public int Size; // bytes, 1..8
            public UInt64 Data;
            public readonly TaskCompletionSource<UInt64?> Done = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        const int MEM_ACCESS_MAX = 8;       // device one shot read returns an UInt64
        const int MEM_TIMEOUT_MS = 3000;
        readonly TcpListener listener;
        readonly ConcurrentQueue<MemRequest> requests = new();
        readonly CancellationTokenSource cts = new();
        TcpClient? client;
        bool noAck;

        public int Port { get; }
        // memory requests refused until set: the connection handshake reads around the
        // (fake) pc, never sent to the target
        public volatile bool Ready;
        // updated by Service(): device with one shot read support (link opened on demand)
        public volatile bool MemoryAvailable;

        public GdbRemoteBridge()
        {
            listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start(1);
            Port = ((IPEndPoint)listener.LocalEndpoint).Port;
            new Thread(AcceptLoop) { IsBackground = true, Name = "gdb-bridge" }.Start();
        }

        /// <summary>forward the pending memory requests to the device, on its Process() thread</summary>
        public void Service(IProtocol? protocol)
        {
            // not connected: the protocol opens the link on demand (fails at once if the device was just found absent)
            MemoryAvailable = (protocol != null) && protocol.SupportsReadMem;
            while (requests.TryDequeue(out var r))
            {
                if (!MemoryAvailable)
                {
                    r.Done.TrySetResult(null);
                }
                else if (r.Write)
                {
                    protocol!.WriteMem(r.Address, r.Size * 8, r.Data);
                    r.Done.TrySetResult(0);
                }
                else
                {
                    protocol!.ReadMem(r.Address, r.Size * 8, v => r.Done.TrySetResult(v), () => r.Done.TrySetResult(null));
                }
            }
        }

        UInt64? Access(bool write, UInt64 address, int size, UInt64 data = 0)
        {
            if (!Ready || !MemoryAvailable)
                return null;
            var r = new MemRequest { Write = write, Address = address, Size = size, Data = data };
            requests.Enqueue(r);
            return r.Done.Task.Wait(MEM_TIMEOUT_MS) ? r.Done.Task.Result : null;
        }

        void AcceptLoop()
        {
            while (!cts.IsCancellationRequested)
            {
                try
                {
                    TcpClient c = listener.AcceptTcpClient();
                    client?.Close(); // one GDB at a time
                    client = c;
                    c.NoDelay = true;
                    noAck = false;
                    Serve(c);
                }
                catch (Exception ex) when (ex is SocketException || ex is IOException || ex is ObjectDisposedException)
                {
                    if (cts.IsCancellationRequested) return;
                }
            }
        }

        void Serve(TcpClient c)
        {
            NetworkStream stream = c.GetStream();
            var packet = new StringBuilder();
            int state = 0; // 0: wait '$', 1: payload, 2/3: checksum chars
            int checksum = 0;
            string sum = string.Empty;
            var buf = new byte[4096];
            int n;
            while ((n = stream.Read(buf, 0, buf.Length)) > 0)
            {
                for (int k = 0; k < n; k++)
                {
                    char ch = (char)buf[k];
                    switch (state)
                    {
                        case 0:
                            if (ch == '$') { packet.Clear(); checksum = 0; state = 1; }
                            else if (ch == '\x03') Send(stream, "S05"); // interrupt: already stopped
                            break; // '+' '-' acks ignored
                        case 1:
                            if (ch == '#') { state = 2; sum = string.Empty; }
                            else { packet.Append(ch); checksum += buf[k]; }
                            break;
                        default:
                            sum += ch;
                            if (++state == 4)
                            {
                                state = 0;
                                bool valid = Convert.ToInt32(sum, 16) == (checksum & 0xFF);
                                if (!noAck)
                                    stream.Write(new[] { (byte)(valid ? '+' : '-') });
                                if (valid)
                                {
                                    string? reply = Handle(packet.ToString());
                                    if (reply == null) return; // detach / kill
                                    Send(stream, reply);
                                }
                            }
                            break;
                    }
                }
            }
        }

        void Send(NetworkStream stream, string payload)
        {
            int sum = 0;
            foreach (char ch in payload) sum += ch;
            byte[] b = Encoding.ASCII.GetBytes($"${payload}#{sum & 0xFF:x2}");
            stream.Write(b, 0, b.Length);
        }

        // reply to one packet, null closes the connection
        string? Handle(string p)
        {
            if (p.StartsWith("qSupported")) return $"PacketSize={4096:x};QStartNoAckMode+";
            if (p == "QStartNoAckMode") { noAck = true; return "OK"; }
            if (p == "?") return "S05";
            if (p == "g") return "00000000";  // shorter than the register set: the rest is asked with 'p'
            if (p.StartsWith("p")) return "00000000";
            if (p.StartsWith("m")) return ReadMemory(p.Substring(1));
            if (p.StartsWith("M")) return WriteMemory(p.Substring(1));
            if (p == "qAttached") return "1";
            if (p.StartsWith("H")) return "OK";
            if (p.StartsWith("qSymbol")) return "OK";
            if (p == "D" || p.StartsWith("D;")) return null;
            if (p == "k") return null;
            if (p == "c" || p == "s" || p.StartsWith("C") || p.StartsWith("S") || p.StartsWith("vCont;"))
                return "S05"; // the target is not controlled: stopped again at once
            return string.Empty; // not supported (X, Z, qXfer, vCont?, ...)
        }

        // m addr,length : one device access at most, GDB asks again for the rest
        string ReadMemory(string args)
        {
            if (!TryParseRange(args, out UInt64 address, out int length))
                return "E01";
            int size = Math.Min(length, MEM_ACCESS_MAX);
            UInt64? value = Access(false, address, size);
            if (value == null)
                return "E01";
            var sb = new StringBuilder(size * 2);
            for (int i = 0; i < size; i++)
                sb.Append(((byte)(value.Value >> (8 * i))).ToString("x2"));
            return sb.ToString();
        }

        // M addr,length:XX... : written by chunks, the device does not acknowledge writes
        string WriteMemory(string args)
        {
            int colon = args.IndexOf(':');
            if (colon < 0 || !TryParseRange(args.Substring(0, colon), out UInt64 address, out int length))
                return "E01";
            string hex = args.Substring(colon + 1);
            if (hex.Length < length * 2)
                return "E01";
            for (int offset = 0; offset < length; offset += MEM_ACCESS_MAX)
            {
                int size = Math.Min(MEM_ACCESS_MAX, length - offset);
                UInt64 data = 0;
                for (int i = 0; i < size; i++)
                    data |= (UInt64)Convert.ToByte(hex.Substring((offset + i) * 2, 2), 16) << (8 * i);
                if (Access(true, address + (UInt64)offset, size, data) == null)
                    return "E01";
            }
            return "OK";
        }

        static bool TryParseRange(string s, out UInt64 address, out int length)
        {
            address = 0; length = 0;
            string[] parts = s.Split(',');
            if (parts.Length != 2
                || !UInt64.TryParse(parts[0], System.Globalization.NumberStyles.HexNumber, null, out address)
                || !int.TryParse(parts[1], System.Globalization.NumberStyles.HexNumber, null, out length)
                || (length <= 0))
                return false;
            return true;
        }

        public void Dispose()
        {
            cts.Cancel();
            try { listener.Stop(); } catch { }
            try { client?.Close(); } catch { }
            while (requests.TryDequeue(out var r))
                r.Done.TrySetResult(null);
        }
    }
}
