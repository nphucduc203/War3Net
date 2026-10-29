using System.Globalization;
using System.Text;
using War3Net.CodeAnalysis.Jass;
using War3Net.CodeAnalysis.Jass.Syntax;

namespace War3Engine.Scripting;

/// <summary>
/// Tree-walking JASS interpreter. Parsing/semantic model come from War3Net.CodeAnalysis.Jass;
/// this class executes the syntax tree with explicit limits and strict native dispatch.
/// </summary>
public sealed class JassVirtualMachine
{
    private readonly JassProgramModel _program;
    private readonly JassNativeRegistry _natives;
    private readonly JassExecutionOptions _options;
    private readonly Dictionary<string, Slot> _globals = new(StringComparer.Ordinal);
    private readonly List<string> _callStack = new();
    private long _steps;
    private int _depth;

    public JassVirtualMachine(JassProgramModel program, JassNativeRegistry natives, JassExecutionOptions? options = null)
    {
        _program = program;
        _natives = natives;
        _options = options ?? new JassExecutionOptions();
    }

    public JassProgramModel Program => _program;

    public long StepsExecuted => _steps;

    /// <summary>Resets the step budget between controlled invocations (debugger/tests).</summary>
    public void ResetStepCounter() => _steps = 0;

    public bool GlobalsInitialized { get; private set; }

    public Action<string>? NativeTrace { get; set; }

    public bool TryGetGlobalValue(string name, out JassValue value)
    {
        if (_globals.TryGetValue(name, out var slot) && slot.Array is null)
        {
            value = slot.Value;
            return true;
        }

        value = JassValue.Null;
        return false;
    }

    public void InitializeGlobals()
    {
        if (GlobalsInitialized)
        {
            throw new InvalidOperationException("globals already initialized");
        }

        foreach (var global in _program.Globals)
        {
            var type = ResolvePrimitive(global.Type);
            if (global.IsArray)
            {
                _globals[global.Name] = new Slot(global.Type, global.IsConstant, new JassArray(type));
                continue;
            }

            var value = global.Initializer is null ? DefaultValue(type) : Evaluate(global.Initializer, null);
            _globals[global.Name] = new Slot(global.Type, global.IsConstant, ConvertForType(value, global.Type, $"global {global.Name}"));
        }

        GlobalsInitialized = true;
    }

    public JassValue Invoke(string functionName, params JassValue[] arguments) => Invoke(functionName, (IReadOnlyList<JassValue>)arguments);

    public JassValue Invoke(string functionName, IReadOnlyList<JassValue> arguments) => InvokeTarget(functionName, arguments);

    private JassValue InvokeTarget(string name, IReadOnlyList<JassValue> arguments)
    {
        if (_program.Functions.TryGetValue(name, out var function))
        {
            return CallFunction(function, arguments);
        }

        if (_natives.TryGet(name, out var native))
        {
            return CallNative(native, arguments);
        }

        var signature = _program.Natives.TryGetValue(name, out var declaration)
            ? FormatNativeSignature(declaration)
            : "not declared in script";
        throw UnsupportedNative(name, signature);
    }

    private JassValue CallNative(JassNative native, IReadOnlyList<JassValue> arguments)
    {
        Step();
        if (arguments.Count != native.ParameterTypes.Count)
        {
            throw Error($"native '{native.Name}' expects {native.ParameterTypes.Count} arguments, got {arguments.Count}", native.Name);
        }

        var converted = new JassValue[arguments.Count];
        for (var i = 0; i < arguments.Count; i++)
        {
            converted[i] = ConvertForType(arguments[i], native.ParameterTypes[i], $"argument {i + 1} of native {native.Name}");
        }

        NativeTrace?.Invoke(native.Name);
        _callStack.Add(native.Name);
        try
        {
            var result = native.Implementation(this, converted);
            var expected = ResolvePrimitive(native.ReturnType);
            if (expected == "nothing")
            {
                return result;
            }

            return ConvertForType(result, native.ReturnType, $"return value of native {native.Name}");
        }
        catch (JassRuntimeException ex)
        {
            if (ex.NativeName is null)
            {
                ex.NativeName = native.Name;
            }

            if (ex.CallStack.Count == 0)
            {
                ex.CallStack = _callStack.ToArray();
            }

            throw;
        }
        catch (Exception ex)
        {
            throw Error($"native '{native.Name}' failed: {ex.Message}", native.Name);
        }
        finally
        {
            _callStack.RemoveAt(_callStack.Count - 1);
        }
    }

    private static string FormatNativeSignature(JassNativeModel native)
    {
        var parameters = native.Parameters.Count == 0
            ? "nothing"
            : string.Join(", ", native.Parameters.Select(p => $"{p.Type} {p.Name}"));
        return $"takes {parameters} returns {native.ReturnType}";
    }

    public JassValue InvokeCode(JassValue code, IReadOnlyList<JassValue>? arguments = null)
    {
        if (code.Kind != JassValueKind.Code || code.Text is null)
        {
            throw Error($"value '{code}' is not a code reference");
        }

        return Invoke(code.Text, arguments ?? Array.Empty<JassValue>());
    }

    private JassValue CallFunction(JassFunctionModel function, IReadOnlyList<JassValue> arguments)
    {
        Step();
        if (_depth >= _options.MaxCallDepth)
        {
            throw Error($"call depth exceeded ({_options.MaxCallDepth})");
        }

        if (arguments.Count != function.Parameters.Count)
        {
            throw Error($"function '{function.Name}' expects {function.Parameters.Count} arguments, got {arguments.Count}");
        }

        var frame = new Dictionary<string, Slot>(StringComparer.Ordinal);
        for (var i = 0; i < arguments.Count; i++)
        {
            var parameter = function.Parameters[i];
            frame[parameter.Name] = new Slot(parameter.Type, false, ConvertForType(arguments[i], parameter.Type, $"parameter {parameter.Name} of {function.Name}"));
        }

        _callStack.Add(function.Name);
        _depth++;
        try
        {
            var flow = ExecuteStatements(function.Declaration.Statements, frame);
            if (flow == Flow.Return)
            {
                return ConvertForType(_returnValue, function.ReturnType, $"return value of {function.Name}");
            }

            return DefaultValue(ResolvePrimitive(function.ReturnType));
        }
        finally
        {
            _depth--;
            _callStack.RemoveAt(_callStack.Count - 1);
            _returnValue = JassValue.Null;
        }
    }

    private JassValue _returnValue;

    private enum Flow
    {
        Normal,
        Return,
        ExitLoop,
    }

    private Flow ExecuteStatements(IEnumerable<JassStatementSyntax> statements, Dictionary<string, Slot> frame)
    {
        foreach (var statement in statements)
        {
            Step();
            switch (statement)
            {
                case JassLocalVariableDeclarationStatementSyntax local:
                    DeclareLocal(local, frame);
                    break;
                case JassSetStatementSyntax set:
                    ExecuteSet(set, frame);
                    break;
                case JassCallStatementSyntax call:
                    Invoke(call.IdentifierName.Token.Text, EvaluateArguments(call.ArgumentList, frame));
                    break;
                case JassIfStatementSyntax ifStatement:
                    var flow = ExecuteIf(ifStatement, frame);
                    if (flow != Flow.Normal)
                    {
                        return flow;
                    }

                    break;
                case JassLoopStatementSyntax loop:
                    var loopFlow = ExecuteLoop(loop, frame);
                    if (loopFlow != Flow.Normal)
                    {
                        return loopFlow;
                    }

                    break;
                case JassReturnStatementSyntax returnStatement:
                    _returnValue = returnStatement.Value is null ? JassValue.Null : Evaluate(returnStatement.Value, frame);
                    return Flow.Return;
                case JassExitStatementSyntax exit:
                    if (EvaluateBoolean(exit.Condition, frame))
                    {
                        return Flow.ExitLoop;
                    }

                    break;
                case JassDebugStatementSyntax debug:
                    if (_options.ExecuteDebugStatements)
                    {
                        var debugFlow = ExecuteStatements(new[] { debug.Statement }, frame);
                        if (debugFlow != Flow.Normal)
                        {
                            return debugFlow;
                        }
                    }

                    break;
                default:
                    throw Error($"unsupported statement {statement.SyntaxKind}");
            }
        }

        return Flow.Normal;
    }

    private Flow ExecuteIf(JassIfStatementSyntax statement, Dictionary<string, Slot> frame)
    {
        if (EvaluateBoolean(statement.IfClause.IfClauseDeclarator.Condition, frame))
        {
            return ExecuteStatements(statement.IfClause.Statements, frame);
        }

        foreach (var elseIf in statement.ElseIfClauses)
        {
            if (EvaluateBoolean(elseIf.ElseIfClauseDeclarator.Condition, frame))
            {
                return ExecuteStatements(elseIf.Statements, frame);
            }
        }

        if (statement.ElseClause is { } elseClause)
        {
            return ExecuteStatements(elseClause.Statements, frame);
        }

        return Flow.Normal;
    }

    private Flow ExecuteLoop(JassLoopStatementSyntax loop, Dictionary<string, Slot> frame)
    {
        while (true)
        {
            Step();
            var flow = ExecuteStatements(loop.Statements, frame);
            switch (flow)
            {
                case Flow.Normal:
                    break;
                case Flow.ExitLoop:
                    return Flow.Normal;
                case Flow.Return:
                    return Flow.Return;
            }
        }
    }

    private void DeclareLocal(JassLocalVariableDeclarationStatementSyntax declaration, Dictionary<string, Slot> frame)
    {
        switch (declaration.Declarator)
        {
            case JassVariableDeclaratorSyntax variable:
                var value = variable.Value is null
                    ? DefaultValue(ResolvePrimitive(TypeName(variable.Type)))
                    : Evaluate(variable.Value.Expression, frame);
                frame[variable.IdentifierName.Token.Text] = new Slot(
                    TypeName(variable.Type),
                    false,
                    ConvertForType(value, TypeName(variable.Type), $"local {variable.IdentifierName.Token.Text}"));
                break;

            case JassArrayDeclaratorSyntax array:
                frame[array.IdentifierName.Token.Text] = new Slot(
                    TypeName(array.Type),
                    false,
                    new JassArray(ResolvePrimitive(TypeName(array.Type))));
                break;

            default:
                throw Error("unsupported local declaration");
        }
    }

    private void ExecuteSet(JassSetStatementSyntax set, Dictionary<string, Slot> frame)
    {
        var name = set.IdentifierName.Token.Text;
        var value = Evaluate(set.Value.Expression, frame);
        var slot = ResolveSlot(name, frame);
        if (slot.IsConstant)
        {
            throw Error($"cannot assign to constant '{name}'");
        }

        if (set.ElementAccessClause is { } element)
        {
            if (slot.Array is null)
            {
                throw Error($"'{name}' is not an array");
            }

            var index = EvaluateInteger(element.Expression, frame);
            slot.Array.Set(index, ConvertForType(value, slot.Type, $"array {name} element"));
            return;
        }

        if (slot.Array is not null)
        {
            throw Error($"cannot assign a value to array '{name}' without an index");
        }

        slot.Value = ConvertForType(value, slot.Type, $"variable {name}");
    }

    private JassValue Evaluate(JassExpressionSyntax expression, Dictionary<string, Slot>? frame)
    {
        Step();
        switch (expression)
        {
            case JassLiteralExpressionSyntax literal:
                return EvaluateLiteral(literal);
            case JassParenthesizedExpressionSyntax parenthesized:
                return Evaluate(parenthesized.Expression, frame);
            case JassIdentifierNameSyntax identifier:
                return ResolveSlot(identifier.Token.Text, frame).Value;
            case JassElementAccessExpressionSyntax element:
                var arraySlot = ResolveSlot(element.IdentifierName.Token.Text, frame);
                if (arraySlot.Array is null)
                {
                    throw Error($"'{element.IdentifierName.Token.Text}' is not an array");
                }

                return arraySlot.Array.Get(EvaluateInteger(element.ElementAccessClause.Expression, frame));
            case JassInvocationExpressionSyntax invocation:
                return Invoke(invocation.IdentifierName.Token.Text, EvaluateArguments(invocation.ArgumentList, frame));
            case JassFunctionReferenceExpressionSyntax reference:
                return JassValue.FromCode(reference.IdentifierName.Token.Text);
            case JassUnaryExpressionSyntax unary:
                return EvaluateUnary(unary, frame);
            case JassBinaryExpressionSyntax binary:
                return EvaluateBinary(binary, frame);
            default:
                throw Error($"unsupported expression {expression.SyntaxKind}");
        }
    }

    private JassValue EvaluateLiteral(JassLiteralExpressionSyntax literal)
    {
        var token = literal.Token;
        return token.SyntaxKind switch
        {
            JassSyntaxKind.DecimalLiteralToken => JassValue.FromInteger(JassLiteral.ParseInt(token.Text)),
            JassSyntaxKind.HexadecimalLiteralToken => JassValue.FromInteger(JassLiteral.ParseHex(token.Text)),
            JassSyntaxKind.OctalLiteralToken => JassValue.FromInteger(JassLiteral.ParseOctal(token.Text)),
            JassSyntaxKind.RealLiteralToken => JassValue.FromReal(float.Parse(token.Text, NumberStyles.Float, CultureInfo.InvariantCulture)),
            JassSyntaxKind.StringLiteralToken => JassValue.FromString(Unescape(token.Text)),
            JassSyntaxKind.FourCCLiteralToken => JassValue.FromInteger(ParseFourCCLittleEndian(token.Text)),
            JassSyntaxKind.CharacterLiteralToken => JassValue.FromInteger(JassLiteral.ParseChar(token.Text)),
            JassSyntaxKind.TrueKeyword => JassValue.FromBoolean(true),
            JassSyntaxKind.FalseKeyword => JassValue.FromBoolean(false),
            JassSyntaxKind.NullKeyword => JassValue.Null,
            _ => throw Error($"unsupported literal {token.SyntaxKind}"),
        };
    }

    private JassValue EvaluateUnary(JassUnaryExpressionSyntax unary, Dictionary<string, Slot>? frame)
    {
        var operand = Evaluate(unary.Expression, frame);
        switch (unary.OperatorToken.Text)
        {
            case "-":
                return operand.Kind switch
                {
                    JassValueKind.Integer => JassValue.FromInteger(-operand.Integer),
                    JassValueKind.Real => JassValue.FromReal(-operand.Real),
                    _ => throw Error($"cannot negate {operand.DisplayType}"),
                };
            case "+":
                return operand;
            case "not":
                if (operand.Kind != JassValueKind.Boolean)
                {
                    throw Error($"operator 'not' expects boolean, got {operand.DisplayType}");
                }

                return JassValue.FromBoolean(!operand.Boolean);
            default:
                throw Error($"unsupported unary operator '{unary.OperatorToken.Text}'");
        }
    }

    private JassValue EvaluateBinary(JassBinaryExpressionSyntax binary, Dictionary<string, Slot>? frame)
    {
        var op = binary.OperatorToken.Text;
        var left = Evaluate(binary.Left, frame);
        if (op == "and")
        {
            if (left.Kind != JassValueKind.Boolean)
            {
                throw Error($"operator 'and' expects boolean, got {left.DisplayType}");
            }

            return JassValue.FromBoolean(left.Boolean && EvaluateBoolean(binary.Right, frame));
        }

        if (op == "or")
        {
            if (left.Kind != JassValueKind.Boolean)
            {
                throw Error($"operator 'or' expects boolean, got {left.DisplayType}");
            }

            return JassValue.FromBoolean(left.Boolean || EvaluateBoolean(binary.Right, frame));
        }

        var right = Evaluate(binary.Right, frame);
        switch (op)
        {
            case "+":
                if (left.Kind == JassValueKind.String && right.Kind == JassValueKind.String)
                {
                    return JassValue.FromString(left.Text + right.Text);
                }

                return Numeric(left, right, "+", (a, b) => a + b, (a, b) => a + b);
            case "-":
                return Numeric(left, right, "-", (a, b) => a - b, (a, b) => a - b);
            case "*":
                return Numeric(left, right, "*", (a, b) => a * b, (a, b) => a * b);
            case "/":
                return Divide(left, right);
            case "==":
                return JassValue.FromBoolean(left.Equals(right));
            case "!=":
                return JassValue.FromBoolean(!left.Equals(right));
            case "<":
                return JassValue.FromBoolean(Compare(left, right, "<") < 0);
            case "<=":
                return JassValue.FromBoolean(Compare(left, right, "<=") <= 0);
            case ">":
                return JassValue.FromBoolean(Compare(left, right, ">") > 0);
            case ">=":
                return JassValue.FromBoolean(Compare(left, right, ">=") >= 0);
            default:
                throw Error($"unsupported binary operator '{op}'");
        }
    }

    private JassValue Numeric(
        JassValue left,
        JassValue right,
        string op,
        Func<int, int, int> integerOp,
        Func<float, float, float> realOp)
    {
        if (left.Kind == JassValueKind.Integer && right.Kind == JassValueKind.Integer)
        {
            return JassValue.FromInteger(integerOp(left.Integer, right.Integer));
        }

        if (IsNumeric(left) && IsNumeric(right))
        {
            return JassValue.FromReal(realOp(AsReal(left), AsReal(right)));
        }

        throw Error($"operator '{op}' is not defined for {left.DisplayType} and {right.DisplayType}");
    }

    private JassValue Divide(JassValue left, JassValue right)
    {
        if (left.Kind == JassValueKind.Integer && right.Kind == JassValueKind.Integer)
        {
            if (right.Integer == 0)
            {
                throw Error("integer division by zero");
            }

            return JassValue.FromInteger(left.Integer / right.Integer);
        }

        if (IsNumeric(left) && IsNumeric(right))
        {
            return JassValue.FromReal(AsReal(left) / AsReal(right));
        }

        throw Error($"operator '/' is not defined for {left.DisplayType} and {right.DisplayType}");
    }

    private int Compare(JassValue left, JassValue right, string op)
    {
        if (IsNumeric(left) && IsNumeric(right))
        {
            return AsReal(left).CompareTo(AsReal(right));
        }

        if (left.Kind == JassValueKind.String && right.Kind == JassValueKind.String)
        {
            return string.CompareOrdinal(left.Text, right.Text);
        }

        throw Error($"operator '{op}' cannot compare {left.DisplayType} and {right.DisplayType}");
    }

    private bool EvaluateBoolean(JassExpressionSyntax expression, Dictionary<string, Slot>? frame)
    {
        var value = Evaluate(expression, frame);
        if (value.Kind != JassValueKind.Boolean)
        {
            throw Error($"expected boolean, got {value.DisplayType}");
        }

        return value.Boolean;
    }

    private int EvaluateInteger(JassExpressionSyntax expression, Dictionary<string, Slot>? frame)
    {
        var value = Evaluate(expression, frame);
        if (value.Kind != JassValueKind.Integer)
        {
            throw Error($"expected integer, got {value.DisplayType}");
        }

        return value.Integer;
    }

    private IReadOnlyList<JassValue> EvaluateArguments(JassArgumentListSyntax list, Dictionary<string, Slot>? frame)
    {
        if (list.ArgumentList.Items.IsEmpty)
        {
            return Array.Empty<JassValue>();
        }

        var values = new JassValue[list.ArgumentList.Items.Length];
        for (var i = 0; i < values.Length; i++)
        {
            values[i] = Evaluate(list.ArgumentList.Items[i], frame);
        }

        return values;
    }

    private Slot ResolveSlot(string name, Dictionary<string, Slot>? frame)
    {
        if (frame is not null && frame.TryGetValue(name, out var local))
        {
            return local;
        }

        if (_globals.TryGetValue(name, out var global))
        {
            return global;
        }

        throw Error($"variable '{name}' is not declared");
    }

    private void Step()
    {
        if (++_steps > _options.MaxSteps)
        {
            throw Error($"step limit exceeded ({_options.MaxSteps})");
        }
    }

    internal JassRuntimeException Error(string message, string? nativeName = null)
    {
        return new JassRuntimeException(message, nativeName)
        {
            CallStack = _callStack.ToArray(),
        };
    }

    internal JassRuntimeException UnsupportedNative(string name, string signature)
    {
        return new UnsupportedNativeException(name, signature, _callStack.ToArray());
    }

    internal string CurrentFunction => _callStack.Count > 0 ? _callStack[^1] : "<entry>";

    /// <summary>Function that called the current native; used to attribute event registrations.</summary>
    public string CallerFunction => _callStack.Count > 1 ? _callStack[^2] : "<entry>";

    public string CallerChain(int depth)
    {
        var start = Math.Max(0, _callStack.Count - 1 - depth);
        return string.Join(" < ", _callStack.Skip(start).Take(_callStack.Count - 1 - start));
    }

    public IEnumerable<KeyValuePair<string, JassValue>> SnapshotGlobals()
    {
        foreach (var pair in _globals)
        {
            if (pair.Value.Array is null)
            {
                yield return new KeyValuePair<string, JassValue>(pair.Key, pair.Value.Value);
            }
        }
    }

    // Type handling ---------------------------------------------------------

    private static readonly HashSet<string> PrimitiveTypes = new(StringComparer.Ordinal)
    {
        "integer", "real", "boolean", "string", "handle", "code", "nothing",
    };

    public string ResolvePrimitive(string declaredType)
    {
        var type = declaredType.Trim();
        var guard = 0;
        while (!PrimitiveTypes.Contains(type) && _program.Types.TryGetValue(type, out var baseType) && guard++ < 32)
        {
            type = baseType.Trim();
        }

        return PrimitiveTypes.Contains(type) ? type : "handle";
    }

    private JassValue DefaultValue(string resolvedType) => resolvedType switch
    {
        "integer" => JassValue.FromInteger(0),
        "real" => JassValue.FromReal(0f),
        "boolean" => JassValue.FromBoolean(false),
        "string" => JassValue.FromString(string.Empty),
        _ => JassValue.Null,
    };

    private JassValue ConvertForType(JassValue value, string declaredType, string context)
    {
        var type = ResolvePrimitive(declaredType);
        switch (type)
        {
            case "integer":
                if (value.Kind == JassValueKind.Integer)
                {
                    return value;
                }

                break;
            case "real":
                if (value.Kind == JassValueKind.Real)
                {
                    return value;
                }

                if (value.Kind == JassValueKind.Integer)
                {
                    return JassValue.FromReal(value.Integer);
                }

                break;
            case "boolean":
                if (value.Kind == JassValueKind.Boolean)
                {
                    return value;
                }

                break;
            case "string":
                // JASS string variables may hold null (Blizzard.j uses `string x = null`).
                if (value.Kind == JassValueKind.String || value.IsNull)
                {
                    return value;
                }

                break;
            case "code":
                if (value.Kind == JassValueKind.Code || value.IsNull)
                {
                    return value;
                }

                break;
            case "nothing":
                if (value.IsNull)
                {
                    return JassValue.Null;
                }

                break;
            default:
                if (value.IsNull || (value.Kind == JassValueKind.Handle && IsHandleCompatible(value.Handle!, declaredType)))
                {
                    return value;
                }

                break;
        }

        throw Error($"{context}: expected {type}, got {value.DisplayType}");
    }

    private bool IsHandleCompatible(JassHandle handle, string declaredType)
    {
        if (!_options.StrictHandleTypes)
        {
            return true;
        }

        var type = declaredType.Trim();
        var guard = 0;
        while (guard++ < 32)
        {
            if (string.Equals(handle.Type, type, StringComparison.Ordinal))
            {
                return true;
            }

            if (!_program.Types.TryGetValue(type, out var baseType))
            {
                break;
            }

            type = baseType.Trim();
        }

        return type is "handle" or "agent" || declaredType is "handle" or "agent";
    }

    private static bool IsNumeric(JassValue value) => value.Kind is JassValueKind.Integer or JassValueKind.Real;

    private static float AsReal(JassValue value) => value.Kind == JassValueKind.Integer ? value.Integer : value.Real;

    private static string TypeName(JassTypeSyntax type) => type.ToString().Trim();

    /// <summary>
    /// Object ids in map/object-data files are little-endian rawcodes ('hfoo' = 0x6F6F6668).
    /// War3Net's JassLiteral.ParseFourCC produces big-endian, which does not match map ids.
    /// </summary>
    private static int ParseFourCCLittleEndian(string literal)
    {
        if (literal.Length < 6)
        {
            throw new JassRuntimeException($"invalid fourcc literal {literal}");
        }

        return literal[1] | (literal[2] << 8) | (literal[3] << 16) | (literal[4] << 24);
    }

    private static string Unescape(string literal)
    {
        var text = JassLiteral.ParseString(literal);
        if (!text.Contains('\\', StringComparison.Ordinal))
        {
            return text;
        }

        var builder = new StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c != '\\' || i + 1 >= text.Length)
            {
                builder.Append(c);
                continue;
            }

            var next = text[++i];
            builder.Append(next switch
            {
                'n' => '\n',
                'r' => '\r',
                't' => '\t',
                'b' => '\b',
                'f' => '\f',
                '\\' => '\\',
                '"' => '"',
                '\'' => '\'',
                _ => next,
            });
        }

        return builder.ToString();
    }

    private sealed class Slot
    {
        public Slot(string type, bool isConstant, JassValue value)
        {
            Type = type;
            IsConstant = isConstant;
            Value = value;
        }

        public Slot(string type, bool isConstant, JassArray array)
        {
            Type = type;
            IsConstant = isConstant;
            Array = array;
        }

        public string Type { get; }

        public bool IsConstant { get; }

        public JassValue Value { get; set; }

        public JassArray? Array { get; }
    }

    private sealed class JassArray
    {
        private readonly Dictionary<int, JassValue> _values = new();

        public JassArray(string elementType)
        {
            ElementType = elementType;
        }

        public string ElementType { get; }

        public JassValue Get(int index)
        {
            ValidateIndex(index);
            return _values.TryGetValue(index, out var value) ? value : JassDefault(ElementType);
        }

        public void Set(int index, JassValue value)
        {
            ValidateIndex(index);
            _values[index] = value;
        }

        private static void ValidateIndex(int index)
        {
            if (index < 0 || index > 8191)
            {
                throw new JassRuntimeException($"array index {index} out of range 0..8191");
            }
        }

        private static JassValue JassDefault(string type) => type switch
        {
            "integer" => JassValue.FromInteger(0),
            "real" => JassValue.FromReal(0f),
            "boolean" => JassValue.FromBoolean(false),
            "string" => JassValue.FromString(string.Empty),
            _ => JassValue.Null,
        };
    }
}
