using eScope.Com;
using System.Buffers;
using System.Collections.Concurrent;
using System.IO.Pipelines;
using System.Timers;

namespace eScope.Protocol
{
    public class ReadMemData
    {
        public UInt32 address = 0;
        public int size = 0;
        public UInt64 data = 0;
        public bool oneShot = false;
        public uint token = 0;
    }

    public class ProtocolV2: IProtocol
    {
        enum State_t
        {
            stopped,
            configuring,
            capturing
        }
        private State_t State = State_t.stopped;
        ICom? hCom;
        ComOpener? opener;
        bool bRun = false;
        private byte[] Config;
        List<ReadMemData> ReadMemList;
        private System.Timers.Timer timeout;
        bool bSendConfig = false;
        private int samplesSize = 0;
        readonly byte[] SyncBytes = new byte[] { 0x55, 0xAA, 0x33 };
        readonly int headerSize;
        int frameSize;
        Pipe pipe;
        ConcurrentQueue<(byte[] data, int lost)> frameQueue;
        readonly FlowCounter flow = new("ProtocolV2");
        uint readMemToken = 1;
        CancellationTokenSource cts;
        public event Action<int, IReadOnlyDictionary<uint, UInt64>>? FrameComplete;
        public event Action? Timeout;
        volatile bool bTimeoutNotify = false; // set by the timer thread, notified from Process()

        public ProtocolV2(string com)
        {
            hCom = ICom.Get(com);
            if (hCom != null) opener = new ComOpener(hCom);
            ReadMemList = new List<ReadMemData> { };
            timeout = new System.Timers.Timer(1000);
            timeout.Elapsed += TimeoutHandler;
            timeout.AutoReset = true;
            headerSize = SyncBytes.Length + 1;
        }
        public void UpdateConfig()
        {
            int i = 0;

            Config = new byte[ReadMemList.Count*8];
            i = 0;
            samplesSize = 0;
            foreach (ReadMemData rmd in ReadMemList)
            {
                if (i >= Config.Length) break;

                Config[i + 0] = (byte)((rmd.address >> 0) & 0xFF);
                Config[i + 1] = (byte)((rmd.address >> 8) & 0xFF);
                Config[i + 2] = (byte)((rmd.address >> 16) & 0xFF);
                Config[i + 3] = (byte)((rmd.address >> 24) & 0xFF);
                Config[i + 4] = (byte)(rmd.size);
                samplesSize += rmd.size;
                i += 8;
            }
            frameSize = headerSize + samplesSize;
            bSendConfig = true;
        }
        public void Start()
        {
            bRun = true;
            opener?.ResetRetryDelay();
        }
        public void Stop()
        {
            bRun = false;
        }
        public void Dispose()
        {
            opener?.Dispose();
            timeout.Dispose();
        }
        public void TimeoutHandler(object? sender, ElapsedEventArgs e)
        {
            Console.WriteLine($"ProtocolV2 Windows timeout");
            timeout.Stop(); // reload timeout timer
            bSendConfig = true;
            bTimeoutNotify = true;
        }
        async Task ReadCom(ICom hCom, PipeWriter writer, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                byte[] buffer = hCom.ReadAll();
                if (buffer.Length > 0)
                {
                    //Console.WriteLine($"ProtocolV2 serialread {buffer.Length}");
                    var mem = writer.GetMemory(buffer.Length);
                    buffer.AsSpan(0, buffer.Length).CopyTo(mem.Span);
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
                {
                    frameQueue.Enqueue((payload, flow.Take()));
                }

                reader.AdvanceTo(buffer.Start, buffer.End);
                if (result.IsCompleted) break;
            }

            await reader.CompleteAsync();
        }
        bool TryReadFrame(ref ReadOnlySequence<byte> buffer, out byte[] payload)
        {
            payload = new byte[samplesSize];
            var reader = new SequenceReader<byte>(buffer);

            while (!reader.End)
            {
                if (reader.Remaining < samplesSize) break;

                if (reader.IsNext(SyncBytes, advancePast: false))
                {
                    var start = reader.Position;
                    var possibleFrame = buffer.Slice(start);

                    if (possibleFrame.Length >= frameSize) // frame complete ?
                    {
                        // verify protocol flow
                        byte flowId = possibleFrame.Slice(SyncBytes.Length, 1).First.Span[0];
                        flow.Check(flowId);

                        // extract frame payload
                        buffer = buffer.Slice(buffer.GetPosition(frameSize, start));
                        Console.WriteLine($"ProtocolV3 frame {BitConverter.ToString(possibleFrame.ToArray())}");
                        payload = possibleFrame.Slice(headerSize, samplesSize).ToArray(); // Skip sync + flow

                        return true;
                    }

                    break;
                }

                reader.Advance(1);
            }

            buffer = buffer.Slice(reader.Position);
            return false;
        }
        UInt64 readSample(byte[] data, int i, int size)
        {
            UInt64 sample = 0;
            int byteIdx = 0;
            if(size == 8)
            {
                sample = BitConverter.ToUInt64(data, i);
            }
            else if (size == 4)
            {
                sample = BitConverter.ToUInt32(data, i);
            }
            else if (size == 2)
            {
                sample = BitConverter.ToUInt16(data, i);
            }
            else if (size == 1)
            {
                sample = data[i];
            }
            else
            {
                for (byteIdx = 0; byteIdx < size; byteIdx++)
                {
                    sample = sample | ((UInt64)data[i + byteIdx] << byteIdx * 8);
                }
            }
            return sample;
        }
        void Unpack()
        {
            UInt64 unpack = 0;
            int size = 0;
            int i;
            while (frameQueue.TryDequeue(out var f))
            {
                var data = f.data;
                i = 0;

                timeout.Stop();
                timeout.Start();

                var frame = new Dictionary<uint, UInt64>(ReadMemList.Count);
                foreach (var readMem in ReadMemList)
                {
                    size = readMem.size;
                    if (data.Length >= (i + size))
                    {
                        unpack = readSample(data, i, size);

                        i += size;
                        frame[readMem.token] = unpack;
                        //Console.WriteLine($"ProtocolV2 {unpack}");
                    }
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
                catch (Exception ex) {
                    Console.WriteLine("ProtocolV2 ReadCom error: " + ex.Message); }
            });

            _ = Task.Run(async () =>
            {
                try { await FetchFrame(pipe.Reader, cts.Token); }
                 catch (Exception ex) { Console.WriteLine("ProtocolV2 FetchFrame error: " + ex.Message); }
            });
            bSendConfig = true;
        }
        void Close()
        {
            cts?.Cancel();
            timeout.Stop();
            bRun = false;
            timeout.Enabled = false;

            hCom.Close();
        }
        public void ReadMem(UInt32 address, int size, Action<UInt64> onReadComplete, Action? onReadFailed = null)
        {
            Console.WriteLine($"ProtocolV2 Read Mem 0x{address:X}:{size} not available on protocolV2");
            onReadFailed?.Invoke();
        }
        public bool IsConnected => State == State_t.capturing;
        public uint ReadMemRegister(UInt32 address, int size)
        {
            if(size > 32)
            {
                while (true) ;
            }
            Console.WriteLine($"ProtocolV2 Register Read Mem 0x{address:X}:{size}");
            ReadMemData d = new ReadMemData();
            d.address = address;
            d.size = size/8;
            d.oneShot = false;
            d.token = readMemToken++;
            ReadMemList.Add(d);
            UpdateConfig();

            return d.token;
        }
        public void ReadMemUnregister(uint token)
        {
            ReadMemData? rmdToRemove = null;

            foreach (ReadMemData rmd in ReadMemList)
            {
                if(rmd.token == token)
                {
                    rmdToRemove = rmd;
                    break;
                }
            }
            if(rmdToRemove != null)
            {
                Console.WriteLine($"ProtocolV2 Unregister Read Mem 0x{rmdToRemove.address:X}:{rmdToRemove.size}");
                ReadMemList.Remove(rmdToRemove);
                UpdateConfig();
            }
        }

        public void WriteMem(UInt32 address, int size, UInt64 data) // /!\ Call be timer thread and com thread
        {
            Console.WriteLine($"ProtocolV2 Write Mem 0x{address:X}:{size} not available on protocolV2");
        }
        private void SendConfig() // /!\ Call be timer thread and com thread
        {
            Console.WriteLine($"ProtocolV2 Configure {BitConverter.ToString(Config)}");
            hCom?.Flush();
            hCom?.Write(Config);
            bSendConfig = false;
            timeout.Stop(); // reload timeout timer
            timeout.Start();
        }
        public void Process()
        {
            if (hCom != null)
            {
                switch (State)
                {
                    default:
                    case State_t.stopped:
                        // connection in background (retried while running), an attempt still running once stopped is closed when over
                        bool wanted = (bRun == true) && (ReadMemList.Count != 0);
                        if ((wanted || opener!.IsPending) && opener!.Poll() == ComOpener.Result.Opened)
                        {
                            if (wanted)
                            {
                                StartReading();
                                State = State_t.capturing;
                                timeout.Enabled = true;
                            }
                            else
                            {
                                hCom.Close();
                            }
                        }
                        break;

                    case State_t.capturing:
                        Unpack();
                        if (bTimeoutNotify)
                        {
                            bTimeoutNotify = false;
                            Timeout?.Invoke();
                        }

                        if(bSendConfig)
                        {
                            SendConfig();
                        }
                        if ((bRun == false) || (ReadMemList.Count == 0))
                        {
                            Close();
                            State = State_t.stopped;
                        }
                        break;

                }
            }
        }
    }
}
