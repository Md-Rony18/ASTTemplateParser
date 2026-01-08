# AST Template Parser - Complete Documentation

A high-performance, HTML-like template parser for .NET with component system, layouts, and security features.

---

## 📑 Table of Contents

1. [Getting Started](#-getting-started)
2. [Variables & Interpolation](#-variables--interpolation)
3. [Conditionals (If/Else)](#-conditionals-ifelse)
4. [Loops (ForEach)](#-loops-foreach)
5. [Component System](#-component-system)
6. [Pre-Render Data Extraction](#-pre-render-data-extraction) *(NEW in v1.0.4)*
7. [Layout System](#-layout-system)
8. [Slots](#-slots)
9. [Security Configuration](#-security-configuration)
10. [API Reference](#-api-reference)
11. [Common Patterns](#-common-patterns)
12. [Troubleshooting](#-troubleshooting)

---

## 🚀 Getting Started

### Installation

**Via NuGet:**
```powershell
Install-Package ASTTemplateParser
```

**Via .NET CLI:**
```bash
dotnet add package ASTTemplateParser
```

### Your First Template

```csharp
using ASTTemplateParser;

// Step 1: Create engine
var engine = new TemplateEngine();

// Step 2: Set data
engine.SetVariable("Name", "Alice");
engine.SetVariable("Age", 25);

// Step 3: Define template
var template = @"
<Element template=""greeting"">
    <p>Hello, {{Name}}!</p>
    <p>You are {{Age}} years old.</p>
</Element>";

// Step 4: Render
string html = engine.Render(template);

// Output:
//     <p>Hello, Alice!</p>
//     <p>You are 25 years old.</p>
```

**What happened?**
- `{{Name}}` was replaced with "Alice"
- `{{Age}}` was replaced with 25
- The `<Element>` tag is a container and doesn't appear in output

---

## 📝 Variables & Interpolation

Variables let you insert dynamic data into your templates.

### Basic Variables

**In C#:**
```csharp
engine.SetVariable("UserName", "John");
engine.SetVariable("Balance", 1250.50);
engine.SetVariable("IsActive", true);
```

**In Template:**
```html
<p>Welcome, {{UserName}}!</p>
<p>Your balance: ${{Balance}}</p>
```

**Output:**
```html
<p>Welcome, John!</p>
<p>Your balance: $1250.5</p>
```

### Object Properties

You can access properties of objects using dot notation.

**In C#:**
```csharp
engine.SetVariable("User", new {
    Name = "Jane",
    Email = "jane@example.com",
    Profile = new {
        Bio = "Developer",
        Location = "NYC"
    }
});
```

**In Template:**
```html
<h1>{{User.Name}}</h1>
<p>Email: {{User.Email}}</p>
<p>Bio: {{User.Profile.Bio}}</p>
<p>Location: {{User.Profile.Location}}</p>
```

**Output:**
```html
<h1>Jane</h1>
<p>Email: jane@example.com</p>
<p>Bio: Developer</p>
<p>Location: NYC</p>
```

### Variable Types Supported

| C# Type | Example | Template Usage |
|---------|---------|----------------|
| string | `"Hello"` | `{{Message}}` |
| int | `42` | `{{Count}}` |
| double | `19.99` | `{{Price}}` |
| bool | `true` | Used in conditions |
| Anonymous | `new { Name = "X" }` | `{{Obj.Name}}` |
| Class | `new User()` | `{{User.Property}}` |
| List | `new List<object>()` | Used in ForEach |

---

## 🔀 Conditionals (If/Else)

Conditionals let you show or hide content based on conditions.

### Simple If

```html
<If condition="IsLoggedIn">
    <p>Welcome back!</p>
</If>
```

- If `IsLoggedIn` is `true` → Shows "Welcome back!"
- If `IsLoggedIn` is `false` → Shows nothing

### If-Else

```html
<If condition="HasItems">
    <p>You have items in your cart.</p>
<Else>
    <p>Your cart is empty.</p>
</If>
```

### If-ElseIf-Else Chain

```html
<If condition="Score >= 90">
    <span class="grade">A</span>
<ElseIf condition="Score >= 80">
    <span class="grade">B</span>
<ElseIf condition="Score >= 70">
    <span class="grade">C</span>
<Else>
    <span class="grade">F</span>
</If>
```

### Comparison Examples

```html
<!-- Equal to -->
<If condition="Status == 'active'">Active User</If>

<!-- Not equal to -->
<If condition="Count != 0">Has items</If>

<!-- Greater than -->
<If condition="Age > 18">Adult</If>

<!-- Less than -->
<If condition="Stock < 10">Low stock!</If>

<!-- Greater or equal -->
<If condition="Points >= 100">Gold member</If>

<!-- Less or equal -->
<If condition="Price <= 50">Affordable</If>
```

### Logical Operators

```html
<!-- AND: Both must be true -->
<If condition="IsLoggedIn && IsPremium">
    <span>⭐ Premium Member</span>
</If>

<!-- OR: At least one must be true -->
<If condition="IsAdmin || IsModerator">
    <a href="/dashboard">Dashboard</a>
</If>

<!-- Alternative syntax -->
<If condition="A and B">Both true</If>
<If condition="A or B">One is true</If>
```

### Nested Conditionals

```html
<If condition="IsLoggedIn">
    <div class="user-panel">
        Welcome, {{UserName}}!
        <If condition="IsAdmin">
            <a href="/admin">Admin Panel</a>
        </If>
    </div>
</If>
```

### Checking Object Properties

```html
<If condition="User.IsVerified">
    <span class="verified">✓ Verified</span>
</If>

<If condition="Order.Total > 100">
    <p>Free shipping!</p>
</If>
```

---

## 🔄 Loops (ForEach)

Loops let you repeat content for each item in a collection.

### Basic Loop

**In C#:**
```csharp
engine.SetVariable("Colors", new List<object> { "Red", "Green", "Blue" });
```

**In Template:**
```html
<ul>
    <ForEach var="color" in="Colors">
        <li>{{color}}</li>
    </ForEach>
</ul>
```

**Output:**
```html
<ul>
    <li>Red</li>
    <li>Green</li>
    <li>Blue</li>
</ul>
```

### Loop with Objects

**In C#:**
```csharp
engine.SetVariable("Products", new List<object>
{
    new { Name = "Laptop", Price = 999.99 },
    new { Name = "Mouse", Price = 29.99 },
    new { Name = "Keyboard", Price = 79.99 }
});
```

**In Template:**
```html
<table>
    <tr>
        <th>Product</th>
        <th>Price</th>
    </tr>
    <ForEach var="p" in="Products">
        <tr>
            <td>{{p.Name}}</td>
            <td>${{p.Price}}</td>
        </tr>
    </ForEach>
</table>
```

**Output:**
```html
<table>
    <tr><th>Product</th><th>Price</th></tr>
    <tr><td>Laptop</td><td>$999.99</td></tr>
    <tr><td>Mouse</td><td>$29.99</td></tr>
    <tr><td>Keyboard</td><td>$79.99</td></tr>
</table>
```

### Loop with Conditionals

```html
<ForEach var="item" in="Products">
    <div class="product">
        <h3>{{item.Name}}</h3>
        <p>${{item.Price}}</p>
        
        <If condition="item.IsOnSale">
            <span class="badge sale">SALE!</span>
        </If>
        
        <If condition="item.InStock">
            <button>Add to Cart</button>
        <Else>
            <button disabled>Out of Stock</button>
        </If>
    </div>
</ForEach>
```

### Nested Loops

```html
<ForEach var="category" in="Categories">
    <section>
        <h2>{{category.Name}}</h2>
        <ul>
            <ForEach var="product" in="category.Products">
                <li>{{product.Name}}</li>
            </ForEach>
        </ul>
    </section>
</ForEach>
```

### Empty Collection

If the collection is empty, nothing is rendered:

```csharp
engine.SetVariable("Items", new List<object>()); // Empty list
```

```html
<ForEach var="item" in="Items">
    <p>{{item}}</p>
</ForEach>
<!-- Output: (nothing) -->
```

You can show a message for empty collections:

```html
<If condition="Items">
    <ForEach var="item" in="Items">
        <p>{{item.Name}}</p>
    </ForEach>
<Else>
    <p>No items found.</p>
</If>
```

---

## 🧩 Component System

Components are reusable template files. Create once, use anywhere!

### Setting Up Components

**1. Create a components directory:**
```
your-project/
├── components/
│   ├── element/
│   │   ├── button.html
│   │   └── header.html
│   └── block/
│       └── card.html
```

**2. Configure in C#:**
```csharp
var engine = new TemplateEngine();
engine.SetComponentsDirectory("./components");
```

### Creating a Component

**File: `components/element/button.html`**
```html
<a href="{{href}}" class="btn btn-{{type}}">
    {{text}}
</a>
```

### Using a Component

```html
<Include component="element/button">
    <Param name="text" value="Click Me" />
    <Param name="type" value="primary" />
    <Param name="href" value="/action" />
</Include>
```

**Output:**
```html
<a href="/action" class="btn btn-primary">
    Click Me
</a>
```

### Component with Conditional Logic

**File: `components/block/alert.html`**
```html
<div class="alert alert-{{type}}">
    <If condition="title">
        <strong>{{title}}</strong>
    </If>
    <p>{{message}}</p>
</div>
```

**Usage:**
```html
<Include component="block/alert">
    <Param name="type" value="warning" />
    <Param name="title" value="Warning!" />
    <Param name="message" value="This action cannot be undone." />
</Include>
```

### Component with Optional Parameters

**File: `components/element/card.html`**
```html
<div class="card">
    <If condition="image">
        <img src="{{image}}" alt="{{title}}" />
    </If>
    <div class="card-body">
        <h3>{{title}}</h3>
        <p>{{content}}</p>
    </div>
</div>
```

**Usage without image:**
```html
<Include component="element/card">
    <Param name="title" value="My Card" />
    <Param name="content" value="Card body text" />
</Include>
```

### Dynamic Param Values (NEW in v1.0.2!)

You can use dynamic values in Param attributes, not just hard-coded strings.

**Method 1: Using `{{variable}}` syntax**
```html
<Include component="element/button">
    <Param name="text" value="{{ButtonText}}" />
    <Param name="type" value="{{ButtonType}}" />
    <Param name="href" value="{{ActionUrl}}" />
</Include>
```

**Method 2: Variable name directly (without braces)**
```html
<Include component="element/button">
    <Param name="text" value="ButtonText" />
</Include>
```
If `ButtonText` is a defined variable, its value will be used.

**Method 3: Mixed content**
```html
<Include component="element/button">
    <Param name="text" value="Welcome, {{UserName}}!" />
    <Param name="href" value="/profile/{{User.Id}}" />
</Include>
```

**Method 4: Nested object properties**
```html
<Include component="element/button">
    <Param name="text" value="{{User.Profile.DisplayName}}" />
    <Param name="href" value="User.ProfileUrl" />
</Include>
```

**C# Setup:**
```csharp
var engine = new TemplateEngine();
engine.SetComponentsDirectory("./components");

// Set variables for dynamic params
engine.SetVariable("ButtonText", "Submit Form");
engine.SetVariable("ButtonType", "primary");
engine.SetVariable("UserName", "Alice");
engine.SetVariable("User", new { 
    Id = 123, 
    Profile = new { DisplayName = "Alice Smith" },
    ProfileUrl = "/users/alice"
});

var result = engine.Render(template);
```

**Summary of Param value types:**

| Syntax | Example | Description |
|--------|---------|-------------|
| Hard-coded | `value="Click Me"` | Literal string |
| `{{var}}` | `value="{{ButtonText}}"` | Resolves variable |
| Variable name | `value="ButtonText"` | Resolves if variable exists |
| Mixed | `value="Hello {{Name}}!"` | Combines text and variables |
| Nested | `value="{{User.Name}}"` | Resolves nested properties |

---

## 🎯 Pre-Render Data Extraction

*(NEW in v1.0.4!)*

Extract Include `name` attributes before rendering to lookup cached data and set variables efficiently.

### The Problem

When your templates use cached element data, you need to know the cache key **before** rendering to fetch the data:

```html
<!-- Template with named Include for caching -->
<Include component="element/header" name="header_abc123_cached">
    <Param name="class" value="{{element.Class}}" />
    <Param name="text" value="{{element.Content}}" />
</Include>
```

The `name="header_abc123_cached"` is your cache key. You need to:
1. Parse the template
2. Extract the name
3. Fetch cached data using that name
4. Set variables
5. Render

### Solution 1: ExtractIncludeNames (Simple)

```csharp
var template = @"<Include component=""element/header"" name=""header_abc123_cached"">
                    <Param name=""text"" value=""{{data.Title}}"" />
                 </Include>";

// Step 1: Extract all Include names (AST is cached automatically!)
var includeInfos = TemplateEngine.ExtractIncludeNames(template);

// Step 2: Get cached data using the names
var engine = new TemplateEngine();
foreach (var info in includeInfos)
{
    Console.WriteLine($"Cache Key: {info.Name}");        // "header_abc123_cached"
    Console.WriteLine($"Component: {info.ComponentPath}"); // "element/header"
    
    // Fetch your cached data
    var cachedData = YourCacheService.Get(info.Name);
    engine.SetVariable("data", cachedData);
}

// Step 3: Render - uses cached AST, NO re-parsing!
var html = engine.Render(template);
```

**Key Point:** `ExtractIncludeNames()` caches the AST internally.
When you call `Render()` with the same template, it reuses the cached AST.
**Zero performance penalty!**

### Solution 2: PrepareTemplate (Most Efficient)

For maximum performance, use the prepare-then-render pattern:

```csharp
var engine = new TemplateEngine();
engine.SetComponentsDirectory("./components");

// Step 1: Prepare the template (parse once, validate, extract names)
var prepared = engine.PrepareTemplate(template);

// Step 2: Access extracted Include info
foreach (var info in prepared.IncludeInfos)
{
    // info.Name = cache key
    // info.ComponentPath = component path  
    // info.Parameters = Dictionary of param names and values
    
    var cachedElement = YourCache.Get(info.Name);
    engine.SetVariable("element", cachedElement);
}

// Step 3: Render directly from prepared AST (FASTEST!)
var html = engine.RenderPrepared(prepared);
```

### IncludeInfo Properties

| Property | Type | Description |
|----------|------|-------------|
| `Name` | `string` | Unique identifier/cache key from `name` attribute |
| `ComponentPath` | `string` | Path to component file |
| `Parameters` | `Dictionary<string, string>` | All Param name-value pairs |

### Performance Comparison

| Approach | Parse Count | Best For |
|----------|-------------|----------|
| `Render()` only | 1 (cached) | Simple templates without pre-fetch |
| `ExtractIncludeNames()` → `Render()` | 1 (cached) | Pre-fetch cache data lazily |
| `PrepareTemplate()` → `RenderPrepared()` | 1 | Maximum efficiency, multiple renders |

### Real-World Example: CMS Element Caching

```csharp
public string RenderPage(string pageTemplate)
{
    var engine = new TemplateEngine();
    engine.SetComponentsDirectory("./templates/components");
    
    // Prepare and extract Include names
    var prepared = engine.PrepareTemplate(pageTemplate);
    
    // Fetch all cached elements in one batch
    var cacheKeys = prepared.IncludeInfos
        .Where(i => !string.IsNullOrEmpty(i.Name))
        .Select(i => i.Name)
        .ToList();
    
    var cachedElements = _elementCache.GetMany(cacheKeys);
    
    // Set all elements as variables
    foreach (var info in prepared.IncludeInfos)
    {
        if (cachedElements.TryGetValue(info.Name, out var element))
        {
            // Use component path as variable prefix
            var varName = info.ComponentPath.Replace("/", "_");
            engine.SetVariable(varName, element);
        }
    }
    
    // Render with all data ready
    return engine.RenderPrepared(prepared);
}
```

---

## 📐 Layout System

Layouts let you define a master page structure with replaceable sections.

### Creating a Layout

**File: `layouts/main.html`**
```html
<!DOCTYPE html>
<html>
<head>
    <title>{{PageTitle}} - My Site</title>
    <RenderSection name="styles">
        <link rel="stylesheet" href="/css/main.css" />
    </RenderSection>
</head>
<body>
    <header>
        <RenderSection name="header" required="true" />
    </header>
    
    <main>
        <RenderBody />
    </main>
    
    <footer>
        <RenderSection name="footer">
            <p>© 2026 My Company</p>
        </RenderSection>
    </footer>
    
    <RenderSection name="scripts" />
</body>
</html>
```

### Using a Layout

**File: `pages/home.html`**
```html
<Layout name="main">
    <Section name="header">
        <h1>Welcome to My Site</h1>
        <nav>
            <a href="/">Home</a>
            <a href="/about">About</a>
        </nav>
    </Section>
    
    <!-- Content outside Section tags becomes the body -->
    <div class="content">
        <p>This is the main content of the page.</p>
    </div>
    
    <Section name="scripts">
        <script src="/js/home.js"></script>
    </Section>
</Layout>
```

### RenderSection Options

```html
<!-- Required: Error if not provided -->
<RenderSection name="header" required="true" />

<!-- Optional: Uses default content if not provided -->
<RenderSection name="sidebar">
    <aside>Default sidebar</aside>
</RenderSection>

<!-- Optional: Empty if not provided -->
<RenderSection name="extra" />
```

### Rendering Layouts in C#

```csharp
engine.SetLayoutsDirectory("./layouts");
engine.SetVariable("PageTitle", "Home");
string html = engine.RenderPage("./pages/home.html");
```

---

## 🎰 Slots

Slots allow components to receive content from their parent template.

### Default Slot

**Component: `components/element/panel.html`**
```html
<div class="panel">
    <Slot>
        Default panel content
    </Slot>
</div>
```

**Usage:**
```html
<Include component="element/panel">
    <p>Custom content here!</p>
</Include>
```

**Output:**
```html
<div class="panel">
    <p>Custom content here!</p>
</div>
```

### Named Slots

**Component: `components/block/modal.html`**
```html
<div class="modal">
    <div class="modal-header">
        <Slot name="header">Modal Title</Slot>
    </div>
    <div class="modal-body">
        <Slot>Modal body content</Slot>
    </div>
    <div class="modal-footer">
        <Slot name="footer">
            <button>Close</button>
        </Slot>
    </div>
</div>
```

**Usage:**
```html
<Include component="block/modal">
    <Slot name="header">
        <h2>Confirm Delete</h2>
    </Slot>
    
    <p>Are you sure you want to delete this item?</p>
    
    <Slot name="footer">
        <button class="btn-danger">Delete</button>
        <button class="btn-secondary">Cancel</button>
    </Slot>
</Include>
```

---

## 🔒 Security Configuration

The parser includes built-in security features.

### Default Security

By default, the parser provides:
- HTML encoding of all output (XSS protection)
- Maximum 1000 loop iterations
- Maximum 10 levels of component nesting
- Blocked access to sensitive properties

### Custom Security Settings

```csharp
var security = new SecurityConfig
{
    // Limits
    MaxLoopIterations = 500,       // Max iterations per loop
    MaxRecursionDepth = 5,         // Max component nesting
    MaxPropertyDepth = 10,         // Max nested property access (a.b.c.d...)
    MaxExpressionLength = 500,     // Max condition length
    
    // Output
    HtmlEncodeOutput = true,       // Encode HTML entities (XSS protection)
    
    // Property Access
    AllowMethodCalls = false,      // Block method invocation
    BlockedPropertyNames = new HashSet<string>
    {
        "Password",
        "Secret", 
        "Token",
        "ConnectionString",
        "ApiKey"
    }
};

var engine = new TemplateEngine(security);
```

### Pre-configured Security Levels

```csharp
// Default: Balanced security
var engine = new TemplateEngine();

// Strict: For user-generated templates
var engine = new TemplateEngine(SecurityConfig.Strict);
```

---

## 📚 API Reference

### TemplateEngine Class

```csharp
public class TemplateEngine
{
    // Constructor
    public TemplateEngine();
    public TemplateEngine(SecurityConfig security);
    
    // Set a variable
    public TemplateEngine SetVariable(string name, object value);
    public TemplateEngine SetVariables(IDictionary<string, object> variables);
    public TemplateEngine SetModel(object model);
    
    // Set directories
    public TemplateEngine SetComponentsDirectory(string path);
    public TemplateEngine SetLayoutsDirectory(string path);
    
    // Render methods
    public string Render(string template);
    public string RenderFile(string filePath);
    
    // Pre-Render Extraction (NEW in v1.0.4)
    public static List<IncludeInfo> ExtractIncludeNames(string template);
    public PreparedTemplate PrepareTemplate(string template);
    public string RenderPrepared(PreparedTemplate prepared);
    
    // Static utilities
    public static RootNode ParseTemplate(string template);
    public static void ClearCaches();
    public static CacheStatistics GetCacheStats();
}
```

### IncludeInfo Class (NEW in v1.0.4)

```csharp
public class IncludeInfo
{
    // Unique identifier/cache key from name attribute
    public string Name { get; set; }
    
    // Path to component file
    public string ComponentPath { get; set; }
    
    // All Param name-value pairs
    public Dictionary<string, string> Parameters { get; set; }
}
```

### PreparedTemplate Class (NEW in v1.0.4)

```csharp
public class PreparedTemplate
{
    // Cache key for this template
    public string CacheKey { get; set; }
    
    // List of all Include tags with their names
    public List<IncludeInfo> IncludeInfos { get; set; }
    
    // Internal: Parsed AST for rendering
    internal RootNode Ast { get; set; }
}
```

### SecurityConfig Class

```csharp
public class SecurityConfig
{
    // Limits
    public int MaxLoopIterations { get; set; }      // Default: 1000
    public int MaxRecursionDepth { get; set; }      // Default: 10
    public int MaxPropertyDepth { get; set; }       // Default: 10
    public int MaxExpressionLength { get; set; }    // Default: 500
    
    // Security Options
    public bool HtmlEncodeOutput { get; set; }      // Default: true
    public bool AllowMethodCalls { get; set; }      // Default: false
    
    // Blocked Properties
    public HashSet<string> BlockedPropertyNames { get; set; }
    
    // Pre-configured
    public static SecurityConfig Default { get; }
    public static SecurityConfig Strict { get; }
}
```

### Tokenizer Class (Advanced)

```csharp
public class Tokenizer
{
    public Tokenizer(string input);
    public List<Token> Tokenize();
}
```

### Parser Class (Advanced)

```csharp
public class Parser
{
    public Parser(List<Token> tokens);
    public RootNode Parse();
}
```

### Evaluator Class (Advanced)

```csharp
public class Evaluator
{
    public Evaluator(Dictionary<string, object> variables, SecurityConfig security);
    public string Evaluate(RootNode ast);
}
```

---

## 🎯 Common Patterns

### Navigation Menu

```csharp
engine.SetVariable("MenuItems", new List<object>
{
    new { Label = "Home", Url = "/", Active = true },
    new { Label = "Products", Url = "/products", Active = false },
    new { Label = "About", Url = "/about", Active = false }
});
```

```html
<nav>
    <ForEach var="item" in="MenuItems">
        <a href="{{item.Url}}" 
           class="{{If condition='item.Active'}}active{{/If}}">
            {{item.Label}}
        </a>
    </ForEach>
</nav>
```

### Product Grid with Sale Badge

```html
<div class="product-grid">
    <ForEach var="p" in="Products">
        <div class="product-card">
            <img src="{{p.Image}}" alt="{{p.Name}}" />
            <h3>{{p.Name}}</h3>
            
            <If condition="p.SalePrice">
                <p class="original-price">$<s>{{p.Price}}</s></p>
                <p class="sale-price">${{p.SalePrice}}</p>
                <span class="sale-badge">SALE!</span>
            <Else>
                <p class="price">${{p.Price}}</p>
            </If>
        </div>
    </ForEach>
</div>
```

### User Role-Based Content

```html
<If condition="User.Role == 'admin'">
    <div class="admin-panel">
        <h2>Admin Controls</h2>
        <button>Manage Users</button>
        <button>View Reports</button>
    </div>
</If>

<If condition="User.Role == 'admin' || User.Role == 'moderator'">
    <div class="mod-tools">
        <button>Review Content</button>
    </div>
</If>
```

### Order Summary Table

```html
<table class="order-summary">
    <thead>
        <tr>
            <th>Item</th>
            <th>Qty</th>
            <th>Price</th>
        </tr>
    </thead>
    <tbody>
        <ForEach var="item" in="Order.Items">
            <tr>
                <td>{{item.Name}}</td>
                <td>{{item.Quantity}}</td>
                <td>${{item.Total}}</td>
            </tr>
        </ForEach>
    </tbody>
    <tfoot>
        <tr>
            <td colspan="2">Subtotal:</td>
            <td>${{Order.Subtotal}}</td>
        </tr>
        <If condition="Order.Discount > 0">
            <tr class="discount">
                <td colspan="2">Discount:</td>
                <td>-${{Order.Discount}}</td>
            </tr>
        </If>
        <tr class="total">
            <td colspan="2"><strong>Total:</strong></td>
            <td><strong>${{Order.Total}}</strong></td>
        </tr>
    </tfoot>
</table>
```

---

## ❓ Troubleshooting

### Common Issues and Solutions

| Problem | Cause | Solution |
|---------|-------|----------|
| Variable shows `{{VarName}}` | Variable not set | Check `SetVariable()` call |
| Condition always false | Wrong variable name | Variable names are case-insensitive |
| Component not found | Wrong path | Path is relative to components directory |
| Infinite loop error | Too many iterations | Reduce collection size or increase limit |
| HTML shown as text | HtmlEncodeOutput on | Disable for trusted HTML content |
| Properties not accessible | BlockedPropertyNames | Remove from blocked list if safe |

### Debugging Tips

**1. Check if variable exists:**
```csharp
Console.WriteLine(engine.GetVariable("MyVar")); // Debug
```

**2. Test template parts separately:**
```csharp
// Test just the condition
var testTemplate = "<If condition=\"MyVar\">YES</If>";
Console.WriteLine(engine.Render(testTemplate));
```

**3. Inspect tokenizer output:**
```csharp
var tokenizer = new Tokenizer(template);
var tokens = tokenizer.Tokenize();
foreach (var t in tokens)
    Console.WriteLine($"{t.Type}: {t.Value}");
```

**4. Check parser output:**
```csharp
var parser = new Parser(tokens);
var ast = parser.Parse();
// Inspect AST structure
```

---

## 📋 Quick Reference Card

### Variables
```
{{VariableName}}
{{Object.Property}}
{{Deep.Nested.Path}}
```

### Conditionals
```html
<If condition="...">...</If>
<If condition="...">...<Else>...</If>
<If condition="...">...<ElseIf condition="...">...<Else>...</If>
```

### Operators
```
==  !=  >  <  >=  <=  &&  ||  and  or
```

### Loops
```html
<ForEach var="item" in="Collection">
    {{item}} or {{item.Property}}
</ForEach>
```

### Components
```html
<Include component="path/name" name="cache_key">
    <Param name="key" value="val" />
</Include>
```

### Pre-Render Extraction (NEW!)
```csharp
// Extract names before render
var infos = TemplateEngine.ExtractIncludeNames(template);
foreach (var i in infos)
    Console.WriteLine(i.Name);  // cache key

// Or use PrepareTemplate for maximum efficiency
var prepared = engine.PrepareTemplate(template);
engine.SetVariable("data", cache.Get(prepared.IncludeInfos[0].Name));
var html = engine.RenderPrepared(prepared);
```

### Container Tags
```html
<Element template="name">...</Element>
<Data section="name">...</Data>
<Nav section="name">...</Nav>
<Block name="name">...</Block>
```

---

## 🏷️ Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0.4 | Jan 2026 | Pre-Render Data Extraction: `ExtractIncludeNames()`, `PrepareTemplate()`, `RenderPrepared()` |
| 1.0.3 | Jan 2026 | Auto Cache Invalidation: Component files auto-reload on modification |
| 1.0.2 | Jan 2026 | Dynamic Param values with `{{variable}}` syntax |
| 1.0.1 | Jan 2026 | Documentation update, improved examples |
| 1.0.0 | Jan 2026 | Initial release with all core features |

---

*Last Updated: January 8, 2026*
