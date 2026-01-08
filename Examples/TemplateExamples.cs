using System;
using System.Collections.Generic;
using ASTTemplateParser;

namespace Examples
{
    /// <summary>
    /// Example usage of AST Template Parser
    /// Uses HTML-like syntax for all control structures
    /// </summary>
    public class TemplateExamples
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("=== AST Template Parser Examples ===");
            Console.WriteLine("(Using HTML-like syntax - no @ directives)\n");

            // Example 1: Basic variable interpolation
            BasicExample();

            // Example 2: Conditionals
            ConditionalsExample();

            // Example 3: Loops
            LoopsExample();

            // Example 4: Nested objects
            NestedObjectsExample();

            // Example 5: Complex template
            ComplexExample();

            // Example 6: Component System
            ComponentExample();

            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }

        /// <summary>
        /// Example 1: Basic variable interpolation
        /// </summary>
        static void BasicExample()
        {
            Console.WriteLine("--- Example 1: Basic Interpolation ---");

            var template = @"
<Element template=""welcome"">
    <h1>Welcome, {{UserName}}!</h1>
    <p>Your email: {{Email}}</p>
    <p>Account balance: ${{Balance}}</p>
</Element>";

            var engine = new TemplateEngine();
            engine.SetVariable("UserName", "John Doe");
            engine.SetVariable("Email", "john@example.com");
            engine.SetVariable("Balance", 1250.50);

            var result = engine.Render(template);
            Console.WriteLine(result);
            Console.WriteLine();
        }

        /// <summary>
        /// Example 2: Conditionals with HTML-like syntax
        /// </summary>
        static void ConditionalsExample()
        {
            Console.WriteLine("--- Example 2: Conditionals (HTML-like syntax) ---");

            var template = @"
<Element template=""user-status"">
    <If condition=""IsLoggedIn"">
        <p>Welcome back, {{UserName}}!</p>
        <If condition=""IsAdmin"">
            <a href=""/admin"">Admin Panel</a>
        </If>
    <Else>
        <p>Please log in to continue.</p>
        <a href=""/login"">Login</a>
    </If>
    
    <If condition=""NotificationCount > 10"">
        <span class=""badge urgent"">{{NotificationCount}} notifications!</span>
    <ElseIf condition=""NotificationCount > 0"">
        <span class=""badge"">{{NotificationCount}} notifications</span>
    <Else>
        <span>No notifications</span>
    </If>
</Element>";

            var engine = new TemplateEngine();
            engine.SetVariable("IsLoggedIn", true);
            engine.SetVariable("UserName", "Alice");
            engine.SetVariable("IsAdmin", true);
            engine.SetVariable("NotificationCount", 15);

            var result = engine.Render(template);
            Console.WriteLine(result);
            Console.WriteLine();
        }

        /// <summary>
        /// Example 3: Loops with ForEach tag
        /// </summary>
        static void LoopsExample()
        {
            Console.WriteLine("--- Example 3: Loops (HTML-like syntax) ---");

            var template = @"
<Element template=""product-list"">
    <h2>Products</h2>
    <ul>
        <ForEach var=""product"" in=""Products"">
            <li>
                <strong>{{product.Name}}</strong> - ${{product.Price}}
                <If condition=""product.IsOnSale"">
                    <span class=""sale"">ON SALE!</span>
                </If>
            </li>
        </ForEach>
    </ul>
    
    <p>Total products: {{ProductCount}}</p>
</Element>";

            var products = new List<object>
            {
                new { Name = "Laptop", Price = 999.99, IsOnSale = false },
                new { Name = "Mouse", Price = 29.99, IsOnSale = true },
                new { Name = "Keyboard", Price = 79.99, IsOnSale = false },
                new { Name = "Monitor", Price = 299.99, IsOnSale = true }
            };

            var engine = new TemplateEngine();
            engine.SetVariable("Products", products);
            engine.SetVariable("ProductCount", products.Count);

            var result = engine.Render(template);
            Console.WriteLine(result);
            Console.WriteLine();
        }

        /// <summary>
        /// Example 4: Nested object properties
        /// </summary>
        static void NestedObjectsExample()
        {
            Console.WriteLine("--- Example 4: Nested Objects ---");

            var template = @"
<Element template=""order"">
    <div class=""order"">
        <h2>Order #{{Order.Id}}</h2>
        <p>Customer: {{Order.Customer.Name}}</p>
        <p>Email: {{Order.Customer.Email}}</p>
        
        <h3>Shipping Address:</h3>
        <p>{{Order.ShippingAddress.Street}}</p>
        <p>{{Order.ShippingAddress.City}}, {{Order.ShippingAddress.State}} {{Order.ShippingAddress.ZipCode}}</p>
        
        <p>Total: ${{Order.Total}}</p>
    </div>
</Element>";

            var order = new
            {
                Id = "ORD-2024-001",
                Customer = new
                {
                    Name = "Jane Smith",
                    Email = "jane@example.com"
                },
                ShippingAddress = new
                {
                    Street = "123 Main St",
                    City = "New York",
                    State = "NY",
                    ZipCode = "10001"
                },
                Total = 549.97
            };

            var engine = new TemplateEngine();
            engine.SetVariable("Order", order);

            var result = engine.Render(template);
            Console.WriteLine(result);
            Console.WriteLine();
        }

        /// <summary>
        /// Example 5: Complex template with multiple features
        /// </summary>
        static void ComplexExample()
        {
            Console.WriteLine("--- Example 5: Complex Template ---");

            var template = @"
<Element template=""dashboard"">
    <header>
        <h1>{{CompanyName}} Dashboard</h1>
        <If condition=""User.IsLoggedIn"">
            <span>Welcome, {{User.Name}}!</span>
        </If>
    </header>
    
    <Nav section=""main-nav"">
        <ul>
            <ForEach var=""item"" in=""MenuItems"">
                <li>
                    <a href=""{{item.Url}}"">{{item.Label}}</a>
                </li>
            </ForEach>
        </ul>
    </Nav>
    
    <main>
        <Data section=""stats"">
            <div class=""stats-grid"">
                <div class=""stat"">
                    <h3>Total Users</h3>
                    <p>{{Stats.TotalUsers}}</p>
                </div>
                <div class=""stat"">
                    <h3>Revenue</h3>
                    <p>${{Stats.Revenue}}</p>
                </div>
            </div>
        </Data>
        
        <Data section=""recent-orders"">
            <h2>Recent Orders</h2>
            <If condition=""Orders"">
                <table>
                    <tr>
                        <th>Order ID</th>
                        <th>Customer</th>
                        <th>Amount</th>
                        <th>Status</th>
                    </tr>
                    <ForEach var=""order"" in=""Orders"">
                        <tr>
                            <td>{{order.Id}}</td>
                            <td>{{order.Customer}}</td>
                            <td>${{order.Amount}}</td>
                            <td>
                                <If condition=""order.Status == 'completed'"">
                                    <span class=""green"">Completed</span>
                                <ElseIf condition=""order.Status == 'pending'"">
                                    <span class=""yellow"">Pending</span>
                                <Else>
                                    <span class=""red"">{{order.Status}}</span>
                                </If>
                            </td>
                        </tr>
                    </ForEach>
                </table>
            <Else>
                <p>No recent orders.</p>
            </If>
        </Data>
    </main>
    
    <Block name=""footer"">
        <footer>
            <p>© {{Year}} {{CompanyName}}. All rights reserved.</p>
        </footer>
    </Block>
</Element>";

            var engine = new TemplateEngine();
            
            // Set variables
            engine.SetVariable("CompanyName", "TechCorp");
            engine.SetVariable("Year", DateTime.Now.Year);
            
            engine.SetVariable("User", new { IsLoggedIn = true, Name = "Admin" });
            
            engine.SetVariable("MenuItems", new List<object>
            {
                new { Label = "Home", Url = "/" },
                new { Label = "Products", Url = "/products" },
                new { Label = "Orders", Url = "/orders" },
                new { Label = "Settings", Url = "/settings" }
            });
            
            engine.SetVariable("Stats", new { TotalUsers = 1250, Revenue = 45680.00 });
            
            engine.SetVariable("Orders", new List<object>
            {
                new { Id = "ORD-001", Customer = "John Doe", Amount = 299.99, Status = "completed" },
                new { Id = "ORD-002", Customer = "Jane Smith", Amount = 149.50, Status = "pending" },
                new { Id = "ORD-003", Customer = "Bob Wilson", Amount = 599.00, Status = "completed" }
            });

            var result = engine.Render(template);
            Console.WriteLine(result);
            Console.WriteLine();
        }

        /// <summary>
        /// Example 6: Component System with Include and Params
        /// </summary>
        static void ComponentExample()
        {
            Console.WriteLine("--- Example 6: Component System ---");

            // This template uses Include to load components
            var template = @"
<Element template=""with-components"">
    <h1>Component System Demo</h1>
    
    <!-- Include button components with parameters -->
    <div class=""buttons"">
        <Include component=""element/button"">
            <Param name=""text"" value=""Primary Action"" />
            <Param name=""type"" value=""primary"" />
            <Param name=""href"" value=""/action"" />
        </Include>
        
        <Include component=""element/button"">
            <Param name=""text"" value=""Secondary"" />
            <Param name=""type"" value=""secondary"" />
            <Param name=""href"" value=""/cancel"" />
        </Include>
    </div>
    
    <!-- Include header component -->
    <Include component=""element/header"">
        <Param name=""title"" value=""Welcome Page"" />
        <Param name=""subtitle"" value=""Using reusable components"" />
    </Include>
    
    <!-- Include card component -->
    <Include component=""block/card"">
        <Param name=""title"" value=""Featured Content"" />
        <Param name=""content"" value=""This is the card body content"" />
    </Include>
</Element>";

            // Get the examples directory (where components folder is)
            var examplesDir = System.IO.Path.GetDirectoryName(
                System.Reflection.Assembly.GetExecutingAssembly().Location);
            var componentsDir = System.IO.Path.Combine(examplesDir, "components");
            
            // Check if components directory exists (for demo purposes)
            if (!System.IO.Directory.Exists(componentsDir))
            {
                Console.WriteLine("Note: Components directory not found at: " + componentsDir);
                Console.WriteLine("To use components, copy the 'components' folder to the output directory.");
                Console.WriteLine();
                
                // Demonstrate inline component-like structure instead
                var simpleTemplate = @"
<Element template=""simple"">
    <h1>Component Syntax Demo</h1>
    <p>The <Include component=""...""> tag allows embedding reusable templates.</p>
    <p>Components can receive parameters via <Param name=""..."" value=""..."" /></p>
    <p>Components can define slots using <Slot>...</Slot> for content injection.</p>
</Element>";
                
                var engine = new TemplateEngine();
                var result = engine.Render(simpleTemplate);
                Console.WriteLine(result);
            }
            else
            {
                // Full component example
                var engine = new TemplateEngine();
                engine.SetComponentsDirectory(componentsDir);
                engine.SetVariable("PageTitle", "Welcome");
                
                var result = engine.Render(template);
                Console.WriteLine(result);
            }
            
            Console.WriteLine();
        }
    }
}
