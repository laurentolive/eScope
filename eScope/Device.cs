using eScope.Com;
using eScope.Plot;
using eScope.Protocol;
using eScope.Symbol;
using IronPython.Compiler.Ast;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.DirectoryServices.ActiveDirectory;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;
using static System.Windows.Forms.AxHost;

namespace eScope
{
    public class Device
    {
        public string Name { get; set; }
        public string Com { get; set; }
        public string SymbolFilePath { get; set; }
        public string GdbFilePath { get; set; }
        public string MapRegex { get; set; }
        public string Protocol { get; set; }
        public string SymbolType { get; set; }
        public double SamplingTime { get; set; }
        public int V1ChannelMax { get; set; }
        public int V1DataSize { get; set; }
        // address bytes on the wire (V3): 4, or 8 for a 64 bits target (firmware simulated on PC)
        public int AddressSize { get; set; }

        public Device()
        {
            Name = string.Empty;
            Com = string.Empty;
            SymbolFilePath = string.Empty;
            GdbFilePath = string.Empty;
            SymbolType = string.Empty;
            Protocol = "V1";
            MapRegex = string.Empty; // automatic detection of the map format (SymbolMap)
            SamplingTime = 1.0;
            V1ChannelMax = 4;
            V1DataSize = 2;
            AddressSize = 4;
        }
    }

    public class DeviceCtrl
    {
        public Device Data = new();
        FileSystemWatcher? watcher;
        System.Threading.Timer? debounceTimer;
        readonly object debounceLock = new();
        bool reloadPending = false;
        volatile bool symbolReload = false; // set by the file watcher, symbols reloaded in Process()
        public ISymbol? Sym;
        // thread running Process(): device protocol and target memory requests of GDB
        int processThreadId = Environment.CurrentManagedThreadId;
        readonly ConcurrentQueue<Action> processActions = new(); // background results applied in Process()
        bool recording = false;
        bool wasRecordingConnected = false;
        int resolveVersion = 0;
        bool inPump = false;
        public IProtocol? Protocol;
        List<ChannelCtrl> Channels = new List<ChannelCtrl>();
        IPlotContext? activeTrace;
        public int ChannelCount => Channels.Count;
        // device time base: one step per received periodic frame
        public long FrameCount { get; private set; } = 0;
        bool bNewSamples = false;
        public DeviceCtrl(Device data)
        {
            Configure(data);
        }
        public void Configure(Device data)
        {
            Data = data;
            if (Protocol != null)
            {
                Protocol.Dispose();
            }

            loadSymbols();

            watcher?.Dispose();
            watcher = null;
            // symbol folder not created yet (firmware never built): no hot-reload, but no crash at startup
            string? symbolDir = string.IsNullOrEmpty(Data.SymbolFilePath) ? null : Path.GetDirectoryName(Path.GetFullPath(Data.SymbolFilePath));
            if (Directory.Exists(symbolDir))
            {
                watcher = new FileSystemWatcher
                {
                    Path = symbolDir,
                    Filter = Path.GetFileName(Data.SymbolFilePath),
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size
                };
                watcher.Changed += OnFileChanged;
                watcher.EnableRaisingEvents = true;
            }
            else if (symbolDir != null)
            {
                LogMsg.Info($"{Data.Name}: symbol folder not found: {symbolDir}");
            }

            Protocol = IProtocol.New(Data.Protocol, Data.Com, Data.AddressSize);
            if (Protocol != null)
            {
                Protocol.FrameComplete += OnFrameComplete;
                Protocol.Timeout += OnTimeout;
                Protocol.Error += msg => LogMsg.Info($"{Data.Name}: {msg}");
            }

            // new protocol: the old tokens are not valid any more
            foreach(var c in Channels)
            {
                c.protocolToken = 0;
                if((Protocol != null) && IsAcquired(c))
                {
                    c.protocolToken = Protocol.ReadMemRegister(c.SignalBase.Address, c.SignalBase.Size);
                }
            }
        }
        // only the channels of the current trace are read on the target (recording applies to the current trace)
        bool IsAcquired(ChannelCtrl c) => (activeTrace != null) && (c.Trace == activeTrace);
        public void SetActiveTrace(IPlotContext? trace)
        {
            if (trace == activeTrace) return;
            activeTrace = trace;
            if (Protocol == null) return;
            bool isChanged = false;
            foreach (var c in Channels)
            {
                bool acquired = IsAcquired(c);
                if (acquired && (c.protocolToken == 0))
                {
                    c.protocolToken = Protocol.ReadMemRegister(c.SignalBase.Address, c.SignalBase.Size);
                    isChanged = true;
                }
                else if (!acquired && (c.protocolToken != 0))
                {
                    Protocol.ReadMemUnregister(c.protocolToken);
                    c.protocolToken = 0;
                    isChanged = true;
                }
            }
            if (isChanged)
            {
                Protocol.UpdateConfig();
            }
        }
        IEnumerable<ChannelCtrl> AcquiredChannels => Channels.Where(c => c.protocolToken != 0);
        // all channels of the device move one step per frame (plus the lost ones), a missing value holds the previous one
        void OnFrameComplete(int lost, IReadOnlyDictionary<uint, UInt64> frame)
        {
            FrameCount += lost + 1;

            var acquired = AcquiredChannels.ToList();
            var starting = acquired.Where(c => c.SignalBase.Y.Count == 0).ToList();
            var running = acquired.Except(starting).ToList();

            // lost frames first, so that starting channels align on the right trace time
            foreach (var c in running)
            {
                c.AddLost(lost);
            }
            // channels without data before the others move forward: they align on the trace time
            foreach (var c in starting)
            {
                if (frame.TryGetValue(c.protocolToken, out UInt64 y))
                {
                    c.AddSample(y);
                }
            }
            foreach (var c in running)
            {
                if (frame.TryGetValue(c.protocolToken, out UInt64 y))
                    c.AddSample(y);
                else
                    c.AddLost(1);
            }
            bNewSamples = true;
        }
        // no frame for a while: unknown number of samples missing, only marked on the channels
        void OnTimeout()
        {
            foreach (var c in AcquiredChannels)
            {
                c.AddTimeout();
            }
        }
        void loadSymbols()
        {
            (Sym as IDisposable)?.Dispose();
            Sym = ISymbol.New(Data.SymbolType, Data.Name, Data.SymbolFilePath, Data.GdbFilePath, Data.MapRegex);
            if (Sym is SymbolGDB gdb)
            {
                gdb.SetWaitPump(Pump, processThreadId);
                ResolveChannelsAsync();
            }
            else
            {
                updateChannels();
            }
        }
        // a synchronous GDB request waits on the Process() thread: keep the protocol running
        // so that the target memory reads it may need are done
        void Pump()
        {
            if (inPump) return;
            inPump = true;
            try
            {
                Protocol?.Process();
                (Sym as SymbolGDB)?.Bridge?.Service(Protocol);
            }
            finally
            {
                inPump = false;
            }
        }
        // resolve the channels again in background (GDB may read pointers on the target),
        // applied on the Process() thread
        void ResolveChannelsAsync()
        {
            if (Sym is not SymbolGDB sym)
                return;
            int version = ++resolveVersion;
            var channels = Channels.Select(c => (c, variable: getVarFromSymbol(c.SignalBase.Symbol))).ToList();
            Task.Run(() =>
            {
                var results = channels.Select(x => (x.c, sd: sym.Get(x.variable))).ToList();
                processActions.Enqueue(() =>
                {
                    if (version != resolveVersion || sym != Sym)
                        return; // superseded
                    bool isChanged = false;
                    foreach (var (c, sd) in results)
                    {
                        if (sd != null && Channels.Contains(c))
                            isChanged |= ApplySymbol(c, sd);
                    }
                    if (isChanged)
                    {
                        Protocol?.UpdateConfig();
                    }
                });
            });
        }
        void OnFileChanged(object sender, FileSystemEventArgs e)
        {

            lock (debounceLock)
            {
                reloadPending = true;

                // Restart or create debounce timer (fires after 300 ms of inactivity)
                debounceTimer?.Dispose();
                debounceTimer = new System.Threading.Timer(_ =>
                {
                    lock (debounceLock)
                    {
                        if (reloadPending)
                        {
                            reloadPending = false;
                            Console.WriteLine($"Symbol file changed at {DateTime.Now:T}, reloading...");
                            symbolReload = true;
                        }
                    }
                }, null, 300, Timeout.Infinite);
            }


        }
        public bool updateChannel(ChannelCtrl c)
        {
            SymbolData? sd = GetSymbol(c.SignalBase.Symbol);
            if (sd == null)
            {
                Console.WriteLine($"Symbol not found {c.SignalBase.Name}");
                return false;
            }
            return ApplySymbol(c, sd);
        }
        // new address of a channel: registered again on the protocol if needed
        bool ApplySymbol(ChannelCtrl c, SymbolData sd)
        {
            if (!sd.Resolved)
            {
                Console.WriteLine($"Symbol not resolved {c.SignalBase.Name} (target not connected?)");
                return false;
            }
            UInt64 uiNewAddress = Utils.strToAddress(sd.Address);
            int size = sd.Size > 0 ? sd.Size : c.SignalBase.Size; // size unknown (map without size): the channel's
            if ((c.SignalBase.Address == uiNewAddress)
                && (c.SignalBase.Size == size)
                && (c.SignalBase.Shift == sd.Shift))
                return false;

            c.SignalBase.Address = uiNewAddress;
            c.SignalBase.Size = size;
            c.SignalBase.Shift = sd.Shift;
            Console.WriteLine($"Update Name:{c.SignalBase.Name}, Symbol:{c.SignalBase.Symbol}, Address:{c.SignalBase.Address}, Size:{c.SignalBase.Size}, Shift:{c.SignalBase.Shift},");
            if ((Protocol != null) && Channels.Contains(c) && (c.protocolToken != 0))
            {
                Protocol.ReadMemUnregister(c.protocolToken);
                c.protocolToken = Protocol.ReadMemRegister(c.SignalBase.Address, c.SignalBase.Size);
            }
            return true;
        }
        public void updateChannels()
        {
            bool isChanged = false;

            foreach (var c in Channels)
            {
                isChanged |= updateChannel(c);
            }
            if (isChanged)
            {
                Protocol?.UpdateConfig();
            }
        }
        public SymbolData? GetSymbol(string name)
        {
            string variable = getVarFromSymbol(name);
            return Sym?.Get(variable);
        }
        public string getVarFromSymbol(string name)
        {
            string variable = name;
            if (name.StartsWith(Data.Name + "."))
            {
                variable = name.Substring(Data.Name.Length + 1);
            }
            return variable;
        }
        public List<string> GetSymbols(string name)
        {
            string variable = string.Empty;
            List<string> l = new List<string>(); 
            if(Sym != null)
            {
                variable = getVarFromSymbol(name);
                l = Sym.List(variable);
            }
            List<string> updatedList = l.Select(s => $"{Data.Name}.{s}").ToList();

            return updatedList;
        }
        public bool isSymbolValid(string name)
        {
            bool valid = false;
            if (Sym != null)
            {
                string variable = getVarFromSymbol(name);
                valid = Sym.isValid(variable);
            }
            return valid;
        }
        public void AddChannel(ChannelCtrl c)
        {
            /* make sure channel data is up to date with symbol file */
            if (updateChannel(c))
            {
                Protocol?.UpdateConfig();
            }
            Channels.Add(c);
            c.protocolToken = 0;
            if((Protocol != null) && IsAcquired(c))
            {
                c.protocolToken = Protocol.ReadMemRegister(c.SignalBase.Address, c.SignalBase.Size);
            }
        }
        public void RemoveChannel(ChannelCtrl c)
        {
            if(Channels.Contains(c))
            {
                Channels.Remove(c);
                if(c.protocolToken != 0)
                {
                    Protocol?.ReadMemUnregister(c.protocolToken);
                    c.protocolToken = 0;
                }
            }
        }
        public void Start()
        {
            recording = true;
            Protocol?.Start();
        }
        public void Stop()
        {
            recording = false;
            Protocol?.Stop();
        }
        public void WriteMem(UInt64 address, int size, UInt64 data)
        {
            Protocol?.WriteMem(address, size, data);
        }
        public void ReadMem(UInt64 address, int size, Action<UInt64> onReadComplete, Action? onReadFailed = null)
        {
            if (Protocol == null)
                onReadFailed?.Invoke();
            else
                Protocol.ReadMem(address, size, onReadComplete, onReadFailed);
        }
        // C expression evaluated by GDB (watch), null when the symbols are not resolved by GDB
        public Task<ExprValue>? EvaluateAsync(string variable)
        {
            return (Sym as SymbolGDB)?.EvaluateAsync(variable);
        }
        public void Process()
        {
            if (processThreadId != Environment.CurrentManagedThreadId)
            {
                processThreadId = Environment.CurrentManagedThreadId;
                (Sym as SymbolGDB)?.SetWaitPump(Pump, processThreadId);
            }
            if (symbolReload)
            {
                symbolReload = false;
                loadSymbols();
            }
            while (processActions.TryDequeue(out var action))
            {
                action();
            }
            Protocol?.Process();
            (Sym as SymbolGDB)?.Bridge?.Service(Protocol);

            // pointers of the channel expressions are read on the target when a recording connects
            // (outside a recording the link opens on demand: the reads of the resolution would reopen it)
            bool recordingConnected = recording && Protocol?.IsConnected == true;
            if (recordingConnected && !wasRecordingConnected)
            {
                ResolveChannelsAsync();
            }
            wasRecordingConnected = recordingConnected;

            // refresh display data once for all the frames received during this tick
            if (bNewSamples)
            {
                bNewSamples = false;
                foreach (var c in Channels)
                {
                    c.Update();
                }
            }
        }
    }
}