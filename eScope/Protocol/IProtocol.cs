using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using eScope.Com;
using eScope.Protocol;
using System.Threading.Channels;
using eScope.Plot;

namespace eScope.Protocol
{
    public interface IProtocol
    {
        void Process();
        void Start();
        void Stop();
        void UpdateConfig();
        void Dispose();
        void WriteMem(UInt32 address, int size, UInt64 data);
        // one shot read: onReadComplete or onReadFailed (not connected, no answer) is called on Process() thread
        void ReadMem(UInt32 address, int size, Action<UInt64> onReadComplete, Action? onReadFailed = null);
        // one shot read available (V3 only)
        bool SupportsReadMem => false;
        // communication with the device established
        bool IsConnected { get; }
        // one-shot accesses coming (V3): opens the link if closed; outside a recording it closes again
        // after a while without one-shot access
        void RequestLink() { }
        // periodic read: values are delivered through FrameComplete, keyed by the returned token
        uint ReadMemRegister(UInt32 address, int size);
        void ReadMemUnregister(uint token);
        // raised once per received periodic frame: frames lost just before it (flow gap), token -> value
        // a token missing from a truncated frame is absent
        event Action<int, IReadOnlyDictionary<uint, UInt64>>? FrameComplete;
        // raised (on Process() thread) when no frame was received for a while
        event Action? Timeout;

        static public IProtocol? New(String protocol, string com)
        {
            if (protocol.Equals("V1"))
                return new ProtocolV1(com);
            else if (protocol.Equals("V2"))
                return new ProtocolV2(com);
            else if (protocol.Equals("V3"))
                return new ProtocolV3(com);
            else
                return null;
        }
    }
}
