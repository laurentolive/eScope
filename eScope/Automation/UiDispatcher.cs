namespace eScope.Automation
{
    // Runs a function on the UI thread, where the whole model lives (devices, traces, commandMgr).
    // The only WinForms dependency of Automation/: to be replaced by the Avalonia dispatcher on the Linux port.
    public interface IUiDispatcher
    {
        Task<T> InvokeAsync<T>(Func<T> func);
    }

    public class WinFormsDispatcher : IUiDispatcher
    {
        readonly Control control;

        public WinFormsDispatcher(Control control) => this.control = control;

        public Task<T> InvokeAsync<T>(Func<T> func)
        {
            if (!control.InvokeRequired)
                return Task.FromResult(func());

            var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            try
            {
                control.BeginInvoke(() =>
                {
                    try { tcs.SetResult(func()); }
                    catch (Exception ex) { tcs.SetException(ex); }
                });
            }
            catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException)
            {
                // handle not created yet or window destroyed
                tcs.SetException(new AutomationException(AutomationError.Unavailable, "eScope is not available"));
            }
            return tcs.Task;
        }
    }
}
