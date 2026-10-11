using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace eScope.Automation.Tools
{
    // one-shot reads / writes of the target memory (protocol V3, while recording)
    [McpServerToolType]
    public class TargetTools
    {
        const string LinkHelp =
            " The link to the target is open only while recording (start_recording / acquire): NOT_CONNECTED otherwise. " +
            "One read at a time on the link, about one per 50 ms.";
        const string DeviceArg = "Device name, optional when the workspace has a single device.";
        const string DisplayArg = "Interpretation: 'signed', 'unsigned', 'hex', 'float' (32 bits) or 'double' (64 bits); " +
                                  "deduced from the C type with GDB symbols, 'signed' with a map.";
        const string SizeArg = "Size in bits (8, 16, 32, 64), only for a map symbol of unknown size (default 32).";

        readonly AutomationService svc;

        public TargetTools(AutomationService svc) => this.svc = svc;

        [McpServerTool(Name = "read_variable", ReadOnly = true, Idempotent = false, OpenWorld = true)]
        [Description("Reads the current value of a variable on the target. With GDB symbols any C expression is " +
                     "accepted and evaluated now (pointers followed on the target): 'ctx->cfg->gain', 'tab[3].x', " +
                     "'*(uint16_t*)0x20000010', 'a * 2 + b'. An lvalue up to 64 bits returns value, raw and hex; " +
                     "other expressions (arithmetic, larger structs) return the text computed by GDB." + LinkHelp)]
        public Task<VariableValue> ReadVariable(
            [Description("Variable name or C expression.")] string expression,
            [Description(DeviceArg)] string? device = null,
            [Description(DisplayArg)] string? display = null,
            [Description(SizeArg)] int? size_bits = null)
            => svc.ReadVariable(device, expression, size_bits, display);

        [McpServerTool(Name = "read_variables", ReadOnly = true, Idempotent = false, OpenWorld = true)]
        [Description("Reads several variables, one after the other (see read_variable); an expression that fails " +
                     "gets an error field instead of stopping the others." + LinkHelp)]
        public Task<List<VariableValue>> ReadVariables(
            [Description("Variable names or C expressions.")] string[] expressions,
            [Description(DeviceArg)] string? device = null)
            => svc.ReadVariables(device, expressions);

        [McpServerTool(Name = "write_variable", ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = true)]
        [Description("Writes a variable of the running target (lvalue up to 64 bits) and returns the value read back. " +
                     "Value checked against the type (range, integer / float). Needs 'Allow writing target memory' in " +
                     "eScope > Tools > AI automation (WRITE_DISABLED otherwise). Not undoable: the target state changes." + LinkHelp)]
        public Task<VariableValue> WriteVariable(
            [Description("Variable name or C expression (lvalue).")] string expression,
            [Description("New value: number, or string for hexadecimal (\"0x1F\").")] JsonElement value,
            [Description(DeviceArg)] string? device = null,
            [Description(DisplayArg)] string? display = null,
            [Description(SizeArg)] int? size_bits = null)
            => svc.WriteVariable(device, expression, value, size_bits, display);

        [McpServerTool(Name = "read_memory", ReadOnly = true, Idempotent = false, OpenWorld = true)]
        [Description("Reads 8, 16, 32 or 64 bits at a target address (raw value, little-endian)." + LinkHelp)]
        public Task<VariableValue> ReadMemory(
            [Description("Address: number or string \"0x20000010\".")] JsonElement address,
            [Description("Size in bits: 8, 16, 32 or 64.")] int size_bits,
            [Description(DeviceArg)] string? device = null)
            => svc.ReadMemory(device, address, size_bits);

        [McpServerTool(Name = "write_memory", ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = true)]
        [Description("Writes 8, 16, 32 or 64 bits at a target address and returns the value read back. Needs 'Allow " +
                     "writing target memory' (WRITE_DISABLED otherwise). Not undoable." + LinkHelp)]
        public Task<VariableValue> WriteMemory(
            [Description("Address: number or string \"0x20000010\".")] JsonElement address,
            [Description("Size in bits: 8, 16, 32 or 64.")] int size_bits,
            [Description("Value: number (negative allowed, two's complement), or string for hexadecimal (\"0x1F\").")] JsonElement value,
            [Description(DeviceArg)] string? device = null)
            => svc.WriteMemory(device, address, size_bits, value);
    }
}
