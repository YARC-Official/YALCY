using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using Avalonia;
using HidSharp;
using HidSharp.Reports.Units;
using ReactiveUI;

namespace YALCY.ViewModels;

public static class Helpers
{
    public static (bool, string) IpValidator(string? bridgeIp)
    {
        const string pattern = @"\b(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\b";

        if (string.IsNullOrWhiteSpace(bridgeIp))
        {
            return (false, "IP Status: No IP address entered.");
        }

        if (!Regex.IsMatch(bridgeIp, pattern))
        {
            return (false, "IP Status: Not a valid IP address.");
        }

        return (true, "IP Status: IP address is valid.");
    }
}
