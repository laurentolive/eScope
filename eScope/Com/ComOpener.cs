using System.Diagnostics;

namespace eScope.Com
{
    // opens a com in background: Process() runs on the UI thread and must never wait for
    // a connection (TCP host absent, RTT bridge starting); a failed attempt is retried after RetryDelayMs
    public class ComOpener
    {
        public enum Result
        {
            Idle,    // nothing in progress (retry delay running after a failure)
            Pending, // connection in progress
            Opened,
            Failed
        }
        public const int RetryDelayMs = 1000;

        readonly ICom com;
        Task<bool>? task;
        readonly Stopwatch sinceFailure = new();

        public ComOpener(ICom com)
        {
            this.com = com;
        }

        public bool IsPending => task != null;
        // last attempt failed less than RetryDelayMs ago
        public bool InRetryDelay => sinceFailure.IsRunning && sinceFailure.ElapsedMilliseconds < RetryDelayMs;

        // starts an attempt if none is running (and the retry delay is over), then reports its state
        public Result Poll()
        {
            if (task == null)
            {
                if (InRetryDelay)
                    return Result.Idle;
                task = Task.Run(() =>
                {
                    try { return com.Open(); }
                    catch (Exception ex) { Console.WriteLine($"ComOpener: {ex.Message}"); return false; }
                });
            }
            if (!task.IsCompleted)
                return Result.Pending;

            bool opened = task.Result;
            task = null;
            if (opened)
            {
                sinceFailure.Reset();
                return Result.Opened;
            }
            sinceFailure.Restart();
            return Result.Failed;
        }
        // new start request: try again at once
        public void ResetRetryDelay() => sinceFailure.Reset();

        // the com is disposed once the attempt in progress is over
        public void Dispose()
        {
            if (task != null)
                task.ContinueWith(_ => com.Dispose());
            else
                com.Dispose();
            task = null;
        }
    }
}
