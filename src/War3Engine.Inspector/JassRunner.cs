using War3Engine.Assets;
using War3Engine.Core;
using War3Engine.Scripting;
using War3Engine.Scripting.Lua;
using War3Net.Common.Extensions;

namespace War3Engine.Inspector;

/// <summary>Runs the target map's JASS initialization through the shared map host.</summary>
internal static class JassRunner
{
    public static int Run(
        EngineSettings settings,
        AssetStore store,
        string? mapPath,
        long maxSteps,
        int advanceMs,
        bool traceNatives,
        string[]? args = null)
    {
        var target = mapPath
                     ?? (string.IsNullOrWhiteSpace(settings.YdweMapPath) ? settings.MapPath : settings.YdweMapPath);
        if (!File.Exists(target))
        {
            Console.WriteLine($"map not found: {target}");
            return 1;
        }

        store.MountMap(target);
        var commonSource = Read(store, @"Scripts\common.j");
        var blizzardSource = Read(store, @"Scripts\Blizzard.j");
        var mapSource = Read(store, "war3map.j");
        Console.WriteLine($"scripts: common.j={commonSource.Length} blizzard.j={blizzardSource.Length} war3map.j={mapSource.Length}");

        var slk = JassSlkQueryProvider.Load(store, target);
        var bridge = new NLuaBridge(slk.Query);
        var chat = GetOption(args: Environment.GetCommandLineArgs(), "--chat");
        var result = JassMapHost.Run(commonSource, blizzardSource, mapSource, maxSteps, advanceMs, lua: bridge, chatMessage: chat);
        Console.WriteLine($"parsed in {result.ParseMilliseconds} ms; functions={result.Program.Functions.Count} natives={result.Program.Natives.Count} globals={result.Program.Globals.Count} types={result.Program.Types.Count}");
        if (result.GlobalsInitialized)
        {
            Console.WriteLine($"globals initialized in {result.InitMilliseconds} ms");
        }

        if (result.MainCompleted)
        {
            Console.WriteLine($"main completed in {result.MainMilliseconds} ms (steps={result.Vm.StepsExecuted})");
        }

        if (result.Failure is UnsupportedNativeException unsupported)
        {
            Console.WriteLine($"RESULT: blocked by unsupported native '{unsupported.NativeName}' ({unsupported.Signature})");
            Console.WriteLine(unsupported.Describe());
            return 2;
        }

        if (result.Failure is JassRuntimeException runtimeError)
        {
            Console.WriteLine($"RESULT: runtime error: {runtimeError.Describe()}");
            return 3;
        }

        if (result.Failure is not null)
        {
            Console.WriteLine($"RESULT: failure: {result.Failure}");
            return 3;
        }

        if (traceNatives)
        {
            foreach (var pair in result.Runtime.NativeCalls.OrderByDescending(p => p.Value).Take(25))
            {
                Console.WriteLine($"  trace {pair.Key}: {pair.Value}");
            }
        }

        var chatPatterns = result.Runtime.Registrations
            .Where(r => r.Kind == "chat")
            .Select(r => $"chat:{r.PlayerId}:{r.Key}:{r.Exact}")
            .Distinct()
            .Take(30)
            .ToArray();
        Console.WriteLine($"chat triggers ({chatPatterns.Length}): {string.Join(" | ", chatPatterns)}");
        var art = MapArtCache.Load(store, target);
        foreach (var unit in result.Runtime.Units.Where(u => !u.Removed))
        {
            var resolved = art.ResolveUnit(unit.TypeId);
            art.TryGetOverride(unit.TypeId, out var overrideModel, out var baseId);
            Console.WriteLine($"unit {unit.TypeId.ToRawcode()} owner={unit.OwnerId} @{unit.X:F0},{unit.Y:F0} facing={unit.Facing:F2} model={resolved?.ModelPath ?? "UNRESOLVED"} scale={resolved?.Scale ?? 0:F2} override={overrideModel ?? "-"} base={baseId.ToRawcode()} overrides={art.OverrideCount}");
        }
        Console.WriteLine($"messages: {result.Runtime.DisplayMessages.Count}; last={result.Runtime.DisplayMessages.LastOrDefault()}");
        if (result.Vm.TryGetGlobalValue("udg_guaibs", out var wave))
        {
            Console.WriteLine($"global udg_guaibs = {wave}");
        }

        Console.WriteLine($"state: units={result.UnitsAlive} triggers={result.TriggerCount} timers={result.TimerCount} frames={result.FrameCount} timerCallbacks={result.TimerCallbacks} chatFired={result.ChatFired} tocs={result.Runtime.LoadedTocs.Count}");
        Console.WriteLine($"native calls: {result.NativeCalls}");
        foreach (var pair in result.Runtime.NativeCalls.OrderByDescending(p => p.Value).Take(15))
        {
            Console.WriteLine($"  {pair.Key}: {pair.Value}");
        }

        Console.WriteLine($"slk: wts={slk.WtsEntries} itemI033={slk.DebugItemFields(858992713)}");
        Console.WriteLine($"lua: calls={bridge.History.Count} errors={bridge.History.Count(h => h.ErrorKind != LuaErrorKind.None)} missingFields={bridge.MissingFields.Count}");
        foreach (var call in bridge.History.Where(h => h.ErrorKind != LuaErrorKind.None).Take(3))
        {
            Console.WriteLine($"  lua error [{call.ErrorKind}] expr={call.Expression} err={call.Error}");
        }

        foreach (var call in bridge.History.Take(5))
        {
            Console.WriteLine($"  lua expr={call.Expression} => {(call.Value is null ? "null" : call.Value)}");
        }

        Console.WriteLine("RESULT: target map initialization executed by the JASS VM");
        return 0;
    }

    private static string? GetOption(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == name)
            {
                return args[i + 1];
            }
        }

        return null;
    }

    private static string Read(AssetStore store, string path)
    {
        try
        {
            return System.Text.Encoding.UTF8.GetString(store.ReadAllBytes(path, out _));
        }
        catch (FileNotFoundException)
        {
            return string.Empty;
        }
    }
}
