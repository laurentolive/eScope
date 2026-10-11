using System.Text.Json.Serialization;

namespace eScope.Watch
{
    public class Data
    {
        // "Device.expression": variable name, or any C expression with GDB symbols
        public string Expression { get; set; }
        public string Device { get; set; }
        public UInt32 Address { get; set; }
        public int Size { get; set; }
        public int Shift { get; set; }
        public Utils.Display Display { get; set; }
        public UInt64 Value { get; set; }
        public bool Prefix;
        // legacy workspaces (Name + Symbol), converted to Expression on load
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Symbol { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Name { get; set; }
        public Data()
        {
            Expression = string.Empty;
            Device = string.Empty;
            Address = 0;
            Size = 1;
            Shift = 0;
            Display = 0;
            Value = 0;
            Prefix = false;
        }
        public void Migrate()
        {
            if (string.IsNullOrEmpty(Expression) && !string.IsNullOrEmpty(Symbol))
                Expression = Device + "." + Symbol;
            Symbol = null;
            Name = null;
        }
    }
}
