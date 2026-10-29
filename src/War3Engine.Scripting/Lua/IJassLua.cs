namespace War3Engine.Scripting.Lua;

public enum LuaErrorKind
{
    None,
    Syntax,
    Runtime,
    UnsupportedValue,
    Internal,
}

public sealed record LuaEvaluation(
    string Expression,
    string? Value,
    string? Error,
    LuaErrorKind ErrorKind);

/// <summary>
/// JASS -> Lua expression bridge with YDWE semantics: the input is a JASS expression
/// (trigstring-decoded), executed as Lua, and the resulting Lua string/number is returned.
/// Non-string values become a null JASS string.
/// </summary>
public interface IJassLua
{
    LuaEvaluation Evaluate(string jassExpression);

    IReadOnlyList<LuaEvaluation> History { get; }
}

/// <summary>Resolves a jass.slk field (kind, object id, field) to a Lua value.</summary>
public delegate object? JassSlkQuery(string kind, int objectId, string field);
