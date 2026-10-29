using War3Net.CodeAnalysis.Jass.Syntax;

namespace War3Engine.Scripting;

public sealed record JassParameterModel(string Type, string Name);

public sealed record JassNativeModel(
    string Name,
    IReadOnlyList<JassParameterModel> Parameters,
    string ReturnType);

public sealed record JassFunctionModel(
    string Name,
    IReadOnlyList<JassParameterModel> Parameters,
    string ReturnType,
    JassFunctionDeclarationSyntax Declaration,
    IReadOnlySet<string> Calls,
    IReadOnlySet<string> References);

public sealed record JassGlobalModel(
    string Name,
    string Type,
    bool IsArray,
    bool IsConstant,
    JassExpressionSyntax? Initializer);

/// <summary>
/// Semantic view of a parsed JASS script: functions, natives, globals and the direct
/// call graph. Parsing and execution remain separate stages.
/// </summary>
public sealed class JassProgramModel
{
    internal static JassProgramModel Create(
        IReadOnlyDictionary<string, JassFunctionModel> functions,
        IReadOnlyDictionary<string, JassNativeModel> natives,
        IReadOnlyList<JassGlobalModel> globals,
        IReadOnlySet<string> globalInitCalls,
        IReadOnlyDictionary<string, string> types) =>
        new(functions, natives, globals, globalInitCalls, types);

    private JassProgramModel(
        IReadOnlyDictionary<string, JassFunctionModel> functions,
        IReadOnlyDictionary<string, JassNativeModel> natives,
        IReadOnlyList<JassGlobalModel> globals,
        IReadOnlySet<string> globalInitCalls,
        IReadOnlyDictionary<string, string> types)
    {
        Functions = functions;
        Natives = natives;
        Globals = globals;
        GlobalInitCalls = globalInitCalls;
        Types = types;
    }

    public IReadOnlyDictionary<string, JassFunctionModel> Functions { get; }

    public IReadOnlyDictionary<string, JassNativeModel> Natives { get; }

    public IReadOnlyList<JassGlobalModel> Globals { get; }

    /// <summary>Names invoked while evaluating global initializers (executed at load).</summary>
    public IReadOnlySet<string> GlobalInitCalls { get; }

    /// <summary>Declared type name -> base type name (JASS "type X extends Y").</summary>
    public IReadOnlyDictionary<string, string> Types { get; }

    public static JassProgramModel Build(JassProgram program)
    {
        var functions = new Dictionary<string, JassFunctionModel>(StringComparer.Ordinal);
        var natives = new Dictionary<string, JassNativeModel>(StringComparer.Ordinal);
        var globals = new List<JassGlobalModel>();
        var globalInitCalls = new HashSet<string>(StringComparer.Ordinal);
        var types = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var declaration in program.CompilationUnit.Declarations)
        {
            switch (declaration)
            {
                case JassFunctionDeclarationSyntax function:
                    var name = function.FunctionDeclarator.IdentifierName.Token.Text;
                    var (calls, references) = CollectCalls(function);
                    functions[name] = new JassFunctionModel(
                        name,
                        ReadParameters(function.FunctionDeclarator.ParameterList),
                        TypeName(function.FunctionDeclarator.ReturnClause.ReturnType),
                        function,
                        calls,
                        references);
                    break;

                case JassNativeFunctionDeclarationSyntax native:
                    var nativeName = native.IdentifierName.Token.Text;
                    natives[nativeName] = new JassNativeModel(
                        nativeName,
                        ReadParameters(native.ParameterList),
                        TypeName(native.ReturnClause.ReturnType));
                    break;

                case JassTypeDeclarationSyntax typeDeclaration:
                    types[typeDeclaration.IdentifierName.Token.Text] = TypeName(typeDeclaration.BaseType);
                    break;

                case JassGlobalsDeclarationSyntax globalsBlock:
                    foreach (var global in globalsBlock.GlobalDeclarations)
                    {
                        ReadGlobal(global, globals, globalInitCalls);
                    }

                    break;
            }
        }

        return new JassProgramModel(functions, natives, globals, globalInitCalls, types);
    }

    public StartupAnalysis AnalyzeStartup(string rootFunction = "main")
    {
        var reachableFunctions = new HashSet<string>(StringComparer.Ordinal);
        var reachableNatives = new HashSet<string>(StringComparer.Ordinal);
        var queue = new Queue<string>();

        if (Functions.ContainsKey(rootFunction))
        {
            queue.Enqueue(rootFunction);
            reachableFunctions.Add(rootFunction);
        }

        foreach (var call in GlobalInitCalls)
        {
            if (Natives.ContainsKey(call))
            {
                reachableNatives.Add(call);
            }

            if (Functions.ContainsKey(call) && reachableFunctions.Add(call))
            {
                queue.Enqueue(call);
            }
        }

        while (queue.Count > 0)
        {
            var current = Functions[queue.Dequeue()];
            foreach (var call in current.Calls)
            {
                if (Natives.ContainsKey(call))
                {
                    reachableNatives.Add(call);
                }
                else if (Functions.ContainsKey(call) && reachableFunctions.Add(call))
                {
                    queue.Enqueue(call);
                }
            }
        }

        return new StartupAnalysis(reachableFunctions, reachableNatives);
    }

    private static void ReadGlobal(
        JassGlobalDeclarationSyntax declaration,
        List<JassGlobalModel> globals,
        HashSet<string> initializerCalls)
    {
        switch (declaration)
        {
            case JassGlobalConstantDeclarationSyntax constant:
                globals.Add(new JassGlobalModel(
                    constant.IdentifierName.Token.Text,
                    TypeName(constant.Type),
                    false,
                    true,
                    constant.Value.Expression));
                initializerCalls.UnionWith(CollectCalls(constant.Value.Expression).Calls);
                break;

            case JassGlobalVariableDeclarationSyntax variable:
                switch (variable.Declarator)
                {
                    case JassVariableDeclaratorSyntax single:
                        globals.Add(new JassGlobalModel(
                            single.IdentifierName.Token.Text,
                            TypeName(single.Type),
                            false,
                            false,
                            single.Value?.Expression));
                        if (single.Value?.Expression is { } initializer)
                        {
                            initializerCalls.UnionWith(CollectCalls(initializer).Calls);
                        }

                        break;

                    case JassArrayDeclaratorSyntax array:
                        globals.Add(new JassGlobalModel(
                            array.IdentifierName.Token.Text,
                            TypeName(array.Type),
                            true,
                            false,
                            null));
                        break;
                }

                break;
        }
    }

    private static IReadOnlyList<JassParameterModel> ReadParameters(JassParameterListOrEmptyParameterListSyntax list)
    {
        if (list is not JassParameterListSyntax parameters)
        {
            return Array.Empty<JassParameterModel>();
        }

        return parameters.ParameterList.Items
            .Select(p => new JassParameterModel(TypeName(p.Type), p.IdentifierName.Token.Text))
            .ToArray();
    }

    private static string TypeName(JassTypeSyntax type) => type.ToString().Trim();

    private static (HashSet<string> Calls, HashSet<string> References) CollectCalls(JassSyntaxNode node)
    {
        var calls = new HashSet<string>(StringComparer.Ordinal);
        var references = new HashSet<string>(StringComparer.Ordinal);
        CollectCalls(node, calls, references);
        return (calls, references);
    }

    private static void AddExecuteFuncTarget(string name, JassArgumentListSyntax arguments, HashSet<string> calls)
    {
        if (name is not ("ExecuteFunc" or "DzExecuteFunc") || arguments.ArgumentList.Items.IsEmpty)
        {
            return;
        }

        if (arguments.ArgumentList.Items[0] is JassLiteralExpressionSyntax
            {
                Token.SyntaxKind: War3Net.CodeAnalysis.Jass.JassSyntaxKind.StringLiteralToken
            } literal)
        {
            calls.Add(War3Net.CodeAnalysis.Jass.JassLiteral.ParseString(literal.Token.Text));
        }
    }

    private static void CollectCalls(JassSyntaxNode node, HashSet<string> calls, HashSet<string> references)
    {
        // GetDescendantNodes skips the statements themselves, so walk all children.
        switch (node)
        {
            case JassCallStatementSyntax call:
                calls.Add(call.IdentifierName.Token.Text);
                AddExecuteFuncTarget(call.IdentifierName.Token.Text, call.ArgumentList, calls);
                break;
            case JassInvocationExpressionSyntax invocation:
                calls.Add(invocation.IdentifierName.Token.Text);
                AddExecuteFuncTarget(invocation.IdentifierName.Token.Text, invocation.ArgumentList, calls);
                break;
            case JassFunctionReferenceExpressionSyntax reference:
                references.Add(reference.IdentifierName.Token.Text);
                break;
        }

        foreach (var child in node.GetChildNodes())
        {
            CollectCalls(child, calls, references);
        }
    }
}

public sealed record StartupAnalysis(
    IReadOnlySet<string> ReachableFunctions,
    IReadOnlySet<string> ReachableNatives);

public static class JassProgramMerge
{
    /// <summary>Combines common.j, Blizzard.j and the map script in load order; later declarations win.</summary>
    public static JassProgramModel Merge(params JassProgramModel[] programs)
    {
        var functions = new Dictionary<string, JassFunctionModel>(StringComparer.Ordinal);
        var natives = new Dictionary<string, JassNativeModel>(StringComparer.Ordinal);
        var globals = new List<JassGlobalModel>();
        var globalInitCalls = new HashSet<string>(StringComparer.Ordinal);
        var types = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var program in programs)
        {
            foreach (var (name, function) in program.Functions)
            {
                functions[name] = function;
            }

            foreach (var (name, native) in program.Natives)
            {
                natives[name] = native;
            }

            globals.AddRange(program.Globals);
            globalInitCalls.UnionWith(program.GlobalInitCalls);
            foreach (var (name, baseType) in program.Types)
            {
                types[name] = baseType;
            }
        }

        return JassProgramModel.Create(functions, natives, globals, globalInitCalls, types);
    }
}
