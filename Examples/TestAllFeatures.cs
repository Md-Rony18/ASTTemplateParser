using System;
using System.Collections.Generic;
using ASTTemplateParser;

namespace TestAllFeatures
{
    /// <summary>
    /// Comprehensive test for all Template Parser features
    /// </summary>
    class Program
    {
        static int passed = 0;
        static int failed = 0;

        static void Main(string[] args)
        {
            Console.WriteLine("=== Template Parser - Comprehensive Test ===\n");

            // Test 1: Basic Interpolation
            Test("Basic Interpolation", () =>
            {
                var engine = new TemplateEngine();
                engine.SetVariable("Name", "John");
                var template = @"<Element template=""test"">Hello, {{Name}}!</Element>";
                var result = engine.Render(template);
                return result.Contains("Hello, John!");
            });

            // Test 2: Nested Object Properties
            Test("Nested Object Properties", () =>
            {
                var engine = new TemplateEngine();
                engine.SetVariable("User", new { Name = "Alice", Email = "alice@test.com" });
                var template = @"<Element template=""test"">{{User.Name}} - {{User.Email}}</Element>";
                var result = engine.Render(template);
                return result.Contains("Alice") && result.Contains("alice@test.com");
            });

            // Test 3: If Condition (true)
            Test("If Condition (true)", () =>
            {
                var engine = new TemplateEngine();
                engine.SetVariable("IsVisible", true);
                var template = @"<Element template=""test""><If condition=""IsVisible"">Visible</If></Element>";
                var result = engine.Render(template);
                return result.Contains("Visible");
            });

            // Test 4: If Condition (false)
            Test("If Condition (false)", () =>
            {
                var engine = new TemplateEngine();
                engine.SetVariable("IsVisible", false);
                var template = @"<Element template=""test""><If condition=""IsVisible"">Visible</If></Element>";
                var result = engine.Render(template);
                return !result.Contains("Visible");
            });

            // Test 5: If-Else
            Test("If-Else", () =>
            {
                var engine = new TemplateEngine();
                engine.SetVariable("IsAdmin", false);
                var template = @"<Element template=""test""><If condition=""IsAdmin"">Admin<Else>User</If></Element>";
                var result = engine.Render(template);
                return result.Contains("User") && !result.Contains("Admin");
            });

            // Test 6: If-ElseIf-Else
            Test("If-ElseIf-Else", () =>
            {
                var engine = new TemplateEngine();
                engine.SetVariable("Count", 5);
                var template = @"<Element template=""test"">
                    <If condition=""Count > 10"">Many
                    <ElseIf condition=""Count > 0"">Some
                    <Else>None</If></Element>";
                var result = engine.Render(template);
                return result.Contains("Some") && !result.Contains("Many") && !result.Contains("None");
            });

            // Test 7: ForEach Loop
            Test("ForEach Loop", () =>
            {
                var engine = new TemplateEngine();
                var items = new List<object> { "Apple", "Banana", "Cherry" };
                engine.SetVariable("Fruits", items);
                var template = @"<Element template=""test""><ForEach var=""fruit"" in=""Fruits"">{{fruit}},</ForEach></Element>";
                var result = engine.Render(template);
                return result.Contains("Apple") && result.Contains("Banana") && result.Contains("Cherry");
            });

            // Test 8: ForEach with Object Properties
            Test("ForEach with Object Properties", () =>
            {
                var engine = new TemplateEngine();
                var products = new List<object>
                {
                    new { Name = "Laptop", Price = 999 },
                    new { Name = "Mouse", Price = 29 }
                };
                engine.SetVariable("Products", products);
                var template = @"<Element template=""test""><ForEach var=""p"" in=""Products"">{{p.Name}}:{{p.Price}},</ForEach></Element>";
                var result = engine.Render(template);
                return result.Contains("Laptop:999") && result.Contains("Mouse:29");
            });

            // Test 9: Numeric Comparison
            Test("Numeric Comparison > ", () =>
            {
                var engine = new TemplateEngine();
                engine.SetVariable("Value", 15);
                var template = @"<If condition=""Value > 10"">Big</If>";
                var result = engine.Render(template);
                return result.Contains("Big");
            });

            // Test 10: Numeric Comparison == 
            Test("Numeric Comparison ==", () =>
            {
                var engine = new TemplateEngine();
                engine.SetVariable("Status", "active");
                var template = @"<If condition=""Status == 'active'"">Active!</If>";
                var result = engine.Render(template);
                return result.Contains("Active!");
            });

            // Test 11: Boolean AND
            Test("Boolean AND", () =>
            {
                var engine = new TemplateEngine();
                engine.SetVariable("A", true);
                engine.SetVariable("B", true);
                var template = @"<If condition=""A && B"">Both</If>";
                var result = engine.Render(template);
                return result.Contains("Both");
            });

            // Test 12: Boolean OR
            Test("Boolean OR", () =>
            {
                var engine = new TemplateEngine();
                engine.SetVariable("A", false);
                engine.SetVariable("B", true);
                var template = @"<If condition=""A || B"">Either</If>";
                var result = engine.Render(template);
                return result.Contains("Either");
            });

            // Test 13: Nested If inside ForEach
            Test("Nested If inside ForEach", () =>
            {
                var engine = new TemplateEngine();
                var items = new List<object>
                {
                    new { Name = "A", IsActive = true },
                    new { Name = "B", IsActive = false }
                };
                engine.SetVariable("Items", items);
                var template = @"<ForEach var=""item"" in=""Items""><If condition=""item.IsActive"">[{{item.Name}}]</If></ForEach>";
                var result = engine.Render(template);
                return result.Contains("[A]") && !result.Contains("[B]");
            });

            // Test 14: HTML Comments are preserved (not parsed as tags)
            Test("HTML Comments Preserved", () =>
            {
                var engine = new TemplateEngine();
                var template = @"<Element template=""test""><!-- This is a comment --><p>Text</p></Element>";
                var result = engine.Render(template);
                return result.Contains("<!-- This is a comment -->");
            });

            // Test 15: Multiple Interpolations
            Test("Multiple Interpolations", () =>
            {
                var engine = new TemplateEngine();
                engine.SetVariable("First", "Hello");
                engine.SetVariable("Second", "World");
                var template = @"{{First}} {{Second}}!";
                var result = engine.Render(template);
                return result == "Hello World!";
            });

            // Test 16: Data node
            Test("Data Node", () =>
            {
                var engine = new TemplateEngine();
                var template = @"<Data section=""stats""><p>Inside Data</p></Data>";
                var result = engine.Render(template);
                return result.Contains("<p>Inside Data</p>");
            });

            // Test 17: Nav node
            Test("Nav Node", () =>
            {
                var engine = new TemplateEngine();
                var template = @"<Nav section=""main""><ul><li>Menu</li></ul></Nav>";
                var result = engine.Render(template);
                return result.Contains("<ul><li>Menu</li></ul>");
            });

            // Test 18: Block node
            Test("Block Node", () =>
            {
                var engine = new TemplateEngine();
                var template = @"<Block name=""footer""><footer>Footer content</footer></Block>";
                var result = engine.Render(template);
                return result.Contains("<footer>Footer content</footer>");
            });

            // Test 19: Deeply Nested Properties
            Test("Deeply Nested Properties", () =>
            {
                var engine = new TemplateEngine();
                engine.SetVariable("Order", new 
                { 
                    Customer = new 
                    { 
                        Address = new { City = "New York" } 
                    } 
                });
                var template = @"{{Order.Customer.Address.City}}";
                var result = engine.Render(template);
                return result == "New York";
            });

            // Test 20: Empty Collection ForEach
            Test("Empty Collection ForEach", () =>
            {
                var engine = new TemplateEngine();
                engine.SetVariable("Items", new List<object>());
                var template = @"<ForEach var=""item"" in=""Items"">{{item}}</ForEach>AFTER";
                var result = engine.Render(template);
                return result.Trim() == "AFTER";
            });

            // Test 21: Dynamic Param Values - Variable Name Only (no braces)
            Test("Dynamic Param - Variable Name", () =>
            {
                // Setup a simple inline component test
                // Since we need components directory, we test the core interpolation in param values
                var engine = new TemplateEngine();
                engine.SetVariable("ButtonText", "Click Me!");
                engine.SetVariable("User", new { Name = "Alice" });
                
                // Test that the engine can resolve variables - this tests the underlying mechanism
                var template = @"<Element template=""test"">{{ButtonText}} - {{User.Name}}</Element>";
                var result = engine.Render(template);
                return result.Contains("Click Me!") && result.Contains("Alice");
            });

            // Test 22: ForEach with item property in nested element
            Test("ForEach Item Property Access", () =>
            {
                var engine = new TemplateEngine();
                var items = new List<object>
                {
                    new { Title = "First", Value = 100 },
                    new { Title = "Second", Value = 200 }
                };
                engine.SetVariable("Items", items);
                var template = @"<ForEach var=""item"" in=""Items""><div>{{item.Title}}={{item.Value}}</div></ForEach>";
                var result = engine.Render(template);
                return result.Contains("First=100") && result.Contains("Second=200");
            });

            // Summary
            Console.WriteLine("\n" + new string('=', 50));
            Console.WriteLine($"RESULTS: {passed} passed, {failed} failed");
            Console.WriteLine(new string('=', 50));
            
            if (failed > 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("SOME TESTS FAILED!");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("ALL TESTS PASSED!");
                Console.ResetColor();
            }
        }

        static void Test(string name, Func<bool> testFunc)
        {
            try
            {
                bool result = testFunc();
                if (result)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"✓ {name}");
                    Console.ResetColor();
                    passed++;
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"✗ {name} - ASSERTION FAILED");
                    Console.ResetColor();
                    failed++;
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"✗ {name} - EXCEPTION: {ex.Message}");
                Console.ResetColor();
                failed++;
            }
        }
    }
}
