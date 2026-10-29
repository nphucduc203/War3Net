using War3Net.CodeAnalysis.Jass.Syntax;

namespace War3Engine.Scripting;

public enum JassValueKind
{
    Null,
    Integer,
    Real,
    Boolean,
    String,
    Handle,
    Code,
}

/// <summary>Immutable JASS value. Handles are reference objects; code values name a function.</summary>
public readonly struct JassValue : IEquatable<JassValue>
{
    private JassValue(JassValueKind kind, int integer, float real, bool boolean, string? text, JassHandle? handle)
    {
        Kind = kind;
        Integer = integer;
        Real = real;
        Boolean = boolean;
        Text = text;
        Handle = handle;
    }

    public JassValueKind Kind { get; }

    public int Integer { get; }

    public float Real { get; }

    public bool Boolean { get; }

    public string? Text { get; }

    public JassHandle? Handle { get; }

    public static JassValue Null => default;

    public static JassValue FromInteger(int value) => new(JassValueKind.Integer, value, value, false, null, null);

    public static JassValue FromReal(float value) => new(JassValueKind.Real, (int)value, value, false, null, null);

    public static JassValue FromBoolean(bool value) => new(JassValueKind.Boolean, value ? 1 : 0, value ? 1 : 0, value, null, null);

    public static JassValue FromString(string value) => new(JassValueKind.String, 0, 0, false, value, null);

    public static JassValue FromHandle(JassHandle value) => new(JassValueKind.Handle, 0, 0, false, null, value);

    public static JassValue FromCode(string functionName) => new(JassValueKind.Code, 0, 0, false, functionName, null);

    public bool IsNull => Kind == JassValueKind.Null || (Kind == JassValueKind.Handle && Handle is null);

    public string DisplayType => Kind switch
    {
        JassValueKind.Null => "null",
        JassValueKind.Integer => "integer",
        JassValueKind.Real => "real",
        JassValueKind.Boolean => "boolean",
        JassValueKind.String => "string",
        JassValueKind.Handle => Handle?.Type ?? "handle",
        JassValueKind.Code => "code",
        _ => "?",
    };

    public override string ToString() => Kind switch
    {
        JassValueKind.Null => "null",
        JassValueKind.Integer => Integer.ToString(System.Globalization.CultureInfo.InvariantCulture),
        JassValueKind.Real => Real.ToString(System.Globalization.CultureInfo.InvariantCulture),
        JassValueKind.Boolean => Boolean ? "true" : "false",
        JassValueKind.String => Text ?? string.Empty,
        JassValueKind.Handle => Handle is null ? "null" : $"{Handle.Type}#{Handle.Id}",
        JassValueKind.Code => $"function {Text}",
        _ => "?",
    };

    public bool Equals(JassValue other) => Kind switch
    {
        JassValueKind.Null => other.IsNull,
        JassValueKind.Integer => other.Kind == JassValueKind.Integer && Integer == other.Integer,
        JassValueKind.Real => (other.Kind == JassValueKind.Real && Real.Equals(other.Real)) ||
                              (other.Kind == JassValueKind.Integer && Real == other.Integer),
        JassValueKind.Boolean => other.Kind == JassValueKind.Boolean && Boolean == other.Boolean,
        JassValueKind.String => other.Kind == JassValueKind.String && string.Equals(Text, other.Text, StringComparison.Ordinal),
        JassValueKind.Handle => other.Kind == JassValueKind.Handle && ReferenceEquals(Handle, other.Handle),
        JassValueKind.Code => other.Kind == JassValueKind.Code && string.Equals(Text, other.Text, StringComparison.Ordinal),
        _ => false,
    };

    public override bool Equals(object? obj) => obj is JassValue value && Equals(value);

    public override int GetHashCode() => Kind switch
    {
        JassValueKind.Integer => Integer,
        JassValueKind.Real => Real.GetHashCode(),
        JassValueKind.Boolean => Boolean.GetHashCode(),
        JassValueKind.String => Text?.GetHashCode(StringComparison.Ordinal) ?? 0,
        JassValueKind.Handle => Handle?.GetHashCode() ?? 0,
        JassValueKind.Code => Text?.GetHashCode(StringComparison.Ordinal) ?? 0,
        _ => 0,
    };
}
