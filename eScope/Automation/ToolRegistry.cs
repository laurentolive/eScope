using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace eScope.Automation
{
    public static class AutomationJson
    {
        // DTO properties in snake_case, same output for MCP and REST
        public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower,
            NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals, // NaN of the analysis results
            TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, // JSON for programs, never embedded in HTML
        };
    }

    // Single catalog of the tools: the [McpServerTool] methods of the [McpServerToolType] classes of Automation.Tools.
    // MCP uses the McpServerTool objects built from them, REST invokes the same methods by reflection.
    public class ToolRegistry
    {
        record Entry(MethodInfo Method, object Target, McpServerTool Tool);

        readonly Dictionary<string, Entry> entries = new(StringComparer.Ordinal);

        public ToolRegistry(AutomationService svc)
        {
            var types = typeof(ToolRegistry).Assembly.GetTypes()
                .Where(t => t.Namespace == "eScope.Automation.Tools" && t.GetCustomAttribute<McpServerToolTypeAttribute>() != null);
            foreach (var type in types)
            {
                object target = Activator.CreateInstance(type, svc)!;
                foreach (var m in type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
                {
                    var attr = m.GetCustomAttribute<McpServerToolAttribute>();
                    if (attr == null) continue;
                    var tool = McpServerTool.Create(m, target, new McpServerToolCreateOptions
                    {
                        SerializerOptions = AutomationJson.Options,
                    });
                    entries.Add(tool.ProtocolTool.Name, new Entry(m, target, new CheckedTool(tool, m.GetParameters())));
                }
            }
        }

        public IEnumerable<McpServerTool> McpTools => entries.Values.Select(e => e.Tool);

        // GET /api/tools: name, description, JSON schema of the arguments
        public JsonArray Describe()
        {
            var list = new JsonArray();
            foreach (var e in entries.Values.OrderBy(e => e.Tool.ProtocolTool.Name))
            {
                var t = e.Tool.ProtocolTool;
                list.Add(new JsonObject
                {
                    ["name"] = t.Name,
                    ["description"] = t.Description,
                    ["read_only"] = t.Annotations?.ReadOnlyHint ?? false,
                    ["destructive"] = t.Annotations?.DestructiveHint ?? false,
                    ["input_schema"] = JsonNode.Parse(t.InputSchema.GetRawText()),
                });
            }
            return list;
        }

        // POST /api/{name}: JSON object properties bound to the method parameters by name
        public async Task<object?> InvokeAsync(string name, JsonElement? args, CancellationToken ct)
        {
            if (!entries.TryGetValue(name, out var e))
                throw new AutomationException(AutomationError.NotFound, $"unknown tool '{name}'");
            if (args is { ValueKind: not (JsonValueKind.Object or JsonValueKind.Null or JsonValueKind.Undefined) })
                throw new AutomationException(AutomationError.InvalidArgument, "the request body must be a JSON object");

            var parameters = e.Method.GetParameters();
            var values = new object?[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                var p = parameters[i];
                if (p.ParameterType == typeof(CancellationToken))
                {
                    values[i] = ct;
                }
                else
                {
                    JsonElement? v = args is { ValueKind: JsonValueKind.Object } obj && obj.TryGetProperty(p.Name!, out var a) ? a : null;
                    values[i] = BindArgument(p, v);
                }
            }

            object? result;
            try
            {
                result = e.Method.Invoke(e.Target, values);
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                throw ex.InnerException;
            }
            if (result is Task task)
            {
                await task;
                var resultProp = task.GetType().GetProperty("Result");
                // Task (no value) is Task<VoidTaskResult> at runtime
                return e.Method.ReturnType.IsGenericType ? resultProp?.GetValue(task) : null;
            }
            return result;
        }

        // value of a tool parameter from its JSON argument (null: absent), same checks and messages for REST and MCP
        static object? BindArgument(ParameterInfo p, JsonElement? v)
        {
            if (v is JsonElement value)
            {
                try
                {
                    return value.Deserialize(p.ParameterType, AutomationJson.Options);
                }
                catch (Exception ex) when (ex is JsonException or NotSupportedException)
                {
                    // without the position in the JSON fragment, meaningless for the client
                    string msg = ex.Message;
                    int pos = msg.IndexOf(" Path:", StringComparison.Ordinal);
                    throw new AutomationException(AutomationError.InvalidArgument, $"'{p.Name}': {(pos > 0 ? msg[..pos] : msg)}");
                }
            }
            if (p.HasDefaultValue)
                return p.DefaultValue;
            throw new AutomationException(AutomationError.InvalidArgument, $"missing argument '{p.Name}'");
        }

        // MCP: an argument the SDK cannot bind (missing, wrong JSON type) comes back to the client as a bare
        // "An error occurred invoking ..." without the cause: the arguments are checked here first, as for REST
        sealed class CheckedTool(McpServerTool inner, ParameterInfo[] parameters) : DelegatingMcpServerTool(inner)
        {
            public override ValueTask<CallToolResult> InvokeAsync(RequestContext<CallToolRequestParams> request, CancellationToken cancellationToken = default)
            {
                var args = request.Params?.Arguments;
                try
                {
                    foreach (var p in parameters)
                    {
                        if (p.ParameterType == typeof(CancellationToken)) continue;
                        BindArgument(p, args != null && args.TryGetValue(p.Name!, out var v) ? v : null);
                    }
                }
                catch (AutomationException ex)
                {
                    return ValueTask.FromResult(new CallToolResult
                    {
                        IsError = true,
                        Content = [new TextContentBlock { Text = $"An error occurred invoking '{ProtocolTool.Name}': {ex.Message}" }],
                    });
                }
                return base.InvokeAsync(request, cancellationToken);
            }
        }
    }
}
