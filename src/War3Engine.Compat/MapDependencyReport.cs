using System.Text;

namespace War3Engine.Compat;

public sealed record MapImportStatus(string Path, bool Resolved, string Kind);

/// <summary>
/// Per-map compatibility report: which extension natives and resources the map needs,
/// which are covered by enabled profiles, and which remain unresolved.
/// </summary>
public sealed class MapDependencyReport
{
    public required string MapPath { get; init; }

    public long MapBytes { get; init; }

    public string? GameVersion { get; init; }

    public required IReadOnlyList<string> EnabledProfiles { get; init; }

    public required JassScriptInventory.Result Script { get; init; }

    public required IReadOnlyList<DetectedComponent> Components { get; init; }

    public required IReadOnlyList<MapImportStatus> Imports { get; init; }

    public IReadOnlyList<RequiredNative> RequiredNatives => Script.Natives;

    public int UnresolvedImports => Imports.Count(i => !i.Resolved);

    public string ProviderStatus(string provider, bool compatEnabled)
    {
        if (RequiredNatives.All(n => n.Provider != provider))
        {
            return "not required";
        }

        return compatEnabled ? "stub registered by compatibility profile" : NativeSupportStatus.Unsupported.ToString();
    }

    public string ToText()
    {
        var text = new StringBuilder();
        text.AppendLine($"map: {MapPath} ({MapBytes} bytes)");
        text.AppendLine($"game version: {GameVersion ?? "unknown"}");
        text.AppendLine($"profiles enabled: {(EnabledProfiles.Count == 0 ? "classic only" : string.Join(", ", EnabledProfiles))}");
        text.AppendLine();
        text.AppendLine("components:");
        foreach (var component in Components)
        {
            text.AppendLine($"  {component.Name} [{component.Kind}] version={component.Version ?? "?"} path={component.Path}");
        }

        text.AppendLine();
        text.AppendLine($"script: {Script.Language} lines={Script.Lines} bytes={Script.ScriptBytes}");
        text.AppendLine($"libraries ({Script.Libraries.Count}): {string.Join(", ", Script.Libraries.Select(l => l.Name))}");
        text.AppendLine($"required extension natives: {RequiredNatives.Count} (declared), {RequiredNatives.Count(n => n.CallCount > 0)} called");
        foreach (var group in Script.NativesByProvider)
        {
                text.AppendLine($"  {group.Key}: {group.Count()} declared, {group.Count(n => n.CallCount > 0)} called");
        }

        text.AppendLine($"imports: {Imports.Count} resolved={Imports.Count - UnresolvedImports} unresolved={UnresolvedImports}");
        foreach (var import in Imports.Where(i => !i.Resolved).Take(20))
        {
            text.AppendLine($"  MISSING {import.Path}");
        }

        foreach (var group in Script.NativesByProvider)
        {
            text.AppendLine();
            text.AppendLine($"{group.Key} natives ({group.Count()}) [{(EnabledProfiles.Count > 0 ? "compat profile enabled: stubs" : "unsupported")}]:");
            foreach (var native in group.OrderByDescending(n => n.CallCount))
            {
                text.AppendLine($"  {native.Name} calls={native.CallCount} takes {native.Parameters} returns {native.Returns}");
            }
        }

        return text.ToString();
    }
}
