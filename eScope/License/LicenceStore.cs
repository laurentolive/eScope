using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace eScope.License
{
    using System.ComponentModel;
    using System.Security.Cryptography;
    using System.Text.Json;

    public static class LicenseStore
    {
        private static readonly string Dir =
            System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "eScope");

        public static string GetPath(LicenseType type)
        {
            return Path.Combine(Dir, LicenseInfo.GetName(type));
        }
        public static void Save(LicenseInfo license)
        {
            Directory.CreateDirectory(Dir!);

            byte[] json = JsonSerializer.SerializeToUtf8Bytes(license);
            byte[] encrypted = ProtectedData.Protect(
                json, null, DataProtectionScope.CurrentUser);

            File.WriteAllBytes(GetPath(license.Type), encrypted);
        }

        public static void Delete(LicenseType type)
        {
            File.Delete(GetPath(type));
        }

        public static LicenseInfo? Load()
        {
            var l = Load(LicenseType.Professional);
            if (l == null)
            {
                l = Load(LicenseType.Personal);
            }
            if (l == null)
            {
                l = Load(LicenseType.Demonstration);
            }

            return l;
        }
        public static LicenseInfo? Load(LicenseType type)
        {
            string path = GetPath(type);

            if (!File.Exists(GetPath(type)))
            {
                return null;
            }

            byte[] encrypted = File.ReadAllBytes(path);
            byte[] json = ProtectedData.Unprotect(
                encrypted, null, DataProtectionScope.CurrentUser);

            return JsonSerializer.Deserialize<LicenseInfo>(json);
        }
    }
}
