using ModelContextProtocol.Server;
using System.ComponentModel;

namespace eScope.Automation.Tools
{
    // Tool classes: [McpServerToolType], constructor taking the AutomationService, one [McpServerTool] method per tool.
    // The same methods serve MCP and REST (ToolRegistry): parameter names are the API names (snake_case).
    [McpServerToolType]
    public class WorkspaceTools
    {
        readonly AutomationService svc;

        public WorkspaceTools(AutomationService svc) => this.svc = svc;

        [McpServerTool(Name = "get_workspace", ReadOnly = true, Idempotent = true, OpenWorld = false)]
        [Description("Overview of the eScope workspace: version, recording state, devices (protocol, transport, " +
                     "symbols, connection) and traces (plot tabs) with their channel count.")]
        public Task<WorkspaceInfo> GetWorkspace() => svc.GetWorkspace();

        [McpServerTool(Name = "create_trace", ReadOnly = false, Idempotent = false, OpenWorld = false)]
        [Description("Creates an empty trace (plot tab) and makes it the current trace, except during a recording " +
                     "(the current trace does not change while recording). Not undoable.")]
        public async Task<NameResult> CreateTrace(
            [Description("Trace name, 'TraceN' when omitted.")] string? name = null)
            => new(await svc.CreateTrace(name));

        [McpServerTool(Name = "select_trace", ReadOnly = false, Idempotent = true, OpenWorld = false)]
        [Description("Makes a trace the current one (the displayed tab, the one recorded). BUSY during a recording.")]
        public async Task<NameResult> SelectTrace([Description("Trace name.")] string trace)
            => new(await svc.SelectTrace(trace));

        [McpServerTool(Name = "rename_trace", ReadOnly = false, Idempotent = true, OpenWorld = false)]
        [Description("Renames a trace (undoable).")]
        public async Task<NameResult> RenameTrace(
            [Description("New name.")] string name,
            [Description(TraceTools.TraceArg)] string? trace = null)
            => new(await svc.RenameTrace(trace, name));

        [McpServerTool(Name = "save_workspace", ReadOnly = false, Idempotent = true, OpenWorld = false)]
        [Description("Saves the workspace (devices, traces, channels, data) to its .escope file, without " +
                     "confirmation. Fails if the workspace was never saved (no file yet).")]
        public async Task<PathResult> SaveWorkspace() => new(await svc.SaveWorkspace());

        [McpServerTool(Name = "undo", ReadOnly = false, Idempotent = false, OpenWorld = false)]
        [Description("Undoes the last change of the eScope history (shared with the user: it may be a change made " +
                     "by the user). Changes made through these tools are labelled 'AI: ...'.")]
        public async Task<HistoryResult> Undo()
            => await svc.Undo() is string label ? new(true, label) : new(false, null);

        [McpServerTool(Name = "redo", ReadOnly = false, Idempotent = false, OpenWorld = false)]
        [Description("Redoes the last undone change.")]
        public async Task<HistoryResult> Redo()
            => await svc.Redo() is string label ? new(true, label) : new(false, null);
    }
}
