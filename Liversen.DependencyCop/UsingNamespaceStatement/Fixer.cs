#pragma warning disable S1144
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
#pragma warning disable SA1204

namespace Liversen.DependencyCop.UsingNamespaceStatement
{
    class Fixer
    {
        readonly Document document;
        readonly UsingDirectiveSyntax usingDirective;
        readonly DottedName usingDirectiveName;
        readonly SemanticModel semanticModel;
        readonly DocumentEditor editor;
        readonly StaticUsingsSet staticUsingDirectives;

        Fixer(
            Document document,
            UsingDirectiveSyntax usingDirective,
            DottedName usingDirectiveName,
            SemanticModel semanticModel,
            DocumentEditor editor,
            StaticUsingsSet staticUsingDirectives)
        {
            this.document = document;
            this.usingDirective = usingDirective;
            this.usingDirectiveName = usingDirectiveName;
            this.semanticModel = semanticModel;
            this.editor = editor;
            this.staticUsingDirectives = staticUsingDirectives;
        }

        public static async Task<Document> Fix(Document document, UsingDirectiveSyntax usingDirective, CancellationToken cancellationToken)
        {
            if (usingDirective.Name == null)
            {
                return document;
            }

            var usingDirectiveName = new DottedName(usingDirective.Name.ToString());

            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
            if (semanticModel == null)
            {
                return document;
            }

            var editor = await DocumentEditor.CreateAsync(document, cancellationToken);
            var staticUsingDirecetive = await StaticUsingsSet.GetExisingStaticUsings(document, cancellationToken);

            var fixer = new Fixer(document, usingDirective, usingDirectiveName, semanticModel, editor, staticUsingDirecetive);

            var back = await fixer.Fix(cancellationToken);
            return back;
        }

        async Task<Document> Fix(CancellationToken cancellationToken)
        {
            await FixViolationsRecursively(cancellationToken);

            editor.RemoveNode(usingDirective);

            var back = editor.GetChangedDocument();
            return back;
        }

        async Task FixViolationsRecursively(CancellationToken token)
        {
            var rootNode = await document.GetSyntaxRootAsync(token);
            if (rootNode == null)
            {
                return;
            }

            GoThroughClasses(rootNode, token);
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
                var newNode = HandlePotentialNodeTree(node,  namespaceWhereTypeWasDeclared, token);
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
                    originalNode = originalNode.ReplaceNode(childNode, newChild);
                    simpleNameSyntax = simpleNameSyntax?.ReplaceNode(childNode, newChild);
                }
            }

            if (simpleNameSyntax != null)
            {
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

        static NameSyntax FixByQualifyingUsageOfType(NameSyntax fullNameSpace, Violation violation)
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

        void FixByAddingStaticUsingDirectiveForExtensionMethodCall(ISymbol symbol)
        {
            var staticUsingDirective = SyntaxFactory.UsingDirective(SyntaxFactory.Token(SyntaxKind.StaticKeyword), null, SyntaxFactory.ParseName(symbol.ContainingType.ToString()));

            if (staticUsingDirectives.Add(staticUsingDirective))
            {
                editor.InsertBefore(usingDirective, staticUsingDirective);
            }
        }

        sealed class Violation
        {
            public Violation(DottedName @namespace, SimpleNameSyntax violatingNode, ISymbol symbol, SyntaxNode originalNode)
            {
                Namespace = @namespace;
                ViolatingNode = violatingNode;
                Symbol = symbol;
                OriginalNode = originalNode;
            }

            public DottedName Namespace { get; }

            public SimpleNameSyntax ViolatingNode { get; }

            public ISymbol Symbol { get; }

            public SyntaxNode OriginalNode { get; }
        }
    }
}
