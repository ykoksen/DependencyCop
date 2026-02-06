#pragma warning disable S1144
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;

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

            return await fixer.Fix(cancellationToken);
        }

        async Task<Document> Fix(CancellationToken cancellationToken)
        {
            await FixViolationsRecursively(cancellationToken);

            editor.RemoveNode(usingDirective);

            return editor.GetChangedDocument();
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

                    GoThroughSimpleNames(childNode, typeOuterNamespace, token);
                }
            }
        }

        void GoThroughSimpleNames(SyntaxNode node, DottedName namespaceWhereTypeWasDeclared, CancellationToken token)
        {
            foreach (var childNode in node.ChildNodes())
            {
                GoThroughSimpleNames(childNode, namespaceWhereTypeWasDeclared, token);

                if (childNode is SimpleNameSyntax nameSyntax)
                {
                    var symbol = semanticModel.GetSymbolInfo(nameSyntax, token).Symbol;
                    var symbolContainingNamespace = Helpers.ContainingNamespace(symbol);
                    if (symbolContainingNamespace == usingDirectiveName)
                    {
                        FixViolation(new Violation(namespaceWhereTypeWasDeclared, nameSyntax), token);
                    }
                }
            }
        }

        void FixViolation(Violation violation, CancellationToken cancellationToken)
        {
            var symbolInfo = semanticModel.GetSymbolInfo(violation.ViolatingNode, cancellationToken);
            var symbol = symbolInfo.Symbol;
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
                    }
                }
                else
                {
                    FixByQualifyingUsageOfType(lol, violation);
                }
            }
        }

        void FixByQualifyingUsageOfType(NameSyntax fullNameSpace, Violation violation)
        {
            var replacementName = new DottedName(fullNameSpace.ToString()).SkipCommonPrefix(violation.Namespace);
            if (replacementName != null)
            {
                var nameSyntax = SyntaxFactory.ParseName(replacementName.Value);
                var qualifiedNameSyntax = SyntaxFactory.QualifiedName(nameSyntax, violation.ViolatingNode);

                if (violation.ViolatingNode.Parent is QualifiedNameSyntax)
                {
                    editor.ReplaceNode(violation.ViolatingNode.Parent, qualifiedNameSyntax);
                }
                else
                {
                    editor.ReplaceNode(violation.ViolatingNode, qualifiedNameSyntax);
                }
            }
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
            public Violation(DottedName @namespace, SimpleNameSyntax violatingNode)
            {
                Namespace = @namespace;
                ViolatingNode = violatingNode;
            }

            public DottedName Namespace { get; }

            public SimpleNameSyntax ViolatingNode { get; }
        }
    }
}
