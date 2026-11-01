using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace UnityZedIntegration.Analyzers
{
    /// <summary>
    /// Fixes UZI0001 by renaming the file's namespace declaration to the expected namespace.
    /// Code in other files that refers to the old namespace is not updated.
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ChangeNamespaceCodeFixProvider)), Shared]
    public sealed class ChangeNamespaceCodeFixProvider : CodeFixProvider
    {
        public override ImmutableArray<string> FixableDiagnosticIds =>
            ImmutableArray.Create(NamespaceMatchesFolderAnalyzer.DiagnosticId);

        public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            if (root is null)
            {
                return;
            }
            foreach (var diagnostic in context.Diagnostics)
            {
                if (!diagnostic.Properties.TryGetValue(NamespaceMatchesFolderAnalyzer.ExpectedNamespaceKey, out var expected)
                    || expected is null)
                {
                    continue;
                }
                var declaration = root.FindNode(diagnostic.Location.SourceSpan)
                    .AncestorsAndSelf()
                    .OfType<BaseNamespaceDeclarationSyntax>()
                    .FirstOrDefault();
                if (declaration is null)
                {
                    continue;
                }
                context.RegisterCodeFix(
                    CodeAction.Create(
                        $"Change namespace to '{expected}'",
                        cancellationToken => ChangeNamespaceAsync(context.Document, declaration, expected, cancellationToken),
                        equivalenceKey: NamespaceMatchesFolderAnalyzer.DiagnosticId),
                    diagnostic);
            }
        }

        static async Task<Document> ChangeNamespaceAsync(
            Document document,
            BaseNamespaceDeclarationSyntax declaration,
            string expected,
            CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            if (root is null)
            {
                return document;
            }
            var newName = SyntaxFactory.ParseName(expected).WithTriviaFrom(declaration.Name);
            return document.WithSyntaxRoot(root.ReplaceNode(declaration.Name, newName));
        }
    }
}
