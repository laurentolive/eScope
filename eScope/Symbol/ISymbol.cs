using eScope.Com;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace eScope.Symbol
{
    public class SymbolData
    {
        public string Device { get; set; }
        public string Name { get; set; }
        public string Address { get; set; }
        public int Size { get; set; }
        public int Shift { get; set; }
        public string Type{ get; set; }
        // false: valid expression whose address depends on target memory (pointer) not readable now
        public bool Resolved { get; set; }

        public SymbolData()
        {
            Device = string.Empty;
            Name = string.Empty;
            Address = string.Empty;
            Size = 2;
            Shift = 0;
            Type = "int";
            Resolved = true;
        }
    }

    public interface ISymbol
    {
        public List <string> List(string name);
        SymbolData? Get(string name);
        public bool isValid(string name);
        public static ISymbol? New(string type, string device, string symbolFilePath, string gdbPath, string mapRegEx)
        {
            if (type.Equals("map"))
                return new SymbolMap(symbolFilePath, device, mapRegEx);
            else if (type.Equals("gdb"))
                return new SymbolGDB(device, symbolFilePath, gdbPath);
            else
                return null;
        }
    }
}
