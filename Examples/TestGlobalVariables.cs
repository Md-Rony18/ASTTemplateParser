using System;
using System.IO;
using System.Collections.Generic;
using ASTTemplateParser;

namespace Examples
{
    /// <summary>
    /// Test Global Variables feature and verify no regression
    /// </summary>
    public class TestGlobalVariables
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("=== Testing Global Variables & Regression Check ===\n");
            
            int passed = 0;
            int failed = 0;

            // Test 1: Global Variable Basic
            try
            {
                TemplateEngine.ClearGlobalVariables();
                TemplateEngine.SetGlobalVariable("SiteName", "My Website");
                
                var engine = new TemplateEngine();
                var result = engine.Render("Welcome to {{SiteName}}!");
                
                if (result == "Welcome to My Website!")
                {
                    Console.WriteLine("✓ Test 1: Global Variable Basic");
                    passed++;
                }
                else
                {
                    Console.WriteLine($"✗ Test 1: Expected 'Welcome to My Website!' got '{result}'");
                    failed++;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Test 1: Exception - {ex.Message}");
                failed++;
            }

            // Test 2: Global Variable Across Multiple Engine Instances
            try
            {
                TemplateEngine.ClearGlobalVariables();
                TemplateEngine.SetGlobalVariable("AppVersion", "2.0.1");

                var engine1 = new TemplateEngine();
                var engine2 = new TemplateEngine();
                
                var result1 = engine1.Render("v{{AppVersion}}");
                var result2 = engine2.Render("version: {{AppVersion}}");
                
                if (result1 == "v2.0.1" && result2 == "version: 2.0.1")
                {
                    Console.WriteLine("✓ Test 2: Global Variable Across Instances");
                    passed++;
                }
                else
                {
                    Console.WriteLine($"✗ Test 2: result1='{result1}', result2='{result2}'");
                    failed++;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Test 2: Exception - {ex.Message}");
                failed++;
            }

            // Test 3: Instance Variable Overrides Global
            try
            {
                TemplateEngine.ClearGlobalVariables();
                TemplateEngine.SetGlobalVariable("Title", "Global Title");
                
                var engine = new TemplateEngine();
                engine.SetVariable("Title", "Instance Title");
                
                var result = engine.Render("{{Title}}");
                
                if (result == "Instance Title")
                {
                    Console.WriteLine("✓ Test 3: Instance Variable Overrides Global");
                    passed++;
                }
                else
                {
                    Console.WriteLine($"✗ Test 3: Expected 'Instance Title' got '{result}'");
                    failed++;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Test 3: Exception - {ex.Message}");
                failed++;
            }

            // Test 4: Render Additional Variables Override All
            try
            {
                TemplateEngine.ClearGlobalVariables();
                TemplateEngine.SetGlobalVariable("Name", "Global");
                
                var engine = new TemplateEngine();
                engine.SetVariable("Name", "Instance");
                
                var result = engine.Render("{{Name}}", new Dictionary<string, object>
                {
                    { "Name", "Additional" }
                });
                
                if (result == "Additional")
                {
                    Console.WriteLine("✓ Test 4: Additional Variables Override All");
                    passed++;
                }
                else
                {
                    Console.WriteLine($"✗ Test 4: Expected 'Additional' got '{result}'");
                    failed++;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Test 4: Exception - {ex.Message}");
                failed++;
            }

            // Test 5: SetGlobalVariables (Multiple)
            try
            {
                TemplateEngine.ClearGlobalVariables();
                TemplateEngine.SetGlobalVariables(new Dictionary<string, object>
                {
                    { "A", "Alpha" },
                    { "B", "Beta" },
                    { "C", "Gamma" }
                });
                
                var engine = new TemplateEngine();
                var result = engine.Render("{{A}}-{{B}}-{{C}}");
                
                if (result == "Alpha-Beta-Gamma")
                {
                    Console.WriteLine("✓ Test 5: SetGlobalVariables (Multiple)");
                    passed++;
                }
                else
                {
                    Console.WriteLine($"✗ Test 5: Expected 'Alpha-Beta-Gamma' got '{result}'");
                    failed++;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Test 5: Exception - {ex.Message}");
                failed++;
            }

            // Test 6: GetGlobalVariable & HasGlobalVariable
            try
            {
                TemplateEngine.ClearGlobalVariables();
                TemplateEngine.SetGlobalVariable("TestKey", "TestValue");
                
                var hasKey = TemplateEngine.HasGlobalVariable("TestKey");
                var hasOther = TemplateEngine.HasGlobalVariable("NonExistent");
                var value = TemplateEngine.GetGlobalVariable("TestKey");
                var count = TemplateEngine.GlobalVariableCount;
                
                if (hasKey && !hasOther && value?.ToString() == "TestValue" && count == 1)
                {
                    Console.WriteLine("✓ Test 6: GetGlobalVariable & HasGlobalVariable");
                    passed++;
                }
                else
                {
                    Console.WriteLine($"✗ Test 6: hasKey={hasKey}, hasOther={hasOther}, value={value}, count={count}");
                    failed++;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Test 6: Exception - {ex.Message}");
                failed++;
            }

            // Test 7: RemoveGlobalVariable
            try
            {
                TemplateEngine.ClearGlobalVariables();
                TemplateEngine.SetGlobalVariable("ToRemove", "Value");
                
                var removed = TemplateEngine.RemoveGlobalVariable("ToRemove");
                var stillExists = TemplateEngine.HasGlobalVariable("ToRemove");
                
                if (removed && !stillExists)
                {
                    Console.WriteLine("✓ Test 7: RemoveGlobalVariable");
                    passed++;
                }
                else
                {
                    Console.WriteLine($"✗ Test 7: removed={removed}, stillExists={stillExists}");
                    failed++;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Test 7: Exception - {ex.Message}");
                failed++;
            }

            // Test 8: Global Objects with Nested Properties
            try
            {
                TemplateEngine.ClearGlobalVariables();
                TemplateEngine.SetGlobalVariable("Company", new { 
                    Name = "ACME Corp",
                    Address = new { City = "New York" }
                });
                
                var engine = new TemplateEngine();
                var result = engine.Render("{{Company.Name}} - {{Company.Address.City}}");
                
                if (result.Contains("ACME Corp") && result.Contains("New York"))
                {
                    Console.WriteLine("✓ Test 8: Global Objects with Nested Properties");
                    passed++;
                }
                else
                {
                    Console.WriteLine($"✗ Test 8: Got '{result}'");
                    failed++;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Test 8: Exception - {ex.Message}");
                failed++;
            }

            // Test 9: Global Variables in RenderFile
            try
            {
                TemplateEngine.ClearGlobalVariables();
                TemplateEngine.SetGlobalVariable("GlobalSite", "TestSite");
                
                var engine = new TemplateEngine();
                engine.SetPagesDirectory("./pages");
                
                // Create a temporary page file
                if (!Directory.Exists("./pages")) Directory.CreateDirectory("./pages");
                File.WriteAllText("./pages/global-test.html", "Welcome to {{GlobalSite}}");
                
                var result = engine.RenderPage("global-test");
                
                if (result == "Welcome to TestSite")
                {
                    Console.WriteLine("✓ Test 9: Global Variables in RenderFile/RenderPage");
                    passed++;
                }
                else
                {
                    Console.WriteLine($"✗ Test 9: Expected 'Welcome to TestSite' got '{result}'");
                    failed++;
                }
                
                // Cleanup
                File.Delete("./pages/global-test.html");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Test 9: Exception - {ex.Message}");
                failed++;
            }

            // Test 10: Recursive Fragment with Global Data (User Scenario)
            try
            {
                TemplateEngine.ClearGlobalVariables();
                
                // Set up global navigation data similar to user's structure
                var menuData = new List<object>
                {
                    new { 
                        Item = new { Title = "Home", Href = "/" },
                        Children = new List<object> {
                            new { Item = new { Title = "About", Href = "/about" } }
                        }
                    },
                    new { 
                        Item = new { Title = "Contact", Href = "/contact" }
                    }
                };
                
                TemplateEngine.SetGlobalVariable("Nav", new { Main = menuData });
                
                var engine = new TemplateEngine();
                var template = @"
                    <nav>
                        <ul>
                            <ForEach var=""item"" in=""Nav.Main"">
                                <Render name=""menuItem"" menuNode=""item"" />
                            </ForEach>
                        </ul>
                    </nav>
                    <Define name=""menuItem"">
                        <li>
                            <a href=""{{menuNode.Item.Href}}"">
                                {{menuNode.Item.Title}}
                                <If condition=""menuNode.Children"">
                                    <i>Has Kids</i>
                                </If>
                            </a>
                            <If condition=""menuNode.Children"">
                                <ul class=""submenu"">
                                    <ForEach var=""child"" in=""menuNode.Children"">
                                        <Render name=""menuItem"" menuNode=""child"" />
                                    </ForEach>
                                </ul>
                            </If>
                        </li>
                    </Define>";
                
                var result = engine.Render(template).Trim();
                
                // Simple checks for expected content
                if (result.Contains("Home") && result.Contains("About") && result.Contains("Has Kids") && result.Contains("/about"))
                {
                    Console.WriteLine("✓ Test 10: Recursive Fragment with Global Data (User Scenario)");
                    passed++;
                }
                else
                {
                    Console.WriteLine($"✗ Test 10: Result missing expected content. Result: {result}");
                    failed++;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Test 10: Exception - {ex.Message}");
                failed++;
            }

            // Test 12: Conditional Truthiness with Dictionary (User Scenario)
            try
            {
                TemplateEngine.ClearGlobalVariables();
                
                // Using Dictionary for items to test PropertyAccessor with IDictionary
                var menuData = new List<object>
                {
                    new Dictionary<string, object> {
                        { "Item", new { Title = "Home", Href = "/" } },
                        { "HasChildren", true }, // Boolean property in a dictionary
                        { "Children", new List<object> {
                            new Dictionary<string, object> {
                                { "Item", new { Title = "Sub", Href = "/sub" } },
                                { "HasChildren", false }
                            }
                        }}
                    }
                };
                
                TemplateEngine.SetGlobalVariable("Nav", new { Main = menuData });
                
                var engine = new TemplateEngine();
                var template = @"
                    <ForEach var=""item"" in=""Nav.Main"">
                        <Render name=""menuItem"" menuNode=""item"" />
                    </ForEach>
                    <Define name=""menuItem"">
                        <If condition=""menuNode.HasChildren"">
                            <i class=""arrow""></i>
                        </If>
                        <span>{{menuNode.Item.Title}}</span>
                    </Define>";
                
                var result = engine.Render(template);
                
                if (result.Contains("class=\"arrow\"") && result.Contains("Home"))
                {
                    Console.WriteLine("✓ Test 12: Complex condition check with Dictionary/Truthiness");
                    passed++;
                }
                else
                {
                    Console.WriteLine($"✗ Test 12: Failed. Arrow not found or Home not found. Result: {result}");
                    failed++;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Test 12: Exception - {ex.Message}");
                failed++;
            }

            // Test 13: User Scenario Exact Snippet (Dictionary with HasChildren)
            try
            {
                TemplateEngine.ClearGlobalVariables();
                
                var menuData = new List<object>
                {
                    new Dictionary<string, object> {
                        { "Item", new { Name = "Category", Href = "/cat" } },
                        { "HasChildren", true }, // Key name matches user snippet
                        { "Children", new List<object> {
                            new Dictionary<string, object> {
                                { "Item", new { Name = "Product", Href = "/prod" } },
                                { "HasChildren", false }
                            }
                        }}
                    }
                };
                
                TemplateEngine.SetGlobalVariable("MenuData", menuData);
                
                var engine = new TemplateEngine();
                var template = @"
                    <Define name=""menuItem"">
                        <li>
                            <a href=""{{menuNode.Item.Href}}"">
                                {{menuNode.Item.Name}}
                                <If condition=""menuNode.HasChildren"">
                                    <i class=""arrow""></i>
                                </If>
                            </a>
                            <If condition=""menuNode.HasChildren"">
                                <ul class=""submenu"">
                                    <ForEach var=""child"" in=""menuNode.Children"">
                                        <Render name=""menuItem"" menuNode=""child"" />
                                    </ForEach>
                                </ul>
                            </If>
                        </li>
                    </Define>
                    <ul>
                        <ForEach var=""item"" in=""MenuData"">
                            <Render name=""menuItem"" menuNode=""item"" />
                        </ForEach>
                    </ul>";
                
                var result = engine.Render(template);
                
                if (result.Contains("class=\"arrow\"") && result.Contains("Category") && result.Contains("Product") && result.Contains("submenu"))
                {
                    Console.WriteLine("✓ Test 13: Exact user scenario (Recursive fragments + Dictionaries)");
                    passed++;
                }
                else
                {
                    Console.WriteLine($"✗ Test 13: Failed. Output missing expected content. Result was truncated for brevity.");
                    // Print small part of result for debugging
                    Console.WriteLine("Truncated Result: " + (result.Length > 200 ? result.Substring(0, 200) : result));
                    failed++;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Test 13: Exception - {ex.Message}");
                failed++;
            }

            // Test 14: Truthiness Logic (Numbers, Strings, Collections)
            try
            {
                TemplateEngine.ClearGlobalVariables();
                
                var data = new Dictionary<string, object>
                {
                    { "NumOne", 1 },
                    { "NumZero", 0 },
                    { "StrTrue", "true" },
                    { "StrFalse", "false" },
                    { "EmptyStr", "" },
                    { "ListFull", new List<int> { 1, 2 } },
                    { "ListEmpty", new List<int>() }
                };
                
                var engine = new TemplateEngine();
                var template = @"
                    <If condition=""NumOne"">1-OK</If>
                    <If condition=""NumZero"">0-FAIL</If>
                    <If condition=""StrTrue"">S-OK</If>
                    <If condition=""StrFalse"">SF-FAIL</If>
                    <If condition=""EmptyStr"">E-FAIL</If>
                    <If condition=""ListFull"">L-OK</If>
                    <If condition=""ListEmpty"">LE-FAIL</If>";
                
                var result = engine.Render(template, data).Replace("\r", "").Replace("\n", "").Replace(" ", "");
                
                if (result.Contains("1-OK") && result.Contains("S-OK") && result.Contains("L-OK") && 
                    !result.Contains("0-FAIL") && !result.Contains("SF-FAIL") && !result.Contains("E-FAIL") && !result.Contains("LE-FAIL"))
                {
                    Console.WriteLine("✓ Test 14: Truthiness Logic (Numbers, Strings, Collections)");
                    passed++;
                }
                else
                {
                    Console.WriteLine($"✗ Test 14: Truthiness Failed. Result: {result}");
                    failed++;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Test 14: Exception - {ex.Message}");
                failed++;
            }

            // Test 15: Complex Nested Conditions (Negation, Comparison)
            try
            {
                TemplateEngine.ClearGlobalVariables();
                
                var data = new Dictionary<string, object>
                {
                    { "menuNode", new Dictionary<string, object> {
                        { "HasChildren", true },
                        { "Status", "Active" }
                    }}
                };
                
                var engine = new TemplateEngine();
                var template = @"
                    <If condition=""menuNode.HasChildren"">1-OK</If>
                    <If condition=""!menuNode.HasChildren"">2-FAIL</If>
                    <If condition=""menuNode.Status == 'Active'"">3-OK</If>
                    <If condition=""menuNode.Status != 'Inactive'"">4-OK</If>
                    <If condition=""menuNode.Missing == null"">5-OK</If>";
                
                var result = engine.Render(template, data).Replace("\r", "").Replace("\n", "").Replace(" ", "");
                
                if (result.Contains("1-OK") && result.Contains("3-OK") && result.Contains("4-OK") && result.Contains("5-OK") && !result.Contains("2-FAIL"))
                {
                    Console.WriteLine("✓ Test 15: Complex Nested Conditions (Negation, Comparison)");
                    passed++;
                }
                else
                {
                    Console.WriteLine($"✗ Test 15: Complex Conditions Failed. Result: {result}");
                    failed++;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Test 15: Exception - {ex.Message}");
                failed++;
            }

            // === REGRESSION TESTS (Ensure old features still work) ===
            Console.WriteLine("\n=== Regression Tests ===\n");

            // Regression 1: Basic Interpolation
            try
            {
                TemplateEngine.ClearGlobalVariables();
                var engine = new TemplateEngine();
                engine.SetVariable("Name", "John");
                var result = engine.Render("Hello {{Name}}!");
                
                if (result == "Hello John!")
                {
                    Console.WriteLine("✓ Regression 1: Basic Interpolation");
                    passed++;
                }
                else
                {
                    Console.WriteLine($"✗ Regression 1: Expected 'Hello John!' got '{result}'");
                    failed++;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Regression 1: Exception - {ex.Message}");
                failed++;
            }

            // Regression 2: If-Else
            try
            {
                var engine = new TemplateEngine();
                engine.SetVariable("IsAdmin", false);
                var result = engine.Render("<If condition=\"IsAdmin\">Admin<Else>User</If>");
                
                if (result.Contains("User") && !result.Contains("Admin"))
                {
                    Console.WriteLine("✓ Regression 2: If-Else Conditionals");
                    passed++;
                }
                else
                {
                    Console.WriteLine($"✗ Regression 2: Got '{result}'");
                    failed++;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Regression 2: Exception - {ex.Message}");
                failed++;
            }

            // Regression 3: ForEach Loop
            try
            {
                var engine = new TemplateEngine();
                engine.SetVariable("Items", new List<object> { "A", "B", "C" });
                var result = engine.Render("<ForEach var=\"x\" in=\"Items\">[{{x}}]</ForEach>");
                
                if (result.Contains("[A]") && result.Contains("[B]") && result.Contains("[C]"))
                {
                    Console.WriteLine("✓ Regression 3: ForEach Loop");
                    passed++;
                }
                else
                {
                    Console.WriteLine($"✗ Regression 3: Got '{result}'");
                    failed++;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Regression 3: Exception - {ex.Message}");
                failed++;
            }

            // Regression 4: XSS Prevention
            try
            {
                var engine = new TemplateEngine();
                engine.SetVariable("Input", "<script>alert('xss')</script>");
                var result = engine.Render("{{Input}}");
                
                if (!result.Contains("<script>") && result.Contains("&lt;script&gt;"))
                {
                    Console.WriteLine("✓ Regression 4: XSS Prevention (HTML Encoding)");
                    passed++;
                }
                else
                {
                    Console.WriteLine($"✗ Regression 4: Got '{result}'");
                    failed++;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Regression 4: Exception - {ex.Message}");
                failed++;
            }

            // Regression 5: Nested Object Properties
            try
            {
                var engine = new TemplateEngine();
                engine.SetVariable("User", new { Name = "Alice", City = "NYC" });
                var result = engine.Render("{{User.Name}} from {{User.City}}");
                
                if (result.Contains("Alice") && result.Contains("NYC"))
                {
                    Console.WriteLine("✓ Regression 5: Nested Object Properties");
                    passed++;
                }
                else
                {
                    Console.WriteLine($"✗ Regression 5: Got '{result}'");
                    failed++;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Regression 5: Exception - {ex.Message}");
                failed++;
            }

            // Summary
            Console.WriteLine($"\n{'=',-50}");
            Console.WriteLine($"RESULTS: {passed} passed, {failed} failed");
            Console.WriteLine($"{'=',-50}");
            
            if (failed == 0)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("✓ ALL TESTS PASSED! No regression detected.");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("✗ SOME TESTS FAILED!");
                Console.ResetColor();
            }

            // Cleanup
            TemplateEngine.ClearGlobalVariables();
        }
    }
}
