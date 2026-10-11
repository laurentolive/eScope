using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO.Ports;
using eScope.Protocol;
using System.Runtime.CompilerServices;

namespace eScope.Com
{
    public interface ICom
    {
        bool Open();
        void Close();
        byte [] Read(int byteNb);
        byte[] ReadAll();
        void Write(byte[] data);
        void Flush();
        void Dispose();
        public Task<byte[]> ReadAsync(CancellationToken token);
        static public ICom? Get(string com)
        {
            string[] comPrm = com.Split(':');
            if (comPrm[0].Equals("Serial"))
                return new ComSerial(comPrm[1]);
            else if (comPrm[0].Equals("TCP"))
                return new ComTcp(comPrm[1]);
            else if (comPrm[0].Equals("RTT"))
            {
                string target   = comPrm.Length > 1 ? comPrm[1] : "";
                string probeUid = comPrm.Length > 2 ? comPrm[2] : "";
                string host     = comPrm.Length > 3 ? comPrm[3] : "127.0.0.1";
                int    port     = comPrm.Length > 4 && int.TryParse(comPrm[4], out int p) ? p : 9090;
                string cbAddr   = comPrm.Length > 5 ? comPrm[5] : "";
                return new ComRtt(target, probeUid, host, port, cbAddr);
            }
            else
                return null;
        }

    }
}
