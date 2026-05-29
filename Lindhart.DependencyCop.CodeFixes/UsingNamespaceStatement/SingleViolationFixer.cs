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
                GoThroughTypeDeclarations(rootNode, cancellationToken);
            }

            editor.RemoveNode(violatingUsingDirective);

            var back = editor.GetChangedDocument();
            return back;
        }

        /// <summary>
        /// Determines if a syntax node is part of a member binding expression (the part after ?. operator).
        /// Member binding expressions can only contain SimpleNameSyntax, not QualifiedNameSyntax.
        /// Example: In "obj?.Property.SubProperty", the ".Property" part is a MemberBindingExpressionSyntax
        /// and can only contain the simple name "Property", not a qualified name like "Namespace.Property".
        /// </summary>
        static bool IsInMemberBindingExpression(SyntaxNode node)
        {
            // Walk up the tree to see if this node is part of a MemberBindingExpressionSyntax
            var parent = node.Parent;
            while (parent != null)
            {
                if (parent is MemberBindingExpressionSyntax)
                {
                    return true;
                }

                // Stop searching once we hit a boundary that would contain the member binding
                if (parent is ConditionalAccessExpressionSyntax)
                {
                    return false;
                }

                parent = parent.Parent;
            }

            return false;
        }

        static NameSyntax FixByQualifyingUsageOfType(NameSyntax fullNameSpace, Violation violation)
        {
            var replacementName = new DottedName(fullNameSpace.ToString()).SkipCommonPrefix(violation.Namespace);
            if (replacementName != null)
            {
                // If the original node was already qualified (e.g., Event.Nested), we need to prepend
                // the namespace to the entire qualified name, not just wrap the rightmost part
                if (violation.OriginalNode is QualifiedNameSyntax)
                {
                    // Parse the full qualified name to handle nested types correctly
                    // E.g., "Event.Nested" becomes "Account.Event.Nested", not "Account.(Event.Nested)"
                    var fullQualifiedName = $"{replacementName.Value}.{violation.OriginalNode}";
                    return SyntaxFactory.ParseName(fullQualifiedName);
                }

                var nameSyntax = SyntaxFactory.ParseName(replacementName.Value);
                var qualifiedNameSyntax = SyntaxFactory.QualifiedName(nameSyntax, violation.ViolatingNode);
                return qualifiedNameSyntax;
            }

            return null;
        }

        void GoThroughTypeDeclarations(SyntaxNode node, CancellationToken token)
        {
            foreach (var childNode in node.ChildNodes())
            {
                GoThroughTypeDeclarations(childNode, token);

                // Process all type declarations: class, record, struct, interface
                if (childNode is TypeDeclarationSyntax typeDeclaration)
                {
                    var declaredSymbol = semanticModel.GetDeclaredSymbol(typeDeclaration, token);
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
            if (node is SimpleNameSyntax)
            {
                var newNode = HandlePotentialNodeTree(node, namespaceWhereTypeWasDeclared, token);
                if (newNode != node)
                {
                    editor.ReplaceNode(node, newNode);
                }
            }
            else if (node is QualifiedNameSyntax qualifiedName)
            {
                // For qualified names (like Event.Nested or Account.Item), check if it needs qualification
                var qualifiedSymbolInfo = semanticModel.GetSymbolInfo(qualifiedName, token);
                if (qualifiedSymbolInfo.Symbol is INamedTypeSymbol typeSymbol &&
                    typeSymbol.ContainingNamespace != null &&
                    typeSymbol.ContainingNamespace.ToDisplayString() == usingDirectiveName.Value)
                {
                    // Check if the qualified name is already properly qualified (e.g., already starts with "Account.")
                    // by checking if the left side contains the namespace we would add
                    var leftSideText = qualifiedName.Left.ToString();
                    var namespacePrefix = new DottedName(typeSymbol.ContainingNamespace.ToDisplayString()).SkipCommonPrefix(namespaceWhereTypeWasDeclared);
                    if (namespacePrefix != null && leftSideText.StartsWith(namespacePrefix.Value))
                    {
                        // Already qualified, don't process further
                        return;
                    }

                    // Qualify the whole qualified name as a unit
                    var rightmostName = qualifiedName.Right;
                    var violation = new Violation(namespaceWhereTypeWasDeclared, rightmostName, qualifiedSymbolInfo.Symbol, qualifiedName);
                    var newNode = FixViolation(violation, token);
                    if (newNode != qualifiedName)
                    {
                        editor.ReplaceNode(qualifiedName, newNode);
                    }
                }
                else
                {
                    // Otherwise recurse into children
                    foreach (var syntaxNode in qualifiedName.ChildNodes())
                    {
                        GoThroughNormalNodes(syntaxNode, namespaceWhereTypeWasDeclared, token);
                    }
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
                // Skip 'var' keyword - it's not a real type reference that needs qualifying
                var syntax = simpleNameSyntax as IdentifierNameSyntax;
                if (syntax?.IsVar == true)
                {
                    return originalNode;
                }

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
                // If this is a member access (property, method, field, etc.) rather than a type reference,
                // we should not qualify it. Only qualify type names.
                if (!(symbol is ITypeSymbol))
                {
                    var possibleMethodCall = violation.ViolatingNode.Parent;
                    if (possibleMethodCall is MemberAccessExpressionSyntax &&
                        semanticModel.GetSymbolInfo(possibleMethodCall, cancellationToken).Symbol is IMethodSymbol possibleExtensionMethod &&
                        possibleExtensionMethod.IsExtensionMethod)
                    {
                        FixByAddingStaticUsingDirectiveForExtensionMethodCall(symbol);
                        return violation.ViolatingNode;
                    }

                    // For non-type symbols (properties, methods, fields), don't try to qualify
                    return violation.OriginalNode;
                }

                // Check if we're inside a member binding expression (e.g., the "Member" in "obj?.Member")
                // These contexts can only contain SimpleNameSyntax, not QualifiedNameSyntax
                if (IsInMemberBindingExpression(violation.ViolatingNode))
                {
                    return violation.OriginalNode;
                }

                var fullNameSpace = symbol.ToDisplayString();
                var lol = ((QualifiedNameSyntax)SyntaxFactory.ParseName(fullNameSpace)).Left;

                return FixByQualifyingUsageOfType(lol, violation);
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
