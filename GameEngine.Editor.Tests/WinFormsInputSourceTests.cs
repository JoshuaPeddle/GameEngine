using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace GameEngine.Editor.Tests;

public class WinFormsInputSourceTests
{
    [Test]
    public void MouseMovesAreForwardedUnconditionallyAndMouseHandlersAreSubscribedOnce()
    {
        var source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "RunnerSources", "MainView.cs"));
        var root = CSharpSyntaxTree.ParseText(source).GetRoot();
        var handler = root.DescendantNodes().OfType<MethodDeclarationSyntax>()
            .Single(method => method.Identifier.ValueText == "OnPointerMoved");
        var forwardedMoves = handler.Body!.Statements.OfType<ExpressionStatementSyntax>()
            .Select(statement => statement.Expression)
            .OfType<ConditionalAccessExpressionSyntax>()
            .Where(expression => expression.Expression.ToString() == "Input"
                && expression.WhenNotNull is InvocationExpressionSyntax
                {
                    Expression: MemberBindingExpressionSyntax { Name.Identifier.ValueText: "PointerMoved" }
                });

        Assert.That(forwardedMoves.Count(), Is.EqualTo(1), "Hover must reach Input without a press condition.");
        foreach (var (mouseEvent, method) in new[]
        {
            ("MouseDown", "OnPointerPressed"), ("MouseMove", "OnPointerMoved"), ("MouseUp", "OnPointerReleased")
        })
        {
            var subscriptions = root.DescendantNodes().OfType<AssignmentExpressionSyntax>()
                .Where(expression => expression.IsKind(SyntaxKind.AddAssignmentExpression)
                    && expression.Left.ToString() == $"skglControl1.{mouseEvent}").ToArray();
            Assert.That(subscriptions, Has.Length.EqualTo(1), mouseEvent);
            Assert.That(subscriptions[0].Right.ToString(), Is.EqualTo(method), mouseEvent);
        }
    }
}
