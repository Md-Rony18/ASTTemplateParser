# AST Template Parser

[![NuGet](https://img.shields.io/nuget/v/ASTTemplateParser.svg)](https://www.nuget.org/packages/ASTTemplateParser/)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-Standard%202.0%20%7C%204.8%20%7C%206.0%20%7C%208.0-purple.svg)](https://dotnet.microsoft.com/)

A **blazing-fast**, **security-hardened** template engine for .NET with HTML-like syntax and **2000x faster cached rendering**.

---

## ⚡ Performance at a Glance

| Scenario | Speed | Comparison |
|----------|-------|------------|
| **Cached Render** | ~0.001ms | 🔥 **2000x faster** |
| **Data-Aware Cache** | ~0.01ms | 🚀 **200x faster** |
| **Normal Render** | ~2ms | Baseline |

---

## ✨ Features

### Core
- 🚀 **High Performance** - 1,000,000+ cached renders/second
- 🔒 **Enterprise Security** - XSS protection, loop limits, property blocking
- 🧩 **Component System** - `<Element>`, `<Block>`, `<Data>`, `<Nav>` components
- 📐 **Layout System** - Master layouts with sections and slots

### Caching (NEW in v2.0.5)
- ⚡ **Render Caching** - Cache rendered output for instant response
- 🔄 **Auto File Invalidation** - Cache updates when template file changes
- 📊 **Data-Aware Caching** - Auto-invalidate when variables change
- ⏰ **Time-Based Expiration** - Optional cache TTL

### Template Features
- 🎯 **Indexers** - `{{ items[0] }}`, `{{ dict["key"] }}`
- 🧪 **Filters** - `{{ Name | uppercase }}`, `{{ Price | currency }}`
- 🌐 **Global Variables** - Set once, use everywhere
- 🔁 **Fragments** - `<Define>` and `<Render>` for recursion

---

## 📦 Installation

```powershell
# NuGet Package Manager
Install-Package ASTTemplateParser

# .NET CLI
dotnet add package ASTTemplateParser
```

---

## 🚀 Quick Start

```csharp
using ASTTemplateParser;

var engine = new TemplateEngine();
engine.SetPagesDirectory("./pages");

// Set data
engine.SetVariable("User", new { Name = "Alice", Role = "Admin" });

// ⚡ Cached render - 2000x faster on repeat calls!
string html = engine.RenderCachedFile("dashboard.html", "dashboard", includeDataHash: true);
```

---

## ⚡ Caching Guide (NEW!)

### Static Pages (Fastest)
```csharp
// Cache indefinitely until file changes
string about = engine.RenderCachedFile("about.html", "about");
string faq = engine.RenderCachedFile("faq.html", "faq");
```

### User-Specific Pages (Smart Cache)
```csharp
// Auto-invalidate when variables change
engine.SetVariable("User", currentUser);
engine.SetVariable("Cart", cartItems);

string dashboard = engine.RenderCachedFile(
    "dashboard.html", 
    "dashboard", 
    includeDataHash: true  // ⬅️ Magic! Data changes = new render
);
```

### Time-Based Expiration
```csharp
// Cache for 5 minutes
string news = engine.RenderCachedFile(
    "news.html", 
    "news", 
    expiration: TimeSpan.FromMinutes(5)
);
```

### Cache Management
```csharp
// Invalidate specific cache
TemplateEngine.InvalidateCache("dashboard");

// Invalidate by prefix (e.g., all user caches)
TemplateEngine.InvalidateCacheByPrefix("user-");

// Clear all cache
TemplateEngine.ClearRenderCache();

// Get stats
var stats = TemplateEngine.GetRenderCacheStats();
Console.WriteLine($"Cached pages: {stats["TotalEntries"]}");
```

---

## 📖 Template Syntax

### Variables & Indexers
```html
{{Name}}                      <!-- Simple -->
{{User.Address.City}}         <!-- Nested -->
{{Items[0].Title}}            <!-- Array -->
{{Config["apiKey"]}}          <!-- Dictionary -->
```

### Filters
```html
{{ Name | uppercase }}                    <!-- ALICE -->
{{ Price | currency:"en-US" }}            <!-- $1,250.00 -->
{{ Created | date:"dd MMM yyyy" }}        <!-- 09 Feb 2026 -->
{{ Description | truncate:100 }}          <!-- Custom filter -->
```

### Conditionals
```html
<If condition="IsLoggedIn">
    <p>Welcome, {{User.Name}}!</p>
<ElseIf condition="Role == 'guest'">
    <p>Hello, Guest!</p>
<Else>
    <p>Please log in</p>
</If>
```

### Loops
```html
<ForEach var="product" in="Products">
    <div class="card">
        <h3>{{product.Name}}</h3>
        <p>{{product.Price | currency}}</p>
    </div>
</ForEach>
```

### Components
```html
<!-- Auto-resolves to components/element/button.html -->
<Element component="button">
    <Param name="text" value="Click Me" />
    <Param name="type" value="primary" />
</Element>

<!-- Auto-resolves to components/block/hero.html -->
<Block component="hero">
    <Param name="title" value="{{PageTitle}}" />
</Block>
```

---

## 🔒 Security

```csharp
var security = new SecurityConfig {
    MaxLoopIterations = 500,        // DoS protection
    MaxRecursionDepth = 5,          // Stack protection
    HtmlEncodeOutput = true,        // XSS protection
    BlockedPropertyNames = new HashSet<string> { "Password", "Secret" }
};

var engine = new TemplateEngine(security);
```

---

## 📊 Performance Benchmarks

| Operation | Speed | Notes |
|-----------|-------|-------|
| Cache Hit (Static) | **~0.001ms** | 1M+ ops/sec |
| Cache Hit (Data Hash) | **~0.01ms** | 90K+ ops/sec |
| Normal Render | ~2ms | 500 ops/sec |
| Property Access | ~0.00008ms | 12M+ ops/sec |
| File Timestamp Check | ~0.001ms | Auto-invalidation |

*Tested on .NET 8.0 / Intel i7 / Windows 11*

---

## 🌐 Global Variables

```csharp
// Set once at startup
TemplateEngine.SetGlobalVariable("SiteName", "My Website");
TemplateEngine.SetGlobalVariable("Year", DateTime.Now.Year);

// Available in ALL templates automatically!
// No need to call SetVariable() every time
```

---

## 🔧 API Reference

### Rendering Methods
| Method | Description |
|--------|-------------|
| `Render()` | Normal template rendering |
| `RenderFile()` | Render from file |
| `RenderCached()` | Cached string template |
| `RenderCachedFile()` | ⭐ **Recommended** - Cached file render |

### Cache Methods
| Method | Description |
|--------|-------------|
| `InvalidateCache(key)` | Remove specific cache |
| `InvalidateCacheByPrefix(prefix)` | Remove matching caches |
| `ClearRenderCache()` | Clear all caches |
| `HasCachedRender(key)` | Check cache exists |
| `RenderCacheCount` | Get cache count |
| `GetRenderCacheStats()` | Get detailed stats |

---

## 📄 License

MIT License - see [LICENSE](LICENSE) for details.

---

## 🔗 Links

- [📚 Full Documentation](DOCUMENTATION.md)
- [🧩 Component Guide](COMPONENT_DEVELOPMENT_GUIDE.md)
- [🎨 Frontend Guide](FRONTEND_GUIDE.md)
- [📦 NuGet Package](https://www.nuget.org/packages/ASTTemplateParser/)

---

Made with ❤️ for the .NET community
