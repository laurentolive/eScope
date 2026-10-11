using ModelContextProtocol;

namespace eScope.Automation
{
    // Error codes of the automation API (see .claude/docs/automation.md)
    public static class AutomationError
    {
        public const string NotFound = "NOT_FOUND";
        public const string InvalidArgument = "INVALID_ARGUMENT";
        public const string NotConnected = "NOT_CONNECTED";
        public const string NotSupported = "NOT_SUPPORTED";
        public const string WriteDisabled = "WRITE_DISABLED";
        public const string Busy = "BUSY";
        public const string Timeout = "TIMEOUT";
        public const string ReadFailed = "READ_FAILED";
        public const string Unavailable = "UNAVAILABLE"; // eScope closing
        public const string IoError = "IO_ERROR"; // file not readable / writable
        public const string Internal = "INTERNAL";

        public static int HttpStatus(string code) => code switch
        {
            NotFound => 404,
            InvalidArgument => 400,
            NotConnected or NotSupported or Busy => 409,
            WriteDisabled => 403,
            Timeout => 504,
            ReadFailed => 502,
            Unavailable => 503,
            IoError => 500,
            _ => 500,
        };
    }

    // McpException: the MCP SDK returns its message to the client as a tool error (isError),
    // other exceptions are reported with a generic message
    public class AutomationException : McpException
    {
        public string Code { get; }

        public AutomationException(string code, string message) : base($"{code}: {message}")
        {
            Code = code;
            Detail = message;
        }

        public string Detail { get; }
    }
}
