
using ASTTemplateParser;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace TestTernaryOperator
{
    class Program
    {
        static void Main(string[] args)
        {
            try 
            {
                var engine = new TemplateEngine();
                int passed = 0, failed = 0;

                var items = new List<Dictionary<string, object>>
                {
                    new Dictionary<string, object> { { "Name", "Alice" }, { "Age", 25 } },
                    new Dictionary<string, object> { { "Name", "Bob" }, { "Age", 30 } },
                    new Dictionary<string, object> { { "Name", "Charlie" }, { "Age", 35 } },
                };
                engine.SetVariable("Items", items);
                engine.SetVariable("IsActive", true);
                engine.SetVariable("Count", 5);

                // Simulate DataController: param["Data.Item"] = item (dotted key)
                var dataItem = new Dictionary<string, object>
                {
                    { "Title", "My Blog Post" },
                    { "Author", "Rony" },
                    { "Description", "Test description" }
                };
                engine.SetVariable("Data.Item", dataItem);

                // User's custom filter for testing (Corrected version)
                TemplateEngine.RegisterFilter("take", (val, args) => {
                    var enumerable = val as System.Collections.IEnumerable;
                    if (enumerable == null) return val;
                    int count = int.Parse(args[0]);
                    return enumerable.Cast<object>().Take(count);
                });

                // ═══════════════════════════════════════════
                // TEST 1: loop.count == 1 (the original bug)
                // ═══════════════════════════════════════════
                string tpl1 = @"<ForEach var=""item"" in=""Items""><If condition=""loop.count == 1"">[FIRST]</If></ForEach>";
                var r1 = engine.Render(tpl1).Trim();
                Check("loop.count == 1", r1.Contains("[FIRST]"), ref passed, ref failed);

                // ═══════════════════════════════════════════
                // TEST 2: loop.Count == 1 (PascalCase)
                // ═══════════════════════════════════════════
                string tpl2 = @"<ForEach var=""item"" in=""Items""><If condition=""loop.Count == 2"">[SECOND]</If></ForEach>";
                var r2 = engine.Render(tpl2).Trim();
                Check("loop.Count == 2", r2.Contains("[SECOND]"), ref passed, ref failed);

                // ═══════════════════════════════════════════
                // TEST 3: loop.index == 0
                // ═══════════════════════════════════════════
                string tpl3 = @"<ForEach var=""item"" in=""Items""><If condition=""loop.index == 0"">[IDX0]</If></ForEach>";
                var r3 = engine.Render(tpl3).Trim();
                Check("loop.index == 0", r3.Contains("[IDX0]"), ref passed, ref failed);

                // ═══════════════════════════════════════════
                // TEST 4: loop.count > 1
                // ═══════════════════════════════════════════
                string tpl4 = @"<ForEach var=""item"" in=""Items""><If condition=""loop.count > 1"">[GT1]</If></ForEach>";
                var r4 = engine.Render(tpl4).Trim();
                Check("loop.count > 1 (should match 2nd & 3rd)", r4.Contains("[GT1]"), ref passed, ref failed);

                // ═══════════════════════════════════════════
                // TEST 5: loop.first (boolean — was already working)
                // ═══════════════════════════════════════════
                string tpl5 = @"<ForEach var=""item"" in=""Items""><If condition=""loop.first"">[FIRST-BOOL]</If></ForEach>";
                var r5 = engine.Render(tpl5).Trim();
                Check("loop.first (boolean)", r5.Contains("[FIRST-BOOL]"), ref passed, ref failed);

                // ═══════════════════════════════════════════
                // TEST 6: loop.last (boolean — was already working)
                // ═══════════════════════════════════════════
                string tpl6 = @"<ForEach var=""item"" in=""Items""><If condition=""loop.last"">[LAST-BOOL]</If></ForEach>";
                var r6 = engine.Render(tpl6).Trim();
                Check("loop.last (boolean)", r6.Contains("[LAST-BOOL]"), ref passed, ref failed);

                // ═══════════════════════════════════════════
                // TEST 7: Negation !loop.first
                // ═══════════════════════════════════════════
                string tpl7 = @"<ForEach var=""item"" in=""Items""><If condition=""!loop.first"">[NOT-FIRST]</If></ForEach>";
                var r7 = engine.Render(tpl7).Trim();
                // Should match 2nd and 3rd iteration (Bob, Charlie)
                int notFirstCount = r7.Split(new[] { "[NOT-FIRST]" }, StringSplitOptions.None).Length - 1;
                Check("!loop.first (should match 2 times)", notFirstCount == 2, ref passed, ref failed);

                // ═══════════════════════════════════════════
                // TEST 8: Negation !IsActive
                // ═══════════════════════════════════════════
                string tpl8 = @"<If condition=""!IsActive"">[INACTIVE]</If><If condition=""IsActive"">[ACTIVE]</If>";
                var r8 = engine.Render(tpl8).Trim();
                Check("!IsActive / IsActive", r8.Contains("[ACTIVE]") && !r8.Contains("[INACTIVE]"), ref passed, ref failed);

                // ═══════════════════════════════════════════
                // TEST 9: AND (&&) — loop.count > 1 && loop.count < 3
                // ═══════════════════════════════════════════
                string tpl9 = @"<ForEach var=""item"" in=""Items""><If condition=""loop.count > 1 && loop.count < 3"">[MID:{{item.Name}}]</If></ForEach>";
                var r9 = engine.Render(tpl9).Trim();
                Check("&& (loop.count > 1 && loop.count < 3)", r9.Contains("[MID:Bob]") && !r9.Contains("Alice") && !r9.Contains("Charlie"), ref passed, ref failed);

                // ═══════════════════════════════════════════
                // TEST 10: OR (||) — loop.first || loop.last
                // ═══════════════════════════════════════════
                string tpl10 = @"<ForEach var=""item"" in=""Items""><If condition=""loop.first || loop.last"">[EDGE:{{item.Name}}]</If></ForEach>";
                var r10 = engine.Render(tpl10).Trim();
                Check("|| (loop.first || loop.last)", r10.Contains("[EDGE:Alice]") && r10.Contains("[EDGE:Charlie]") && !r10.Contains("[EDGE:Bob]"), ref passed, ref failed);

                // ═══════════════════════════════════════════
                // TEST 11: Hardcoded 1 == 1
                // ═══════════════════════════════════════════
                string tpl11 = @"<If condition=""1 == 1"">[TRUE]</If>";
                var r11 = engine.Render(tpl11).Trim();
                Check("1 == 1 (hardcoded)", r11.Contains("[TRUE]"), ref passed, ref failed);

                // ═══════════════════════════════════════════
                // TEST 12: String comparison item['Name'] == 'Bob' 
                // ═══════════════════════════════════════════
                string tpl12 = @"<ForEach var=""item"" in=""Items""><If condition=""item['Name'] == 'Bob'"">[FOUND-BOB]</If></ForEach>";
                var r12 = engine.Render(tpl12).Trim();
                Check("item['Name'] == 'Bob'", r12.Contains("[FOUND-BOB]"), ref passed, ref failed);

                // ═══════════════════════════════════════════
                // TEST 13: != operator
                // ═══════════════════════════════════════════
                string tpl13 = @"<If condition=""Count != 0"">[NOT-ZERO]</If>";
                var r13 = engine.Render(tpl13).Trim();
                Check("Count != 0", r13.Contains("[NOT-ZERO]"), ref passed, ref failed);

                // ═══════════════════════════════════════════
                // TEST 14: >= operator
                // ═══════════════════════════════════════════
                string tpl14 = @"<If condition=""Count >= 5"">[GTE-5]</If>";
                var r14 = engine.Render(tpl14).Trim();
                Check("Count >= 5", r14.Contains("[GTE-5]"), ref passed, ref failed);

                // ═══════════════════════════════════════════
                // TEST 15: Ternary still works (regression check)
                // ═══════════════════════════════════════════
                string tpl15 = @"<ForEach var=""item"" in=""Items"">{{ loop.count == 1 ? 'FIRST' : 'OTHER' }},</ForEach>";
                var r15 = engine.Render(tpl15).Trim();
                Check("Ternary regression", r15.StartsWith("FIRST,") && r15.Contains("OTHER,"), ref passed, ref failed);

                // ═══════════════════════════════════════════
                // TEST 16: {{ loop.count }} interpolation still works
                // ═══════════════════════════════════════════
                string tpl16 = @"<ForEach var=""item"" in=""Items"">{{loop.count}},</ForEach>";
                var r16 = engine.Render(tpl16).Trim();
                Check("Interpolation regression", r16 == "1,\n2,\n3," || r16.Replace("\r\n", "\n").Replace("\n", "") == "1,2,3,", ref passed, ref failed);

                // ═══════════════════════════════════════════
                // TEST 17: Data.Item.Title (dotted key variable — DataController pattern)
                // ═══════════════════════════════════════════
                string tpl17 = "{{ Data.Item.Title }}";
                var r17 = engine.Render(tpl17).Trim();
                Check("Data.Item.Title (dotted key)", r17 == "My Blog Post", ref passed, ref failed);

                // ═══════════════════════════════════════════
                // TEST 18: Data.Item.Author
                // ═══════════════════════════════════════════
                string tpl18 = "{{ Data.Item.Author }}";
                var r18 = engine.Render(tpl18).Trim();
                Check("Data.Item.Author (dotted key)", r18 == "Rony", ref passed, ref failed);

                // ═══════════════════════════════════════════
                // TEST 19: If condition with Data.Item.Author
                // ═══════════════════════════════════════════
                string tpl19 = @"<If condition=""Data.Item.Author == 'Rony'"">[IS-RONY]</If>";
                var r19 = engine.Render(tpl19).Trim();
                Check("If condition: Data.Item.Author == 'Rony'", r19.Contains("[IS-RONY]"), ref passed, ref failed);

                // TEST 20: Custom filter in ForEach loop (take:2)
                // ═══════════════════════════════════════════
                string tpl20 = @"<ForEach var=""item"" in=""Items | take:2"">{{ item.Name }},</ForEach>";
                var r20 = engine.Render(tpl20).Trim();
                Console.WriteLine($"DEBUG: Test 20 Output: '{r20}'");
                Check("Custom filter in ForEach (Items | take:2)", r20 == "Alice,Bob," || r20.Replace("\n", "").Replace("\r", "") == "Alice,Bob,", ref passed, ref failed);

                // ═══════════════════════════════════════════
                // TEST 21: {{name}} interpolation in nested component names
                // This tests the Block → Element → child Element chain
                // ═══════════════════════════════════════════
                Console.WriteLine("\n═══ TEST 21: Nested {{name}} interpolation ═══");
                var engine21 = new TemplateEngine();
                engine21.Security.EnableStrictMode = true;
                
                string tempDir = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "ASTTestComponents_" + Guid.NewGuid().ToString("N"));
                System.IO.Directory.CreateDirectory(tempDir);
                engine21.SetLocalComponentsDirectory(tempDir);
                
                string blockTemplate = @"<div class=""block-content"">BLOCK-NAME={{name}} | <Include component=""section-title"" name=""{{name}}_st"" oldname=""{{oldname}}_st""><Param name=""defaultTitle"" value=""Hello World"" /></Include></div>";
                System.IO.File.WriteAllText(System.IO.Path.Combine(tempDir, "block-header.html"), blockTemplate);
                
                string sectionTitleTemplate = @"<div class=""section-title"">ST-NAME={{name}} | TITLE={{defaultTitle}}</div>";
                System.IO.File.WriteAllText(System.IO.Path.Combine(tempDir, "section-title.html"), sectionTitleTemplate);
                
                string pageTemplate = @"<Include component=""block-header"" name=""myblock_abc_un"" oldname=""myblock_abc_un""><Param name=""sectionClass"" value=""test-class"" /></Include>";
                
                Console.WriteLine($"DEBUG: tempDir = {tempDir}");
                
                var r21 = engine21.Render(pageTemplate).Trim();
                Console.WriteLine($"  Output: '{r21}'");
                
                bool blockNameOk = r21.Contains("BLOCK-NAME=myblock_abc_un");
                Check("Block scope: {{name}} = 'myblock_abc_un'", blockNameOk, ref passed, ref failed);
                
                bool stNameOk = r21.Contains("ST-NAME=myblock_abc_un_st");
                Check("Child scope: {{name}} = 'myblock_abc_un_st' (derived from parent)", stNameOk, ref passed, ref failed);
                
                try { System.IO.Directory.Delete(tempDir, true); } catch { }
                
                // ═══════════════════════════════════════════
                // TEST 22: OnDataTagRender for inline <Data>
                // ═══════════════════════════════════════════
                Console.WriteLine("\n═══ TEST 22: OnDataTagRender Event ═══");
                var engine22 = new TemplateEngine();
                engine22.OnDataTagRender((attrs, vars) => {
                    if (attrs.TryGetValue("source", out string source) && source == "services-list") {
                        vars.SetVariable("MyData", "Dynamic Services Loaded!");
                    }
                });
                string dataTemplate = @"<Data source=""services-list"" items=""3"">Result: {{ MyData }}</Data>";
                var r22 = engine22.Render(dataTemplate).Trim();
                Console.WriteLine($"  Output: {r22}");
                Check("OnDataTagRender dynamically loads variables", r22.Contains("Result: Dynamic Services Loaded!"), ref passed, ref failed);
                
                Console.WriteLine("");

                // ═══════════════════════════════════════════
                // PERFORMANCE TEST
                // ═══════════════════════════════════════════
                Console.WriteLine("\n═══ PERFORMANCE ═══");
                int perfIterations = 50000;
                
                // Warm up
                engine.Render(tpl1);
                engine.Render(tpl9);
                engine.Render(tpl10);

                var sw = Stopwatch.StartNew();
                for (int i = 0; i < perfIterations; i++)
                    engine.Render(tpl1); // loop.count == 1
                sw.Stop();
                Console.WriteLine($"  loop.count == 1:       {sw.Elapsed.TotalMilliseconds:F1}ms ({perfIterations:N0} iterations, avg {sw.Elapsed.TotalMilliseconds/perfIterations:F4}ms)");

                sw = Stopwatch.StartNew();
                for (int i = 0; i < perfIterations; i++)
                    engine.Render(tpl9); // && compound
                sw.Stop();
                Console.WriteLine($"  && compound:           {sw.Elapsed.TotalMilliseconds:F1}ms ({perfIterations:N0} iterations, avg {sw.Elapsed.TotalMilliseconds/perfIterations:F4}ms)");

                sw = Stopwatch.StartNew();
                for (int i = 0; i < perfIterations; i++)
                    engine.Render(tpl10); // || compound
                sw.Stop();
                Console.WriteLine($"  || compound:           {sw.Elapsed.TotalMilliseconds:F1}ms ({perfIterations:N0} iterations, avg {sw.Elapsed.TotalMilliseconds/perfIterations:F4}ms)");

                sw = Stopwatch.StartNew();
                for (int i = 0; i < perfIterations; i++)
                    engine.Render(tpl5); // loop.first (boolean, fast path)
                sw.Stop();
                Console.WriteLine($"  loop.first (baseline): {sw.Elapsed.TotalMilliseconds:F1}ms ({perfIterations:N0} iterations, avg {sw.Elapsed.TotalMilliseconds/perfIterations:F4}ms)");

                // ═══════════════════════════════════════════
                // SUMMARY
                // ═══════════════════════════════════════════
                Console.WriteLine($"\n═══ RESULTS: {passed} PASSED, {failed} FAILED ═══");
                if (failed == 0)
                    Console.WriteLine("ALL TESTS PASSED! ✅");
                else
                    Console.WriteLine("SOME TESTS FAILED! ❌");
            }
            catch(Exception ex)
            {
                Console.WriteLine("FATAL ERROR: " + ex.Message);
                Console.WriteLine(ex.StackTrace);
            }
        }

        static void Check(string name, bool condition, ref int passed, ref int failed)
        {
            if (condition)
            {
                Console.WriteLine($"  ✅ {name}");
                passed++;
            }
            else
            {
                Console.WriteLine($"  ❌ {name}");
                failed++;
            }
        }
    }
}
