namespace War3Engine.Scripting;

/// <summary>Runtime handle object. State is owned by the runtime service that created it.</summary>
public sealed class JassHandle
{
    private static int _nextId;

    public JassHandle(string type, object? state = null)
    {
        Id = Interlocked.Increment(ref _nextId);
        Type = type;
        State = state;
    }

    public int Id { get; }

    public string Type { get; }

    public object? State { get; set; }

    public override string ToString() => $"{Type}#{Id}";
}

public sealed class JassExecutionOptions
{
    public long MaxSteps { get; init; } = 20_000_000;

    public int MaxCallDepth { get; init; } = 256;

    /// <summary>WC3 release builds skip debug blocks; enable to execute them in tests.</summary>
    public bool ExecuteDebugStatements { get; init; }

    /// <summary>When false, any handle satisfies a handle parameter (common.j hierarchy is not loaded for map scripts).</summary>
    public bool StrictHandleTypes { get; init; }
}

public class JassRuntimeException : Exception
{
    public JassRuntimeException(string message, string? nativeName = null)
        : base(message)
    {
        NativeName = nativeName;
    }

    public string? NativeName { get; set; }

    public IReadOnlyList<string> CallStack { get; set; } = Array.Empty<string>();

    public string Describe()
    {
        var stack = CallStack.Count == 0 ? string.Empty : $"\ncall stack: {string.Join(" <- ", CallStack)}";
        return $"{Message}{(NativeName is null ? string.Empty : $" (native {NativeName})")}{stack}";
    }
}

public sealed class UnsupportedNativeException : JassRuntimeException
{
    public UnsupportedNativeException(string name, string signature, IReadOnlyList<string> callStack)
        : base($"unsupported native '{name}' ({signature})", name)
    {
        Signature = signature;
        CallStack = callStack;
    }

    public string Signature { get; }
}
