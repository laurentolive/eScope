using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace eScope.Com
{
    public interface ICom
    {
        void Open(string model);
        void Close();
        int Read(char[] d, int count);
        void Write(char[] d);
    }

    public class Com
    {
        public ICom? Com(String model)
        {
            string[] s = model.Split(' ');
            if (s[0].Equals("Serial"))
                return null;#TODO
            else
                return null;
        }
    }

}
