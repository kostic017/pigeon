﻿using System.Collections.Generic;
using System.Linq;

namespace Kostic017.Pigeon.AST
{
    class SyntaxTokenWrap : AstNode
    {
        internal SyntaxToken Token { get; }

        internal SyntaxTokenWrap(SyntaxToken token)
        {
            Token = token;
        }

        internal override SyntaxNodeKind Kind => SyntaxNodeKind.SyntaxTokenWrap;
        internal override IEnumerable<AstNode> GetChildren() => Enumerable.Empty<AstNode>();
    }
}