using System;
using System.Collections.Generic;
using System.DirectoryServices.ActiveDirectory;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;
using System.Security.Cryptography;

namespace eScope.License
{
    public enum LicenseType
    {
        Demonstration,
        Personal,
        Professional
    }

    public sealed class LicenseInfo
    {
        public LicenseType Type { get; set; }
        public DateTime? ExpiresUtc { get; set; }
        public DateTime ActivatedUtc { get; set; }
        public bool IsPublished { get; set; } = false;
        string ADDomain { get; set; } = string.Empty;
        string UserDomain { get; set; } = string.Empty;
        string UserName { get; set; } = string.Empty;
        string MachineId { get; set; } = string.Empty;

        // Professional: Lemon Squeezy key activated on this machine
        public string? LicenseKey { get; set; }
        public string? InstanceId { get; set; }
        public string? ServerStatus { get; set; }       // Lemon Squeezy key status (active, expired, disabled) or invalid
        public string? CustomerEmail { get; set; }
        public DateTime? LastValidatedUtc { get; set; }

        // without validation for this long, a Professional license counts as expired: the reminder is back at
        // each startup, so a cancelled subscription cannot be kept by staying offline
        public const int OfflineGraceDays = 30;

        public bool IsOfflineTooLong =>
            Type == LicenseType.Professional &&
            (LastValidatedUtc == null || DateTime.UtcNow > LastValidatedUtc.Value.AddDays(OfflineGraceDays));

        public bool IsExpired =>
            (ExpiresUtc.HasValue && DateTime.UtcNow > ExpiresUtc.Value) ||
            (Type == LicenseType.Professional && ServerStatus is "expired" or "disabled" or "invalid") ||
            IsOfflineTooLong;

        public static LicenseInfo FromServer(LsResponse r)
        {
            var l = new LicenseInfo(LicenseType.Professional);
            l.Update(r);
            return l;
        }

        // refresh from an activate / validate answer
        public void Update(LsResponse r)
        {
            if (r.LicenseKey != null)
            {
                LicenseKey = r.LicenseKey.Key;
                ServerStatus = r.LicenseKey.Status;
                // null for a key tied to a subscription: the status follows the subscription instead
                ExpiresUtc = r.LicenseKey.ExpiresAt?.ToUniversalTime();
            }
            if (r.Instance != null)
                InstanceId = r.Instance.Id;
            if (r.Meta?.CustomerEmail != null)
                CustomerEmail = r.Meta.CustomerEmail;
            LastValidatedUtc = DateTime.UtcNow;
        }
        public LicenseInfo(LicenseType type)
        {
            Type = type;
            ActivatedUtc = DateTime.UtcNow;

            UserDomain = Environment.UserDomainName;
            UserName = Environment.UserName;
            MachineId = GetMachineId();

            if (type == LicenseType.Professional)
            {
                ExpiresUtc = DateTime.UtcNow.AddYears(1);
            }
            else if (type == LicenseType.Personal)
            {
                ExpiresUtc = DateTime.MaxValue;
            }
            else if (type == LicenseType.Demonstration)
            {
                ExpiresUtc = DateTime.UtcNow.AddMonths(1);
            }
        }

        public static string GetWindowsMachineGuid()
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(
                    @"SOFTWARE\Microsoft\Cryptography");

                var value = key?.GetValue("MachineGuid")?.ToString();

                return string.IsNullOrWhiteSpace(value)
                    ? "UNKNOWN"
                    : value;
            }
            catch
            {
                return "UNKNOWN";
            }
        }
        public static string GetName(LicenseType type)
        {
            return $"license{type.ToString()}.dat";
        }
        public static string GetMachineId()
        {
            string raw =
                Environment.MachineName + "|" +
                GetWindowsMachineGuid();

            using var sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(raw));

            return Convert.ToHexString(hash);
        }

        public string GetLicenseStatusText()
        {
            return Type switch
            {
                LicenseType.Demonstration =>
                    IsExpired
                        ? "Demonstration License – Expired"
                        : $"Demonstration License – Valid until {ExpiresUtc:yyyy-MM-dd}",

                LicenseType.Personal =>
                    "Personal License – Non-commercial use only",

                LicenseType.Professional =>
                    IsExpired
                        ? ServerStatus switch
                        {
                            "disabled" => "Professional License – Disabled",
                            "invalid" => "Professional License – Invalid",
                            "active" when IsOfflineTooLong => "Professional License – Online validation required",
                            _ => "Professional License – Expired"
                        }
                        : ExpiresUtc.HasValue
                            ? $"Professional License – Valid until {ExpiresUtc:yyyy-MM-dd}"
                            : "Professional License",

                _ => "Unlicensed"
            };
        }
    }


}
