
using ASTTemplateParser;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace TestTernaryOperator
{
    public static class BenchmarkTernary
    {
        public static void Run()
        {
            var engine = new TemplateEngine();
            var model = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                { "IsActive", true },
                { "Status", "Success" },
                { "Value", 10 }
            };
            engine.SetVariables(model);

            int iterations = 100000;

            string ternaryTemplate = "Status: {{ IsActive ? 'Active' : 'Inactive' }}";
            string ifTemplate = "@if(IsActive)Status: Active @else Status: Inactive @endif";

            Console.WriteLine($"Running benchmark with {iterations} iterations...");

            // Warm up
            engine.Render(ternaryTemplate);
            engine.Render(ifTemplate);

            // Benchmark Ternary
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                engine.Render(ternaryTemplate);
            }
            sw.Stop();
            double ternaryTime = sw.Elapsed.TotalMilliseconds;
            Console.WriteLine($"Ternary Operator ({{{{ ? : }}}}): {ternaryTime:F2} ms ({(iterations / (ternaryTime / 1000)):N0} ops/sec)");

            // Benchmark If Block
            sw.Restart();
            for (int i = 0; i < iterations; i++)
            {
                engine.Render(ifTemplate);
            }
            sw.Stop();
            double ifTime = sw.Elapsed.TotalMilliseconds;
            Console.WriteLine($"If block (@if):             {ifTime:F2} ms ({(iterations / (ifTime / 1000)):N0} ops/sec)");

            Console.WriteLine("----------------------------------");
            Console.WriteLine($"Difference: {((ternaryTime / ifTime) - 1) * 100:F1}% slower than @if");

            // Benchmark Null Coalescing
            string coalescingTemplate = "Title: {{ element.Title ?? 'Default' }}";
            engine.SetVariable("element", new Dictionary<string, object>());
            
            sw.Restart();
            for (int i = 0; i < iterations; i++)
            {
                engine.Render(coalescingTemplate);
            }
            sw.Stop();
            double coalescingTime = sw.Elapsed.TotalMilliseconds;
            Console.WriteLine($"Null Coalescing ({{{{ ?? }}}}): {coalescingTime:F2} ms ({(iterations / (coalescingTime / 1000)):N0} ops/sec)");
            
            // Complex case inside loop
            int loopIterations = 5000;
            int listSize = 20;
            var list = new List<object>();
            for(int i=0; i<listSize; i++) list.Add(new { Id = i, Active = i % 2 == 0 });
            engine.SetVariable("Items", list);

            string loopTernary = "@foreach(item in Items)<li class=\"{{ item.Active ? 'active':'inactive' }}\">{{item.Id}}</li>@endforeach";
            string loopIf = "@foreach(item in Items)<li class=\"@if(item.Active)active @else inactive @endif\">{{item.Id}}</li>@endforeach";

            Console.WriteLine($"\nRunning Loop benchmark ({loopIterations} iterations, list size {listSize})...");

            sw.Restart();
            for (int i = 0; i < loopIterations; i++)
            {
                engine.Render(loopTernary);
            }
            sw.Stop();
            double loopTernaryTime = sw.Elapsed.TotalMilliseconds;
            Console.WriteLine($"Loop with Ternary: {loopTernaryTime:F2} ms");

            sw.Restart();
            for (int i = 0; i < loopIterations; i++)
            {
                engine.Render(loopIf);
            }
            sw.Stop();
            double loopIfTime = sw.Elapsed.TotalMilliseconds;
            Console.WriteLine($"Loop with If:      {loopIfTime:F2} ms");
            Console.WriteLine($"Difference: {((loopTernaryTime / loopIfTime) - 1) * 100:F1}% slower");
        }
    }
}
