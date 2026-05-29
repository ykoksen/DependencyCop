using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Lindhart.DependencyCop.UsingNamespaceStatement
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(FixProvider))]
    [Shared]
    public class FixProvider : CodeFixProvider
    {
        public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(Analyzer.RuleId);

        public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var document = context.Document;

            if (!SupportedDocument(document))
            {
                return;
            }

            var rootNode = await document.GetSyntaxRootAsync(CancellationToken.None);
            if (rootNode == null)
            {
                return;
            }

            foreach (var diagnostic in context.Diagnostics)
            {
                var syntaxNode = rootNode.FindNode(diagnostic.Location.SourceSpan);
                if (syntaxNode is UsingDirectiveSyntax usingDirective)
                {
                    context.RegisterCodeFix(
                        CodeAction.Create(
                            title: $"Qualify usages and remove this line ('{usingDirective.ToString()}').",
                            createChangedDocument: c => Fix(document, usingDirective, c),
                            equivalenceKey: $"QualifyAndRemoveUsing:{usingDirective.Name}"),
                        diagnostic);
                }
            }
        }

        static bool SupportedDocument(Document document) =>
            document.SupportsSyntaxTree && document.SupportsSemanticModel && document.SourceCodeKind == SourceCodeKind.Regular;

        static async Task<Document> Fix(Document document, UsingDirectiveSyntax violatingUsingDirective, CancellationToken cancellationToken)
        {
            if (violatingUsingDirective.Name == null)
            {
                return document;
            }

            var usingDirectiveName = new DottedName(violatingUsingDirective.Name.ToString());

            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
            if (semanticModel == null)
            {
                return document;
            }

            var root = await document.GetSyntaxRootAsync(cancellationToken);
            if (root == null)
            {
                return document;
            }

            // Collect names of pre-existing "using static" directives so we don't add duplicates.
            var existingStaticUsings = new HashSet<string>(
                root.DescendantNodes()
                    .OfType<UsingDirectiveSyntax>()
                    .Where(u => !string.IsNullOrEmpty(u.StaticKeyword.Text))
                    .Select(u => u.Name?.ToString() ?? string.Empty));

            var rewriter = new TypeQualifyingRewriter(
                usingDirectiveName, semanticModel, violatingUsingDirective, existingStaticUsings);

            var newRoot = rewriter.Visit(root);

            // Insert any "using static" directives needed for extension-method calls.
            if (rewriter.StaticUsingsToAdd.Count > 0 && newRoot is CompilationUnitSyntax compUnit)
            {
                // Reuse the violating using's trailing trivia (typically a newline) so the
                // inserted directive ends with a proper line break before the next node.
                var trailingTrivia = violatingUsingDirective.GetTrailingTrivia();

                var newUsings = compUnit.Usings;
                foreach (var su in rewriter.StaticUsingsToAdd)
                {
                    newUsings = newUsings.Insert(0, su.WithTrailingTrivia(trailingTrivia));
                }

                newRoot = compUnit.WithUsings(newUsings);
            }

            return document.WithSyntaxRoot(newRoot);
        }
    }
}
