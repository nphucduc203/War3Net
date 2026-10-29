using War3Engine.Scripting.Lua;

namespace War3Engine.Scripting;

public sealed class JassMapRunResult
{
    public required JassProgramModel Program { get; init; }

    public required JassGameRuntime Runtime { get; init; }

    public required JassVirtualMachine Vm { get; init; }

    public required JassNativeRegistry Registry { get; init; }

    public bool GlobalsInitialized { get; set; }

    public bool MainCompleted { get; set; }

    public Exception? Failure { get; set; }

    public long ParseMilliseconds { get; set; }

    public long InitMilliseconds { get; set; }

    public long MainMilliseconds { get; set; }

    public int TimerCallbacks => Runtime.TimerCallbacksFired;

    public int UnitsAlive => Runtime.Units.Count(u => !u.Removed);

    public int TriggerCount => Runtime.Triggers.Count;

    public int TimerCount => Runtime.Timers.Count;

    public int FrameCount => Runtime.Frames.Count;

    public int ChatFired { get; set; }

    public long NativeCalls => Runtime.NativeCalls.Values.Sum();
}

/// <summary>
/// Composes common.j + Blizzard.j + map script into a runnable JASS program and executes
/// the map initialization lifecycle. Shared by the inspector and the test suite.
/// </summary>
public static class JassMapHost
{
    public static JassNativeRegistry CreateRegistry(JassProgramModel program, JassGameRuntime runtime)
    {
        var registry = new JassNativeRegistry();
        StandardNatives.Register(registry, runtime);
        RuntimeNatives.Register(registry, program, runtime);
        ConstantNatives.Register(registry, program, runtime);
        YdweNatives.Register(registry, runtime);
        return registry;
    }

    public static JassMapRunResult Run(
        string commonSource,
        string blizzardSource,
        string mapSource,
        long maxSteps = 5_000_000,
        int advanceMs = 0,
        bool executeDebugStatements = false,
        IJassLua? lua = null,
        string? chatMessage = null)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var common = JassProgramModel.Build(JassProgram.Parse(commonSource));
        var blizzard = JassProgramModel.Build(JassProgram.Parse(blizzardSource));
        var map = JassProgramModel.Build(JassProgram.Parse(mapSource));
        var merged = JassProgramMerge.Merge(common, blizzard, map);
        watch.Stop();

        var runtime = new JassGameRuntime { Lua = lua };
        var registry = CreateRegistry(merged, runtime);
        var vm = new JassVirtualMachine(merged, registry, new JassExecutionOptions
        {
            MaxSteps = maxSteps,
            ExecuteDebugStatements = executeDebugStatements,
        });
        runtime.Vm = vm;

        var result = new JassMapRunResult
        {
            Program = merged,
            Runtime = runtime,
            Vm = vm,
            Registry = registry,
            ParseMilliseconds = watch.ElapsedMilliseconds,
        };

        try
        {
            watch.Restart();
            vm.InitializeGlobals();
            result.GlobalsInitialized = true;
            result.InitMilliseconds = watch.ElapsedMilliseconds;

            watch.Restart();
            vm.Invoke("main");
            result.MainCompleted = true;
            result.MainMilliseconds = watch.ElapsedMilliseconds;

            Advance(runtime, advanceMs);

            if (!string.IsNullOrEmpty(chatMessage))
            {
                result.ChatFired = runtime.DispatchChat(0, chatMessage);
                runtime.Advance(0);
                runtime.RunDueTimers();
            }
        }
        catch (Exception ex)
        {
            result.Failure = ex;
        }

        return result;
    }

    /// <summary>Advances the runtime in deterministic 100 ms steps and runs due timers.</summary>
    public static void Advance(JassGameRuntime runtime, int milliseconds)
    {
        runtime.Advance(0);
        runtime.RunDueTimers();
        for (var elapsed = 0; elapsed < milliseconds; elapsed += 100)
        {
            runtime.Advance(100);
            runtime.RunDueTimers();
        }
    }
}

/// <summary>Registers Convert* enum helpers with cached handles (common.j constants).</summary>
public static class ConstantNatives
{
    private static readonly Dictionary<(string Type, int Id), JassHandle> Cache = new();

    public static void Register(JassNativeRegistry registry, JassProgramModel program, JassGameRuntime? runtime = null)
    {
        foreach (var native in program.Natives.Values)
        {
            if (!native.Name.StartsWith("Convert", StringComparison.Ordinal))
            {
                continue;
            }

            var parameters = native.Parameters.Select(p => p.Type).ToArray();
            var returnType = native.ReturnType.Trim();
            var primitive = returnType is "integer" or "real" or "boolean" or "string";
            registry.Register(new JassNative(
                native.Name,
                native.ReturnType,
                parameters,
                (_, args) =>
                {
                    runtime?.CountNative(native.Name);
                    if (primitive)
                    {
                        return returnType switch
                        {
                            "integer" => JassValue.FromInteger(args.Length > 0 ? args[0].Integer : 0),
                            "real" => JassValue.FromReal(args.Length > 0 ? args[0].Integer : 0),
                            "boolean" => JassValue.FromBoolean(args.Length > 0 && args[0].Integer != 0),
                            _ => JassValue.FromString(string.Empty),
                        };
                    }

                    var id = args.Length > 0 ? args[0].Integer : 0;
                    if (!Cache.TryGetValue((returnType, id), out var handle))
                    {
                        handle = new JassHandle(returnType, id);
                        Cache[(returnType, id)] = handle;
                    }

                    return JassValue.FromHandle(handle);
                },
                "implemented",
                "Returns a cached enum handle carrying the integer id"));
        }
    }
}
