using System.Text.RegularExpressions;

namespace Dcc.Extensions;

public static class StringExtensions {
    public static bool IsBase64String(this string? base64) {
        return base64 != null && base64.Length % 4 == 0 && Regex.IsMatch(base64, @"^[a-zA-Z0-9\+/]*={0,3}$", RegexOptions.None);
    }

    public static bool IsHexString(this string? hex) {
        return hex != null && hex.Length % 2 == 0 && hex.All(x => char.IsDigit(x) || "ABCDEF".Contains(char.ToUpperInvariant(x)));
    }
}
