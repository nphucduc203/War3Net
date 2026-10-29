using War3Engine.Assets;
using War3Engine.Core;
using War3Engine.Scripting;

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
        bool traceNatives)
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

        var result = JassMapHost.Run(commonSource, blizzardSource, mapSource, maxSteps, advanceMs);
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

        Console.WriteLine($"state: units={result.UnitsAlive} triggers={result.TriggerCount} timers={result.TimerCount} frames={result.FrameCount} timerCallbacks={result.TimerCallbacks} tocs={result.Runtime.LoadedTocs.Count}");
        Console.WriteLine($"native calls: {result.NativeCalls}");
        foreach (var pair in result.Runtime.NativeCalls.OrderByDescending(p => p.Value).Take(15))
        {
            Console.WriteLine($"  {pair.Key}: {pair.Value}");
        }

        Console.WriteLine("RESULT: target map initialization executed by the JASS VM");
        return 0;
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
