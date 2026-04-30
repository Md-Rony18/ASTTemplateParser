# AST Template Parser

[![NuGet](https://img.shields.io/nuget/v/ASTTemplateParser.svg)](https://www.nuget.org/packages/ASTTemplateParser/)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-Standard%202.0%20%7C%204.8%20%7C%206.0%20%7C%208.0%20%7C%209.0%20%7C%2010.0-purple.svg)](https://dotnet.microsoft.com/)

A **blazing-fast**, **security-hardened** template engine for .NET with HTML-like syntax, **native JSON support**, and **2000x faster cached rendering**.

---

## ⚡ Performance at a Glance

| Scenario | Speed | Comparison |
|----------|-------|------------|
| **Cached Render** | ~0.001ms | 🔥 **2000x faster** |
| **Compiled Expression** | ~0.002ms | 🚀 **500x faster** |
| **Data-Aware Cache** | ~0.01ms | ⚡ **200x faster** |
| **Normal Render** | ~2ms | Baseline |

---

## ✨ Features

### Core
- 🚀 **High Performance** - 1,000,000+ cached renders/second
- 🔒 **Enterprise Security** - XSS protection, loop limits, property blocking
- 🧩 **Component System** - `<Element>`, `<Block>`, `<Data>`, `<Nav>` components
- 📐 **Layout System** - Master layouts with sections and slots

### Caching (v2.0.5)
- ⚡ **Render Caching** - Cache rendered output for instant response
- 🔄 **Auto File Invalidation** - Cache updates when template file changes
- 📊 **Data-Aware Caching** - Auto-invalidate when variables change
- ⏰ **Time-Based Expiration** - Optional cache TTL

### Template Features
- 🎯 **Indexers** - `{{ items[0] }}`, `{{ dict["key"] }}`
- 🧪 **Filters** - `{{ Name | uppercase }}`, `{{ Price | currency }}`
- 🌐 **Global Variables** - Set once, use everywhere
- 🔁 **Fragments** - `<Define>` and `<Render>` for recursion

### Performance Optimizations (v2.0.6)
- 🧮 **NCalc Expression Caching** - Parsed expression trees cached & reused (~2.5x faster)
- ⚡ **ICollection Fast Path** - O(1) count check instead of enumerator allocation (~10x faster)
- 📐 **Adaptive StringBuilder Pool** - Tiered small/large pools with template size hints
- 📊 **Data Hash Dirty Flag** - Skip hash recomputation when variables unchanged (~50x faster)
- 🗂️ **Pre-allocated Variable Merge** - Capacity estimation eliminates dictionary resizing
- 🌐 **.NET 9.0 & 10.0 Support** - Full support for latest .NET frameworks

### 🔥 Native JSON Support (NEW in v3.0.0 - April 12, 2026)
- 📄 **Zero-Config JSON Binding** — Pass `JsonConvert.DeserializeObject<dynamic>()` directly to `SetVariable()`. JObject, JArray, and JValue are automatically converted to native .NET types.
- ⚡ **Upfront Conversion** — JToken → Dictionary/List conversion happens once at `SetVariable()` time, not per-access. Zero reflection overhead during rendering.
- 🔌 **No Hard Dependency** — Newtonsoft.Json types are detected via reflection. If Newtonsoft.Json is not loaded, the engine simply ignores JToken logic with zero cost.
- 🧩 **Full Property Access** — `{{ item.Title }}`, `{{ item.Properties.Price }}`, `{{ item.Properties['Button Text'] }}` all work natively with JSON data.
- 🔁 **ForEach Friendly** — JArray collections work directly in `<ForEach>` loops.
- 🛡️ **SetGlobalVariable Too** — Same auto-conversion works for global/website-scoped variables.

### Master Expression Cache & Ternary Optimization (NEW in v2.2.6 - April 8, 2026)
- 🚀 **Master Expression Cache** — All template expressions (ternary, null-coalescing, comparisons) are now compiled once and cached. This skips string scanning, security checks, and parsing on every render.
- ⚡ **500% Faster Ternary Operators** — Ternary expressions (`{{ ? : }}`) are now ~5x faster, matching the speed of structural `@if` blocks.
- ✅ **HTML Attribute Support** — Ternary expressions now work flawlessly inside normal HTML attributes such as `class="{{ cond ? 'a' : 'b' }}"`.
- ✅ **Numeric Comparisons** — Conditions like `{{ Stock > 0 ? 'Yes' : 'No' }}` now evaluate correctly.

### Attribute & Expression Fixes (NEW in v2.2.5 - April 8, 2026)
- ✅ **HTML Attribute Ternary Support** — Ternary expressions now work inside normal HTML attributes such as `class="{{ menuNode.Items ? 'active' : 'unactive' }}"`.
- ✅ **Mixed Attribute Interpolation** — Mixed values such as `class="menu-link {{ cond ? 'active' : 'inactive' }}"` now resolve correctly.
- ✅ **Numeric Ternary Comparisons** — Numeric conditions like `{{ Product.Stock > 0 ? 'In Stock' : 'Out of Stock' }}` now evaluate correctly.
- ✅ **Interpolation Ternary Support** — Existing interpolation ternary support remains available, including bracketed paths such as `{{ item['Author'] == 'Rony' ? 'active' : 'inactive' }}`.
- 🎯 **Bracketed Expression Handling** — Complex bracketed expressions are no longer misclassified as simple indexer lookups during interpolation.

### JsonPath Support (NEW in v2.2.1 - April 4, 2026)
- 🗂️ **JsonPath Property** — `IncludeInfo` now exposes a `JsonPath` property, extracted from the `jsonpath="..."` attribute on component tags.
- 🔗 **Full Pipeline Support** — Available in `PrepareTemplate()`, `ExtractIncludeNames()`, `OnBeforeIncludeRender`, and `OnAfterIncludeRender` callbacks.
- 🧩 **All Component Types** — Works on `<Include>`, `<Element>`, `<Data>`, `<Nav>`, and `<Block>` tags.

### Loop Metadata & Dynamic Attributes (v2.1.0)
- 🔢 **Loop Metadata** - Use `{{loop.index}}`, `{{loop.count}}`, and `{{loop.first}}` inside `ForEach`
- 🏷️ **Attr Filter** - Render dynamic attributes only if value exists: `{{ myClass | attr:"class" }}`
- 🧹 **Auto Attribute Cleanup** - Enable `RemoveEmptyAttributes` in `SecurityConfig` to auto-strip empty attributes
- 🛠️ **Error-Tolerant Parsing** - `BlockParser` now preserves invalid block tags as HTML instead of removing them
- 🚀 **Zero-Allocation Metadata** - Optimized metadata dictionary reuse for maximum loop performance
- 🛡️ **Component Validation** - Blocks `../` and `:` in component paths

### BlockParser Dual-Path Support (NEW in v2.1.5 - March 28, 2026)
- 🧩 **Dual-Path Parsing** — `BlockParser` now uses the same local/global resolution logic as the engine (local pages override).
- 📄 **Auto .html Extension** — Engine now automatically appends `.html` in `ResolveDualPath` for cleaner calls.
- 🛠️ **Public Path Helpers** — `ResolvePagePath(name)` and `ResolveComponentPath(name)` exposed to TemplateEngine for manual path resolution.
- 🛡️ **Validation Overhaul** — `BlockParser` now validates paths against BOTH local and global allowed directories.

### Dual-Path Directory Support (NEW in v2.1.4 - March 28, 2026)
- 📁 **Local Components Path** — `SetLocalComponentsDirectory()` sets a per-engine local override or fallback for components
- 📄 **Local Pages Path** — `SetLocalPagesDirectory()` sets a per-engine local override or fallback for pages
- 🔀 **Priority Control** — `SetDirectoryPriority(preferGlobal)`: `false` (default) = local first; `true` = global first
- 🛡️ **Security Aware** — Both paths auto-registered in `AllowedTemplatePaths` for safe file access
- 🔄 **Consistent Resolution** — `LoadComponent()`, `RenderFile()`, and `RenderCachedFile()` all honour the same dual-path logic

### Performance & Security Hardening (v2.1.3 - March 11, 2026)
- 🚀 **Zero-Allocation ForEach** — Optimized context swap eliminates object allocations in loops.
- ⚡ **Ultra-Fast Expressions** — Skip character scans for simple variable lookups (~2-3x faster).
- 🛡️ **Auto-PreWarming** — `PreWarmThemes()` and `PreWarmAll()` to pre-cache templates at startup.
- 🔒 **HTML Auto-Encoding** — Default XSS prevention for all `{{ variable }}` outputs.
- 🧮 **Fast Path Resolution** — Zero-allocation resolution for nested paths (e.g., `item.Name`).

### Caching & Stability Fixes (NEW in v2.1.2)
- 🔧 **Complete Cache Clear** — `ClearCaches()` now clears ALL caches (render, path, block, expression, property)
- 📂 **Path Cache Validation** — Stale cached paths auto-removed when files are moved or deleted
- 🔄 **Component Change Detection** — `RenderCachedFile` auto-detects component file changes via version tracking
- 🧮 **Content-Aware Hashing** — `ComputeDataHash` properly hashes List/Dictionary contents (not references)
- ⚡ **Race Condition Fix** — Expression cache eviction is now thread-safe under high concurrency
- 🚀 **ForEach Optimization** — Loop metadata dictionary allocated once and reused across iterations

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

### JSON Data (NEW in v3.0.0)
```csharp
// Read JSON and pass directly — zero manual conversion needed!
var jsonData = JsonConvert.DeserializeObject<dynamic>(File.ReadAllText("data.json"));
engine.SetVariable("Items", jsonData.Items);

// Template: {{ item.Title }}, {{ item.Properties.Price }}, {{ item.Properties['Button Text'] }}
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

## 🧱 Block Parser (NEW in v2.0.8)

### Parse Blocks from Page Templates
```csharp
var engine = new TemplateEngine();
engine.SetPagesDirectory("./pages");

var blockParser = new BlockParser(engine);

// Extract <Block> components from page template
var blocks = blockParser.ParseBlocks("home");

foreach (var block in blocks)
{
    Console.WriteLine($"{block.Order}: {block.Name} → {block.ComponentPath}");
    // 0: slider_un → slider
    // 1: about_un → about/standard
    // 2: blog_un → blog
}
```

### Mixed Content — Blocks + Raw HTML
```csharp
// Parse page template that has BOTH <Block> calls AND raw HTML
var segments = blockParser.ParseTemplateSegments("home");

foreach (var segment in segments)
{
    if (segment.IsBlock)
    {
        // Render block via engine
        var html = engine.RenderCachedFile(
            "block/" + segment.Block.ComponentPath,
            "block-" + segment.Block.ComponentPath);
        output.AppendLine(html);
    }
    else if (segment.IsHtml)
    {
        // Raw HTML — directly append
        output.AppendLine(segment.RawHtml);
    }
}
```

### Cache Management
```csharp
// Clear all block/segment caches
BlockParser.ClearCache();

// Clear specific template cache
BlockParser.ClearCache("home");

// Monitor cache
Console.WriteLine($"Block cache: {BlockParser.CacheCount}");
Console.WriteLine($"Segment cache: {BlockParser.SegmentCacheCount}");
```

## 📖 Template Syntax

### Variables & Indexers
```html
{{Name}}                                           <!-- Simple -->
{{User.Address.City}}                              <!-- Nested -->
{{Items[0].Title}}                                 <!-- Array -->
{{Config["apiKey"]}}                               <!-- Dictionary -->
{{ item['Author'] == 'Rony' ? 'active' : 'inactive' }} <!-- Interpolation ternary -->
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
<Block component="hero" jsonpath="$.pageData.hero">
    <Param name="title" value="{{PageTitle}}" />
</Block>

<!-- JsonPath available in IncludeInfo -->
<Data component="products" name="prod_list" jsonpath="$.api.products">
    <Param name="limit" value="10" />
</Data>
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
| Compiled Expression | **~0.002ms** | 500K+ ops/sec (5x faster) |
| Cache Hit (Data Hash) | **~0.003ms** | 300K+ ops/sec |
| Normal Render (Simple) | **~0.001ms** | 886K+ ops/sec |
| Normal Render (ForEach) | **~0.013ms** | 75K+ ops/sec |
| Property Access | ~0.00008ms | 12M+ ops/sec |
| JSON SetVariable Overhead | ~0.0001μs | Zero regression |
| ConvertJTokenToNative (passthrough) | ~0.0000μs | Non-JToken = free |

*Tested on .NET 8.0+ / Intel i7 / Windows 11*

---

## 🎯 Supported Frameworks

| Framework | Version | Status |
|-----------|---------|--------|
| .NET Standard | 2.0 | ✅ Supported |
| .NET Framework | 4.8 | ✅ Supported |
| .NET | 6.0 | ✅ Supported |
| .NET | 8.0 | ✅ Supported |
| .NET | 9.0 | ✅ Supported |
| .NET | 10.0 | ✅ Supported |

---

## 🌐 Global Variables (App-Wide or Website-Scoped)

Global variables are **static variables that persist across ALL TemplateEngine instances**. Set them once at application startup, and they're available in every template.

### 🏢 Website-Scoped Globals (NEW!)
You can now scope global variables to a specific `websiteId`. This is perfect for multi-tenant applications.

```csharp
// App-wide global (All websites)
TemplateEngine.SetGlobalVariable("SiteName", "My Main Site");

// Website-scoped global (Only for website ID 5)
TemplateEngine.SetGlobalVariable("ThemeColor", "Blue", websiteId: 5);
TemplateEngine.SetGlobalVariable("Banner", "sale.png", websiteId: 5);

// To use website globals, set the ID on your engine instance
var engine = new TemplateEngine();
engine.SetWebsiteId(5); 

// Will use "My Main Site" AND include "ThemeColor" / "Banner"
string html = engine.RenderCachedFile("home.html", "home", includeDataHash: true);
```

### ⚡ Auto Cache Invalidation
Whenever you call `SetGlobalVariable()`, the engine automatically increments a global version counter. Any **Data-Aware Cache** (using `includeDataHash: true`) will detect this change and re-render on the next call to ensure your pages always show the latest global data.

---

## 🔧 API Reference

### Block Parser Methods
| Method | Description |
|--------|-------------|
| `ParseBlocks()` | Extract `<Block>` tags from page template |
| `ParseTemplateSegments()` | ⭐ **NEW** - Parse blocks + raw HTML segments |
| `ParseBlocksFromContent()` | Parse from string content |
| `ParseSegmentsFromContent()` | Parse segments from string |
| `ClearCache()` | Clear all block & segment caches |
| `CacheCount` | Block cache entry count |
| `SegmentCacheCount` | Segment cache entry count |

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

### Directory Methods (NEW in v2.1.4 / v2.1.5)
| Method | Description |
|--------|-------------|
| `SetComponentsDirectory(path)` | Global components directory |
| `SetPagesDirectory(path)` | Global pages directory |
| `SetLocalComponentsDirectory(path)` | ⭐ **NEW** - Local/override components directory |
| `SetLocalPagesDirectory(path)` | ⭐ **NEW** - Local/override pages directory |
| `SetDirectoryPriority(preferGlobal)` | ⭐ **NEW** - `false` (default) = local first; `true` = global first |
| `ResolvePagePath(name)` | ⭐ **NEW** - Resolve page path using dual-path priority |
| `ResolveComponentPath(name)` | ⭐ **NEW** - Resolve component path using dual-path priority |

---

## 📁 Dual-Path Directory (NEW in v2.1.4)

### Local override (default — local takes priority)
```csharp
// Global/theme path for the website
_engine.SetComponentsDirectory(website.GetComponentPath());
_engine.SetPagesDirectory(website.BasePageTemplatePath());

// Local path overrides global (checked first by default)
_engine.SetLocalComponentsDirectory(localThemePath);
_engine.SetLocalPagesDirectory(localPagePath);

// Now: localThemePath checked first → falls back to website path if not found
string html = _engine.RenderCachedFile("home", "home", includeDataHash: true);
```

### Global priority (global first, local as fallback)
```csharp
_engine.SetDirectoryPriority(preferGlobal: true);
// Now: website path checked first → localThemePath used only as fallback
```

---

## 📄 License

MIT License - see [LICENSE](LICENSE) for details.

---

## 🔗 Links

- [📚 Full Documentation](DOCUMENTATION.md)
- [🧩 Component Guide](COMPONENT_DEVELOPMENT_GUIDE.md)
- [🎨 Theme Build Guide](TEMPLATE_BUILD_GUIDE.md)
- [📦 NuGet Package](https://www.nuget.org/packages/ASTTemplateParser/)

---

Made with ❤️ for the .NET community
