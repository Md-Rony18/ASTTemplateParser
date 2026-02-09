using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace ASTTemplateParser
{
    /// <summary>
    /// Performance benchmarking utility for template parser
    /// </summary>
    public static class PerformanceBenchmark
    {
        /// <summary>
        /// Runs comprehensive performance tests and returns results
        /// </summary>
        public static BenchmarkResults RunBenchmarks(int iterations = 1000)
        {
            var results = new BenchmarkResults();
            
            // Test templates of various sizes
            var smallTemplate = GenerateTemplate(10, 5);      // ~500 chars
            var mediumTemplate = GenerateTemplate(50, 20);    // ~5KB
            var largeTemplate = GenerateTemplate(100, 50);    // ~20KB
            
            // Sample data model
            var model = new Dictionary<string, object>
            {
                { "Title", "Performance Test" },
                { "Items", GenerateItems(100) },
                { "User", new { Name = "TestUser", Email = "test@example.com", IsActive = true } },
                { "Count", 42 },
                { "Price", 99.99 }
            };

            // Warm-up run
            Console.WriteLine("Warming up...");
            var engine = new TemplateEngine();
            engine.SetVariables(model);
            engine.Render(smallTemplate);
            engine.Render(mediumTemplate);
            TemplateEngine.ClearCaches();

            // Benchmark tokenization
            Console.WriteLine("Benchmarking tokenization...");
            results.TokenizeSmall = BenchmarkTokenize(smallTemplate, iterations);
            results.TokenizeMedium = BenchmarkTokenize(mediumTemplate, iterations);
            results.TokenizeLarge = BenchmarkTokenize(largeTemplate, iterations);

            // Benchmark parsing
            Console.WriteLine("Benchmarking parsing...");
            results.ParseSmall = BenchmarkParse(smallTemplate, iterations);
            results.ParseMedium = BenchmarkParse(mediumTemplate, iterations);
            results.ParseLarge = BenchmarkParse(largeTemplate, iterations);

            // Benchmark full render (cold cache)
            Console.WriteLine("Benchmarking cold render...");
            results.RenderColdSmall = BenchmarkRenderCold(smallTemplate, model, iterations / 10);
            results.RenderColdMedium = BenchmarkRenderCold(mediumTemplate, model, iterations / 10);
            results.RenderColdLarge = BenchmarkRenderCold(largeTemplate, model, iterations / 10);

            // Benchmark full render (hot cache)
            Console.WriteLine("Benchmarking hot render...");
            results.RenderHotSmall = BenchmarkRenderHot(smallTemplate, model, iterations);
            results.RenderHotMedium = BenchmarkRenderHot(mediumTemplate, model, iterations);
            results.RenderHotLarge = BenchmarkRenderHot(largeTemplate, model, iterations);

            // Benchmark property access
            Console.WriteLine("Benchmarking property access...");
            results.PropertyAccessCold = BenchmarkPropertyAccess(100, iterations);
            results.PropertyAccessHot = BenchmarkPropertyAccessHot(100, iterations);

            // Memory stats
            results.CacheStats = TemplateEngine.GetCacheStats();

            return results;
        }

        private static TimingResult BenchmarkTokenize(string template, int iterations)
        {
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                var tokenizer = new Tokenizer(template);
                tokenizer.Tokenize();
            }
            sw.Stop();
            return new TimingResult
            {
                TotalMs = sw.Elapsed.TotalMilliseconds,
                Iterations = iterations,
                TemplateSize = template.Length
            };
        }

        private static TimingResult BenchmarkParse(string template, int iterations)
        {
            // Pre-tokenize
            var tokenizer = new Tokenizer(template);
            var tokens = tokenizer.Tokenize();

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                var parser = new Parser(tokens);
                parser.Parse();
            }
            sw.Stop();
            return new TimingResult
            {
                TotalMs = sw.Elapsed.TotalMilliseconds,
                Iterations = iterations,
                TemplateSize = template.Length
            };
        }

        private static TimingResult BenchmarkRenderCold(string template, Dictionary<string, object> model, int iterations)
        {
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                TemplateEngine.ClearCaches();
                var engine = new TemplateEngine();
                engine.SetVariables(model);
                engine.Render(template);
            }
            sw.Stop();
            return new TimingResult
            {
                TotalMs = sw.Elapsed.TotalMilliseconds,
                Iterations = iterations,
                TemplateSize = template.Length
            };
        }

        private static TimingResult BenchmarkRenderHot(string template, Dictionary<string, object> model, int iterations)
        {
            // Prime the cache
            var engine = new TemplateEngine();
            engine.SetVariables(model);
            engine.Render(template);

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                engine.Render(template);
            }
            sw.Stop();
            return new TimingResult
            {
                TotalMs = sw.Elapsed.TotalMilliseconds,
                Iterations = iterations,
                TemplateSize = template.Length
            };
        }

        private static TimingResult BenchmarkPropertyAccess(int propertyCount, int iterations)
        {
            PropertyAccessor.ClearCache();
            var testObj = new TestObject { Name = "Test", Value = 42 };

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                PropertyAccessor.GetValue(testObj, "Name");
                PropertyAccessor.GetValue(testObj, "Value");
            }
            sw.Stop();

            return new TimingResult
            {
                TotalMs = sw.Elapsed.TotalMilliseconds,
                Iterations = iterations * 2,
                TemplateSize = 0
            };
        }

        private static TimingResult BenchmarkPropertyAccessHot(int propertyCount, int iterations)
        {
            var testObj = new TestObject { Name = "Test", Value = 42 };
            
            // Prime cache
            PropertyAccessor.GetValue(testObj, "Name");
            PropertyAccessor.GetValue(testObj, "Value");

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                PropertyAccessor.GetValue(testObj, "Name");
                PropertyAccessor.GetValue(testObj, "Value");
            }
            sw.Stop();

            return new TimingResult
            {
                TotalMs = sw.Elapsed.TotalMilliseconds,
                Iterations = iterations * 2,
                TemplateSize = 0
            };
        }

        private static string GenerateTemplate(int elementCount, int itemsPerLoop)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<Element template=\"root\">");
            sb.AppendLine("  <h1>{{Title}}</h1>");
            
            for (int i = 0; i < elementCount / 2; i++)
            {
                sb.AppendLine($"  <Data section=\"section{i}\">");
                sb.AppendLine($"    <p>Count: {{{{Count}}}} - Price: {{{{Price}}}}</p>");
                sb.AppendLine($"    @if(Count > 0)");
                sb.AppendLine($"    <span>Has items</span>");
                sb.AppendLine($"    @endif");
                sb.AppendLine("  </Data>");
            }
            
            sb.AppendLine("  @foreach(item in Items)");
            sb.AppendLine("    <li>{{item}}</li>");
            sb.AppendLine("  @endforeach");
            
            sb.AppendLine("</Element>");
            return sb.ToString();
        }

        private static List<string> GenerateItems(int count)
        {
            var items = new List<string>(count);
            for (int i = 0; i < count; i++)
            {
                items.Add($"Item {i}");
            }
            return items;
        }

        private class TestObject
        {
            public string Name { get; set; }
            public int Value { get; set; }
        }
    }

    /// <summary>
    /// Benchmark results container
    /// </summary>
    public class BenchmarkResults
    {
        // Tokenization
        public TimingResult TokenizeSmall { get; set; }
        public TimingResult TokenizeMedium { get; set; }
        public TimingResult TokenizeLarge { get; set; }

        // Parsing
        public TimingResult ParseSmall { get; set; }
        public TimingResult ParseMedium { get; set; }
        public TimingResult ParseLarge { get; set; }

        // Cold render (no cache)
        public TimingResult RenderColdSmall { get; set; }
        public TimingResult RenderColdMedium { get; set; }
        public TimingResult RenderColdLarge { get; set; }

        // Hot render (cached)
        public TimingResult RenderHotSmall { get; set; }
        public TimingResult RenderHotMedium { get; set; }
        public TimingResult RenderHotLarge { get; set; }

        // Property access
        public TimingResult PropertyAccessCold { get; set; }
        public TimingResult PropertyAccessHot { get; set; }

        // Cache stats
        public CacheStatistics CacheStats { get; set; }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine("╔══════════════════════════════════════════════════════════════════╗");
            sb.AppendLine("║           AST Template Parser - Performance Benchmark            ║");
            sb.AppendLine("╠══════════════════════════════════════════════════════════════════╣");
            
            sb.AppendLine("║ TOKENIZATION                                                     ║");
            sb.AppendLine($"║   Small  ({TokenizeSmall?.TemplateSize,5} chars): {TokenizeSmall?.AverageUs,8:F2} µs/op  ({TokenizeSmall?.OpsPerSecond,10:N0} ops/sec) ║");
            sb.AppendLine($"║   Medium ({TokenizeMedium?.TemplateSize,5} chars): {TokenizeMedium?.AverageUs,8:F2} µs/op  ({TokenizeMedium?.OpsPerSecond,10:N0} ops/sec) ║");
            sb.AppendLine($"║   Large  ({TokenizeLarge?.TemplateSize,5} chars): {TokenizeLarge?.AverageUs,8:F2} µs/op  ({TokenizeLarge?.OpsPerSecond,10:N0} ops/sec) ║");
            
            sb.AppendLine("╠══════════════════════════════════════════════════════════════════╣");
            sb.AppendLine("║ PARSING (tokens → AST)                                           ║");
            sb.AppendLine($"║   Small:  {ParseSmall?.AverageUs,8:F2} µs/op  ({ParseSmall?.OpsPerSecond,10:N0} ops/sec)           ║");
            sb.AppendLine($"║   Medium: {ParseMedium?.AverageUs,8:F2} µs/op  ({ParseMedium?.OpsPerSecond,10:N0} ops/sec)           ║");
            sb.AppendLine($"║   Large:  {ParseLarge?.AverageUs,8:F2} µs/op  ({ParseLarge?.OpsPerSecond,10:N0} ops/sec)           ║");
            
            sb.AppendLine("╠══════════════════════════════════════════════════════════════════╣");
            sb.AppendLine("║ RENDER (Cold Cache - Full Pipeline)                              ║");
            sb.AppendLine($"║   Small:  {RenderColdSmall?.AverageUs,8:F2} µs/op  ({RenderColdSmall?.OpsPerSecond,10:N0} ops/sec)           ║");
            sb.AppendLine($"║   Medium: {RenderColdMedium?.AverageUs,8:F2} µs/op  ({RenderColdMedium?.OpsPerSecond,10:N0} ops/sec)           ║");
            sb.AppendLine($"║   Large:  {RenderColdLarge?.AverageUs,8:F2} µs/op  ({RenderColdLarge?.OpsPerSecond,10:N0} ops/sec)           ║");
            
            sb.AppendLine("╠══════════════════════════════════════════════════════════════════╣");
            sb.AppendLine("║ RENDER (Hot Cache - Evaluation Only)                             ║");
            sb.AppendLine($"║   Small:  {RenderHotSmall?.AverageUs,8:F2} µs/op  ({RenderHotSmall?.OpsPerSecond,10:N0} ops/sec)           ║");
            sb.AppendLine($"║   Medium: {RenderHotMedium?.AverageUs,8:F2} µs/op  ({RenderHotMedium?.OpsPerSecond,10:N0} ops/sec)           ║");
            sb.AppendLine($"║   Large:  {RenderHotLarge?.AverageUs,8:F2} µs/op  ({RenderHotLarge?.OpsPerSecond,10:N0} ops/sec)           ║");
            
            sb.AppendLine("╠══════════════════════════════════════════════════════════════════╣");
            sb.AppendLine("║ PROPERTY ACCESS                                                  ║");
            sb.AppendLine($"║   Cold (reflection): {PropertyAccessCold?.AverageUs,8:F4} µs/access                     ║");
            sb.AppendLine($"║   Hot  (compiled):   {PropertyAccessHot?.AverageUs,8:F4} µs/access                     ║");
            
            sb.AppendLine("╠══════════════════════════════════════════════════════════════════╣");
            sb.AppendLine("║ CACHE STATUS                                                     ║");
            sb.AppendLine($"║   AST Cache Entries:      {CacheStats.AstCacheCount,5}                                  ║");
            sb.AppendLine($"║   Template Cache Entries: {CacheStats.TemplateCacheCount,5}                                  ║");
            sb.AppendLine("╚══════════════════════════════════════════════════════════════════╝");
            
            return sb.ToString();
        }
    }

    /// <summary>
    /// Individual timing result
    /// </summary>
    public class TimingResult
    {
        public double TotalMs { get; set; }
        public int Iterations { get; set; }
        public int TemplateSize { get; set; }

        public double AverageMs => TotalMs / Iterations;
        public double AverageUs => AverageMs * 1000;
        public double OpsPerSecond => Iterations / (TotalMs / 1000.0);
    }
}
