using System.Text;
using System.Text.RegularExpressions;

namespace War3Engine.Scripting.Lua;

/// <summary>Converts WC3 trigstrings to plain text (YDWE calls jass::from_trigstring before Lua).</summary>
public static partial class JassTrigString
{
    public static string FromTrigString(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var stripped = ColorCode().Replace(text, string.Empty);
        stripped = ResetCode().Replace(stripped, string.Empty);
        stripped = stripped.Replace("|n", "\n", StringComparison.Ordinal)
                           .Replace("|r", string.Empty, StringComparison.Ordinal)
                           .Replace("||", "|", StringComparison.Ordinal);
        return stripped;
    }

    [GeneratedRegex(@"\|c[0-9a-fA-F]{8}", RegexOptions.Compiled)]
    private static partial Regex ColorCode();

    [GeneratedRegex(@"\|r", RegexOptions.Compiled)]
    private static partial Regex ResetCode();
}
