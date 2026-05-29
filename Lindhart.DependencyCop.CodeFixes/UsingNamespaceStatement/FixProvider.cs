using System.Collections.Generic;
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

            // When the violating using was the only one in the file AND no replacement
            // using directives (e.g. "using static") were added, Roslyn's rewriter
            // transfers the removed using's leading trivia (UTF-8 BOM, if present) plus
            // the blank-line separator that preceded the first declaration onto that
            // declaration's first token.  This leaves a spurious blank line at the very
            // top of the fixed file.  Strip it now — but only after the using-static
            // insertion above, so we don't accidentally remove the blank line that acts
            // as a separator between a newly-inserted "using static" and the namespace
            // declaration below it.
            //
            // Safety rules:
            //   • Only strip when the file ends up with zero using directives (if any
            //     usings remain there is still a natural separator, or the BOM is already
            //     sitting on a using token, not on the namespace keyword).
            //   • Preserve any leading WhitespaceTrivia (the UTF-8 BOM character).
            //   • Never strip SingleLineComment / MultiLineComment / XmlDocComment
            //     trivia — those are file-level copyright headers that must be kept.
            if (!root.DescendantNodes().OfType<UsingDirectiveSyntax>()
                    .Any(u => u != violatingUsingDirective)
                && newRoot is CompilationUnitSyntax finalUnit
                && finalUnit.Usings.Count == 0)
            {
                var firstToken = newRoot.GetFirstToken();
                if (firstToken != default)
                {
                    var leadingTrivia = firstToken.LeadingTrivia;

                    // Skip over any leading WhitespaceTrivia (e.g. the UTF-8 BOM) and
                    // then strip the immediately-following EndOfLineTrivia (blank lines).
                    // Stop at the first piece of trivia that is neither whitespace nor
                    // end-of-line so that comments are never removed.
                    int eolStart = 0;
                    while (eolStart < leadingTrivia.Count
                           && leadingTrivia[eolStart].IsKind(SyntaxKind.WhitespaceTrivia))
                    {
                        eolStart++;
                    }

                    int eolEnd = eolStart;
                    while (eolEnd < leadingTrivia.Count
                           && leadingTrivia[eolEnd].IsKind(SyntaxKind.EndOfLineTrivia))
                    {
                        eolEnd++;
                    }

                    if (eolEnd > eolStart)
                    {
                        var newLeadingTrivia = SyntaxFactory.TriviaList(
                            leadingTrivia.Take(eolStart).Concat(leadingTrivia.Skip(eolEnd)));
                        newRoot = newRoot.ReplaceToken(
                            firstToken,
                            firstToken.WithLeadingTrivia(newLeadingTrivia));
                    }
                }
            }

            return document.WithSyntaxRoot(newRoot);
        }
    }
}
