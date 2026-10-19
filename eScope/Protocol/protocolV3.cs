using eScope.Com;
using System.Buffers;
using System.Collections.Concurrent;
using System.IO.Pipelines;
using System.Timers;

namespace eScope.Protocol
{

    public class ProtocolV3: IProtocol
    {
        public class MemData
        {
            public uint cmd = 0;
            public UInt64 address = 0;
            public int size = 0;
            public UInt64 data = 0;
            public uint token = 0;
            public Action<UInt64> onReadComplete { get; set; } = onReadCompleteDefault;
            public Action? onReadFailed { get; set; }
            static public void onReadCompleteDefault(UInt64 y) { }
        }

        enum State_t
        {
            stopped,
            configuring,
            capturing
        }
        private State_t State = State_t.stopped;
        ICom hCom;
        ComOpener opener;
        bool bRun = false; // recording: periodic reads configured
        bool bRunApplied = false; // bRun the device configuration was sent for
        // outside a recording the link is opened on demand by the one shot accesses (Watch, GDB),
        // and closed after LINK_IDLE_MS without any
        const int LINK_IDLE_MS = 3000;
        readonly System.Diagnostics.Stopwatch linkActivity = new();
        bool LinkRequested => linkActivity.IsRunning && linkActivity.ElapsedMilliseconds < LINK_IDLE_MS;
        bool LinkWanted => bRun || LinkRequested;
        private byte[] Config;
        uint cfgLen = 0;
        List<MemData> configTxList;
        List<MemData> configRxList;
        private System.Timers.Timer timeout;
        bool bReconfig = false;
        // one shot read sent, waiting for its answer: released firmwares drop the commands
        // received meanwhile, and the ones following a one shot read in the same config burst
        MemData? pendingRead;
        readonly System.Diagnostics.Stopwatch pendingReadTime = new();
        const int READ_APERIODIC_TIMEOUT_MS = 1000;
        Pipe pipe;
        // err: 0 for a data frame, else the error code of an error frame (payload in data)
        ConcurrentQueue<(byte[] data, int lost, uint err)> frameQueue;
        readonly FlowCounter flow = new("ProtocolV3");
        uint readMemToken = 0;
        CancellationTokenSource cts;
        public event Action<int, IReadOnlyDictionary<uint, UInt64>>? FrameComplete;
        public event Action? Timeout;
        public event Action<string>? Error;
        // error frames are repeated by the device until CMD_CLEAR: handled once, until a data frame,
        // a timeout or a new link
        bool bErrorHandled = false;
        // configuration not accepted by the device (address size): no reconfiguration until Start() or a new link
        bool bConfigError = false;
        volatile bool bTimeoutFlag = false; // set by the timer thread, handled in Process()
        public bool SupportsReadMem => true;
        public bool IsConnected => State == State_t.capturing;

        public const uint HEADER_BYTE = 0x55;
        public const uint FOOTER_BYTE = 0xAA;
        public const uint ERR_FLAG =   0x80;
        public const uint READ_FLAG =  0x40;
        public const uint WRITE_FLAG = 0x20;
        public const uint CMD_NOP = 0x00;
        public const uint CMD_DEINIT = 0x01;
        public const uint CMD_CLEAR = 0x02;
        public const uint CMD_READ_PERIODIC = 0x03 | READ_FLAG;
        public const uint CMD_DEBUG = 0x06;
        public const uint CMD_READ_APERIODIC = 0x04 | READ_FLAG;
        public const uint CMD_WRITE_APERIODIC = 0x05 | WRITE_FLAG;
        public const uint ERR_TXOVERFLOW = 0x06 | ERR_FLAG;
        public const uint ERR_CFGOVERFLOW = 0x07 | ERR_FLAG;
        public const uint ERR_ADDR = 0x08 | ERR_FLAG;      // address rejected by the device: payload address + size
        public const uint ERR_BWOVERFLOW = 0x09 | ERR_FLAG; // Arduino
        public const uint ERR_ADDRSIZE = 0x0A | ERR_FLAG;  // address size of the device differs: payload its size
        public const uint ADDR64_FLAG = 0x10; // read / write command with a 64 bits address
        readonly int AddressSize; // address bytes on the wire: 4, or 8 (64 bits target)
        uint CMD_SIZE => 2U + (uint)AddressSize; /* CMDx1 + SIZEx1 + ADDRESSxN */
        public const uint CMD_IDX = 0U;
        public const uint CMD_SIZE_IDX = 1U;
        public const uint CMD_ADDR_IDX = 2U;
        public const uint HEADER_SIZE = 4U;
        public const uint FOOTER_SIZE = 1U;
        readonly byte[] SyncBytes = new byte[] { 0x55 };
        long totalBytes = 0;
        DateTime lastReport = DateTime.Now;
        private int _framesPerSecond = 0;
        private int _frameAccumulator = 0;
        private int _lastFrameCount = 0;
        private DateTime _lastRateUpdate = DateTime.Now;
        private float _unpackBudgetPerTick = 1.0f; // frames to process per tick
        private float _unpackBudgetAccumulator = 0f;
        private int tickCnt = 0;
        public ProtocolV3(string com, int addressSize = 4)
        {
            AddressSize = (addressSize == 8) ? 8 : 4;
            hCom = ICom.Get(com);
            opener = new ComOpener(hCom);
            configTxList = new List<MemData> {};
            configRxList = new List<MemData> {};
            timeout = new System.Timers.Timer(1000);
            timeout.Elapsed += TimeoutHandler;
            timeout.AutoReset = true;
        }
        public void UpdateConfig()
        {
            if (pendingRead != null)
                return; // sent once the one shot read is answered

            // periodic reads kept back while not recording
            List<MemData> ready = bRun ? configTxList : configTxList.Where(d => d.cmd != CMD_READ_PERIODIC).ToList();
            // at most one one shot read per burst, sent last
            int count = ready.FindIndex(d => d.cmd == CMD_READ_APERIODIC);
            count = (count < 0) ? ready.Count : count + 1;
            List<MemData> burst = ready.GetRange(0, count);
            if (bRun)
                configTxList.RemoveRange(0, count);
            else
                configTxList.RemoveAll(d => burst.Contains(d));

            cfgLen = 0;
            foreach (var d in burst)
            {
                cfgLen += CMD_SIZE;

                if (d.cmd == CMD_WRITE_APERIODIC)
                {
                    cfgLen += (uint)d.size;
                }
            }
            Config = new byte[cfgLen];
            int cfgIdx = 0;
            foreach (var d in burst)
            {
                Config[cfgIdx++] = (byte)(d.cmd | ((AddressSize == 8) ? ADDR64_FLAG : 0));
                Config[cfgIdx++] = (byte)d.size;
                for (int i = 0; i < AddressSize; i++)
                {
                    Config[cfgIdx++] = (byte)((d.address >> (i * 8)) & 0xFF);
                }

                if(d.cmd == CMD_WRITE_APERIODIC)
                {
                    for(int i=0; i<d.size; i++)
                    {
                        Config[cfgIdx++] = (byte)((d.data >> (i*8)) & 0xFF);
                    }
                }
                else if((d.cmd & READ_FLAG) != 0)
                {
                    configRxList.Add(d);
                    if (d.cmd == CMD_READ_APERIODIC)
                    {
                        pendingRead = d;
                        pendingReadTime.Restart();
                    }
                }
            }
            if(cfgLen != 0)
            {
                SendConfig();
            }
        }
        // one shot reads not answered: failed
        void FailReads(List<MemData> list)
        {
            var failed = list.Where(d => d.cmd == CMD_READ_APERIODIC).ToList();
            list.RemoveAll(d => d.cmd == CMD_READ_APERIODIC);
            if (failed.Contains(pendingRead!))
                pendingRead = null;
            foreach (var d in failed)
            {
                d.onReadFailed?.Invoke();
            }
        }
        bool HasPeriodic => configRxList.Any(d => d.cmd == CMD_READ_PERIODIC) || (bRun && configTxList.Any(d => d.cmd == CMD_READ_PERIODIC));
        public void Start()
        {
            bRun = true;
            bConfigError = false;
            opener.ResetRetryDelay();
        }
        public void Stop()
        {
            bRun = false;
        }
        public void RequestLink() => linkActivity.Restart();
        public void Dispose()
        {
            opener.Dispose();
        }
        public void TimeoutHandler(object? sender, ElapsedEventArgs e)
        {
            Console.WriteLine($"ProtocolV3 Windows timeout");
            timeout?.Stop(); // reload timeout timer

            bTimeoutFlag = true;
        }
        async Task ReadCom(ICom hCom, PipeWriter writer, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                byte[] buffer = await hCom.ReadAsync(token); // blocks until data arrives

                //Console.WriteLine($"ReadCom {buffer.Length} bytes @ {DateTime.Now:HH:mm:ss.fff}");
                if (buffer.Length > 0)
                {

                    // in ReadCom after getting buffer: debug
                    // totalBytes += buffer.Length;
                    // if ((DateTime.Now - lastReport).TotalSeconds >= 1.0)
                    // {
                        //Console.WriteLine($"Throughput: {totalBytes} bytes/sec");
                    //     totalBytes = 0;
                    //     lastReport = DateTime.Now;
                    // }
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

                while (TryReadFrame(ref buffer, out var payload, out uint err))
                {
                    frameQueue.Enqueue((payload, flow.Take(), err));
                }

                reader.AdvanceTo(buffer.Start, buffer.End);/*?? */
                if (result.IsCompleted) break;
            }

            await reader.CompleteAsync();
        }
        bool TryReadFrame(ref ReadOnlySequence<byte> buffer, out byte[] payload, out uint err)
        {
            payload = Array.Empty<byte>();
            err = 0;
            var reader = new SequenceReader<byte>(buffer);

            while (reader.Remaining >= HEADER_SIZE + FOOTER_SIZE)
            {
                // Cherche le sync byte
                if (!reader.IsNext(SyncBytes, advancePast: false))
                {
                    reader.Advance(1);
                    continue;
                }

                var frameStart = reader.Position;

                // Lit le header sans avancer reader
                Span<byte> header = stackalloc byte[(int)HEADER_SIZE];
                if (!reader.TryCopyTo(header))
                    break;

                uint flowId = header[1];
                uint dataSize = header[2];
                uint cmd = header[3];
                uint frameSize = dataSize + HEADER_SIZE + FOOTER_SIZE;

                // Trame complète reçue ?
                if (reader.Remaining < frameSize)
                    break;

                // Vérifie le footer
                var frameSeq = buffer.Slice(frameStart, frameSize);
                byte footerByte = frameSeq.Slice(frameSize - 1, 1).First.Span[0];

                if (footerByte != FOOTER_BYTE)
                {
                    Console.WriteLine($"ProtocolV3 Footer sync byte error : {footerByte} but expect {FOOTER_BYTE}");
                    reader.Advance(1);  // mauvais footer, avance d'1 et cherche prochain sync
                    continue;
                }

                // ── Trame valide ──────────────────────────────────────────
                flow.Check((byte)flowId);

                // Avance reader sur toute la trame
                reader.Advance(frameSize);

                // Met à jour buffer depuis reader.Position — UN SEUL mécanisme
                buffer = buffer.Slice(reader.Position);

                if (cmd == CMD_READ_PERIODIC)
                {
                    payload = frameSeq.Slice(HEADER_SIZE, dataSize).ToArray();
                    //Console.WriteLine($"ProtocolV3 payload {BitConverter.ToString(payload)}");
                    return true;
                }
                else if ((cmd & ERR_FLAG) != 0)
                {
                    payload = frameSeq.Slice(HEADER_SIZE, dataSize).ToArray();
                    err = cmd;
                    return true;
                }
                else if (cmd == CMD_DEBUG)
                {
                    var dbg = frameSeq.Slice(HEADER_SIZE, dataSize).ToArray();
                    Console.WriteLine($"ProtocolV3 debug {BitConverter.ToString(dbg)}");
                }
                else
                {
                    Console.WriteLine($"ProtocolV3 unknown cmd : {cmd}");
                }
            }

            // Consomme ce qui a été parcouru
            buffer = buffer.Slice(reader.Position);
            return false;
        }
        UInt64 Unpack(byte[] data, int i, int size)
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
        void frameUnpack()
        {
            int unpackFrame = 0;
            int framCount = frameQueue.Count;
            int unpackQty = (int)_unpackBudgetPerTick;
            //Console.WriteLine($"Unpack {framesToProcess}");
            while (/*unpackQty-- > 0 && */frameQueue.TryDequeue(out var f))
            {
                var data = f.data;
                UInt64 unpack = 0;
                int size = 0;
                int i = 0;
                int cmdIdx = 0;
                unpackFrame++;
                if (f.err != 0)
                {
                    HandleError(f.err, data, f.lost);
                    continue;
                }
                bErrorHandled = false;
                timeout?.Stop();
                timeout?.Start();

                if (cmdIdx >= configRxList.Count)
                {
                    Console.WriteLine("No rX configure ????");
                    continue;
                }
                size = configRxList[cmdIdx].size;
                var frame = new Dictionary<uint, UInt64>(configRxList.Count);

                while ((i + size) <= data.Length)
                {
                    unpack = Unpack(data, i, size);
                    if (configRxList[cmdIdx].cmd == CMD_READ_PERIODIC)
                    {
                        frame[configRxList[cmdIdx].token] = unpack;
                    }
                    else
                    {
                        configRxList[cmdIdx].onReadComplete(unpack);
                    }

                    if (configRxList[cmdIdx].cmd == CMD_READ_APERIODIC) /* aperiodic are one shot read */
                    {
                        if (configRxList[cmdIdx] == pendingRead)
                            pendingRead = null;
                        configRxList.RemoveAt(cmdIdx);
                    }
                    else
                    {
                        cmdIdx++;
                    }
                    if (cmdIdx >= configRxList.Count) 
                    {
                        break;
                    }
                    i += size; /* process next data */
                    size = configRxList[cmdIdx].size;
                }
                if (cmdIdx != configRxList.Count)
                {
                    Console.WriteLine("Missing data ?????");
                }
                // outside a recording the frames only carry one shot answers: no sample
                if (bRun)
                    FrameComplete?.Invoke(f.lost, frame);
            }

            // measure arrival rate to adjust unpack rate to give a smotth refresh,
            // serial driver read data by chunck of 4096 by default, which lead to UI freeze every seconds...
            _frameAccumulator += unpackFrame; // rough count, updated each tick
            var now = DateTime.Now;
            double elapsed = (now - _lastRateUpdate).TotalSeconds;
            tickCnt++;
            if (elapsed >= 1.0)
            {
                _framesPerSecond = (int)((_frameAccumulator+ framCount) / elapsed);
                _unpackBudgetPerTick = (_frameAccumulator + framCount) / tickCnt;
                _frameAccumulator = 0;
                _lastRateUpdate = now;
                tickCnt = 0;

                //Console.WriteLine($"Rate: {_framesPerSecond} frames/sec => {_unpackBudgetPerTick:F1} frames/tick");
            }

            //Console.WriteLine($"Unpack {framCount} queued => processing {_unpackBudgetPerTick}");
        }
        // error frame: sent by the device on the tick of a sample, until CMD_CLEAR
        void HandleError(uint err, byte[] data, int lost)
        {
            // the time base goes on: the error frame is a lost sample
            if (bRun)
                FrameComplete?.Invoke(lost, new Dictionary<uint, UInt64>());
            if (bErrorHandled)
                return;
            bErrorHandled = true;
            Console.WriteLine($"ProtocolV3 Err Rx : 0x{err:X} {BitConverter.ToString(data)}");

            if ((err == ERR_ADDR) && (data.Length >= AddressSize + 1))
            {
                UInt64 address = 0;
                for (int i = 0; i < AddressSize; i++)
                {
                    address |= (UInt64)data[i] << (i * 8);
                }
                int size = data[AddressSize];
                // the rejected read is not sent again (a rejected write is only dropped by the device)
                var rejected = configRxList.Where(d => (d.address == address) && (d.size == size)).ToList();
                configRxList.RemoveAll(d => rejected.Contains(d));
                foreach (var d in rejected.Where(d => d.cmd == CMD_READ_APERIODIC))
                {
                    if (d == pendingRead)
                        pendingRead = null;
                    d.onReadFailed?.Invoke();
                }
                Error?.Invoke($"address 0x{address:X} ({size} B) rejected by the device");
                bReconfig = true; // the other commands sent again
            }
            else if ((err == ERR_ADDRSIZE) && (data.Length >= 1))
            {
                bConfigError = true;
                Error?.Invoke($"the device expects {data[0] * 8} bits addresses, configured with {AddressSize * 8} bits");
            }
            else
            {
                string name = err switch
                {
                    ERR_TXOVERFLOW => "data frame too small for the channels (ERR_TXOVF)",
                    ERR_CFGOVERFLOW => "configuration buffer full (ERR_CFGOVF)",
                    ERR_BWOVERFLOW => "bandwidth exceeded (ERR_BWOVF)",
                    _ => $"error 0x{err:X2}",
                };
                Error?.Invoke($"device {name}"); // reconfigured on timeout
            }
        }
        // com opened by the ComOpener: start reading it
        void StartReading()
        {
            bErrorHandled = false;
            bConfigError = false; // firmware may have changed: checked again by the device
            pipe = new();
            frameQueue = new();
            flow.Resync();
            cts = new CancellationTokenSource();
            _ = Task.Run(async () =>
            {
                try { await ReadCom(hCom, pipe.Writer, cts.Token); }
                catch (OperationCanceledException)
                {
                    // com port closed
                }
                catch (Exception ex) {
                    Console.WriteLine("ProtocolV3 ReadCom error: " + ex.Message); }
            });

            _ = Task.Run(async () =>
            {
                try
                {
                    await FetchFrame(pipe.Reader, cts.Token);
                }
                catch (OperationCanceledException)
                {
                    // com port closed
                }
                catch (Exception ex)
                {
                     Console.WriteLine("ProtocolV3 FetchFrame error: " + ex.Message);
                    Console.WriteLine(ex.StackTrace);
                }
            });
        }
        void Close()
        {
            cts?.Cancel();
            timeout?.Stop();
            bRun = false;
            timeout.Enabled = false;

            hCom.Close();
            FailReads(configRxList);
            FailReads(configTxList);
        }
        public void ReadMem(UInt64 address, int size, Action<UInt64> onReadComplete, Action? onReadFailed = null)
        {
            //Console.WriteLine($"ProtocolV3 Read Mem 0x{address:X}:{size}");
            if ((State != State_t.capturing) && opener.InRetryDelay)
            {
                onReadFailed?.Invoke(); // device just found absent: no new attempt for each read
                return;
            }
            linkActivity.Restart(); // opens the link if needed
            MemData d = new MemData();
            d.cmd = CMD_READ_APERIODIC;
            d.address = address;
            d.size = size/8;
            d.onReadComplete = onReadComplete;
            d.onReadFailed = onReadFailed;
            d.token = readMemToken++;
            configTxList.Add(d);
        }
        public uint ReadMemRegister(UInt64 address, int size)
        {
            //Console.WriteLine($"ProtocolV3 Register Read Mem 0x{address:X}:{size}");
            MemData d = new MemData();
            d.cmd = CMD_READ_PERIODIC;
            d.address = address;
            d.size = size/8;
            d.token = ++readMemToken;
            configTxList.Add(d);

            return d.token;
        }
        public void ReadMemUnregister(uint token)
        {
            for (int idx = configRxList.Count - 1; idx >= 0; idx--)
            {
                if (configRxList[idx].token == token)
                {
                    //Console.WriteLine($"ProtocolV3 Unregister Read Mem 0x{configRxList[idx].address:X}:{configRxList[idx].size}");
                    configRxList.RemoveAt(idx);
                    bReconfig = true;
                    return;
                }
            }
            for (int idx = configTxList.Count - 1; idx >= 0; idx--)
            {
                if (configTxList[idx].token == token)
                {
                    //Console.WriteLine($"ProtocolV3 Unregister Read Mem 0x{configTxList[idx].address:X}:{configTxList[idx].size}");
                    configTxList.RemoveAt(idx);
                    //bReconfig = true; /* no need to reconfigure */
                    return;
                }
            }
        }
        // clear the device config and send it again: periodic reads first, then the queued commands
        // one shot reads not answered are failed (timeout) or sent again
        void Reconfigure(bool failReads)
        {
            Config = new byte[1];
            Config[0] = (byte)CMD_CLEAR;
            SendConfig();

            if (failReads)
            {
                FailReads(configRxList);
            }
            pendingRead = null;
            var periodic = configRxList.Where(d => d.cmd == CMD_READ_PERIODIC).ToList();
            var oneShot = configRxList.Where(d => d.cmd == CMD_READ_APERIODIC).ToList();
            configTxList.InsertRange(0, periodic);
            configTxList.AddRange(oneShot);
            configRxList.Clear();
            frameQueue.Clear();
            flow.Resync();
        }
        public void WriteMem(UInt64 address, int size, UInt64 data) // /!\ Call be timer thread and com thread
        {
            //Console.WriteLine($"ProtocolV3 WriteMem0x{address:X}:{size}:{data}");
            linkActivity.Restart(); // opens the link if needed
            MemData d = new MemData();
            d.cmd = CMD_WRITE_APERIODIC;
            d.address = address;
            d.size = size / 8;
            d.data = data;
            configTxList.Add(d);
        }
        private void SendConfig() // /!\ Call be timer thread and com thread
        {
            Console.WriteLine($"ProtocolV3 Configure {BitConverter.ToString(Config)}");
            hCom?.Write(Config);
            timeout?.Stop(); // reload timeout timer
            timeout?.Start();
        }
        public void Process()
        {
            if (hCom != null)
            {
                switch (State)
                {
                    default:
                    case State_t.stopped:
                        // connection in background (retried while running), an attempt still running once not wanted is closed when over
                        if (LinkWanted || opener.IsPending)
                        {
                            var result = opener.Poll();
                            if (result == ComOpener.Result.Opened)
                            {
                                if (LinkWanted)
                                {
                                    StartReading();
                                    State = State_t.capturing;
                                    timeout.Enabled = true;
                                    bReconfig = true;
                                    bRunApplied = bRun;
                                }
                                else
                                {
                                    hCom.Close();
                                }
                            }
                            else if (result == ComOpener.Result.Failed)
                            {
                                FailReads(configTxList); // device absent
                                if (!bRun)
                                    linkActivity.Reset(); // on demand: next attempt on the next access
                            }
                        }
                        break;

                    case State_t.capturing:
                        frameUnpack();
                        bool failReads = false;
                        if (bConfigError)
                        {
                            // device configuration to change: nothing sent, the one shot accesses fail
                            bTimeoutFlag = false;
                            bReconfig = false;
                            FailReads(configTxList);
                            configTxList.RemoveAll(d => d.cmd == CMD_WRITE_APERIODIC);
                            FailReads(configRxList);
                            pendingRead = null;
                            break;
                        }
                        if (bTimeoutFlag)
                        {
                            bTimeoutFlag = false;
                            bErrorHandled = false;
                            if (HasPeriodic) // no frame expected otherwise
                            {
                                Timeout?.Invoke();
                                bReconfig = true;
                                failReads = true;
                            }
                        }
                        if ((pendingRead != null) && (pendingReadTime.ElapsedMilliseconds > READ_APERIODIC_TIMEOUT_MS))
                        {
                            Console.WriteLine($"ProtocolV3 one shot read timeout 0x{pendingRead.address:X}");
                            bErrorHandled = false;
                            bReconfig = true;
                            failReads = true;
                        }
                        if (bRun != bRunApplied)
                        {
                            // recording started / stopped with the link open: periodic reads sent / cleared
                            bRunApplied = bRun;
                            bReconfig = true;
                        }
                        if(bReconfig == true)
                        {
                            Reconfigure(failReads);
                            bReconfig = false;
                        }
                        else
                        {
                            UpdateConfig();
                        }
                        // not recording: link closed once the one shot accesses are over
                        if (!LinkWanted && (pendingRead == null) && !configTxList.Any(d => d.cmd != CMD_READ_PERIODIC))
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
