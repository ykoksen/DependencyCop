using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Lindhart.DependencyCop.UsingNamespaceStatement
{
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
