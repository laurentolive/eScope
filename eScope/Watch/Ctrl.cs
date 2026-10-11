using eScope.Symbol;

namespace eScope.Watch
{
    public class Ctrl
    {
        public const int ExpressionColumn = 0;
        public const int AddressColumn = 1;
        public const int DisplayColumn = 2;
        public const int ValueColumn = 3;

        public Watch.Data Data { get; set; }
        List<DeviceCtrl> Devices;
        DeviceCtrl? Dev;
        UInt64 lastVal;
        DataGridViewRow Row;
        bool reading = false;  // GDB evaluation in progress
        string? text;          // value computed by GDB (expression without address)
        public Ctrl(Watch.Data data, List<DeviceCtrl> devices, DataGridViewRow row)
        {
            Data = data;
            Data.Migrate();
            Devices = devices;
            Dev = Utils.GetDevice(Devices, Data.Device);
            lastVal = Data.Value;
            Row = row;
            Update();
        }
        // device named by the "Device." prefix, or the only device of the workspace
        public static DeviceCtrl? GetDevice(List<DeviceCtrl> devices, string expression)
        {
            DeviceCtrl? dev = devices.FirstOrDefault(d => expression.StartsWith(d.Data.Name + "."));
            if (dev == null && devices.Count == 1)
                dev = devices[0];
            return dev;
        }
        // resolve a new expression, returns an error message when it is not valid
        public static string? Resolve(Watch.Data data, List<DeviceCtrl> devices, string expression)
        {
            expression = expression.Trim();
            DeviceCtrl? dev = GetDevice(devices, expression);
            if (dev == null)
                return "Unknown device (expected Device.expression)";
            SymbolData? sd = dev.GetSymbol(expression);
            if (sd == null)
                return $"Invalid expression '{dev.getVarFromSymbol(expression)}'";

            data.Device = dev.Data.Name;
            data.Expression = dev.Data.Name + "." + dev.getVarFromSymbol(expression);
            if (sd.Size > 0)
                data.Size = sd.Size;
            else if (data.Size <= 0)
                data.Size = 32; // map without size: a word by default, editable in the grid
            data.Shift = sd.Shift;
            if (sd.Resolved) // address read through a pointer: known once the target is connected
                data.Address = Utils.strToAddress(sd.Address);
            return null;
        }
        public string? SetExpression(string expression)
        {
            string? error = Resolve(Data, Devices, expression);
            if (error == null)
            {
                Dev = Utils.GetDevice(Devices, Data.Device);
                text = null;
                Update();
            }
            return error;
        }
        string Variable => Dev?.getVarFromSymbol(Data.Expression) ?? Data.Expression;
        public void UpdateRow(DataGridViewRow row)
        {
            Row = row;
            Update();
        }
        public void Update()
        {
            if (lastVal != Data.Value)
            {
                Write(Data.Value);
            }
            Row.Cells[ExpressionColumn].Value = Data.Expression;
            Row.Cells[AddressColumn].Value = text == null ? Utils.AddressToStr(Data.Address) : string.Empty;
            Row.Cells[DisplayColumn].Value = Data.Display;
            Row.Cells[ValueColumn].Value = text ?? Utils.UInt64ToStr(Data.Value, Data.Size, Data.Shift, Data.Display, Data.Prefix);
        }
        public void onReadCompletion(UInt64 val)
        {
            lastVal = val;
            Data.Value = val;
            if (!Row.Cells[ValueColumn].IsInEditMode) // ne pas écraser une saisie en cours
                Row.Cells[ValueColumn].Value = Utils.UInt64ToStr(Data.Value, Data.Size, Data.Shift, Data.Display, Data.Prefix);
        }
        // symbol resolved by GDB: the C expression is evaluated on each read, so that the
        // pointers it goes through are followed
        public async void Read()
        {
            if (Dev == null || reading)
                return;
            Task<ExprValue>? evaluation = string.IsNullOrWhiteSpace(Variable) ? null : Dev.EvaluateAsync(Variable);
            if (evaluation == null)
            {
                Dev.ReadMem(Data.Address, Data.Size, onReadCompletion);
                return;
            }

            reading = true;
            try
            {
                ExprValue v = await evaluation; // continues on the UI thread
                if (Row.DataGridView == null) // row removed meanwhile
                    return;
                if (v.Address != null)
                {
                    text = null;
                    if ((Data.Address != v.Address.Value) || (Data.Size != v.Size))
                    {
                        Data.Address = v.Address.Value;
                        Data.Size = v.Size;
                        Row.Cells[AddressColumn].Value = Utils.AddressToStr(Data.Address);
                    }
                    Dev.ReadMem(Data.Address, Data.Size, onReadCompletion);
                }
                else
                {
                    text = v.Text ?? $"<{v.Error}>";
                    Row.Cells[AddressColumn].Value = string.Empty;
                    if (!Row.Cells[ValueColumn].IsInEditMode)
                        Row.Cells[ValueColumn].Value = text;
                }
            }
            finally
            {
                reading = false;
            }
        }
        public void Write(UInt64 val)
        {
            lastVal = val;
            if (text == null) // a value computed by GDB has no address
                Dev?.WriteMem(Data.Address, Data.Size, val);
        }
    }
}
