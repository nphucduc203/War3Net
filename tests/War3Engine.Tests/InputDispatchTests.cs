using War3Engine.Client;
using War3Engine.Scripting;
using Xunit;

namespace War3Engine.Tests;

public class InputDispatchTests
{
    [Fact]
    public void ChatHotkeyDispatchesThroughTriggerEventLayer()
    {
        const string script = """
            native CreateTrigger takes nothing returns trigger
            native TriggerRegisterPlayerChatEvent takes trigger t, player p, string chatMessage, boolean exactMatch returns event
            native TriggerAddAction takes trigger t, code actionFunc returns triggeraction
            native Player takes integer number returns player

            globals
                integer G = 0
            endglobals

            function Actions takes nothing returns nothing
                set G = G + 1
            endfunction

            function Init takes nothing returns nothing
                local trigger t = CreateTrigger()
                call TriggerRegisterPlayerChatEvent(t, Player(0), "hello", true)
                call TriggerAddAction(t, function Actions)
            endfunction

            function GetG takes nothing returns integer
                return G
            endfunction
            """;

        var program = JassProgramModel.Build(JassProgram.Parse(script));
        var runtime = new JassGameRuntime();
        var registry = new JassNativeRegistry();
        StandardNatives.Register(registry, runtime);
        var vm = new JassVirtualMachine(program, registry);
        runtime.Vm = vm;
        vm.InitializeGlobals();
        vm.Invoke("Init");

        Assert.Equal(0, ViewerInput.DispatchChatHotkey(runtime, "bye"));
        Assert.Equal(1, ViewerInput.DispatchChatHotkey(runtime, "hello"));
        Assert.Equal(1, vm.Invoke("GetG").Integer);
        Assert.Equal(1, runtime.ChatEventsFired);
    }
}
