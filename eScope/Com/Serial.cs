using System;
using System.IO.Ports;

namespace eScope.Com
{
    public class ProtocolV1: ICom
    {
        private SerialPort s;

        public ProtocolV1()
        {
            s = new SerialPort();
        }

        public void Open(string model)
        {
            string port = model.Split(' ')[1];

            // Configure serial port
            s.PortName = port;
            s.BaudRate = 57600;
            s.Parity = Parity.None;
            s.DataBits = 8;
            s.StopBits = StopBits.Zero;
            s.Handshake = Handshake.None;
            s.ReadTimeout = 500;
            s.WriteTimeout = 500;

            s.Open();
        }

        public void Close()
        {
            s.Close();
        }

        public int Read(char[] d, int count)
        {
            return s.Read(d, count);
        }

        public void Write(char[] d)
        {
            s.Write(d, 0, d.Length);
        }
    }
}
