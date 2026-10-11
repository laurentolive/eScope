using Serilog;

namespace eScope.Automation
{
    // Owns the settings and the server: started / stopped / restarted when the settings change
    public sealed class AutomationManager : IDisposable
    {
        readonly ToolRegistry registry;
        AutomationHost? host;

        public AutomationSettings Settings { get; private set; } = AutomationSettings.Load();
        public bool IsRunning => host != null;
        public string? Error { get; private set; } // last start failure (port in use...)
        public string McpUrl => $"http://localhost:{Settings.Port}/mcp";

        public string? LastTool { get; private set; }
        public DateTime LastCall { get; private set; }

        // server started / stopped / failed, or tool called: raised on any thread
        public event Action? StateChanged;

        public AutomationManager(AutomationService svc)
        {
            registry = new ToolRegistry(svc);
            svc.AllowWrite = () => Settings.AllowWrite;
        }

        public void Start()
        {
            Stop();
            Error = null;
            if (Settings.Enabled)
            {
                Settings.EnsureToken();
                var h = new AutomationHost(registry, Settings.Port, Settings.Token);
                h.ToolCalled += OnToolCalled;
                try
                {
                    h.Start();
                    host = h;
                }
                catch (Exception ex)
                {
                    h.Dispose();
                    Error = ex.Message;
                    Log.Warning(ex, "Automation server cannot listen on port {Port}", Settings.Port);
                }
            }
            StateChanged?.Invoke();
        }

        public void Stop()
        {
            if (host == null) return;
            host.ToolCalled -= OnToolCalled;
            host.Dispose();
            host = null;
            StateChanged?.Invoke();
        }

        // new settings saved and applied
        public void Apply(AutomationSettings settings)
        {
            if (settings.Enabled) settings.EnsureToken();
            settings.Save();
            Settings = settings;
            Start();
        }

        void OnToolCalled(string name)
        {
            LastTool = name;
            LastCall = DateTime.Now;
            StateChanged?.Invoke();
        }

        public void Dispose() => Stop();
    }
}
