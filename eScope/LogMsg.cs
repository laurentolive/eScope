using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting;

namespace eScope
{
    static public class LogMsg
    {

        //public LogMsg()
        //{

        //    Log.Logger = new LoggerConfiguration()
        //    .WriteTo.Console()
        //        .WriteTo.Sink(new LogMsgSink(b, new Serilog.Formatting.Display.MessageTemplateTextFormatter("{Timestamp:HH:mm:ss} {Message}")))
        //        .WriteTo.File("log.txt", PlayingInterval: PlayingInterval.Day)
        //        .CreateLogger();
        //}
        //public void Dispose()
        //{
        //    Log.CloseAndFlush(); // Flush and close the logger
        //}
        static public void Info(string message)
        {
            Log.Information(message);
        }
        static public void Error(string message)
        {
            Log.Error(message);
        }
    }

    public class LogMsgSink : ILogEventSink
    {
        private readonly ITextFormatter F;

        public LogMsgSink(ToolStripDropDownButton b, ITextFormatter formatter)
        {
            F = formatter;
        }

        public void Emit(LogEvent logEvent)
        {
            // Format the log event
            var renderedText = new StringWriter();
            F.Format(logEvent, renderedText);

        }
    }
}
