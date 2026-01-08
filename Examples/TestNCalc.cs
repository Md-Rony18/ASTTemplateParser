using System;
using System.Collections.Generic;
using NCalc;

namespace TestNCalc
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=== Direct NCalc Testing ===\n");
            
            // Test 1: Simple comparison
            Console.WriteLine("Test 1: Count > 10");
            try
            {
                var expr1 = new Expression("Count > 10");
                expr1.Parameters["Count"] = 15;
                var result1 = expr1.Evaluate();
                Console.WriteLine($"Count=15, Result: {result1}, Type: {result1?.GetType().Name}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
            
            // Test 2: Boolean variable
            Console.WriteLine("\nTest 2: Boolean variable");
            try
            {
                var expr2 = new Expression("IsActive");
                expr2.Parameters["IsActive"] = true;
                var result2 = expr2.Evaluate();
                Console.WriteLine($"IsActive=true, Result: {result2}, Type: {result2?.GetType().Name}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
            
            // Test 3: Integer as boolean
            Console.WriteLine("\nTest 3: Integer as condition");
            try
            {
                var expr3 = new Expression("Count");
                expr3.Parameters["Count"] = 15;
                var result3 = expr3.Evaluate();
                Console.WriteLine($"Count=15, Result: {result3}, Type: {result3?.GetType().Name}");
                Console.WriteLine($"Is Bool? {result3 is bool}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
            
            // Test 4: Collection as parameter
            Console.WriteLine("\nTest 4: Collection as condition");
            try
            {
                var orders = new List<object> { new { Id = 1 }, new { Id = 2 } };
                var expr4 = new Expression("Orders");
                expr4.Parameters["Orders"] = orders;
                var result4 = expr4.Evaluate();
                Console.WriteLine($"Orders, Result: {result4}, Type: {result4?.GetType().Name}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
            
            Console.WriteLine("\n=== Tests Complete ===");
        }
    }
}
