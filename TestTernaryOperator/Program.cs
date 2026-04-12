
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
                int iterations = 100000;
                
                Console.WriteLine($"Running performance test with {iterations:N0} iterations...\n");

                // 1. Ternary Operator Test
                string ternaryTpl = "{{ 1 == 1 ? 'YES' : 'NO' }}";
                engine.Render(ternaryTpl); // Warm up cache
                
                var sw = Stopwatch.StartNew();
                for (int i = 0; i < iterations; i++)
                {
                    engine.Render(ternaryTpl);
                }
                sw.Stop();
                double ternaryMs = sw.Elapsed.TotalMilliseconds;
                Console.WriteLine($"[1] Ternary Operator: {ternaryMs:F2}ms (avg: {ternaryMs/iterations:F5}ms)");

                // 2. Structural @if Test
                string ifTpl = "@if(1 == 1){YES}@else{NO}";
                engine.Render(ifTpl); // Warm up
                
                sw = Stopwatch.StartNew();
                for (int i = 0; i < iterations; i++)
                {
                    engine.Render(ifTpl);
                }
                sw.Stop();
                double ifMs = sw.Elapsed.TotalMilliseconds;
                Console.WriteLine($"[2] Structural @if:   {ifMs:F2}ms (avg: {ifMs/iterations:F5}ms)");

                // 3. Null Coalescing Test
                engine.SetVariable("p", null);
                string nullTpl = "{{ p ?? 'DEFAULT' }}";
                engine.Render(nullTpl); // Warm up
                
                sw = Stopwatch.StartNew();
                for (int i = 0; i < iterations; i++)
                {
                    engine.Render(nullTpl);
                }
                sw.Stop();
                double nullMs = sw.Elapsed.TotalMilliseconds;
                Console.WriteLine($"[3] Null Coalescing:  {nullMs:F2}ms (avg: {nullMs/iterations:F5}ms)");

                Console.WriteLine("\n--- Performance Report ---");
                double diff = ((ternaryMs - ifMs) / ifMs) * 100;
                Console.WriteLine($"Ternary vs @if overhead: {diff:F1}%");
                
                if (diff < 30)
                {
                    Console.WriteLine("STATUS: HIGH PERFORMANCE ✅ (Near structural speed)");
                }
                else
                {
                    Console.WriteLine("STATUS: OPTIMIZATION REQUIRED ⚠️");
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine("ERROR: " + ex.Message);
            }
        }
    }
}
