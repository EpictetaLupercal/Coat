// Coat — Epicteta Lupercal
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

var source = Path.GetFullPath(args[1]);
var files = Directory.GetFiles(source, "*.cs").Where(p => !Path.GetFileName(p).Contains("Tests") && Path.GetFileName(p) != "Localization.cs");
var inventory = new List<object>();
var translations = args[0] == "rewrite" ? JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(args[2]))! : new();
foreach (var path in files)
{
    var root = CSharpSyntaxTree.ParseText(File.ReadAllText(path)).GetRoot();
    if (args[0] == "rewrite")
    {
        File.WriteAllText(path, new Rewriter(translations).Visit(root)!.ToFullString());
        continue;
    }
    foreach (var node in root.DescendantNodes())
    {
        string? text = node is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.StringLiteralExpression) ? literal.Token.ValueText : node is InterpolatedStringExpressionSyntax formatted ? Rewriter.Template(formatted) : null;
        if (text != null && text.Length > 0) inventory.Add(new { file = Path.GetFileName(path), line = node.GetLocation().GetLineSpan().StartLinePosition.Line + 1, kind = node is InterpolatedStringExpressionSyntax ? "format" : "text", text, localized = node.Ancestors().OfType<InvocationExpressionSyntax>().Any(n => n.Expression.ToString().StartsWith("L.")), scope = node.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault()?.Identifier.Text });
    }
}
if (args[0] == "inventory") File.WriteAllText(args[2], JsonSerializer.Serialize(inventory, new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));

class Rewriter(Dictionary<string, string> translations) : CSharpSyntaxRewriter
{
    public static string Template(InterpolatedStringExpressionSyntax node)
    {
        int i = 0;
        return string.Concat(node.Contents.Select(c => c is InterpolatedStringTextSyntax text ? text.TextToken.ValueText : c is InterpolationSyntax hole ? "{" + i++ + (hole.AlignmentClause == null ? "" : "," + hole.AlignmentClause.Value.ToString()) + (hole.FormatClause == null ? "" : ":" + hole.FormatClause.FormatStringToken.ValueText) + "}" : ""));
    }
    private bool StableKey(SyntaxNode node) => node.Ancestors().OfType<MethodDeclarationSyntax>().Any(m => m.Identifier.Text == "CreateActions" || m.Identifier.Text == "ParseHint") || node.Ancestors().OfType<InvocationExpressionSyntax>().Any(n => n.Expression.ToString().StartsWith("L."));
    public override SyntaxNode? VisitLiteralExpression(LiteralExpressionSyntax node)
    {
        if (node.IsKind(SyntaxKind.StringLiteralExpression) && translations.ContainsKey(node.Token.ValueText) && !StableKey(node)) return SyntaxFactory.ParseExpression("L.T(" + node.WithoutTrivia().ToFullString() + ")").WithTriviaFrom(node);
        return base.VisitLiteralExpression(node);
    }
    public override SyntaxNode? VisitInterpolatedStringExpression(InterpolatedStringExpressionSyntax node)
    {
        var visited = (InterpolatedStringExpressionSyntax)base.VisitInterpolatedStringExpression(node)!;
        return translations.ContainsKey(Template(node)) && !StableKey(node) ? SyntaxFactory.ParseExpression("L.F(" + visited.WithoutTrivia().ToFullString() + ")").WithTriviaFrom(node) : visited;
    }
}
