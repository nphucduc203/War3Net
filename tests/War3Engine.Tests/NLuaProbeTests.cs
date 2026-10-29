using NLua;
using Xunit;

namespace War3Engine.Tests;

public class NLuaProbeTests
{
    [Fact]
    public void NLuaLoadsAndRunsLua54()
    {
        using var lua = new Lua();
        var result = lua.DoString("return tostring(2.0) .. '|' .. tostring(2) .. '|' .. (7 // 2) .. '|' .. (3 % 2)")[0];
        Console.WriteLine($"NLUA RESULT: {result}");
        Assert.Equal("2.0|2|3|1", result);
    }
}
