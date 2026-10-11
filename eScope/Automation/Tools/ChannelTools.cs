using ModelContextProtocol.Server;
using System.ComponentModel;

namespace eScope.Automation.Tools
{
    [McpServerToolType]
    public class ChannelTools
    {
        const string DisplayArg = "Display format: 'signed', 'unsigned', 'hex' or 'float' (32 bits).";
        const string ScriptHelp =
            "C# method body evaluated for each sample index i, returning a double. Y[i] is this channel, " +
            "data[\"name\"][i] another channel of the trace; System.Math is available. " +
            "Example: return (data[\"current_a\"][i] + data[\"current_b\"][i]) / 2;";
        const string PeriodArg =
            "Sampling period in ms of the computed channel (default 2). data[\"x\"][i] is sample i of x, read by " +
            "index: give the period of the channels read, else the computed curve is shifted in time.";

        readonly AutomationService svc;

        public ChannelTools(AutomationService svc) => this.svc = svc;

        [McpServerTool(Name = "add_channel", ReadOnly = false, Idempotent = false, OpenWorld = false)]
        [Description("Adds a channel plotting a target variable (undoable). With GDB symbols (ELF) any C lvalue is " +
                     "accepted (e.g. 'ctx.cfg.gain', 'tab[3].x', 'uart->Instance->BRR') and the size and display " +
                     "format are deduced from its C type (struct / array: give a member or an element). With a map " +
                     "file the size comes from the map when known, else give size_bits. Samples are acquired while recording.")]
        public Task<ChannelInfo> AddChannel(
            [Description("Variable name or C expression.")] string symbol,
            [Description("Device name, optional when the workspace has a single device.")] string? device = null,
            [Description(TraceTools.TraceArg)] string? trace = null,
            [Description("Channel name, the expression when omitted.")] string? name = null,
            [Description("Unit shown in the legend.")] string? unit = null,
            [Description("Color '#RRGGBB', next palette color when omitted.")] string? color = null,
            [Description("Size in bits (1 to 64), overrides the deduced size.")] int? size_bits = null,
            [Description(DisplayArg + " Overrides the deduced format.")] string? display = null)
            => svc.AddChannel(trace, device, symbol, name, unit, color, size_bits, display);

        [McpServerTool(Name = "add_computed_channel", ReadOnly = false, Idempotent = false, OpenWorld = false)]
        [Description("Adds a channel computed from the other channels of the trace by a C# script (undoable): " +
                     "filtering, conversion, combination. " + ScriptHelp)]
        public Task<ChannelInfo> AddComputedChannel(
            [Description("Channel name, used by other scripts as data[\"name\"].")] string name,
            [Description("Script. " + ScriptHelp)] string script,
            [Description(TraceTools.TraceArg)] string? trace = null,
            [Description("Unit shown in the legend.")] string? unit = null,
            [Description("Color '#RRGGBB'.")] string? color = null,
            [Description(PeriodArg)] double? period_ms = null)
            => svc.AddComputedChannel(trace, name, script, unit, color, period_ms);

        [McpServerTool(Name = "configure_channel", ReadOnly = false, Idempotent = true, OpenWorld = false)]
        [Description("Changes settings of a channel, only the given ones (undoable). A script on a device channel " +
                     "transforms its values (Y[i] = converted target value); an empty script removes it.")]
        public Task<ChannelInfo> ConfigureChannel(
            [Description("Channel name.")] string channel,
            [Description(TraceTools.TraceArg)] string? trace = null,
            [Description("New name.")] string? name = null,
            [Description("Unit.")] string? unit = null,
            [Description("Color '#RRGGBB'.")] string? color = null,
            [Description("Shown on the plot.")] bool? visible = null,
            [Description(DisplayArg)] string? display = null,
            [Description("Size in bits (1 to 64).")] int? size_bits = null,
            [Description("Vertical scale: channel units per division.")] double? y_gain = null,
            [Description("Vertical offset in channel units.")] double? y_offset = null,
            [Description("Horizontal offset in ms.")] double? x_offset = null,
            [Description("Script. " + ScriptHelp)] string? script = null,
            [Description(PeriodArg + " Channel without device only.")] double? period_ms = null)
            => svc.ConfigureChannel(trace, channel, name, unit, color, visible, display, size_bits, y_gain, y_offset, x_offset, script, period_ms);

        [McpServerTool(Name = "remove_channel", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = false)]
        [Description("Removes a channel and its data from the trace (undoable).")]
        public async Task<NameResult> RemoveChannel(
            [Description("Channel name.")] string channel,
            [Description(TraceTools.TraceArg)] string? trace = null)
            => new(await svc.RemoveChannel(trace, channel));
    }
}
