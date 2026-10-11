using System.Diagnostics;
using System.Text;

namespace eScope.Symbol.Gdb
{
    public class MiResult
    {
        public bool Ok;                     // ^done / ^connected
        public string Error = string.Empty; // ^error msg, or timeout / exit reason
        public Dictionary<string, object> Results = new();
        public List<string> Output = new();  // ~ console stream records of the command
        public string OutputText => string.Concat(Output);
    }

    /// <summary>
    /// GDB process driven through the MI interpreter. Commands are serialized and matched
    /// to their result record by token. Usable from any thread: Run() blocks, RunAsync() does not.
    /// </summary>
    public sealed class GdbMi : IDisposable
    {
        readonly Process process;
        readonly SemaphoreSlim busy = new(1, 1);
        readonly object sync = new();
        int nextToken = 1;
        int pendingToken;
        TaskCompletionSource<MiResult>? pending;
        MiResult? pendingResult;
        volatile bool exited;

        // called while Run() waits on the thread PumpThreadId: keeps alive what the
        // command may wait for (target memory read through the device protocol)
        public Action? WaitPump { get; set; }
        public int PumpThreadId { get; set; } = -1;

        public bool HasExited => exited;

        public GdbMi(string gdbPath, string elfPath)
        {
            process = new Process();
            process.StartInfo.FileName = gdbPath;
            process.StartInfo.Arguments = $"-q -nx --interpreter=mi2 \"{elfPath}\"";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardInput = true;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.StandardOutputEncoding = Encoding.UTF8;
            process.Start();
            process.StandardInput.AutoFlush = true;

            new Thread(ReadOutput) { IsBackground = true, Name = "gdb-mi" }.Start();
            process.ErrorDataReceived += (s, e) => { if (e.Data != null) Console.WriteLine($"gdb: {e.Data}"); };
            process.BeginErrorReadLine();
        }

        void ReadOutput()
        {
            try
            {
                string? line;
                while ((line = process.StandardOutput.ReadLine()) != null)
                {
                    ProcessLine(line);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("GdbMi read error: " + ex.Message);
            }
            exited = true;
            Complete(new MiResult { Error = "gdb exited" });
        }

        void ProcessLine(string line)
        {
            if (line.Length == 0 || line.StartsWith("(gdb)"))
                return;

            // optional token then record type
            int i = 0;
            while (i < line.Length && char.IsDigit(line[i])) i++;
            int token = (i > 0) && int.TryParse(line.AsSpan(0, i), out int t) ? t : 0;
            if (i >= line.Length) return;
            char type = line[i];
            string rest = line.Substring(i + 1);

            switch (type)
            {
                case '~': // console stream
                    lock (sync) { pendingResult?.Output.Add(MiParser.Unescape(rest)); }
                    break;
                case '^': // result record
                    {
                        int comma = rest.IndexOf(',');
                        string cls = comma < 0 ? rest : rest.Substring(0, comma);
                        int idx = comma < 0 ? rest.Length : comma + 1;
                        var results = MiParser.ParseResults(rest, ref idx);
                        MiResult r;
                        lock (sync)
                        {
                            if (token != pendingToken || pendingResult == null)
                                return; // answer of a timed out command
                            r = pendingResult;
                        }
                        r.Results = results;
                        r.Ok = cls == "done" || cls == "connected" || cls == "running";
                        if (!r.Ok)
                            r.Error = results.GetString("msg") ?? cls;
                        Complete(r);
                        break;
                    }
                default: // '&' log, '=' '*' '+' async records: not used
                    break;
            }
        }

        void Complete(MiResult r)
        {
            TaskCompletionSource<MiResult>? tcs;
            lock (sync)
            {
                tcs = pending;
                pending = null;
                pendingResult = null;
                pendingToken = 0;
            }
            tcs?.TrySetResult(r);
        }

        public async Task<MiResult> RunAsync(string command, int timeoutMs = 5000)
        {
            if (exited)
                return new MiResult { Error = "gdb exited" };

            if (!await busy.WaitAsync(timeoutMs).ConfigureAwait(false))
                return new MiResult { Error = "gdb busy" };
            try
            {
                var tcs = new TaskCompletionSource<MiResult>(TaskCreationOptions.RunContinuationsAsynchronously);
                int token;
                lock (sync)
                {
                    token = nextToken++;
                    pendingToken = token;
                    pending = tcs;
                    pendingResult = new MiResult();
                }
                try
                {
                    await process.StandardInput.WriteLineAsync($"{token}{command}").ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Complete(new MiResult { Error = ex.Message });
                }
                if (await Task.WhenAny(tcs.Task, Task.Delay(timeoutMs)).ConfigureAwait(false) != tcs.Task)
                {
                    lock (sync)
                    {
                        if (pendingToken == token) { pending = null; pendingResult = null; pendingToken = 0; }
                    }
                    return new MiResult { Error = $"gdb timeout: {command}" };
                }
                return await tcs.Task.ConfigureAwait(false);
            }
            finally
            {
                busy.Release();
            }
        }

        public MiResult Run(string command, int timeoutMs = 5000)
        {
            Task<MiResult> t = RunAsync(command, timeoutMs);
            if ((WaitPump != null) && (Environment.CurrentManagedThreadId == PumpThreadId))
            {
                while (!t.Wait(5))
                    WaitPump();
            }
            return t.GetAwaiter().GetResult();
        }

        // CLI command, output collected from the console stream
        public MiResult Cli(string cliCommand, int timeoutMs = 5000)
            => Run($"-interpreter-exec console {MiParser.Quote(cliCommand)}", timeoutMs);

        public Task<MiResult> EvaluateAsync(string expression, int timeoutMs = 5000)
            => RunAsync($"-data-evaluate-expression {MiParser.Quote(expression)}", timeoutMs);

        public MiResult Evaluate(string expression, int timeoutMs = 5000)
            => Run($"-data-evaluate-expression {MiParser.Quote(expression)}", timeoutMs);

        public void Dispose()
        {
            try
            {
                if (!process.HasExited)
                {
                    process.StandardInput.WriteLine("-gdb-exit");
                    if (!process.WaitForExit(500))
                        process.Kill();
                }
            }
            catch { }
            process.Dispose();
        }
    }
}
