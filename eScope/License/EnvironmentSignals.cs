using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace eScope.License
{
    using Microsoft.Win32;
    using System.DirectoryServices.ActiveDirectory;

    public static class EnvironmentSignals
    {
        // Active Directory domain or Azure AD / Entra ID: likely a work computer
        public static bool IsManagedByOrganization() => IsDomainJoined() || IsAzureAdJoined();


        public static bool IsDomainJoined()
        {
            try
            {
                Domain.GetComputerDomain();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool IsAzureAdJoined()
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                @"SYSTEM\CurrentControlSet\Control\CloudDomainJoin\JoinInfo");

            return key != null && key.GetSubKeyNames().Length > 0;
        }
    }

}
