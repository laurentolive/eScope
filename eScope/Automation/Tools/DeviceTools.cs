using ModelContextProtocol.Server;
using System.ComponentModel;

namespace eScope.Automation.Tools
{
    [McpServerToolType]
    public class DeviceTools
    {
        readonly AutomationService svc;

        public DeviceTools(AutomationService svc) => this.svc = svc;

        [McpServerTool(Name = "list_devices", ReadOnly = true, Idempotent = true, OpenWorld = false)]
        [Description("Devices (target microcontrollers) of the workspace: protocol, transport, symbol file, connection.")]
        public Task<List<DeviceInfo>> ListDevices() => svc.ListDevices();

        [McpServerTool(Name = "device_status", ReadOnly = true, Idempotent = true, OpenWorld = false)]
        [Description("Status of a device: symbols loaded, connected, one-shot memory reads supported (protocol V3), " +
                     "periodic frames received, channels registered for acquisition.")]
        public Task<DeviceStatus> DeviceStatus(
            [Description("Device name, optional when the workspace has a single device.")] string? device = null)
            => svc.GetDeviceStatus(device);

        [McpServerTool(Name = "search_symbols", ReadOnly = true, Idempotent = true, OpenWorld = false)]
        [Description("Symbol completion: target variables starting with the prefix. With GDB symbols (ELF), " +
                     "a prefix ending with '.' or '->' lists the members of a struct (e.g. 'ctx.cfg.').")]
        public Task<List<string>> SearchSymbols(
            [Description("Beginning of the variable name or C expression.")] string prefix,
            [Description("Device name, optional when the workspace has a single device.")] string? device = null,
            [Description("Maximum number of results.")] int limit = 100)
            => svc.SearchSymbols(device, prefix, limit);

        [McpServerTool(Name = "resolve_symbol", ReadOnly = true, Idempotent = true, OpenWorld = false)]
        [Description("Address, size and type of a variable or C expression (GDB symbols: any lvalue, e.g. " +
                     "'tab[3].x', 'uart->Instance->BRR'). resolved = false when the address depends on a pointer " +
                     "that cannot be read now (device not connected).")]
        public Task<SymbolInfo> ResolveSymbol(
            [Description("Variable name or C expression.")] string expression,
            [Description("Device name, optional when the workspace has a single device.")] string? device = null)
            => svc.ResolveSymbol(device, expression);
    }
}
