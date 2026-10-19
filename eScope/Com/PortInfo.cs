using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;

namespace eScope.Com
{
    // what is behind each COM port, to tell them apart in the port list:
    // the paired device name for a Bluetooth SPP port, the driver / USB product name otherwise
    public static class PortInfo
    {
        // port name ("COM14") -> description ("ESP32_BT"), present devices only
        public static Dictionary<string, string> Describe()
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                Guid ports = GUID_DEVCLASS_PORTS;
                IntPtr set = SetupDiGetClassDevsW(ref ports, IntPtr.Zero, IntPtr.Zero, DIGCF_PRESENT);
                if (set == INVALID_HANDLE_VALUE)
                    return result;
                try
                {
                    var data = new SP_DEVINFO_DATA { cbSize = (uint)Marshal.SizeOf<SP_DEVINFO_DATA>() };
                    for (uint i = 0; SetupDiEnumDeviceInfo(set, i, ref data); i++)
                    {
                        string? port = PortName(set, ref data);
                        if (string.IsNullOrEmpty(port))
                            continue;
                        string? desc = Description(set, ref data, port);
                        if (!string.IsNullOrWhiteSpace(desc))
                            result[port] = desc;
                    }
                }
                finally
                {
                    SetupDiDestroyDeviceInfoList(set);
                }
            }
            catch (Exception ex)
            {
                LogMsg.Info("COM port description: " + ex.Message);
            }
            return result;
        }

        // "COM14 (ESP32_BT)" in the port list, "COM14" when nothing is known
        public static string Label(string port, Dictionary<string, string> descriptions)
            => descriptions.TryGetValue(port, out string? desc) ? $"{port} ({desc})" : port;

        // port name of a list label (or of a port typed by hand)
        public static string FromLabel(string label)
        {
            int idx = label.IndexOf(" (", StringComparison.Ordinal);
            return (idx < 0 ? label : label[..idx]).Trim();
        }

        static string? Description(IntPtr set, ref SP_DEVINFO_DATA data, string port)
        {
            string instanceId = InstanceId(set, ref data) ?? string.Empty;
            if (instanceId.StartsWith(@"BTHENUM\", StringComparison.OrdinalIgnoreCase))
            {
                // BTHENUM\{service}_...\..&<MAC>_C00000000: no MAC for an incoming port (a device connects to the PC)
                Match m = Regex.Match(instanceId, @"&([0-9A-F]{12})_", RegexOptions.IgnoreCase);
                if (!m.Success || m.Groups[1].Value.Trim('0').Length == 0)
                    return "Bluetooth incoming";
                return BluetoothName(m.Groups[1].Value) ?? "Bluetooth " + m.Groups[1].Value;
            }

            // generic Windows driver ("USB Serial Device", localized): the USB product string says more
            if (string.Equals(RegistryString(set, ref data, SPDRP_SERVICE), "usbser", StringComparison.OrdinalIgnoreCase))
            {
                string? product = BusReportedDescription(set, ref data);
                if (!string.IsNullOrWhiteSpace(product))
                    return product;
            }

            string? friendly = RegistryString(set, ref data, SPDRP_FRIENDLYNAME);
            return friendly?.Replace($"({port})", "", StringComparison.OrdinalIgnoreCase).Trim();
        }

        static string? BluetoothName(string mac)
        {
            using RegistryKey? key = Registry.LocalMachine.OpenSubKey(
                @"SYSTEM\CurrentControlSet\Services\BTHPORT\Parameters\Devices\" + mac.ToLowerInvariant());
            if (key?.GetValue("Name") is not byte[] name)
                return null;
            string s = Encoding.UTF8.GetString(name).TrimEnd('\0');
            return s.Length > 0 ? s : null;
        }

        static string? PortName(IntPtr set, ref SP_DEVINFO_DATA data)
        {
            IntPtr hKey = SetupDiOpenDevRegKey(set, ref data, DICS_FLAG_GLOBAL, 0, DIREG_DEV, KEY_READ);
            if (hKey == INVALID_HANDLE_VALUE)
                return null;
            using RegistryKey key = RegistryKey.FromHandle(new SafeRegistryHandle(hKey, true));
            return key.GetValue("PortName") as string;
        }

        static string? InstanceId(IntPtr set, ref SP_DEVINFO_DATA data)
        {
            var sb = new StringBuilder(512);
            return SetupDiGetDeviceInstanceIdW(set, ref data, sb, sb.Capacity, out _) ? sb.ToString() : null;
        }

        static string? RegistryString(IntPtr set, ref SP_DEVINFO_DATA data, uint property)
        {
            byte[] buf = new byte[1024];
            if (!SetupDiGetDeviceRegistryPropertyW(set, ref data, property, out _, buf, (uint)buf.Length, out uint size))
                return null;
            return Encoding.Unicode.GetString(buf, 0, (int)size).TrimEnd('\0');
        }

        static string? BusReportedDescription(IntPtr set, ref SP_DEVINFO_DATA data)
        {
            var key = DEVPKEY_Device_BusReportedDeviceDesc;
            byte[] buf = new byte[1024];
            if (!SetupDiGetDevicePropertyW(set, ref data, ref key, out _, buf, (uint)buf.Length, out uint size, 0))
                return null;
            return Encoding.Unicode.GetString(buf, 0, (int)size).TrimEnd('\0');
        }

        // ── SetupAPI ─────────────────────────────────────────────────────────────
        static readonly Guid GUID_DEVCLASS_PORTS = new("4d36e978-e325-11ce-bfc1-08002be10318");
        static readonly DEVPROPKEY DEVPKEY_Device_BusReportedDeviceDesc =
            new() { fmtid = new Guid("540b947e-8b40-45bc-a8a2-6a0b894cbda2"), pid = 4 };
        static readonly IntPtr INVALID_HANDLE_VALUE = new(-1);
        const uint DIGCF_PRESENT = 0x02;
        const uint SPDRP_SERVICE = 0x04;
        const uint SPDRP_FRIENDLYNAME = 0x0C;
        const uint DICS_FLAG_GLOBAL = 0x01;
        const uint DIREG_DEV = 0x01;
        const int KEY_READ = 0x20019;

        [StructLayout(LayoutKind.Sequential)]
        struct SP_DEVINFO_DATA
        {
            public uint cbSize;
            public Guid ClassGuid;
            public uint DevInst;
            public IntPtr Reserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct DEVPROPKEY
        {
            public Guid fmtid;
            public uint pid;
        }

        [DllImport("setupapi.dll", SetLastError = true)]
        static extern IntPtr SetupDiGetClassDevsW(ref Guid classGuid, IntPtr enumerator, IntPtr hwndParent, uint flags);
        [DllImport("setupapi.dll", SetLastError = true)]
        static extern bool SetupDiEnumDeviceInfo(IntPtr set, uint index, ref SP_DEVINFO_DATA data);
        [DllImport("setupapi.dll", SetLastError = true)]
        static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);
        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern bool SetupDiGetDeviceInstanceIdW(IntPtr set, ref SP_DEVINFO_DATA data, StringBuilder id, int size, out int required);
        [DllImport("setupapi.dll", SetLastError = true)]
        static extern bool SetupDiGetDeviceRegistryPropertyW(IntPtr set, ref SP_DEVINFO_DATA data, uint property,
            out uint regType, byte[] buffer, uint size, out uint required);
        [DllImport("setupapi.dll", SetLastError = true)]
        static extern bool SetupDiGetDevicePropertyW(IntPtr set, ref SP_DEVINFO_DATA data, ref DEVPROPKEY key,
            out uint type, byte[] buffer, uint size, out uint required, uint flags);
        [DllImport("setupapi.dll", SetLastError = true)]
        static extern IntPtr SetupDiOpenDevRegKey(IntPtr set, ref SP_DEVINFO_DATA data, uint scope, uint profile, uint keyType, int access);
    }
}
