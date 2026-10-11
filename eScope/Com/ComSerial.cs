using RJCP.IO.Ports;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Data;
using System.IO.Pipelines;
using System.Linq;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
namespace eScope.Com
{
    public class ComSerial: ICom, IDisposable
    {
        public string Name { get; set; }
        public int Baudrate { get; set; }
        public int BitNb{ get; set; }
        public Parity Parity { get; set; }
        public StopBits StopBitsNb { get; set; }
        public int Timeout { get; set; }
        SerialPortStream? sp;
        StreamWriter log;

        public ComSerial(string com)
        {
            string[] comPrm = com.Split(',');
            try 
            {
                Name = comPrm[0];
                Baudrate = int.Parse(comPrm[1]);
                BitNb = int.Parse(comPrm[2]);
                if (comPrm[3] == "N")
                {
                    Parity = Parity.None;
                }
                else if (comPrm[3] == "E")
                {
                    Parity = Parity.Even;
                }
                else if (comPrm[3] == "O")
                {
                    Parity = Parity.Odd;
                }
                else if (comPrm[4] == "1")
                {
                    StopBitsNb = StopBits.One;
                }
                else if (comPrm[5] == "2")
                {
                    StopBitsNb = StopBits.Two;
                }

                Timeout = int.Parse(comPrm[5]);
            }
            catch (Exception ex)
            {
                LogMsg.Info("Invalid cfg: " + com + ex.ToString());
            }
        }
        ~ComSerial()
        {
            Console.WriteLine("~ in ComSerial instance called");
        }
        public void Dispose()
        {
            try
            {
                if (sp != null)
                {
                    if (sp.IsOpen == true)
                    {
                        sp.Close();
                    }
                    sp.Dispose();
                }
                log?.Close();
                sp = null;
            }
            catch (Exception ex)
            {
                LogMsg.Info(ex.Message);
            }
        }

        public bool Open()
        {
            try
            {
                int bytesPerMs = Baudrate / 10 / 1000;
                int targetFlushMs = 20;
                int readBufferSize = Math.Max(256, bytesPerMs * targetFlushMs);

                sp = new SerialPortStream(Name, Baudrate)
                {
                    ReadBufferSize = 16,
                    WriteBufferSize = 4096,
                    Parity = Parity,
                    StopBits = StopBitsNb,
                    DataBits = 8,
                    Handshake = Handshake.None,
                    ReadTimeout = 10,   //  flush partial buffer after 20ms of inactivity
                    WriteTimeout = 500,
                };
                sp.ErrorReceived += (s, e) =>
                {
                    Console.WriteLine($"ComSerial exception: {e.EventType.ToString()}");
                };
                if (!sp.IsOpen)
                {

                    sp?.Open();
                    Console.WriteLine($"ComSerial Open {Name} {Baudrate} {Parity} {BitNb} {StopBitsNb}");
                }
            }
            catch (Exception ex)
            {
                LogMsg.Info(ex.Message);
                Console.WriteLine($"     exception: {ex.Message}");
                return false;
            }

            try
            {
                string p = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"log{Name}.txt");
                log = new StreamWriter(p);
            } 
            catch (Exception ex)
            {
                LogMsg.Info(ex.Message);
            }
            return true;
        }
        public void Flush()
        {
            try
            {
                if ((sp != null) && (sp.IsOpen == true))
                {
                    sp.DiscardInBuffer();
                }
            }
            catch (Exception ex)
            {
                LogMsg.Info(ex.Message);
            }
        }
        public void Close()
        {
            try
            {
                sp?.Close();
                log?.Close();

                Console.WriteLine($"ComSerial Close {Name}");
            }
            catch (Exception ex)
            {
                LogMsg.Info(ex.Message);
            }
            sp = null;
        }

        public byte[] Read(int sizeNb)
        {
            try
            {
                if ((sp != null) && (sp.IsOpen == true))
                {
                    if (sizeNb < sp.BytesToRead)
                    {
                        byte[] buffer = new byte[sizeNb];
                        sp?.Read(buffer, 0, sizeNb);
                        //log?.WriteLine(BitConverter.ToString(buffer).Replace("-", ""));
                        //log?.Flush();
                        return buffer;
                    }
                }
            }
            catch (Exception ex)
            {
                LogMsg.Info(ex.Message);
            }

            return Array.Empty<byte>();
        }
        public async Task<byte[]> ReadAsync(CancellationToken token)
        {
            if (sp == null) return Array.Empty<byte>();

            try
            {
                var tcs = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);

                void onData(object? s, SerialDataReceivedEventArgs e)
                    => tcs.TrySetResult(true);

                using var reg = token.Register(() => tcs.TrySetCanceled());

                sp.DataReceived += onData;

                // check AFTER subscribing to avoid race condition
                if (sp.BytesToRead > 0)
                    tcs.TrySetResult(true);

                try
                {
                    await tcs.Task;
                }
                finally
                {

                    if(sp != null)
                    {
                        sp.DataReceived -= onData; // always unsubscribe
                    }
                }

                int sizeNb = sp.BytesToRead;
                if (sizeNb == 0) return Array.Empty<byte>();

                byte[] buffer = new byte[sizeNb];
                int bytesRead = sp.Read(buffer, 0, sizeNb); // sync read, data is ready
                return buffer[..bytesRead];
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                Console.WriteLine($"ReadAsync error: {ex.Message}");
                return Array.Empty<byte>();
            }
        }
        public byte[] ReadAll()
        {
            try
            {
                if (sp != null)
                {
                    int sizeNb = sp.BytesToRead;
                    byte[] buffer = new byte[sizeNb];
                    sp?.Read(buffer, 0, sizeNb);
                    //log?.WriteLine(BitConverter.ToString(buffer).Replace("-", ""));
                    //log?.Flush();
                    return buffer;
                }
            }
            catch (Exception ex)
            {
                LogMsg.Info(ex.Message);
            }

            return Array.Empty<byte>();
        }

        public void Write(byte[] data)
        {
            try
            {
                sp?.Write(data, 0, data.Length);
            }
            catch (Exception ex)
            {
                LogMsg.Info(ex.Message);
                sp = null;
            }
        }
    }
}
