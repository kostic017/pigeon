﻿using Xunit;

namespace Kostic017.Pigeon.Tests
{
    public class ParserTest
    {
        [Theory]
        [InlineData("4 + 4", "(4 + 4)")]
        [InlineData("4 + 4 - 4", "((4 + 4) - 4)")]
        [InlineData("4 + 4 * 4", "(4 + (4 * 4))")]
        [InlineData("4 + 4 > 4", "((4 + 4) > 4)")]
        [InlineData("4 > 4 && 4 < 4", "((4 > 4) && (4 < 4))")]
        public void ParseExpression(string text, string expected)
        {
            var syntaxTree = new SyntaxTree(text);
            Assert.Equal(expected, syntaxTree.Ast.ToString());
        }
    }
}
