using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Lindhart.DependencyCop.UsingNamespaceStatement
{
    /// <summary>
    /// A <see cref="CSharpSyntaxRewriter"/> that, in a single tree walk:
    /// removes the violating <c>using</c> directive, qualifies every reference
    /// to types from the violating namespace, and collects <c>using static</c>
    /// directives needed for extension-method calls.
    ///
    /// The semantic model is always queried against the <em>original</em> nodes
    /// (before rewriting), which is the standard contract of CSharpSyntaxRewriter.
    /// </summary>
    internal sealed class TypeQualifyingRewriter : CSharpSyntaxRewriter
    {
        readonly DottedName violatingNs;
        readonly SemanticModel semanticModel;
        readonly UsingDirectiveSyntax violatingUsing;
        readonly HashSet<string> existingStaticUsings;
        readonly List<UsingDirectiveSyntax> staticUsingsToAdd = new List<UsingDirectiveSyntax>();

        // Updated as we descend into namespace declarations.
        DottedName currentNamespace;
        bool insideViolatingNamespace;

        public TypeQualifyingRewriter(
            DottedName violatingNs,
            SemanticModel semanticModel,
            UsingDirectiveSyntax violatingUsing,
            HashSet<string> existingStaticUsings)
        {
            this.violatingNs = violatingNs;
            this.semanticModel = semanticModel;
            this.violatingUsing = violatingUsing;
            this.existingStaticUsings = existingStaticUsings;
        }

        public IReadOnlyList<UsingDirectiveSyntax> StaticUsingsToAdd => this.staticUsingsToAdd;

        // ── Remove the violating using directive ──────────────────────────────────
        public override SyntaxNode VisitUsingDirective(UsingDirectiveSyntax node)
        {
            if (node == this.violatingUsing)
            {
                return null;
            }

            return base.VisitUsingDirective(node);
        }

        // ── Namespace-context tracking ────────────────────────────────────────────
        public override SyntaxNode VisitNamespaceDeclaration(NamespaceDeclarationSyntax node)
        {
            var (prev, prevFlag) = this.PushNamespace(node.Name.ToString());
            var result = base.VisitNamespaceDeclaration(node);
            this.PopNamespace(prev, prevFlag);
            return result;
        }

        public override SyntaxNode VisitFileScopedNamespaceDeclaration(FileScopedNamespaceDeclarationSyntax node)
        {
            var (prev, prevFlag) = this.PushNamespace(node.Name.ToString());
            var result = base.VisitFileScopedNamespaceDeclaration(node);
            this.PopNamespace(prev, prevFlag);
            return result;
        }

        // ── IdentifierName ────────────────────────────────────────────────────────
        public override SyntaxNode VisitIdentifierName(IdentifierNameSyntax node)
        {
            if (node.IsVar)
            {
                return node;
            }

            // When this identifier is the right child of a QualifiedName the parent
            // VisitQualifiedName call owns the qualification decision.
            if (node.Parent is QualifiedNameSyntax)
            {
                return node;
            }

            if (this.insideViolatingNamespace)
            {
                return node;
            }

            if (IsInMemberBindingExpression(node))
            {
                return node;
            }

            if (this.IsRightSideOfDisallowedMemberAccess(node))
            {
                return node;
            }

            var symbol = this.semanticModel.GetSymbolInfo(node).Symbol;
            if (symbol?.ContainingNamespace?.ToDisplayString() == this.violatingNs.Value)
            {
                if (symbol is ITypeSymbol typeSymbol)
                {
                    return this.BuildQualified(typeSymbol, node, node);
                }

                // Non-type (extension-method call): schedule a using static instead.
                if (node.Parent is MemberAccessExpressionSyntax mae &&
                    this.semanticModel.GetSymbolInfo(mae).Symbol is IMethodSymbol extMethod &&
                    extMethod.IsExtensionMethod)
                {
                    this.HandleExtensionMethod(symbol);
                }
            }

            return node;
        }

        // ── GenericName ───────────────────────────────────────────────────────────
        public override SyntaxNode VisitGenericName(GenericNameSyntax node)
        {
            // Always rewrite type arguments first so nested violations are fixed.
            var visited = (GenericNameSyntax)base.VisitGenericName(node);

            // When this generic name is the right child of a QualifiedName, the parent
            // VisitQualifiedName handles top-level qualification; we have already
            // rewritten the type arguments above.
            if (node.Parent is QualifiedNameSyntax)
            {
                return visited;
            }

            if (this.insideViolatingNamespace)
            {
                return visited;
            }

            if (IsInMemberBindingExpression(node))
            {
                return visited;
            }

            if (this.IsRightSideOfDisallowedMemberAccess(node))
            {
                return visited;
            }

            // Symbol lookup must use the original node (semantic model is for original tree).
            var symbol = this.semanticModel.GetSymbolInfo(node).Symbol as ITypeSymbol;
            if (symbol?.ContainingNamespace?.ToDisplayString() == this.violatingNs.Value)
            {
                return this.BuildQualified(symbol, visited, node);
            }

            return visited;
        }

        // ── QualifiedName ─────────────────────────────────────────────────────────
        public override SyntaxNode VisitQualifiedName(QualifiedNameSyntax node)
        {
            if (this.insideViolatingNamespace)
            {
                return base.VisitQualifiedName(node);
            }

            // If the whole QualifiedName resolves to a type in the violating namespace
            // (e.g. "UsingNamespaceStatementAnalyzer.Account.Id"), replace it as a unit.
            var sym = this.semanticModel.GetSymbolInfo(node).Symbol as INamedTypeSymbol;
            if (sym?.ContainingNamespace?.ToDisplayString() == this.violatingNs.Value)
            {
                // Visit node.Right so that any generic type arguments it contains are
                // also rewritten. VisitGenericName / VisitIdentifierName detect
                // "parent is QualifiedNameSyntax" and skip top-level qualification,
                // returning only the rewritten leaf.
                var rightRewritten = (SimpleNameSyntax)this.Visit(node.Right);
                return this.BuildQualified(sym, rightRewritten, node);
            }

            return base.VisitQualifiedName(node);
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns <see langword="true"/> when the node is inside a <c>?.</c>
        /// member-binding chain.  C# syntax does not allow qualified names in
        /// member-binding expressions, so we must leave those nodes unchanged.
        /// </summary>
        static bool IsInMemberBindingExpression(SyntaxNode node)
        {
            for (var parent = node.Parent; parent != null; parent = parent.Parent)
            {
                if (parent is MemberBindingExpressionSyntax)
                {
                    return true;
                }

                if (parent is ConditionalAccessExpressionSyntax)
                {
                    return false;
                }
            }

            return false;
        }

        (DottedName Prev, bool PrevFlag) PushNamespace(string nameStr)
        {
            var prev = this.currentNamespace;
            var prevFlag = this.insideViolatingNamespace;
            this.currentNamespace = prev != null
                ? new DottedName($"{prev.Value}.{nameStr}")
                : new DottedName(nameStr);
            this.insideViolatingNamespace = this.currentNamespace == this.violatingNs;
            return (prev, prevFlag);
        }

        void PopNamespace(DottedName prev, bool prevFlag)
        {
            this.currentNamespace = prev;
            this.insideViolatingNamespace = prevFlag;
        }

        /// <summary>
        /// Returns <see langword="true"/> when <paramref name="node"/> is the
        /// <c>.Name</c> (right side) of a <see cref="MemberAccessExpressionSyntax"/>
        /// whose left side already resolves to a type in the violating namespace.
        /// Qualifying the right side separately would produce double-qualification.
        /// </summary>
        bool IsRightSideOfDisallowedMemberAccess(SyntaxNode node)
        {
            if (node.Parent is MemberAccessExpressionSyntax mae && node == mae.Name)
            {
                var leftSym = this.semanticModel.GetSymbolInfo(mae.Expression).Symbol;
                if (leftSym is ITypeSymbol leftType &&
                    leftType.ContainingNamespace?.ToDisplayString() == this.violatingNs.Value)
                {
                    return true;
                }
            }

            return false;
        }

        SyntaxNode BuildQualified(ITypeSymbol sym, SimpleNameSyntax leaf, SyntaxNode originalNode)
        {
            var typeNs = new DottedName(sym.ContainingNamespace.ToDisplayString());
            var prefix = this.currentNamespace != null
                ? typeNs.SkipCommonPrefix(this.currentNamespace)
                : typeNs;

            // Null means the type lives in the same namespace — no qualification needed.
            if (prefix == null)
            {
                return leaf.WithTriviaFrom(originalNode);
            }

            // Expression context: the original node is the left (.Expression) side of a
            // MemberAccessExpression.  The replacement must also be an expression node.
            bool inExpressionContext =
                originalNode.Parent is MemberAccessExpressionSyntax mae &&
                mae.Expression == originalNode;

            if (inExpressionContext)
            {
                return SyntaxFactory
                    .MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        SyntaxFactory.ParseExpression(prefix.Value),
                        leaf.WithoutTrivia())
                    .WithTriviaFrom(originalNode);
            }

            // Type-annotation context: use a QualifiedName.
            return SyntaxFactory
                .QualifiedName(
                    SyntaxFactory.ParseName(prefix.Value),
                    leaf.WithoutTrivia())
                .WithTriviaFrom(originalNode);
        }

        void HandleExtensionMethod(ISymbol symbol)
        {
            var typeName = symbol.ContainingType.ToDisplayString();

            // HashSet.Add returns false when the name was already present — either from
            // a pre-existing using static or from a previous call in this rewrite pass.
            if (this.existingStaticUsings.Add(typeName))
            {
                this.staticUsingsToAdd.Add(
                    SyntaxFactory.UsingDirective(
                        SyntaxFactory.Token(SyntaxKind.StaticKeyword),
                        null,
                        SyntaxFactory.ParseName(typeName)));
            }
        }
    }
}
