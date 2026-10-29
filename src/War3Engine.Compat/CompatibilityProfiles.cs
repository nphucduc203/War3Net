namespace War3Engine.Compat;

public enum NativeSupportStatus
{
    /// <summary>Declared by a map but no engine implementation exists.</summary>
    Unsupported,

    /// <summary>Registered as a safe placeholder because a compatibility profile is enabled.</summary>
    Stub,

    Implemented,
}

/// <summary>A single JASS native required by a map script.</summary>
public sealed record RequiredNative(string Name, string Parameters, string Returns, string Provider, int CallCount);

public sealed record ExtensionLibrary(string Name);

/// <summary>Static description of a compatibility environment (never loads YDWEConfig).</summary>
public sealed record CompatibilityProfile(string Id, string DisplayName, IReadOnlyList<string> Extensions, bool IsDefault);

public static class CompatibilityProfiles
{
    public const string Ydwe = "ydwe";
    public const string Kkwe = "kkwe";
    public const string DzApi = "dzapi";

    public static readonly CompatibilityProfile Classic = new(
        "classic-1.27",
        "Warcraft III classic 1.27.0.52240 (MPQ)",
        Array.Empty<string>(),
        IsDefault: true);

    public static readonly CompatibilityProfile YdweProfile = new(
        Ydwe,
        "YDWE 2.0.0.2485 runtime extensions (EX/JAPI natives)",
        new[] { "YDWE-JAPI", "YDWE-Extra" },
        IsDefault: false);

    public static readonly CompatibilityProfile KkweProfile = new(
        Kkwe,
        "KKWE 2485 editor/runtime plugin set",
        new[] { "KKWE" },
        IsDefault: false);

    public static readonly CompatibilityProfile DzApiProfile = new(
        DzApi,
        "DzAPI plugin (dz_w3_plugin.dll)",
        new[] { "DzAPI" },
        IsDefault: false);

    public static readonly IReadOnlyList<CompatibilityProfile> All = new[]
    {
        Classic, YdweProfile, KkweProfile, DzApiProfile,
    };
}

/// <summary>
/// Maps a native name to the environment that provides it. Rules are intentionally
/// small and evidence-based: names seen in real maps and in the local plugin binaries.
/// </summary>
public static class NativeProviders
{
    public const string DzApi = "DzAPI";
    public const string Japi = "YDWE-JAPI";
    public const string Extra = "YDWE-Extra";
    public const string Unknown = "Unknown";

    public static string Classify(string name)
    {
        if (name.StartsWith("Dz", StringComparison.Ordinal))
        {
            return DzApi;
        }

        if (name.StartsWith("RequestExtra", StringComparison.Ordinal) ||
            name == "EXExecuteScript")
        {
            return Extra;
        }

        if (name.StartsWith("EX", StringComparison.Ordinal) ||
            name.StartsWith("Japi", StringComparison.OrdinalIgnoreCase))
        {
            return Japi;
        }

        return Unknown;
    }
}
