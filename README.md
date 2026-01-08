# AST Template Parser

[![NuGet](https://img.shields.io/nuget/v/ASTTemplateParser.svg)](https://www.nuget.org/packages/ASTTemplateParser/)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

A **high-performance**, **security-hardened** template parser for .NET with HTML-like syntax. Works on .NET Standard 2.0, .NET Framework 4.8, .NET 6.0, and .NET 8.0.

## ✨ Features

- 🚀 **High Performance** - 90,000+ renders/second with compiled property accessors
- 🔒 **Enterprise Security** - Built-in XSS, injection, and DoS protection
- 🧩 **Component System** - Reusable components with dynamic parameters
- 📐 **Layout System** - Master page layouts with sections
- 📝 **HTML-like Syntax** - No special directives needed, pure HTML tags
- 🔄 **Auto Cache** - Component files automatically reload when modified
- 🎯 **Pre-Render Extraction** - Extract Include names before rendering for cache lookup (NEW!)
- 🌐 **Cross-Platform** - Works on Windows, Linux, and macOS

---

## 📦 Installation

**NuGet Package Manager:**
```powershell
Install-Package ASTTemplateParser
```

**.NET CLI:**
```bash
dotnet add package ASTTemplateParser
```

---

## 🚀 Quick Start (5 Minutes)

### Step 1: Create the Engine

```csharp
using ASTTemplateParser;

var engine = new TemplateEngine();
```

### Step 2: Set Your Data

```csharp
// Simple variables
engine.SetVariable("UserName", "John");
engine.SetVariable("IsLoggedIn", true);

// Objects with properties
engine.SetVariable("User", new {
    Name = "John Doe",
    Email = "john@example.com",
    IsAdmin = false
});

// Lists for loops
engine.SetVariable("Products", new List<object> {
    new { Name = "Laptop", Price = 999.99 },
    new { Name = "Mouse", Price = 29.99 }
});
```

### Step 3: Write Your Template

```html
<Element template="page">
    <h1>Welcome, {{UserName}}!</h1>
    
    <If condition="IsLoggedIn">
        <p>Hello {{User.Name}}, your email is {{User.Email}}</p>
    <Else>
        <p>Please log in to continue.</p>
    </If>
    
    <h2>Our Products</h2>
    <ul>
        <ForEach var="product" in="Products">
            <li>{{product.Name}} - ${{product.Price}}</li>
        </ForEach>
    </ul>
</Element>
```

### Step 4: Render!

```csharp
string html = engine.Render(template);
Console.WriteLine(html);
```

---

## 📖 Template Syntax Guide

### 1. Variables (Interpolation)

Use `{{variableName}}` to display values:

```html
<!-- Simple variable -->
<p>Hello, {{UserName}}!</p>

<!-- Nested object property -->
<p>Email: {{User.Email}}</p>

<!-- Deeply nested -->
<p>City: {{Order.Customer.Address.City}}</p>
```

### 2. Conditionals (If/Else)

Show or hide content based on conditions:

```html
<!-- Simple if -->
<If condition="IsLoggedIn">
    <p>Welcome back!</p>
</If>

<!-- If with else -->
<If condition="IsAdmin">
    <button>Delete</button>
<Else>
    <button disabled>Delete (Admin only)</button>
</If>

<!-- If with else-if chain -->
<If condition="Role == 'admin'">
    <span>👑 Admin</span>
<ElseIf condition="Role == 'moderator'">
    <span>🛡️ Moderator</span>
<Else>
    <span>👤 User</span>
</If>
```

**Supported Operators:**

| Operator | Example | Description |
|----------|---------|-------------|
| `==` | `Status == 'active'` | Equal to |
| `!=` | `Count != 0` | Not equal to |
| `>` | `Age > 18` | Greater than |
| `<` | `Stock < 10` | Less than |
| `>=` | `Score >= 50` | Greater or equal |
| `<=` | `Price <= 100` | Less or equal |
| `&&` or `and` | `A && B` | Both must be true |
| `||` or `or` | `A || B` | At least one true |

### 3. Loops (ForEach)

Repeat content for each item in a collection:

```html
<!-- Basic loop -->
<ul>
    <ForEach var="item" in="Items">
        <li>{{item}}</li>
    </ForEach>
</ul>

<!-- Loop with object properties -->
<table>
    <ForEach var="product" in="Products">
        <tr>
            <td>{{product.Name}}</td>
            <td>${{product.Price}}</td>
            <If condition="product.IsOnSale">
                <td class="sale">SALE!</td>
            </If>
        </tr>
    </ForEach>
</table>

<!-- Nested loops -->
<ForEach var="category" in="Categories">
    <h3>{{category.Name}}</h3>
    <ForEach var="item" in="category.Items">
        <p>{{item.Name}}</p>
    </ForEach>
</ForEach>
```

### 4. Components (Reusable Templates)

Create reusable UI pieces:

**Create a component file** (`components/element/button.html`):
```html
<a href="{{href}}" class="btn btn-{{type}}">
    {{text}}
</a>
```

**Use the component with static values:**
```html
<Include component="element/button">
    <Param name="text" value="Click Me" />
    <Param name="type" value="primary" />
    <Param name="href" value="/action" />
</Include>
```

**Use with dynamic values (NEW in v1.0.2!):**
```html
<!-- Using {{variable}} syntax -->
<Include component="element/button">
    <Param name="text" value="{{ButtonText}}" />
    <Param name="type" value="{{ButtonType}}" />
    <Param name="href" value="/user/{{User.Id}}" />
</Include>

<!-- Using variable name directly -->
<Include component="element/button">
    <Param name="text" value="ButtonText" />
</Include>

<!-- Mixed content -->
<Include component="element/button">
    <Param name="text" value="Hello {{UserName}}!" />
</Include>
```

**Setup components directory in C#:**
```csharp
engine.SetComponentsDirectory("./components");
engine.SetVariable("ButtonText", "Submit");
engine.SetVariable("UserName", "Alice");
```

### 5. Pre-Render Data Extraction (NEW in v1.0.4!)

Use `name` attribute on Include tags to identify components for cache lookup **before** rendering:

```html
<!-- Template with named Include for caching -->
<Include component="element/header" name="header_abc123_cached">
    <Param name="class" value="{{element.Class}}" />
    <Param name="text" value="{{element.Content}}" />
</Include>
```

**Extract Include names before rendering:**
```csharp
var template = @"<Include component=""element/header"" name=""header_abc123_cached"">
                    <Param name=""text"" value=""{{data.Title}}"" />
                 </Include>";

// Option 1: Simple extraction (AST is cached for later Render())
var includeInfos = TemplateEngine.ExtractIncludeNames(template);

foreach (var info in includeInfos)
{
    // info.Name = "header_abc123_cached" (your cache key!)
    // info.ComponentPath = "element/header"
    // info.Parameters = {"text": "{{data.Title}}"}
    
    var cachedData = YourCacheService.Get(info.Name);
    engine.SetVariable("data", cachedData);
}

// Render uses cached AST - NO performance penalty!
var html = engine.Render(template);
```

**Most efficient approach with PrepareTemplate:**
```csharp
var engine = new TemplateEngine();

// Step 1: Prepare once - parse, validate, cache, extract names
var prepared = engine.PrepareTemplate(template);

// Step 2: Use extracted names to fetch cached data
foreach (var info in prepared.IncludeInfos)
{
    var cachedElement = YourCache.Get(info.Name);
    engine.SetVariable("element", cachedElement);
}

// Step 3: Render directly from prepared AST (FASTEST!)
var html = engine.RenderPrepared(prepared);
```

### 6. Container Tags

| Tag | Purpose | Example |
|-----|---------|---------|
| `<Element>` | Main content wrapper | `<Element template="page">...</Element>` |
| `<Data>` | Data section | `<Data section="meta">...</Data>` |
| `<Nav>` | Navigation section | `<Nav section="main">...</Nav>` |
| `<Block>` | Named block | `<Block name="footer">...</Block>` |

---

## 🔒 Security Features

The parser includes built-in protection against common attacks:

### Default Protection (Automatic)
- ✅ **XSS Prevention** - All output is HTML-encoded
- ✅ **Loop Limits** - Maximum 1000 iterations (prevents DoS)
- ✅ **Recursion Limits** - Maximum 10 component depth
- ✅ **Path Validation** - Prevents directory traversal attacks

### Custom Security Configuration

```csharp
var security = new SecurityConfig
{
    MaxLoopIterations = 500,        // Limit loop iterations
    MaxRecursionDepth = 5,          // Limit component nesting
    MaxPropertyDepth = 10,          // Limit property chain depth
    HtmlEncodeOutput = true,        // Encode output (XSS protection)
    AllowMethodCalls = false,       // Block method invocation
    BlockedPropertyNames = new HashSet<string>
    {
        "Password", "Secret", "Token", "ConnectionString"
    }
};

var engine = new TemplateEngine(security);
```

---

## 📊 Performance

| Template Size | Operations/Second | Memory |
|--------------|-------------------|--------|
| Small (1KB) | ~7,000 ops/sec | Low |
| Medium (4KB) | ~1,600 ops/sec | Medium |
| Large (8KB) | ~800 ops/sec | Higher |

---

## 💡 Complete Example

```csharp
using ASTTemplateParser;
using System;
using System.Collections.Generic;

class Program
{
    static void Main()
    {
        // 1. Create engine
        var engine = new TemplateEngine();
        
        // 2. Set components directory (optional)
        engine.SetComponentsDirectory("./components");
        
        // 3. Set your data
        engine.SetVariable("PageTitle", "My Store");
        engine.SetVariable("User", new { Name = "Alice", IsVIP = true });
        engine.SetVariable("Products", new List<object>
        {
            new { Name = "Laptop", Price = 999, InStock = true },
            new { Name = "Tablet", Price = 499, InStock = false },
            new { Name = "Phone", Price = 699, InStock = true }
        });
        
        // 4. Define template
        var template = @"
<Element template=""store"">
    <h1>{{PageTitle}}</h1>
    
    <If condition=""User.IsVIP"">
        <div class=""vip-badge"">⭐ VIP Member: {{User.Name}}</div>
    </If>
    
    <div class=""products"">
        <ForEach var=""p"" in=""Products"">
            <div class=""product"">
                <h3>{{p.Name}}</h3>
                <p class=""price"">${{p.Price}}</p>
                <If condition=""p.InStock"">
                    <button>Add to Cart</button>
                <Else>
                    <span class=""out-of-stock"">Out of Stock</span>
                </If>
            </div>
        </ForEach>
    </div>
</Element>";
        
        // 5. Render and output
        string html = engine.Render(template);
        Console.WriteLine(html);
    }
}
```

---

## 🎯 Target Platforms

| Platform | Version | Status |
|----------|---------|--------|
| .NET Standard | 2.0 | ✅ Supported |
| .NET Framework | 4.8 | ✅ Supported |
| .NET | 6.0 | ✅ Supported |
| .NET | 8.0 | ✅ Supported |

---

## 📄 License

MIT License - Free for personal and commercial use.

---

## 📚 Documentation

For complete documentation including Layout System, Advanced Components, and Slots, see [DOCUMENTATION.md](DOCUMENTATION.md).

---

## ❓ FAQ

**Q: How is this different from Razor?**
A: This uses pure HTML-like syntax (`<If>`, `<ForEach>`) instead of `@` directives. It's faster and has no runtime compilation.

**Q: Can I use this with ASP.NET Core?**
A: Yes! It works with any .NET platform that supports .NET Standard 2.0 or higher.

**Q: Is it safe for user-generated templates?**
A: With proper `SecurityConfig`, yes. Enable strict mode for user templates.

---

*Version 1.0.4 - January 2026*

