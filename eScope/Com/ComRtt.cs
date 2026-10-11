using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace eScope.Com
{
    public class ComRtt : ICom
    {
        private readonly string _target;
        private readonly string _probeUid;
        private readonly string _host;
        private readonly int _port;
        private readonly string _cbAddr;

        private TcpClient? _client;
        private NetworkStream? _stream;
        private Process? _bridge;

        private readonly byte[] _rxBuf = new byte[4096];
        private int _rxHead, _rxTail;
        private readonly object _rxLock = new();
        private readonly SemaphoreSlim _rxReady = new(0, 1);
        private CancellationTokenSource? _cts;

        public ComRtt(string target, string probeUid, string host = "127.0.0.1", int port = 9090, string cbAddr = "")
        {
            _target   = target;
            _probeUid = probeUid;
            _host     = host;
            _port     = port;
            _cbAddr   = cbAddr;
        }

        private bool IsLocalHost => _host is "127.0.0.1" or "localhost" or "::1";

        public bool Open()
        {
            try
            {
                if (IsLocalHost)
                {
                    _bridge = StartBridge();
                    if (_bridge == null) return false;

                    // Block until bridge prints its ready line
                    string? line;
                    while ((line = _bridge.StandardOutput.ReadLine()) != null)
                    {
                        if (line.Contains("Listening on")) break;
                    }
                }

                _client = new TcpClient();
                _client.Connect(_host, _port);
                _stream = _client.GetStream();
                _stream.ReadTimeout = 10;
                _stream.WriteTimeout = 500;

                _cts = new CancellationTokenSource();
                new Thread(ReadLoop) { IsBackground = true, Name = "ComRtt-Reader" }.Start();

                Console.WriteLine($"ComRtt connected (target={_target})");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ComRtt.Open failed: {ex.Message}");
                CloseAll();
                return false;
            }
        }

        public void Close() => CloseAll();
        public void Dispose() => CloseAll();

        public byte[] Read(int sizeNb)
        {
            lock (_rxLock)
            {
                if (Available() < sizeNb) return Array.Empty<byte>();
                return Drain(sizeNb);
            }
        }

        public byte[] ReadAll()
        {
            lock (_rxLock) { return Drain(Available()); }
        }

        public async Task<byte[]> ReadAsync(CancellationToken token)
        {
            while (true)
            {
                token.ThrowIfCancellationRequested();
                lock (_rxLock) { if (Available() > 0) return Drain(Available()); }
                try { await _rxReady.WaitAsync(token); }
                catch (OperationCanceledException) { return Array.Empty<byte>(); }
            }
        }

        public void Write(byte[] data)
        {
            try { _stream?.Write(data, 0, data.Length); }
            catch (Exception ex) { Console.WriteLine($"ComRtt.Write failed: {ex.Message}"); }
        }

        public void Flush()
        {
            lock (_rxLock) { _rxHead = _rxTail = 0; }
        }

        // ── Internals ───────────────────────────────────────────────────────

        private void ReadLoop()
        {
            var tmp = new byte[4096];
            while (_cts?.IsCancellationRequested == false)
            {
                try
                {
                    int n = _stream!.Read(tmp, 0, tmp.Length);
                    if (n > 0)
                    {
                        Append(tmp, n);
                    }
                    else
                    {
                        // 0 bytes with no exception means the remote closed gracefully (EOF)
                        Console.WriteLine("ComRtt: connection closed by remote");
                        break;
                    }
                }
                catch (IOException ex) when (ex.InnerException is SocketException { SocketErrorCode: SocketError.TimedOut })
                {
                    // ReadTimeout — normal polling behaviour
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ComRtt: connection lost: {ex.Message}");
                    break;
                }
            }
        }

        private void Append(byte[] src, int n)
        {
            lock (_rxLock)
            {
                for (int i = 0; i < n; i++)
                {
                    _rxBuf[_rxTail] = src[i];
                    _rxTail = (_rxTail + 1) % _rxBuf.Length;
                    if (_rxTail == _rxHead)
                        _rxHead = (_rxHead + 1) % _rxBuf.Length; // overwrite oldest on overflow
                }
            }
            if (_rxReady.CurrentCount == 0) _rxReady.Release();
        }

        private int Available() => (_rxTail - _rxHead + _rxBuf.Length) % _rxBuf.Length;

        private byte[] Drain(int n)
        {
            var result = new byte[n];
            for (int i = 0; i < n; i++)
            {
                result[i] = _rxBuf[_rxHead];
                _rxHead = (_rxHead + 1) % _rxBuf.Length;
            }
            return result;
        }

        private void CloseAll()
        {
            _cts?.Cancel();
            try { _stream?.Close(); } catch { }
            try { _client?.Close(); } catch { }
            _stream = null;
            _client = null;
            try { if (_bridge != null && !_bridge.HasExited) _bridge.Kill(); } catch { }
            _bridge = null;
        }

        private Process? StartBridge()
        {
            string script = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "bridge_TcpRtt.py");
            if (!File.Exists(script))
            {
                Console.WriteLine($"ComRtt: bridge script not found at {script}");
                return null;
            }

            string args = $"\"{script}\" --target {_target} --host {_host} --port {_port}";
            if (!string.IsNullOrWhiteSpace(_probeUid))
                args += $" --probe {_probeUid}";
            if (!string.IsNullOrWhiteSpace(_cbAddr))
                args += $" --cb-addr 0x{_cbAddr}";

            var psi = new ProcessStartInfo(PythonLauncher.Executable, args)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = false,
            };

            var proc = Process.Start(psi);
            if (proc == null) Console.WriteLine("ComRtt: failed to start bridge process");
            return proc;
        }
    }
}
