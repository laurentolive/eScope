using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Serilog;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace eScope.Automation
{
    // Local HTTP server of the automation API, on HttpListener (no ASP.NET Core runtime needed):
    //   POST /mcp           MCP Streamable HTTP, stateless (one McpServer per request)
    //   GET  /api/tools     tool list with the JSON schema of their arguments
    //   POST /api/{tool}    tool call, JSON arguments → JSON result
    // Every request needs "Authorization: Bearer <token>", comes from the loopback and not from a web page.
    public sealed class AutomationHost : IDisposable
    {
        public const string ServerInstructions =
            "eScope is a software oscilloscope for embedded developers: it reads variables in the RAM of a running " +
            "microcontroller (devices) and plots them in real time (traces = plot tabs, channels = plotted variables). " +
            "The user sees the eScope window while you work. " +
            "Start with get_workspace. Typical flow: add_channel (variable or C expression), acquire (records then " +
            "stops), analyze / find_edges / get_channel_data, screenshot to look at the curves, add_delta / " +
            "add_annotation to explain a finding. " +
            "Rules: times are in ms on the trace X axis; the recording applies to the current trace, which cannot " +
            "change while recording; the target memory (read_variable, write_variable) is reachable with or without " +
            "a recording (the link opens on demand); writes change the real equipment and are " +
            "allowed only when the user enabled them; model changes are undoable by the user (Ctrl+Z, 'AI: ...'), " +
            "undo shares the user's history.";

        readonly ToolRegistry registry;
        readonly byte[] token;
        readonly HttpListener listener = new();
        readonly CancellationTokenSource cts = new();

        public int Port { get; }
        public string McpUrl => $"http://localhost:{Port}/mcp";

        // tool name, raised on the request thread
        public event Action<string>? ToolCalled;

        public AutomationHost(ToolRegistry registry, int port, string token)
        {
            this.registry = registry;
            this.token = Encoding.UTF8.GetBytes(token);
            Port = port;
        }

        public void Start()
        {
            // "localhost" prefix: allowed without admin rights (no URL ACL), unlike 127.0.0.1
            listener.Prefixes.Add($"http://localhost:{Port}/");
            listener.Start();
            _ = Task.Run(AcceptLoop);
            Log.Information("Automation server listening on {Url}", McpUrl);
        }

        public void Dispose()
        {
            cts.Cancel();
            try { listener.Close(); } catch (ObjectDisposedException) { }
        }

        async Task AcceptLoop()
        {
            while (!cts.IsCancellationRequested)
            {
                HttpListenerContext ctx;
                try
                {
                    ctx = await listener.GetContextAsync();
                }
                catch (Exception) when (cts.IsCancellationRequested || !listener.IsListening)
                {
                    return;
                }
                catch (HttpListenerException ex)
                {
                    Log.Warning(ex, "Automation server accept failed");
                    continue;
                }
                _ = Task.Run(() => HandleAsync(ctx));
            }
        }

        async Task HandleAsync(HttpListenerContext ctx)
        {
            var req = ctx.Request;
            var resp = ctx.Response;
            try
            {
                if (!IsAllowed(req, out int status, out string reason))
                {
                    await WriteErrorAsync(resp, status, status == 401 ? "UNAUTHORIZED" : "FORBIDDEN", reason);
                    return;
                }

                string path = req.Url!.AbsolutePath.TrimEnd('/');
                if (path == "/mcp")
                {
                    if (req.HttpMethod == "POST")
                        await HandleMcpAsync(req, resp);
                    else
                        await WriteErrorAsync(resp, 405, "METHOD_NOT_ALLOWED", "stateless server: POST only");
                }
                else if (path == "/api/tools" && req.HttpMethod == "GET")
                {
                    await WriteJsonAsync(resp, 200, registry.Describe());
                }
                else if (path.StartsWith("/api/") && req.HttpMethod == "POST")
                {
                    await HandleRestAsync(path["/api/".Length..], req, resp);
                }
                else
                {
                    await WriteErrorAsync(resp, 404, AutomationError.NotFound, $"no route for {req.HttpMethod} {path}");
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Automation request {Method} {Url} failed", req.HttpMethod, req.Url);
                try { await WriteErrorAsync(resp, 500, AutomationError.Internal, ex.Message); } catch { }
            }
            finally
            {
                try { resp.Close(); } catch { }
            }
        }

        bool IsAllowed(HttpListenerRequest req, out int status, out string reason)
        {
            // HttpListener binds the port on every interface and filters on the Host header only
            if (!IPAddress.IsLoopback(req.RemoteEndPoint.Address))
            {
                (status, reason) = (403, "only local clients are accepted");
                return false;
            }
            // DNS rebinding / browser pages: a cross-origin request carries a non-local Origin
            string? origin = req.Headers["Origin"];
            if (!string.IsNullOrEmpty(origin) &&
                !(Uri.TryCreate(origin, UriKind.Absolute, out var o) && (o.IsLoopback || o.Host == "localhost")))
            {
                (status, reason) = (403, "requests from web pages are not accepted");
                return false;
            }
            string auth = req.Headers["Authorization"] ?? string.Empty;
            byte[] given = auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                ? Encoding.UTF8.GetBytes(auth["Bearer ".Length..].Trim())
                : Array.Empty<byte>();
            if (!CryptographicOperations.FixedTimeEquals(given, token))
            {
                (status, reason) = (401, "missing or invalid token (Authorization: Bearer <token>, see eScope > Tools > AI automation)");
                return false;
            }
            (status, reason) = (200, string.Empty);
            return true;
        }

        async Task HandleMcpAsync(HttpListenerRequest req, HttpListenerResponse resp)
        {
            JsonRpcMessage? message;
            try
            {
                message = await JsonSerializer.DeserializeAsync<JsonRpcMessage>(req.InputStream, McpJsonUtilities.DefaultOptions, cts.Token);
            }
            catch (JsonException)
            {
                message = null;
            }
            if (message == null)
            {
                await WriteJsonAsync(resp, 400, new JsonObject
                {
                    ["jsonrpc"] = "2.0",
                    ["id"] = null,
                    ["error"] = new JsonObject { ["code"] = (int)McpErrorCode.InvalidRequest, ["message"] = "invalid JSON-RPC message" },
                });
                return;
            }
            string? protocolVersion = req.Headers["MCP-Protocol-Version"];
            if (!string.IsNullOrEmpty(protocolVersion))
            {
                message.Context ??= new();
                message.Context.ProtocolVersion = protocolVersion;
            }
            if (message is JsonRpcRequest { Method: RequestMethods.ToolsCall } call &&
                call.Params?["name"]?.GetValue<string>() is string toolName)
            {
                ToolCalled?.Invoke(toolName);
            }

            // stateless: a transport and a server for this request only (what MapMcp does in stateless mode)
            var transport = new StreamableHttpServerTransport(null) { Stateless = true };
            var server = McpServer.Create(transport, NewServerOptions(), null, null);
            using var runCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token);
            Task runTask = server.RunAsync(runCts.Token);
            try
            {
                resp.ContentType = "text/event-stream";
                resp.Headers["Cache-Control"] = "no-cache,no-store";
                resp.SendChunked = true;
                bool wrote = await transport.HandlePostRequestAsync(message, resp.OutputStream, cts.Token);
                if (!wrote)
                {
                    // notification or response from the client: nothing to send back
                    resp.SendChunked = false;
                    resp.ContentType = null;
                    resp.ContentLength64 = 0;
                    resp.StatusCode = 202;
                }
            }
            finally
            {
                await transport.DisposeAsync();
                runCts.Cancel();
                try { await runTask; } catch (OperationCanceledException) { }
                await server.DisposeAsync();
            }
        }

        McpServerOptions NewServerOptions()
        {
            var tools = new McpServerPrimitiveCollection<McpServerTool>();
            foreach (var t in registry.McpTools) tools.Add(t);
            return new McpServerOptions
            {
                ServerInfo = new Implementation { Name = "eScope", Version = eScope.AppVersion },
                ServerInstructions = ServerInstructions,
                ToolCollection = tools,
            };
        }

        async Task HandleRestAsync(string toolName, HttpListenerRequest req, HttpListenerResponse resp)
        {
            JsonElement? args = null;
            if (req.HasEntityBody)
            {
                try
                {
                    using var doc = await JsonDocument.ParseAsync(req.InputStream, cancellationToken: cts.Token);
                    args = doc.RootElement.Clone();
                }
                catch (JsonException ex)
                {
                    await WriteErrorAsync(resp, 400, AutomationError.InvalidArgument, "invalid JSON body: " + ex.Message);
                    return;
                }
            }
            ToolCalled?.Invoke(toolName);
            try
            {
                object? result = await registry.InvokeAsync(toolName, args, cts.Token);
                if (result is ImageContentBlock image)
                {
                    // screenshot: the image itself
                    byte[] data = image.DecodedData.ToArray();
                    resp.StatusCode = 200;
                    resp.ContentType = image.MimeType;
                    resp.ContentLength64 = data.Length;
                    await resp.OutputStream.WriteAsync(data);
                    return;
                }
                await WriteJsonAsync(resp, 200, result);
            }
            catch (AutomationException ex)
            {
                await WriteErrorAsync(resp, AutomationError.HttpStatus(ex.Code), ex.Code, ex.Detail);
            }
        }

        static Task WriteErrorAsync(HttpListenerResponse resp, int status, string code, string message)
            => WriteJsonAsync(resp, status, new JsonObject
            {
                ["error"] = new JsonObject { ["code"] = code, ["message"] = message },
            });

        static async Task WriteJsonAsync(HttpListenerResponse resp, int status, object? body)
        {
            byte[] data = JsonSerializer.SerializeToUtf8Bytes(body, AutomationJson.Options);
            resp.StatusCode = status;
            resp.ContentType = "application/json; charset=utf-8";
            resp.ContentLength64 = data.Length;
            await resp.OutputStream.WriteAsync(data);
        }
    }
}
