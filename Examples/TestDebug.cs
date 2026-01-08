using System;
using System.Collections.Generic;
using ASTTemplateParser;

namespace TestDebug
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=== Debug Tokenizer and Parser ===\n");
            
            var template = @"<If condition=""Count > 10""><p>More than 10</p></If>";
            
            Console.WriteLine("Template: " + template);
            Console.WriteLine();
            
            // Test tokenizer
            Console.WriteLine("--- Tokenizer Output ---");
            var tokenizer = new Tokenizer(template);
            var tokens = tokenizer.Tokenize();
            
            foreach (var token in tokens)
            {
                Console.WriteLine($"Type: {token.Type,-15} Value: {token.Value,-15} Metadata: {token.Metadata}");
            }
            
            // Test parser
            Console.WriteLine("\n--- Parser Output ---");
            var parser = new Parser(tokens);
            var ast = parser.Parse();
            
            PrintAst(ast, 0);
            
            // Test full render with variable
            Console.WriteLine("\n--- Render Output ---");
            var engine = new TemplateEngine();
            engine.SetVariable("Count", 15);
            var result = engine.Render(template);
            Console.WriteLine($"Count=15, Result: [{result}]");
            
            Console.WriteLine("\n=== Debug Complete ===");
        }
        
        static void PrintAst(AstNode node, int depth)
        {
            string indent = new string(' ', depth * 2);
            
            if (node is RootNode root)
            {
                Console.WriteLine($"{indent}RootNode ({root.Children.Count} children)");
                foreach (var child in root.Children)
                    PrintAst(child, depth + 1);
            }
            else if (node is IfNode ifNode)
            {
                Console.WriteLine($"{indent}IfNode: Condition=\"{ifNode.Condition}\"");
                Console.WriteLine($"{indent}  ThenBranch ({ifNode.ThenBranch.Count} children):");
                foreach (var child in ifNode.ThenBranch)
                    PrintAst(child, depth + 2);
                if (ifNode.ElseBranch.Count > 0)
                {
                    Console.WriteLine($"{indent}  ElseBranch ({ifNode.ElseBranch.Count} children):");
                    foreach (var child in ifNode.ElseBranch)
                        PrintAst(child, depth + 2);
                }
            }
            else if (node is TextNode text)
            {
                Console.WriteLine($"{indent}TextNode: \"{text.Content.Substring(0, Math.Min(50, text.Content.Length))}\"");
            }
            else if (node is InterpolationNode interp)
            {
                Console.WriteLine($"{indent}InterpolationNode: \"{interp.Expression}\"");
            }
            else
            {
                Console.WriteLine($"{indent}{node.GetType().Name}");
            }
        }
    }
}
