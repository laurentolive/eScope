using eScope.Com;
using System.Buffers;
using System.Collections.Concurrent;
using System.IO.Pipelines;
using System.Linq;
using System.Timers;

namespace eScope.Protocol
{
    public class ProtocolV1 : IProtocol
    {
        enum State_t { stopped, capturing }
        private State_t State = State_t.stopped;
        ICom? hCom;
        ComOpener? opener;
        bool bRun = false;
        byte[] Config = Array.Empty<byte>();
        List<ReadMemData> ReadMemList;
        private System.Timers.Timer timeout;
        bool bSendConfig = false;
        uint readMemToken = 1;

        const int ADDRESS_SIZE = 4;
        readonly byte[] SyncBytes = new byte[] { 0x55 };
        const uint HEADER_SIZE = 4U;
        const uint FOOTER_SIZE = 1U;
        const byte FOOTER_BYTE = 0xAA;
        const uint CMD_READ_PERIODIC = 0x03 | 0x40;

        Pipe? pipe;
        ConcurrentQueue<(byte[] data, int lost)>? frameQueue;
        readonly FlowCounter flow = new("ProtocolV1");
        CancellationTokenSource? cts;
        public event Action<int, IReadOnlyDictionary<uint, UInt64>>? FrameComplete;
        public event Action? Timeout;
        volatile bool bTimeoutNotify = false; // set by the timer thread, notified from Process()

        public ProtocolV1(string com)
        {
            hCom = ICom.Get(com);
            if (hCom != null) opener = new ComOpener(hCom);
            ReadMemList = new List<ReadMemData>();
            timeout = new System.Timers.Timer(1000);
            timeout.Elapsed += TimeoutHandler;
            timeout.AutoReset = true;
        }

        public void UpdateConfig()
        {
            // Send only addresses; target infers channel count and data size from build-time config
            Config = new byte[ReadMemList.Count * ADDRESS_SIZE];
            int i = 0;
            foreach (ReadMemData rmd in ReadMemList)
            {
                Config[i * ADDRESS_SIZE + 0] = (byte)((rmd.address >>  0) & 0xFF);
                Config[i * ADDRESS_SIZE + 1] = (byte)((rmd.address >>  8) & 0xFF);
                Config[i * ADDRESS_SIZE + 2] = (byte)((rmd.address >> 16) & 0xFF);
                Config[i * ADDRESS_SIZE + 3] = (byte)((rmd.address >> 24) & 0xFF);
                i++;
            }
            bSendConfig = true;
        }

        public void Start() { bRun = true; opener?.ResetRetryDelay(); }
        public void Stop() { bRun = false; }

        public void Dispose()
        {
            opener?.Dispose();
            timeout.Dispose();
        }

        void TimeoutHandler(object? sender, ElapsedEventArgs e)
        {
            Console.WriteLine("ProtocolV1 timeout");
            bSendConfig = true;
            bTimeoutNotify = true;
            timeout.Stop();
            timeout.Start();
        }

        async Task ReadCom(ICom hCom, PipeWriter writer, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                byte[] buffer = await hCom.ReadAsync(token);
                if (buffer.Length > 0)
                {
                    var mem = writer.GetMemory(buffer.Length);
                    buffer.AsSpan().CopyTo(mem.Span);
                    writer.Advance(buffer.Length);
                    await writer.FlushAsync(token);
                }
            }
            await writer.CompleteAsync();
        }

        async Task FetchFrame(PipeReader reader, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                var result = await reader.ReadAsync(token);
                var buffer = result.Buffer;

                while (TryReadFrame(ref buffer, out var payload))
                    frameQueue!.Enqueue((payload, flow.Take()));

                reader.AdvanceTo(buffer.Start, buffer.End);
                if (result.IsCompleted) break;
            }
            await reader.CompleteAsync();
        }

        bool TryReadFrame(ref ReadOnlySequence<byte> buffer, out byte[] payload)
        {
            payload = Array.Empty<byte>();
            var reader = new SequenceReader<byte>(buffer);
            Span<byte> header = stackalloc byte[(int)HEADER_SIZE];

            while (reader.Remaining >= HEADER_SIZE + FOOTER_SIZE)
            {
                if (!reader.IsNext(SyncBytes, advancePast: false))
                {
                    reader.Advance(1);
                    continue;
                }

                var frameStart = reader.Position;

                if (!reader.TryCopyTo(header)) break;

                uint flowId   = header[1];
                uint dataSize = header[2];
                uint cmd      = header[3];
                uint frameSize = dataSize + HEADER_SIZE + FOOTER_SIZE;

                if (reader.Remaining < frameSize) break;

                var frameSeq = buffer.Slice(frameStart, frameSize);
                byte footerByte = frameSeq.Slice(frameSize - 1, 1).First.Span[0];

                if (footerByte != FOOTER_BYTE)
                {
                    Console.WriteLine($"ProtocolV1 footer error: 0x{footerByte:X}");
                    reader.Advance(1);
                    continue;
                }

                flow.Check((byte)flowId);

                reader.Advance(frameSize);
                buffer = buffer.Slice(reader.Position);

                if (cmd == CMD_READ_PERIODIC)
                {
                    payload = frameSeq.Slice(HEADER_SIZE, dataSize).ToArray();
                    return true;
                }
                Console.WriteLine($"ProtocolV1 unexpected cmd: 0x{cmd:X}");
            }

            buffer = buffer.Slice(reader.Position);
            return false;
        }

        UInt64 UnpackSample(byte[] data, int offset, int size)
        {
            if (size == 8) return BitConverter.ToUInt64(data, offset);
            if (size == 4) return BitConverter.ToUInt32(data, offset);
            if (size == 2) return BitConverter.ToUInt16(data, offset);
            if (size == 1) return data[offset];
            UInt64 sample = 0;
            for (int b = 0; b < size; b++)
                sample |= (UInt64)data[offset + b] << (b * 8);
            return sample;
        }

        void frameUnpack()
        {
            while (frameQueue!.TryDequeue(out var f))
            {
                var data = f.data;
                timeout.Stop();
                timeout.Start();
                int i = 0;
                var frame = new Dictionary<uint, UInt64>(ReadMemList.Count);
                foreach (var rmd in ReadMemList)
                {
                    if (i + rmd.size > data.Length) break;
                    frame[rmd.token] = UnpackSample(data, i, rmd.size);
                    i += rmd.size;
                }
                FrameComplete?.Invoke(f.lost, frame);
            }
        }

        // com opened by the ComOpener: start reading it
        void StartReading()
        {
            pipe = new();
            frameQueue = new();
            flow.Resync();
            cts = new CancellationTokenSource();
            _ = Task.Run(async () =>
            {
                try { await ReadCom(hCom, pipe.Writer, cts.Token); }
                catch (OperationCanceledException) { }
                catch (Exception ex) { Console.WriteLine("ProtocolV1 ReadCom error: " + ex.Message); }
            });
            _ = Task.Run(async () =>
            {
                try { await FetchFrame(pipe.Reader, cts.Token); }
                catch (OperationCanceledException) { }
                catch (Exception ex) { Console.WriteLine("ProtocolV1 FetchFrame error: " + ex.Message); }
            });
        }

        void Close()
        {
            cts?.Cancel();
            timeout.Stop();
            timeout.Enabled = false;
            bRun = false;
            hCom?.Close();
        }

        public void WriteMem(UInt32 address, int size, UInt64 data) { }
        public void ReadMem(UInt32 address, int size, Action<UInt64> onReadComplete, Action? onReadFailed = null) { onReadFailed?.Invoke(); }
        public bool IsConnected => State == State_t.capturing;

        public uint ReadMemRegister(UInt32 address, int size)
        {
            Console.WriteLine($"ProtocolV1 Register 0x{address:X}:{size}");
            var rmd = new ReadMemData
            {
                address = address,
                size = size / 8,
                oneShot = false,
                token = readMemToken++
            };
            ReadMemList.Add(rmd);
            UpdateConfig();
            return rmd.token;
        }

        public void ReadMemUnregister(uint token)
        {
            var rmd = ReadMemList.FirstOrDefault(r => r.token == token);
            if (rmd != null)
            {
                Console.WriteLine($"ProtocolV1 Unregister 0x{rmd.address:X}");
                ReadMemList.Remove(rmd);
                UpdateConfig();
            }
        }

        private void SendConfig()
        {
            Console.WriteLine($"ProtocolV1 Configure {BitConverter.ToString(Config)}");
            hCom?.Flush();
            hCom?.Write(Config);
            bSendConfig = false;
            timeout.Stop();
            timeout.Start();
        }

        public void Process()
        {
            if (hCom == null) return;

            switch (State)
            {
                case State_t.stopped:
                    // connection in background, an attempt still running once stopped is closed when over
                    bool wanted = bRun && ReadMemList.Count != 0;
                    if ((wanted || opener!.IsPending) && opener!.Poll() == ComOpener.Result.Opened)
                    {
                        if (wanted)
                        {
                            StartReading();
                            State = State_t.capturing;
                            timeout.Enabled = true;
                            SendConfig();
                        }
                        else
                        {
                            hCom.Close();
                        }
                    }
                    break;

                case State_t.capturing:
                    frameUnpack();
                    if (bTimeoutNotify)
                    {
                        bTimeoutNotify = false;
                        Timeout?.Invoke();
                    }
                    if (bSendConfig) SendConfig();
                    if (!bRun || ReadMemList.Count == 0)
                    {
                        Close();
                        State = State_t.stopped;
                    }
                    break;
            }
        }
    }
}
