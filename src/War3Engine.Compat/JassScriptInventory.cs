using System.Text.RegularExpressions;

namespace War3Engine.Compat;

/// <summary>
/// Extracts the dependency surface of a compiled JASS map script without executing it:
/// declared extension natives, YDWE libraries and the script language.
/// </summary>
public static partial class JassScriptInventory
{
    public sealed class Result
    {
        public string Language { get; init; } = "jass";

        public List<RequiredNative> Natives { get; init; } = new();

        public List<ExtensionLibrary> Libraries { get; init; } = new();

        public int ScriptBytes { get; init; }

        public int Lines { get; init; }

        public IEnumerable<IGrouping<string, RequiredNative>> NativesByProvider =>
            Natives.GroupBy(n => n.Provider).OrderByDescending(g => g.Count());
    }

    public static Result Parse(string script)
    {
        var result = new Result
        {
            ScriptBytes = System.Text.Encoding.UTF8.GetByteCount(script),
            Lines = script.Count(c => c == '\n') + 1,
        };

        var calls = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (Match match in CallSite().Matches(script))
        {
            var name = match.Groups[1].Value;
            calls[name] = calls.TryGetValue(name, out var count) ? count + 1 : 1;
        }

        foreach (Match match in NativeDeclaration().Matches(script))
        {
            var name = match.Groups[1].Value;
            result.Natives.Add(new RequiredNative(
                name,
                match.Groups[2].Value.Trim(),
                match.Groups[3].Value.Trim(),
                NativeProviders.Classify(name),
                calls.TryGetValue(name, out var used) ? used : 0));
        }

        var libraries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in GlobalsLibrary().Matches(script))
        {
            libraries.Add(match.Groups[1].Value);
        }

        foreach (Match match in LibraryComment().Matches(script))
        {
            libraries.Add(match.Groups[1].Value);
        }

        result.Libraries.AddRange(libraries.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).Select(x => new ExtensionLibrary(x)));
        return result;
    }

    // vJASS compiles to flat JASS; extension natives are the only "native" lines left.
    [GeneratedRegex(@"^[ \t]*(?:constant[ \t]+)?native[ \t]+([A-Za-z_]\w*)[ \t]+takes[ \t]+(.+?)[ \t]+returns[ \t]+(\w+)", RegexOptions.Multiline)]
    private static partial Regex NativeDeclaration();

    [GeneratedRegex(@"\b([A-Za-z_]\w*)\s*\(", RegexOptions.Multiline)]
    private static partial Regex CallSite();

    [GeneratedRegex(@"^//globals from ([A-Za-z0-9_]+):", RegexOptions.Multiline)]
    private static partial Regex GlobalsLibrary();

    [GeneratedRegex(@"^//(?:library|scope|module)[ \t]+([A-Za-z0-9_]+)", RegexOptions.Multiline)]
    private static partial Regex LibraryComment();
}
