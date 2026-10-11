using IronPython.Compiler.Ast;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.DirectoryServices;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static IronPython.Runtime.Profiler;

namespace eScope
{

    public class Utils
    {
        public enum Display
        {
            signedInt = 0,
            unsignedInt = 1,
            hexa = 2,
            float32 = 3,
        }
        static public double UInt64ToDouble(UInt64 y, int size, int shift, Display display)
        {
            double d;

            if(display == Utils.Display.signedInt) // signed decimal
            {
                int sh = (63 - (size - 1));
                Int64 y_;
                y = (UInt64)(y >> shift);
                y_ = (Int64)(y & (((UInt64)Math.Pow(2, size) - 1)));
                y_ = y_ << sh; // sign extension to 32 bits
                y_ = y_ >> sh;
                d = ((Int64)unchecked((long)y_));
            }
            else if (display == Utils.Display.float32) // float
            {
                byte[] bytes = BitConverter.GetBytes(y);
                d = BitConverter.ToSingle(bytes, 0);
                if (double.IsNaN(d) || double.IsInfinity(d))
                {
                    d = 0.0;
                }
            }
            else // unsigned 
            {
                y = (UInt64)(y >> shift);
                y = (UInt64)(y & (((UInt64)Math.Pow(2, size) - 1)));
                d = y;
            }
            return d;
        }
        static public string DoubleToStr(double val, Display display, bool isPrefix)
        {
            string ret = val.ToString(); // by default

            if (isPrefix)
            {
                if (display == Display.unsignedInt) // unsigned decimal
                {
                    ret = "0u" + ret;
                }
                else if (display == Display.hexa) // hexa
                {
                    UInt64 u64Val = (UInt64)val;
                    ret = u64Val.ToString("x");
                    ret = "0x" + ret;
                }
                else if (display == Display.float32) // float
                {
                    ret = "0f" + ret;
                }
                else // signed decimal
                {
                    ret = "0d" + ret;
                }
            }
            return ret;
        }
        static public string UInt64ToStr(UInt64 val, int size, int shift, Display display, bool isPrefix)
        {
            string ret;

            double d = UInt64ToDouble(val, size, shift, display);
            ret = DoubleToStr(d, display, isPrefix);
            return ret;
        }
        static public UInt64 StrToUInt64(string val, Display display, int size)
        {
            UInt64 ret;
            try
            {                
                if (display == Display.unsignedInt) // unsigned decimal
                {
                    if (val.StartsWith("0u", StringComparison.OrdinalIgnoreCase))
                    {
                        val = val.Substring(2);
                    }
                    if(size > 32)
                    {
                        ret = UInt64.Parse(val);
                    }
                    else if (size > 16)
                    {
                        ret = (UInt64)UInt32.Parse(val);
                    }
                    else if (size > 8)
                    {
                        ret = (UInt64)UInt16.Parse(val);
                    }
                    else
                    {
                        ret = (UInt64)byte.Parse(val);
                    }
                }
                else if (display == Display.signedInt)
                {
                    if (val.StartsWith("0d", StringComparison.OrdinalIgnoreCase))
                    {
                        val = val.Substring(2);
                    }
                    if (size > 32)
                    {
                        ret = (UInt64)Int64.Parse(val);
                    }
                    else if (size > 16)
                    {
                        ret = (UInt64)Int32.Parse(val);
                    }
                    else if (size > 8)
                    {
                        ret = (UInt64)Int16.Parse(val);
                    }
                    else
                    {
                        ret = (UInt64)sbyte.Parse(val);
                    }
                }
                else if (display == Display.hexa) // hexa
                {
                    if (val.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                    {
                        val = val.Substring(2);
                    }
                    ret = Convert.ToUInt64(val, 16);
                }
                else // if (display == Display.float32) // float
                {
                    if (val.StartsWith("0f", StringComparison.OrdinalIgnoreCase))
                    {
                        val = val.Substring(2);
                    }
                    float d = float.Parse(val.Replace(".", ","));
                    byte[] fourthbytes = BitConverter.GetBytes(d);
                    byte[] eightBytes = new byte[8];
                    Array.Copy(fourthbytes, 0, eightBytes, 0, 4); // low bits (little-endian)

                    ret = BitConverter.ToUInt64(eightBytes, 0);
                }
            }
            catch
            {
                Console.WriteLine($"Failed to parse {val} as {display}");
                ret = 0;
            }
            return ret;
        }
        // inverse of UInt64ToDouble: displayed value -> raw sample (size mask then shift)
        static public UInt64 DoubleToUInt64(double val, int size, int shift, Display display)
        {
            if (double.IsNaN(val) || double.IsInfinity(val))
            {
                val = 0;
            }
            if (display == Display.float32)
            {
                return BitConverter.SingleToUInt32Bits((float)val);
            }
            double r = Math.Round(val);
            UInt64 y = r >= 0 ? (UInt64)r : unchecked((UInt64)(Int64)r);
            UInt64 mask = size >= 64 ? UInt64.MaxValue : ((1UL << size) - 1);
            return (y & mask) << shift;
        }
        static public UInt32 strToAddress(string str)
        {
            if (str.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                str = str.Substring(2);
            }
            if (UInt32.TryParse(str, System.Globalization.NumberStyles.HexNumber, null, out UInt32 address) == false)
            {
                address = 0;
            }
            return address;
        }
        static public string AddressToStr(UInt32 address)
        {
            return "0x" + address.ToString("x");
        }
        static public DeviceCtrl? GetDevice(List<DeviceCtrl> devices, string DeviceName)
        {
            foreach (DeviceCtrl ctrl in devices)
            {
                if (DeviceName == ctrl.Data.Name)
                {
                    return ctrl;
                }
            }
            return null;
        }
    }
}
