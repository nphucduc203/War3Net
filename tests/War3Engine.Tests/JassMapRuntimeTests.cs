using War3Engine.Assets;
using War3Engine.Core;
using War3Engine.Scripting;
using Xunit;

namespace War3Engine.Tests;

public class JassMapRuntimeTests
{
    [Fact]
    public void TargetMapInitializationRunsThroughTheVm()
    {
        var settings = EngineSettings.Load();
        if (!File.Exists(settings.YdweMapPath))
        {
            Console.WriteLine($"SKIPPED (YDWE map not found): {settings.YdweMapPath}");
            return;
        }

        using var store = new AssetStore(settings.WarcraftPath);
        store.MountMap(settings.YdweMapPath);
        var common = System.Text.Encoding.UTF8.GetString(store.ReadAllBytes(@"Scripts\common.j", out _));
        var blizzard = System.Text.Encoding.UTF8.GetString(store.ReadAllBytes(@"Scripts\Blizzard.j", out _));
        var map = System.Text.Encoding.UTF8.GetString(store.ReadAllBytes("war3map.j", out _));

        var result = JassMapHost.Run(common, blizzard, map, 5_000_000, advanceMs: 0);
        if (result.Failure is UnsupportedNativeException unsupported)
        {
            Assert.Fail($"unsupported native during init: {unsupported.NativeName} ({unsupported.Signature})\n{unsupported.Describe()}");
        }

        Assert.Null(result.Failure);
        Assert.True(result.GlobalsInitialized);
        Assert.True(result.MainCompleted);
        Assert.True(result.UnitsAlive > 0, "map init created no units");
        Assert.True(result.TriggerCount > 100, $"unexpected trigger count {result.TriggerCount}");
        Assert.True(result.TimerCount > 0);
        Assert.True(result.NativeCalls > 1_000);
        Console.WriteLine($"map init: units={result.UnitsAlive} triggers={result.TriggerCount} timers={result.TimerCount} frames={result.FrameCount} natives={result.NativeCalls} steps={result.Vm.StepsExecuted}");
    }

    [Fact]
    public void TimersExecuteCallbacksAgainstRuntimeState()
    {
        const string script = """
            native CreateTimer takes nothing returns timer
            native TimerStart takes timer t, real timeout, boolean periodic, code handler returns nothing
            native I2S takes integer i returns string

            globals
                integer N = 0
            endglobals

            function Callback takes nothing returns nothing
                set N = N + 1
            endfunction

            function StartOneShot takes nothing returns nothing
                local timer t = CreateTimer()
                call TimerStart(t, 0.10, false, function Callback)
            endfunction

            function StartPeriodic takes nothing returns nothing
                local timer t = CreateTimer()
                call TimerStart(t, 0.05, true, function Callback)
            endfunction

            function GetN takes nothing returns integer
                return N
            endfunction
            """;

        var program = JassProgramModel.Build(JassProgram.Parse(script));
        var runtime = new JassGameRuntime();
        var registry = new JassNativeRegistry();
        StandardNatives.Register(registry, runtime);
        var vm = new JassVirtualMachine(program, registry);
        runtime.Vm = vm;
        vm.InitializeGlobals();

        vm.Invoke("StartOneShot");
        runtime.Advance(50);
        runtime.RunDueTimers();
        Assert.Equal(0, vm.Invoke("GetN").Integer);
        runtime.Advance(60);
        runtime.RunDueTimers();
        Assert.True(vm.Invoke("GetN").Integer == 1, $"n={vm.Invoke("GetN").Integer} timers={runtime.Timers.Count} fired={runtime.TimerCallbacksFired} remaining={string.Join(",", runtime.Timers.Select(t => t.RemainingMs))} running={string.Join(",", runtime.Timers.Select(t => t.Running))}");

        vm.Invoke("StartPeriodic");
        runtime.Advance(160);
        runtime.RunDueTimers();
        Assert.True(vm.Invoke("GetN").Integer >= 3, $"n={vm.Invoke("GetN").Integer} fired={runtime.TimerCallbacksFired} timers={runtime.Timers.Count}");
        Console.WriteLine($"timer callbacks fired: {runtime.TimerCallbacksFired}");
    }
}
