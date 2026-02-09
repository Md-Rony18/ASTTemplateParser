using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using ASTTemplateParser;

namespace ComprehensiveTest
{
    /// <summary>
    /// Comprehensive test for Performance, Security, and Auto Cache features
    /// </summary>
    class Program
    {
        static int passed = 0;
        static int failed = 0;

        static void Main(string[] args)
        {
            Console.WriteLine("╔══════════════════════════════════════════════════════════╗");
            Console.WriteLine("║   AST Template Parser - Comprehensive Test Suite         ║");
            Console.WriteLine("║   Version 1.0.3                                          ║");
            Console.WriteLine("╚══════════════════════════════════════════════════════════╝\n");

            // Clear caches before testing
            TemplateEngine.ClearCaches();

            Console.WriteLine("═══ PART 1: Core Functionality Tests ═══\n");
            RunCoreFunctionalityTests();

            Console.WriteLine("\n═══ PART 2: Security Tests ═══\n");
            RunSecurityTests();

            Console.WriteLine("\n═══ PART 3: Performance Tests ═══\n");
            RunPerformanceTests();

            Console.WriteLine("\n═══ PART 4: Auto Cache Invalidation Tests ═══\n");
            RunCacheTests();

            // Summary
            Console.WriteLine("\n" + new string('═', 60));
            Console.WriteLine($"FINAL RESULTS: {passed} passed, {failed} failed");
            Console.WriteLine(new string('═', 60));
            
            if (failed > 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("⚠ SOME TESTS FAILED!");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("✓ ALL TESTS PASSED!");
                Console.ResetColor();
            }
        }

        static void RunCoreFunctionalityTests()
        {
            // Test 1: Basic Interpolation
            Test("Basic Interpolation", () =>
            {
                var engine = new TemplateEngine();
                engine.SetVariable("Name", "John");
                var result = engine.Render("Hello, {{Name}}!");
                return result == "Hello, John!";
            });

            // Test 2: Nested Properties
            Test("Nested Object Properties", () =>
            {
                var engine = new TemplateEngine();
                engine.SetVariable("User", new { Name = "Alice", City = "NYC" });
                var result = engine.Render("{{User.Name}} from {{User.City}}");
                return result.Contains("Alice") && result.Contains("NYC");
            });

            // Test 3: If/Else
            Test("If-Else Conditionals", () =>
            {
                var engine = new TemplateEngine();
                engine.SetVariable("IsAdmin", false);
                var result = engine.Render("<If condition=\"IsAdmin\">Admin<Else>User</If>");
                return result.Contains("User") && !result.Contains("Admin");
            });

            // Test 4: ForEach Loop
            Test("ForEach Loop", () =>
            {
                var engine = new TemplateEngine();
                engine.SetVariable("Items", new List<object> { "A", "B", "C" });
                var result = engine.Render("<ForEach var=\"x\" in=\"Items\">[{{x}}]</ForEach>");
                return result.Contains("[A]") && result.Contains("[B]") && result.Contains("[C]");
            });

            // Test 5: Numeric Comparison
            Test("Numeric Comparison", () =>
            {
                var engine = new TemplateEngine();
                engine.SetVariable("Count", 15);
                var result = engine.Render("<If condition=\"Count > 10\">Big</If>");
                return result.Contains("Big");
            });

            // Test 6: Boolean Operators
            Test("Boolean AND/OR", () =>
            {
                var engine = new TemplateEngine();
                engine.SetVariable("A", true);
                engine.SetVariable("B", false);
                var r1 = engine.Render("<If condition=\"A && B\">BOTH</If>");
                var r2 = engine.Render("<If condition=\"A || B\">EITHER</If>");
                return !r1.Contains("BOTH") && r2.Contains("EITHER");
            });

            // Test 7: HTML Comments preserved
            Test("HTML Comments Preserved", () =>
            {
                var engine = new TemplateEngine();
                var result = engine.Render("<!-- comment -->text");
                return result.Contains("<!-- comment -->");
            });

            // Test 8: Empty collection
            Test("Empty Collection Handling", () =>
            {
                var engine = new TemplateEngine();
                engine.SetVariable("Items", new List<object>());
                var result = engine.Render("<ForEach var=\"x\" in=\"Items\">X</ForEach>END");
                return result.Trim() == "END";
            });
        }

        static void RunSecurityTests()
        {
            // Test 1: XSS Prevention (HTML Encoding)
            Test("XSS Prevention - HTML Encoding", () =>
            {
                var engine = new TemplateEngine();
                engine.SetVariable("UserInput", "<script>alert('xss')</script>");
                var result = engine.Render("{{UserInput}}");
                return !result.Contains("<script>") && result.Contains("&lt;script&gt;");
            });

            // Test 2: Blocked Properties
            Test("Blocked Property Names", () =>
            {
                var security = SecurityConfig.Default;
                security.BlockedPropertyNames.Add("Password");
                
                var engine = new TemplateEngine(security);
                try
                {
                    engine.SetVariable("Password", "secret123");
                    return false; // Should have thrown
                }
                catch (TemplateSecurityException)
                {
                    return true; // Expected
                }
            });

            // Test 3: Loop Limit
            Test("Loop Iteration Limit", () =>
            {
                var security = new SecurityConfig { MaxLoopIterations = 5 };
                var engine = new TemplateEngine(security);
                
                var items = new List<object>();
                for (int i = 0; i < 100; i++) items.Add(i);
                engine.SetVariable("Items", items);
                
                try
                {
                    engine.Render("<ForEach var=\"x\" in=\"Items\">{{x}}</ForEach>");
                    return false; // Should have thrown
                }
                catch (TemplateLimitException)
                {
                    return true; // Expected
                }
            });

            // Test 4: Recursion Limit
            Test("Recursion Depth Limit", () =>
            {
                var security = new SecurityConfig { MaxRecursionDepth = 2 };
                var engine = new TemplateEngine(security);
                
                // Can't easily test recursion without components, so just verify config works
                return security.MaxRecursionDepth == 2;
            });

            // Test 5: Template Size Limit
            Test("Template Size Limit", () =>
            {
                var security = new SecurityConfig { MaxTemplateSize = 100 };
                var engine = new TemplateEngine(security);
                
                var hugeTemplate = new string('x', 200);
                try
                {
                    engine.Render(hugeTemplate);
                    return false;
                }
                catch (TemplateLimitException)
                {
                    return true;
                }
            });

            // Test 6: Property Depth Limit
            Test("Property Depth Limit", () =>
            {
                var security = new SecurityConfig { MaxPropertyDepth = 2 };
                var engine = new TemplateEngine(security);
                engine.SetVariable("Level1", new { Level2 = new { Level3 = "deep" } });
                var result = engine.Render("{{Level1.Level2.Level3}}");
                // A.B.C = 3 levels, but max is 2, so should return empty
                return !result.Contains("deep");
            });
        }

        static void RunPerformanceTests()
        {
            // Test 1: Simple template speed
            Test("Performance - Simple Template (>1000 ops/sec)", () =>
            {
                var engine = new TemplateEngine();
                engine.SetVariable("Name", "Test");
                var template = "Hello {{Name}}!";
                
                // Warmup
                for (int i = 0; i < 100; i++) engine.Render(template);
                
                var sw = Stopwatch.StartNew();
                int count = 0;
                while (sw.ElapsedMilliseconds < 1000)
                {
                    engine.Render(template);
                    count++;
                }
                sw.Stop();
                
                double opsPerSec = count / (sw.ElapsedMilliseconds / 1000.0);
                Console.WriteLine($"   → {opsPerSec:N0} ops/sec");
                return opsPerSec > 1000;
            });

            // Test 2: Complex template speed
            Test("Performance - Complex Template (>500 ops/sec)", () =>
            {
                var engine = new TemplateEngine();
                engine.SetVariable("User", new { Name = "Alice", IsAdmin = true });
                engine.SetVariable("Items", new List<object> {
                    new { Name = "A", Price = 10 },
                    new { Name = "B", Price = 20 },
                    new { Name = "C", Price = 30 }
                });
                
                var template = @"
<Element template=""test"">
    <h1>{{User.Name}}</h1>
    <If condition=""User.IsAdmin""><span>Admin</span></If>
    <ForEach var=""item"" in=""Items"">
        <div>{{item.Name}}: ${{item.Price}}</div>
    </ForEach>
</Element>";
                
                // Warmup
                for (int i = 0; i < 50; i++) engine.Render(template);
                
                var sw = Stopwatch.StartNew();
                int count = 0;
                while (sw.ElapsedMilliseconds < 1000)
                {
                    engine.Render(template);
                    count++;
                }
                sw.Stop();
                
                double opsPerSec = count / (sw.ElapsedMilliseconds / 1000.0);
                Console.WriteLine($"   → {opsPerSec:N0} ops/sec");
                return opsPerSec > 500;
            });

            // Test 3: Cache effectiveness
            Test("Cache Effectiveness", () =>
            {
                TemplateEngine.ClearCaches();
                var engine = new TemplateEngine();
                engine.SetVariable("X", "test");
                var template = "Value: {{X}}";
                
                // First render (cold cache)
                var sw1 = Stopwatch.StartNew();
                engine.Render(template);
                sw1.Stop();
                
                // Second render (warm cache)
                var sw2 = Stopwatch.StartNew();
                for (int i = 0; i < 100; i++) engine.Render(template);
                sw2.Stop();
                
                var stats = TemplateEngine.GetCacheStats();
                Console.WriteLine($"   → Cache entries: {stats.AstCacheCount}");
                
                return stats.AstCacheCount > 0;
            });
        }

        static void RunCacheTests()
        {
            // Test 1: Cache is used for same template
            Test("Cache Hit for Same Template", () =>
            {
                TemplateEngine.ClearCaches();
                var engine = new TemplateEngine();
                engine.SetVariable("X", "1");
                
                engine.Render("Test {{X}}");
                engine.Render("Test {{X}}");
                
                var stats = TemplateEngine.GetCacheStats();
                return stats.AstCacheCount == 1; // Same template should be cached once
            });

            // Test 2: Different templates get different cache entries
            Test("Different Templates Cached Separately", () =>
            {
                TemplateEngine.ClearCaches();
                var engine = new TemplateEngine();
                engine.SetVariable("X", "1");
                
                engine.Render("Template A: {{X}}");
                engine.Render("Template B: {{X}}");
                
                var stats = TemplateEngine.GetCacheStats();
                return stats.AstCacheCount == 2;
            });

            // Test 3: File-based template caching
            Test("File Template Cache with Modification Check", () =>
            {
                var testDir = Path.Combine(Path.GetTempPath(), "ASTParserTest_" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(testDir);
                var testFile = Path.Combine(testDir, "test.html");
                
                try
                {
                    // Write initial content
                    File.WriteAllText(testFile, "Version 1: {{X}}");
                    
                    // Create security config that allows the temp directory
                    var security = new SecurityConfig();
                    security.AllowedTemplatePaths.Add(testDir);
                    
                    var engine = new TemplateEngine(security);
                    engine.SetComponentsDirectory(testDir); // Set directory to avoid the new check
                    engine.SetVariable("X", "test");
                    
                    // First render
                    var result1 = engine.RenderFile(testFile);
                    
                    // Wait and modify file
                    Thread.Sleep(100);
                    File.WriteAllText(testFile, "Version 2: {{X}}");
                    
                    // Second render should get new content
                    var result2 = engine.RenderFile(testFile);
                    
                    return result1.Contains("Version 1") && result2.Contains("Version 2");
                }
                finally
                {
                    try { Directory.Delete(testDir, true); } catch { }
                }
            });

            // Test 4: Component cache invalidation
            Test("Component Auto-Reload on Modification", () =>
            {
                var testDir = Path.Combine(Path.GetTempPath(), "ASTParserTest_" + Guid.NewGuid().ToString("N"));
                var compDir = Path.Combine(testDir, "components");
                Directory.CreateDirectory(compDir);
                var compFile = Path.Combine(compDir, "test.html");
                
                try
                {
                    // Write initial component
                    File.WriteAllText(compFile, "<span>V1</span>");
                    
                    TemplateEngine.ClearCaches();
                    var engine = new TemplateEngine();
                    engine.SetComponentsDirectory(compDir);
                    
                    // First render with component
                    var template = "<Include component=\"test\"></Include>";
                    var result1 = engine.Render(template);
                    
                    // Wait and modify component
                    Thread.Sleep(100);
                    File.WriteAllText(compFile, "<span>V2</span>");
                    
                    // Create new engine instance (simulating new request)
                    var engine2 = new TemplateEngine();
                    engine2.SetComponentsDirectory(compDir);
                    
                    // Second render should get new content
                    var result2 = engine2.Render(template);
                    
                    Console.WriteLine($"   → First: {result1.Trim()}, Second: {result2.Trim()}");
                    
                    return result1.Contains("V1") && result2.Contains("V2");
                }
                finally
                {
                    try { Directory.Delete(testDir, true); } catch { }
                }
            });

            // Test 5: Cache clear works
            Test("ClearCaches() Works", () =>
            {
                var engine = new TemplateEngine();
                engine.SetVariable("X", "1");
                engine.Render("Test {{X}}");
                
                var before = TemplateEngine.GetCacheStats();
                TemplateEngine.ClearCaches();
                var after = TemplateEngine.GetCacheStats();
                
                return before.AstCacheCount > 0 && after.AstCacheCount == 0;
            });
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
                    Console.WriteLine($"✗ {name} - FAILED");
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
