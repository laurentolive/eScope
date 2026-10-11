using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace eScope.Com
{
    public class ComTcp : ICom
    {
        public string Address { get; set; }
        public int Port { get; set; }
        public int Timeout { get; set; }
        public int ConnectTimeoutMs { get; set; } = 2000;

        TcpClient? client;
        NetworkStream? stream;

        public ComTcp(string com)
        {
            string[] comPrm = com.Split(',');
            try
            {
                Address = comPrm[0];
                Port = int.Parse(comPrm[1]);
            }
            catch (Exception ex)
            {
                LogMsg.Info("Invalid cfg: " + com + ex.ToString());
            }
        }
        ~ComTcp()
        {
            Console.WriteLine("~ in ComTcp instance called");
        }
        public void Dispose()
        {
            try
            {
                stream?.Close();
                client?.Close();
                stream = null;
                client = null;
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
                // opened in background (ComOpener): bounded wait so that a stop is not delayed by an absent host
                client = new TcpClient();
                if (!client.ConnectAsync(Address, Port).Wait(ConnectTimeoutMs))
                    throw new TimeoutException($"no answer after {ConnectTimeoutMs} ms");
                stream = client.GetStream();
                stream.ReadTimeout = 10;
                stream.WriteTimeout = 500;
                Console.WriteLine($"ComTcp Open {Address}:{Port}");
            }
            catch (Exception ex)
            {
                string msg = (ex as AggregateException)?.InnerException?.Message ?? ex.Message;
                LogMsg.Info(msg);
                Console.WriteLine($"ComTcp Open {Address}:{Port} failed: {msg}");
                client?.Close();
                client = null;
                stream = null;
                return false;
            }
            return true;
        }
        public void Flush()
        {
            try
            {
                if ((client != null) && (client.Connected) && (stream != null))
                {
                    while (client.Available > 0)
                    {
                        byte[] buffer = new byte[client.Available];
                        stream.Read(buffer, 0, buffer.Length);
                    }
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
                stream?.Close();
                client?.Close();

                Console.WriteLine($"ComTcp Close {Address}:{Port}");
            }
            catch (Exception ex)
            {
                LogMsg.Info(ex.Message);
            }
            stream = null;
            client = null;
        }

        public byte[] Read(int sizeNb)
        {
            try
            {
                if ((client != null) && (client.Connected) && (stream != null))
                {
                    if (sizeNb <= client.Available)
                    {
                        byte[] buffer = new byte[sizeNb];
                        stream.Read(buffer, 0, sizeNb);
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
            if (stream == null) return Array.Empty<byte>();

            try
            {
                byte[] buffer = new byte[4096];
                int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, token);
                if (bytesRead == 0) return Array.Empty<byte>();
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
                if ((client != null) && (stream != null))
                {
                    int sizeNb = client.Available;
                    byte[] buffer = new byte[sizeNb];
                    if (sizeNb > 0)
                        stream.Read(buffer, 0, sizeNb);
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
                stream?.Write(data, 0, data.Length);
            }
            catch (Exception ex)
            {
                LogMsg.Info(ex.Message);
                stream = null;
            }
        }
    }
}
