using System.Collections.Immutable;
using War3Net.CodeAnalysis.Jass;
using War3Net.CodeAnalysis.Jass.Syntax;

namespace War3Engine.Scripting;

public sealed class JassProgram
{
    private JassProgram(JassCompilationUnitSyntax compilationUnit)
    {
        CompilationUnit = compilationUnit;
    }

    public JassCompilationUnitSyntax CompilationUnit { get; }

    public static JassProgram Parse(string source)
    {
        var unit = JassSyntaxFactory.ParseCompilationUnit(source);
        return new JassProgram(unit);
    }

    public static bool TryParse(string source, out JassProgram? program)
    {
        if (JassSyntaxFactory.TryParseCompilationUnit(source, out var unit) && unit is not null)
        {
            program = new JassProgram(unit);
            return true;
        }

        program = null;
        return false;
    }

    public IEnumerable<string> FunctionNames => CompilationUnit.Declarations
        .OfType<JassFunctionDeclarationSyntax>()
        .Select(f => f.FunctionDeclarator.IdentifierName.Token.Text);

    public IEnumerable<JassFunctionDeclarationSyntax> Functions => CompilationUnit.Declarations.OfType<JassFunctionDeclarationSyntax>();

    public IEnumerable<JassNativeFunctionDeclarationSyntax> Natives => CompilationUnit.Declarations.OfType<JassNativeFunctionDeclarationSyntax>();
}
