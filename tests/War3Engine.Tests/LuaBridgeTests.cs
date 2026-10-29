using War3Engine.Scripting;
using War3Engine.Scripting.Lua;
using Xunit;

namespace War3Engine.Tests;

public class LuaBridgeTests
{
    private static NLuaBridge CreateBridge() => new((kind, id, field) => kind switch
    {
        "item" when id == 1 && field == "class" => "Artifact",
        "item" when id == 1 && field == "prio" => 126,
        "item" when id == 1 && field == "Name" => "Crown of Kings",
        "item" when id == 1 && field == "Ubertip" => "Uber tip",
        "unit" when id == 42 && field == "weapsOn" => 1,
        _ => null,
    });

    [Fact]
    public void EvaluatesStringAndNumericExpressions()
    {
        var lua = CreateBridge();
        Assert.Equal("Artifact", lua.Evaluate("(require'jass.slk').item[1].class").Value);
        Assert.Equal("126", lua.Evaluate("(require'jass.slk').item[1].prio").Value);
        Assert.Equal("1", lua.Evaluate("(require'jass.slk').unit[42].weapsOn").Value);
        Assert.Equal("abcd", lua.Evaluate("'ab' .. 'cd'").Value);
        Assert.Equal("2", lua.Evaluate("1 + 1").Value);
    }

    [Fact]
    public void Lua53NumericAndStringSemantics()
    {
        var lua = CreateBridge();
        Assert.Equal("2.0", lua.Evaluate("2.0").Value);
        Assert.Equal("2", lua.Evaluate("2").Value);
        Assert.Equal("0.5", lua.Evaluate("1/2").Value);
        Assert.Equal("3", lua.Evaluate("7//2").Value);
        Assert.Equal("1", lua.Evaluate("3%2").Value);
        Assert.Equal("1", lua.Evaluate("-3%2").Value);
        Assert.Equal("-1", lua.Evaluate("3%-2").Value);
        Assert.Equal("1e+20", lua.Evaluate("1e20").Value);
        Assert.Equal("0.3", lua.Evaluate("0.1+0.2").Value);
        Assert.Equal("0.33333333333333", lua.Evaluate("1/3").Value);
        Assert.Equal("0.333", lua.Evaluate("string.format('%.3f', 1/3)").Value);
        Assert.Equal("16", lua.Evaluate("tonumber('0x10')").Value);
        Assert.Equal("9.2233720368548e+18", lua.Evaluate("2^63").Value);
        Assert.Equal("-9223372036854775808", lua.Evaluate("9223372036854775807 + 1").Value);
        Assert.Equal("2.0", lua.Evaluate("tostring(2.0)").Value);
        Assert.Equal("x2.0", lua.Evaluate("'x' .. 2.0").Value);
        Assert.Equal("inf", lua.Evaluate("1e308*10").Value);

        var previous = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
            Assert.Equal("1.5", lua.Evaluate("1.5").Value);
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void NonStringResultsBecomeNull()
    {
        var lua = CreateBridge();
        Assert.Null(lua.Evaluate("true").Value);
        Assert.Null(lua.Evaluate("nil").Value);
        Assert.Null(lua.Evaluate("{}").Value);
        Assert.Null(lua.Evaluate("(require'jass.slk').item[1].missing").Value);
        Assert.Contains(lua.MissingFields, m => m.EndsWith(".missing", StringComparison.Ordinal));
    }

    [Fact]
    public void TrigStringsAreDecodedBeforeLua()
    {
        var lua = CreateBridge();
        var result = lua.Evaluate("\"|cffff0000Hi|r\"");
        Assert.Equal("Hi", result.Value);
        Assert.Equal(LuaErrorKind.None, result.ErrorKind);
    }

    [Fact]
    public void SyntaxAndRuntimeErrorsReturnNullAndAreRecorded()
    {
        var lua = CreateBridge();
        var syntax = lua.Evaluate("1 +");
        Assert.True(syntax.ErrorKind == LuaErrorKind.Syntax, $"kind={syntax.ErrorKind} err={syntax.Error}");
        Assert.Null(syntax.Value);
        Assert.NotNull(syntax.Error);

        var runtime = lua.Evaluate("error('boom')");
        Assert.True(runtime.ErrorKind == LuaErrorKind.Runtime, $"kind={runtime.ErrorKind} err={runtime.Error}");
        Assert.Null(runtime.Value);
        Assert.Contains("boom", runtime.Error);

        Assert.Equal(2, lua.History.Count);
    }

    [Fact]
    public void RepeatedCallsKeepWorking()
    {
        var lua = CreateBridge();
        for (var i = 0; i < 20; i++)
        {
            Assert.Equal("Artifact", lua.Evaluate("(require'jass.slk').item[1].class").Value);
        }

        Assert.Equal(20, lua.History.Count);
        Assert.DoesNotContain(lua.History, h => h.ErrorKind != LuaErrorKind.None);
    }

    [Fact]
    public void JassCallerReceivesTheLuaString()
    {
        const string script = """
            native EXExecuteScript takes string script returns string

            function ItemClass takes nothing returns string
                return EXExecuteScript("(require'jass.slk').item[1].class")
            endfunction

            function Missing takes nothing returns string
                return EXExecuteScript("(require'jass.slk').item[1].missing")
            endfunction
            """;

        var program = JassProgramModel.Build(JassProgram.Parse(script));
        var runtime = new JassGameRuntime { Lua = CreateBridge() };
        var registry = new JassNativeRegistry();
        StandardNatives.Register(registry, runtime);
        YdweNatives.Register(registry, runtime);
        var vm = new JassVirtualMachine(program, registry);
        vm.InitializeGlobals();

        Assert.Equal("Artifact", vm.Invoke("ItemClass").Text);
        Assert.True(vm.Invoke("Missing").IsNull);
    }
}
