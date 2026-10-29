using System.Globalization;
using NLua;
using NLua.Exceptions;
using NLuaLua = NLua.Lua;

namespace War3Engine.Scripting.Lua;

/// <summary>
/// EXExecuteScript bridge on a real Lua runtime (NLua / Lua 5.4). Target YDWE ships Lua
/// 5.3.6; 5.4 was chosen over managed Lua 5.2 because its integer/float and tostring
/// semantics match 5.3 much more closely and it supports //, %, 64-bit integers.
/// YDWE behavior: "return (trigstring expr)", persistent state, only string/number results
/// are converted (via Lua tostring), everything else is a null JASS string, errors -> null.
/// </summary>
public sealed class NLuaBridge : IJassLua, IDisposable
{
    private const string Bootstrap = """
        local __query = __slk_query
        local function __collection(kind)
          local cache = {}
          return setmetatable({}, { __index = function(_, id)
            id = math.tointeger(id) or id
            local row = cache[id]
            if row == nil then
              row = setmetatable({}, { __index = function(_, field)
                return __query(kind, id, field)
              end})
              cache[id] = row
            end
            return row
          end })
        end
        local __module = {
          item = __collection('item'),
          unit = __collection('unit'),
          ability = __collection('ability'),
          buff = __collection('buff'),
          doodad = __collection('doodad'),
          destructable = __collection('destructable'),
          upgrade = __collection('upgrade'),
        }
        package.loaded['jass.slk'] = __module
        local __base_require = require
        function require(name)
          local loaded = package.loaded[name]
          if loaded ~= nil then return loaded end
          return __base_require(name)
        end
        """;

    private readonly NLuaLua _lua;
    private readonly JassSlkQuery _query;
    private readonly List<LuaEvaluation> _history = new();
    private readonly HashSet<string> _missing = new(StringComparer.Ordinal);

    public NLuaBridge(JassSlkQuery query)
    {
        _query = query;
        _lua = new NLuaLua();
        _lua.RegisterFunction("__slk_query", this, typeof(NLuaBridge).GetMethod(nameof(QueryFromLua))!);
        _lua.DoString(Bootstrap, "jass.slk");
    }

    public IReadOnlyList<LuaEvaluation> History => _history;

    public IReadOnlyCollection<string> MissingFields => _missing;

    public LuaEvaluation Evaluate(string jassExpression)
    {
        var converted = JassTrigString.FromTrigString(jassExpression);
        // Only string/number results are observable through EXExecuteScript; numbers use
        // Lua's own tostring so 5.4 formatting (integer vs float, .0 suffix) is preserved.
        var source = $"local __v = ({converted})\n"
                     + "local __t = type(__v)\n"
                     + "if __t == 'string' then return __t, __v end\n"
                     + "if __t == 'number' then return __t, tostring(__v) end\n"
                     + "return __t, nil";
        LuaEvaluation evaluation;
        try
        {
            var results = _lua.DoString(source, "EXExecuteScript");
            var value = results.Length > 1 ? results[1] as string : null;
            evaluation = new LuaEvaluation(converted, value, null, LuaErrorKind.None);
        }
        catch (LuaException ex)
        {
            var syntax = ex.Message.Contains("syntax error", StringComparison.OrdinalIgnoreCase)
                         || ex.Message.Contains("unexpected symbol", StringComparison.OrdinalIgnoreCase)
                         || ex.Message.Contains("unexpected token", StringComparison.OrdinalIgnoreCase)
                         || ex.Message.Contains("near '<eof>'", StringComparison.OrdinalIgnoreCase);
            evaluation = new LuaEvaluation(converted, null, ex.Message, syntax ? LuaErrorKind.Syntax : LuaErrorKind.Runtime);
        }
        catch (Exception ex)
        {
            evaluation = new LuaEvaluation(converted, null, ex.Message, LuaErrorKind.Internal);
        }

        _history.Add(evaluation);
        if (_history.Count > 200)
        {
            _history.RemoveAt(0);
        }

        return evaluation;
    }

    public object? QueryFromLua(string kind, long objectId, string field)
    {
        var value = _query(kind, (int)objectId, field);
        if (value is null)
        {
            _missing.Add($"{kind}:{objectId.ToString(CultureInfo.InvariantCulture)}.{field}");
        }

        return value;
    }

    public void Dispose() => _lua.Dispose();
}
