namespace War3Engine.Scripting;

public delegate JassValue JassNativeImplementation(JassVirtualMachine vm, JassValue[] arguments);

public sealed record JassNative(
    string Name,
    string ReturnType,
    IReadOnlyList<string> ParameterTypes,
    JassNativeImplementation Implementation,
    string Status = "implemented",
    string SideEffects = "");

/// <summary>
/// Typed native dispatch table. Missing natives are always reported, never silently
/// replaced with zero/null.
/// </summary>
public sealed class JassNativeRegistry
{
    private readonly Dictionary<string, JassNative> _natives = new(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, JassNative> Natives => _natives;

    public void Register(JassNative native)
    {
        _natives[native.Name] = native;
    }

    public void Register(
        string name,
        string returnType,
        IReadOnlyList<string> parameters,
        JassNativeImplementation implementation,
        string sideEffects = "")
    {
        Register(new JassNative(name, returnType, parameters, implementation, "implemented", sideEffects));
    }

    public bool TryGet(string name, out JassNative native) => _natives.TryGetValue(name, out native!);

    public IEnumerable<string> Names => _natives.Keys;
}
