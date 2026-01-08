using System;
using System.Collections.Generic;
using ASTTemplateParser;

namespace TestCondition
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=== Testing Condition Evaluation ===\n");
            
            // Test 1: Simple boolean condition
            Console.WriteLine("Test 1: Simple Boolean");
            var engine1 = new TemplateEngine();
            engine1.SetVariable("IsActive", true);
            var result1 = engine1.Render("<If condition=\"IsActive\"><p>Active</p></If><Else><p>Inactive</p></Else>");
            Console.WriteLine($"Result: {result1}");
            
            // Test 2: Comparison condition
            Console.WriteLine("\nTest 2: Comparison (Count > 10)");
            var engine2 = new TemplateEngine();
            engine2.SetVariable("Count", 15);
            var template2 = @"
<If condition=""Count > 10"">
    <p>More than 10</p>
<ElseIf condition=""Count > 0"">
    <p>Between 1 and 10</p>
<Else>
    <p>Zero or less</p>
</If>";
            var result2 = engine2.Render(template2);
            Console.WriteLine($"Count = 15, Result: {result2}");
            
            // Test 3: Collection truthiness
            Console.WriteLine("\nTest 3: Collection Check");
            var engine3 = new TemplateEngine();
            var orders = new List<object>
            {
                new { Id = "ORD-001", Customer = "John" },
                new { Id = "ORD-002", Customer = "Jane" }
            };
            engine3.SetVariable("Orders", orders);
            var template3 = @"
<If condition=""Orders"">
    <p>Has orders: Count = {{Orders.Count}}</p>
<Else>
    <p>No orders</p>
</If>";
            var result3 = engine3.Render(template3);
            Console.WriteLine($"Result: {result3}");
            
            // Test 4: Using Count property
            Console.WriteLine("\nTest 4: Using Orders.Count > 0");
            var engine4 = new TemplateEngine();
            engine4.SetVariable("Orders", orders);
            engine4.SetVariable("OrderCount", orders.Count);
            var template4 = @"
<If condition=""OrderCount > 0"">
    <p>Has {{OrderCount}} orders</p>
<Else>
    <p>No orders</p>
</If>";
            var result4 = engine4.Render(template4);
            Console.WriteLine($"Result: {result4}");
            
            Console.WriteLine("\n=== Tests Complete ===");
        }
    }
}
