namespace eScope.Automation
{
    // devices and symbols
    public partial class AutomationService
    {
        static DeviceInfo ToDeviceInfo(DeviceCtrl d) => new(
            d.Data.Name,
            d.Data.Protocol,
            d.Data.Com,
            d.Data.SymbolType,
            d.Data.SymbolFilePath,
            d.Protocol?.IsConnected == true,
            d.Protocol?.SupportsReadMem == true);

        public Task<List<DeviceInfo>> ListDevices() => ui.InvokeAsync(() =>
            scope.Devices.Select(ToDeviceInfo).ToList());

        public Task<DeviceStatus> GetDeviceStatus(string? device) => ui.InvokeAsync(() =>
        {
            var d = ResolveDevice(device);
            return new DeviceStatus(
                d.Data.Name,
                d.Data.Protocol,
                d.Data.Com,
                d.Data.SymbolType,
                d.Data.SymbolFilePath,
                d.Sym != null,
                d.Protocol?.IsConnected == true,
                d.Protocol?.SupportsReadMem == true,
                d.FrameCount,
                d.ChannelCount);
        });

        // completion: variables, and with GDB the members after '.' / '->'
        public Task<List<string>> SearchSymbols(string? device, string prefix, int limit) => ui.InvokeAsync(() =>
        {
            var d = ResolveDevice(device);
            if (d.Sym == null)
                throw new AutomationException(AutomationError.NotSupported, $"device '{d.Data.Name}': symbols not loaded");
            string devicePrefix = d.Data.Name + ".";
            return d.GetSymbols(prefix)
                .Select(s => s.StartsWith(devicePrefix) ? s[devicePrefix.Length..] : s)
                .Take(Math.Max(1, limit))
                .ToList();
        });

        public Task<SymbolInfo> ResolveSymbol(string? device, string expression) => ui.InvokeAsync(() =>
        {
            var d = ResolveDevice(device);
            if (d.Sym == null)
                throw new AutomationException(AutomationError.NotSupported, $"device '{d.Data.Name}': symbols not loaded");
            var s = d.GetSymbol(expression)
                ?? throw new AutomationException(AutomationError.NotFound, $"'{expression}' not found in the symbols of '{d.Data.Name}'");
            return new SymbolInfo(
                d.Data.Name,
                expression,
                s.Resolved,
                s.Resolved && !string.IsNullOrEmpty(s.Address) ? FormatAddress(Utils.strToAddress(s.Address)) : null,
                s.Size,
                s.Shift,
                string.IsNullOrEmpty(s.Type) ? null : s.Type);
        });
    }
}
