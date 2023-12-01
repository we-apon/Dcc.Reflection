using System.Buffers;

namespace Dcc.Extensions;

public static partial class StringExtensions {
    public static bool IsBase64String(this string? base64) {
        if (base64 == null || base64.Length % 4 != 0)
            return false;

        var span = base64.AsSpan();
        var index = span.IndexOfAnyExcept(_base64Chars);
        if (index == -1)
            return true;

        return span[index..].IndexOfAnyExcept(_equalSign) == -1;
    }

    public static bool IsHexString(this string? hex) {
        if (hex == null || hex.Length % 2 != 0)
            return false;

        var span = hex.AsSpan();
        return span.IndexOfAnyExcept(_hexCharsUpper) == -1 || span.IndexOfAnyExcept(_hexCharsLower) == -1;
    }

    // [GeneratedRegex(@"^[a-zA-Z0-9\+/]*={0,3}$", RegexOptions.None)]
    // private static partial Regex Base64Regex();

    static readonly SearchValues<char> _hexCharsUpper = SearchValues.Create("ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789");
    static readonly SearchValues<char> _hexCharsLower = SearchValues.Create("abcdefghijklmnopqrstuvwxyz0123456789");
    static readonly SearchValues<char> _base64Chars = SearchValues.Create("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/");
    static readonly SearchValues<char> _equalSign = SearchValues.Create("=");
}
