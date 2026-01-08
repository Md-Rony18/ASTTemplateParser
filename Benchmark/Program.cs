using System;
using ASTTemplateParser;

namespace Benchmark
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("╔══════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║        AST Template Parser - Performance Benchmark Suite        ║");
            Console.WriteLine("╚══════════════════════════════════════════════════════════════════╝");
            Console.WriteLine();
            
            Console.WriteLine("Starting benchmarks with 1000 iterations...");
            Console.WriteLine();

            try
            {
                var results = PerformanceBenchmark.RunBenchmarks(1000);
                Console.WriteLine();
                Console.WriteLine(results.ToString());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Benchmark failed: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }

            Console.WriteLine();
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
    }
}
