using War3Engine.Scripting;
using Xunit;

namespace War3Engine.Tests;

public class JassVmTests
{
    private const string BasicScript = """
        globals
            integer G = 3
            integer array A
            constant real PI = 3.5
        endglobals

        function Add takes integer a, integer b returns integer
            return a + b
        endfunction

        function Fact takes integer n returns integer
            if n <= 1 then
                return 1
            endif
            return n * Fact(n - 1)
        endfunction

        function LoopSum takes nothing returns integer
            local integer i = 0
            local integer sum = 0
            loop
                exitwhen i >= 5
                set i = i + 1
                set sum = sum + i
            endloop
            return sum
        endfunction

        function UseArray takes nothing returns integer
            set A[2] = 7
            return A[0] + A[2]
        endfunction

        function GetG takes nothing returns integer
            return G
        endfunction

        function GetPI takes nothing returns real
            return PI
        endfunction
        """;

    private static JassVirtualMachine CreateVm(string script, JassNativeRegistry? natives = null, JassExecutionOptions? options = null)
    {
        var program = JassProgramModel.Build(JassProgram.Parse(script));
        var vm = new JassVirtualMachine(program, natives ?? new JassNativeRegistry(), options);
        vm.InitializeGlobals();
        return vm;
    }

    [Fact]
    public void ExecutesGlobalsFunctionsRecursionLoopsAndArrays()
    {
        var vm = CreateVm(BasicScript);
        Assert.Equal(5, vm.Invoke("Add", JassValue.FromInteger(2), JassValue.FromInteger(3)).Integer);
        Assert.Equal(120, vm.Invoke("Fact", JassValue.FromInteger(5)).Integer);
        Assert.Equal(15, vm.Invoke("LoopSum").Integer);
        Assert.Equal(7, vm.Invoke("UseArray").Integer);
        Assert.Equal(3, vm.Invoke("GetG").Integer);
        Assert.Equal(3.5f, vm.Invoke("GetPI").Real);
    }

    [Fact]
    public void DispatchesNativesWithTypeConversionAndCodeValues()
    {
        const string script = """
            native Test_Add takes integer a, integer b returns integer
            native Test_ExpectReal takes real x returns real
            native RunCode takes code c, integer v returns integer

            function Double takes integer x returns integer
                return x * 2
            endfunction

            function CallNative takes nothing returns integer
                return Test_Add(2, 5)
            endfunction

            function CallReal takes nothing returns real
                return Test_ExpectReal(3)
            endfunction

            function UseCode takes nothing returns integer
                return RunCode(function Double, 21)
            endfunction
            """;

        var registry = new JassNativeRegistry();
        registry.Register("Test_Add", "integer", new[] { "integer", "integer" },
            (_, args) => JassValue.FromInteger(args[0].Integer + args[1].Integer));
        registry.Register("Test_ExpectReal", "real", new[] { "real" },
            (_, args) => JassValue.FromReal(args[0].Real));
        registry.Register("RunCode", "integer", new[] { "code", "integer" },
            (vm, args) => vm.InvokeCode(args[0], new[] { args[1] }));

        var vm = CreateVm(script, registry);
        Assert.Equal(7, vm.Invoke("CallNative").Integer);
        Assert.Equal(3f, vm.Invoke("CallReal").Real);
        Assert.Equal(42, vm.Invoke("UseCode").Integer);
    }

    [Fact]
    public void UnsupportedNativeFailsWithNameAndCallStack()
    {
        const string script = """
            native Test_Missing takes integer x returns nothing

            function UsesMissing takes nothing returns nothing
                call Test_Missing(1)
            endfunction
            """;

        var vm = CreateVm(script);
        var ex = Assert.Throws<UnsupportedNativeException>(() => vm.Invoke("UsesMissing"));
        Assert.Equal("Test_Missing", ex.NativeName);
        Assert.Contains("UsesMissing", ex.CallStack);
        Assert.Contains("unsupported native", ex.Message);
    }

    [Fact]
    public void HandlesStringsBooleansNullAndStepLimits()
    {
        const string script = """
            native GetNull takes nothing returns handle

            function Strings takes nothing returns string
                return "a" + "b"
            endfunction

            function Logic takes integer x returns boolean
                return (x > 2) and not (x == 4)
            endfunction

            function IsNullH takes nothing returns boolean
                return GetNull() == null
            endfunction

            function Spin takes nothing returns nothing
                loop
                endloop
            endfunction

            function Bad takes nothing returns nothing
                local integer x = 1
                set x = "s"
            endfunction
            """;

        var registry = new JassNativeRegistry();
        registry.Register("GetNull", "handle", Array.Empty<string>(), (_, _) => JassValue.Null);
        var vm = CreateVm(script, registry, new JassExecutionOptions { MaxSteps = 5_000 });

        Assert.Equal("ab", vm.Invoke("Strings").Text);
        Assert.True(vm.Invoke("Logic", JassValue.FromInteger(3)).Boolean);
        Assert.False(vm.Invoke("Logic", JassValue.FromInteger(4)).Boolean);
        Assert.True(vm.Invoke("IsNullH").Boolean);
        Assert.Throws<JassRuntimeException>(() => vm.Invoke("Spin"));
        vm.ResetStepCounter();
        var typeError = Assert.Throws<JassRuntimeException>(() => vm.Invoke("Bad"));
        Assert.Contains("expected integer", typeError.Message);
    }

    [Fact]
    public void InitializesGlobalHandlesThroughNatives()
    {
        const string script = """
            native InitHashtable takes nothing returns hashtable
            globals
                hashtable HT = InitHashtable()
            endglobals
            function HasTable takes nothing returns boolean
                return HT != null
            endfunction
            """;

        var calls = 0;
        var registry = new JassNativeRegistry();
        registry.Register("InitHashtable", "hashtable", Array.Empty<string>(), (_, _) =>
        {
            calls++;
            return JassValue.FromHandle(new JassHandle("hashtable"));
        });

        var vm = CreateVm(script, registry);
        Assert.Equal(1, calls);
        Assert.True(vm.Invoke("HasTable").Boolean);
    }

    [Fact]
    public void StartupAnalysisFollowsExecuteFuncStrings()
    {
        const string script = """
            function Target takes nothing returns nothing
            endfunction

            function main takes nothing returns nothing
                call ExecuteFunc("Target")
            endfunction
            """;

        var program = JassProgramModel.Build(JassProgram.Parse(script));
        var startup = program.AnalyzeStartup();
        Assert.Contains("Target", startup.ReachableFunctions);
    }
}
