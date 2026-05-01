using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;

namespace Lindhart.DependencyCop.UsingNamespaceStatement
{
    class SingleViolationFixer
    {
        readonly UsingDirectiveSyntax violatingUsingDirective;
        readonly DottedName usingDirectiveName;
        readonly SemanticModel semanticModel;
        readonly DocumentEditor editor;
        readonly StaticUsingsSet staticUsingDirectives;

        public SingleViolationFixer(
            UsingDirectiveSyntax violatingUsingDirective,
            DottedName usingDirectiveName,
            SemanticModel semanticModel,
            DocumentEditor editor,
            StaticUsingsSet staticUsingDirectives)
        {
            this.violatingUsingDirective = violatingUsingDirective;
            this.usingDirectiveName = usingDirectiveName;
            this.semanticModel = semanticModel;
            this.editor = editor;
            this.staticUsingDirectives = staticUsingDirectives;
        }

        public Document FixViolation(SyntaxNode rootNode, CancellationToken cancellationToken)
        {
            if (rootNode != null)
            {
                GoThroughClasses(rootNode, cancellationToken);
            }

            editor.RemoveNode(violatingUsingDirective);

            var back = editor.GetChangedDocument();
            return back;
        }

        static QualifiedNameSyntax FixByQualifyingUsageOfType(NameSyntax fullNameSpace, Violation violation)
        {
            var replacementName = new DottedName(fullNameSpace.ToString()).SkipCommonPrefix(violation.Namespace);
            if (replacementName != null)
            {
                var nameSyntax = SyntaxFactory.ParseName(replacementName.Value);
                var qualifiedNameSyntax = SyntaxFactory.QualifiedName(nameSyntax, violation.ViolatingNode);
                return qualifiedNameSyntax;
            }

            return null;
        }

        void GoThroughClasses(SyntaxNode node, CancellationToken token)
        {
            foreach (var childNode in node.ChildNodes())
            {
                GoThroughClasses(childNode, token);

                if (childNode is ClassDeclarationSyntax classDeclaration)
                {
                    var declaredSymbol = semanticModel.GetDeclaredSymbol(classDeclaration, token);
                    if (declaredSymbol == null)
                    {
                        continue;
                    }

                    var typeOuterNamespace = Helpers.ContainingNamespace(declaredSymbol);
                    if (typeOuterNamespace == null || typeOuterNamespace == usingDirectiveName)
                    {
                        continue;
                    }

                    GoThroughNormalNodes(childNode, typeOuterNamespace, token);
                }
            }
        }

        void GoThroughNormalNodes(SyntaxNode node, DottedName namespaceWhereTypeWasDeclared, CancellationToken token)
        {
            if (node is SimpleNameSyntax || node is QualifiedNameSyntax)
            {
                var newNode = HandlePotentialNodeTree(node, namespaceWhereTypeWasDeclared, token);
                if (newNode != node)
                {
                    editor.ReplaceNode(node, newNode);
                }
            }
            else
            {
                foreach (var syntaxNode in node.ChildNodes())
                {
                    GoThroughNormalNodes(syntaxNode, namespaceWhereTypeWasDeclared, token);
                }
            }
        }

        SyntaxNode HandlePotentialNodeTree(SyntaxNode originalNode, DottedName namespaceWhereTypeWasDeclared, CancellationToken token)
        {
            SyntaxNode node = originalNode;
            if (originalNode is QualifiedNameSyntax qualifiedNameSyntax)
            {
                node = qualifiedNameSyntax.Right;
            }

            SymbolInfo symbolInfo = default;
            var simpleNameSyntax = node as SimpleNameSyntax;
            if (simpleNameSyntax != null)
            {
                symbolInfo = semanticModel.GetSymbolInfo(originalNode, token);
            }

            foreach (var childNode in node.ChildNodes())
            {
                var newChild = HandlePotentialNodeTree(childNode, namespaceWhereTypeWasDeclared, token);
                if (newChild != childNode)
                {
                    // Sometimes we don't change the parent node - so we keep the possible qualified name syntax / original node. Sometimes we need to change so we also change the simple name syntax.
                    originalNode = originalNode.ReplaceNode(childNode, newChild);
                    simpleNameSyntax = simpleNameSyntax?.ReplaceNode(childNode, newChild);
                }
            }

            if (simpleNameSyntax != null)
            {
                // Add both the simplename syntax AND the original node in case we don't need to change anything.
                var violation = new Violation(namespaceWhereTypeWasDeclared, simpleNameSyntax, symbolInfo.Symbol, originalNode);
                return FixViolation(violation, token);
            }

            return originalNode;
        }

        SyntaxNode FixViolation(Violation violation, CancellationToken cancellationToken)
        {
            var symbol = violation.Symbol;
            if (symbol?.ContainingNamespace != null &&
                symbol.ContainingNamespace.ToDisplayString() == usingDirectiveName.Value)
            {
                var fullNameSpace = symbol.ToDisplayString();
                var lol = ((QualifiedNameSyntax)SyntaxFactory.ParseName(fullNameSpace)).Left;

                var possibleMethodCall = violation.ViolatingNode.Parent;
                if (possibleMethodCall is MemberAccessExpressionSyntax)
                {
                    if (semanticModel.GetSymbolInfo(possibleMethodCall, cancellationToken).Symbol is IMethodSymbol possibleExtensionMethod
                        && possibleExtensionMethod.IsExtensionMethod)
                    {
                        FixByAddingStaticUsingDirectiveForExtensionMethodCall(symbol);
                        return violation.ViolatingNode;
                    }
                }
                else
                {
                    return FixByQualifyingUsageOfType(lol, violation);
                }
            }

            return violation.OriginalNode;
        }

        void FixByAddingStaticUsingDirectiveForExtensionMethodCall(ISymbol symbol)
        {
            var staticUsingDirective = SyntaxFactory.UsingDirective(SyntaxFactory.Token(SyntaxKind.StaticKeyword), null, SyntaxFactory.ParseName(symbol.ContainingType.ToString()));

            if (staticUsingDirectives.Add(staticUsingDirective))
            {
                editor.InsertBefore(violatingUsingDirective, staticUsingDirective);
            }
        }
    }
}
