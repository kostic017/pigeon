﻿using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using System;
using System.IO;

namespace Kostic017.Pigeon
{
    public static class ParseTreePrinter
    {
        public static void PrintTree(this IParseTree tree, TextWriter writer, string[] ruleNames, string indent = "", bool isLastChild = true)
        {
            writer.Write(indent);
            writer.Write(isLastChild ? "└──" : "├──");
            writer.Write(" ");

            indent += isLastChild ? "    " : "│   ";

            SetOutputColor(writer, tree);
            writer.Write(Trees.GetNodeText(tree, ruleNames));
            ResetOutputColor(writer);

            writer.WriteLine();

            for (int i = 0; i < tree.ChildCount; i++)
                PrintTree(tree.GetChild(i), writer, ruleNames, indent, i == tree.ChildCount - 1);
        }

        private static void SetOutputColor(TextWriter writer, IParseTree tree)
        {
            if (writer == Console.Out)
            {
                if (tree.Payload is IToken token)
                {
                    if (SyntaxFacts.Keywords.Contains(token.Text))
                        Console.ForegroundColor = ConsoleColor.Magenta;
                    else if (SyntaxFacts.Types.Contains(token.Text))
                        Console.ForegroundColor = ConsoleColor.Blue;
                    else if (token.Type == PigeonParser.ID)
                        Console.ForegroundColor = ConsoleColor.Yellow;
                    else if (token.Type == PigeonParser.NUMBER)
                        Console.ForegroundColor = ConsoleColor.Green;
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Cyan;
                }
            }
        }

        private static void ResetOutputColor(TextWriter writer)
        {
            if (writer == Console.Out)
                Console.ResetColor();
        }
    }
}