# AST Template Parser

[![NuGet](https://img.shields.io/nuget/v/ASTTemplateParser.svg)](https://www.nuget.org/packages/ASTTemplateParser/)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

A **high-performance**, **security-hardened** template parser for .NET with HTML-like syntax. Works on .NET Standard 2.0, .NET Framework 4.8, .NET 6.0, and .NET 8.0.

## ✨ Features

- 🚀 **High Performance** - 90,000+ renders/second with compiled property accessors.
- 🔒 **Enterprise Security** - Built-in XSS protection, loop limits, and property whitelisting.
- 🧩 **Component System** - Reusable `<Element>`, `<Block>`, `<Data>`, and `<Nav>` components.
- 📐 **Layout System** - Master page layouts with sections and slots.
- 🔄 **Auto Cache** - AST and component files automatically reload when modified.
- 🎯 **Indexer Support** - Access arrays and dictionaries: `{{ items[0] }}` (v2.0.1+).
- 🧪 **Template Filters** - Transform data with pipes: `{{ Name | uppercase }}` (v2.0.3+).
- 🔁 **Template Fragments** - `<Define>` and `<Render>` for inline recursion and menus.
- 🌐 **Global Variables** - Set static variables once for all engine instances.
- 🌍 **Cross-Platform** - Fully compatible with Windows, Linux, and macOS.

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
engine.SetVariable("UserName", "Antigravity");
engine.SetVariable("Products", new List<object> {
    new { Name = "Laptop", Price = 999.99 },
    new { Name = "Mouse", Price = 29.99 }
});
```

### Step 3: Write Template & Render
```html
<h1>Welcome, {{UserName}}!</h1>
<ul>
    <ForEach var="p" in="Products">
        <li>{{p.Name}} - {{p.Price | currency:"en-US"}}</li>
    </ForEach>
</ul>
```
```csharp
string html = engine.Render(template);
```

---

## 📖 Template Syntax Guide

### 1. Variables & Accessors
Supports simple variables, nested properties, and indexers.
```html
{{Name}}                      <!-- Simple -->
{{User.Address.City}}         <!-- Nested -->
{{Items[0].Title}}            <!-- Array Indexer -->
{{Meta["version"]}}           <!-- Dictionary Key -->
```

### 2. Template Filters (NEW in v2.0.3)
Transform data using the pipe (`|`) syntax.

| Filter | Usage Example | Output |
|--------|---------------|--------|
| `uppercase` | `{{ Name | uppercase }}` | `NAME` |
| `lowercase` | `{{ Name | lowercase }}` | `name` |
| `date` | `{{ Created | date:"dd MMM yyyy" }}` | `21 Jan 2026` |
| `currency` | `{{ Price | currency:"bn-BD" }}` | `1,250.75৳` |

**Custom Filters:**
```csharp
TemplateEngine.RegisterFilter("shout", (val, args) => val?.ToString() + "!!!");
```

### 3. Conditionals (If/Else)
```html
<If condition="IsLoggedIn">
    <p>Welcome back!</p>
<ElseIf condition="Role == 'admin'">
    <p>Admin Dashboard</p>
<Else>
    <p>Please log in</p>
</If>
```

### 4. Loops (ForEach)
```html
<ForEach var="item" in="Items">
    <li>{{item.Name}}</li>
</ForEach>
```

### 5. Components & Tags
Instead of generic includes, use type-specific tags for auto-path resolution:

| Tag | Directory Prefix | Example |
|-----|------------------|---------|
| `<Element>` | `element/` | `<Element component="button">` |
| `<Block>` | `block/` | `<Block component="hero">` |
| `<Data>` | `data/` | `<Data component="meta">` |
| `<Nav>` | `navigation/` | `<Nav component="menu">` |

---

## 🔒 Security Configuration

The parser is hardened by default, but you can customize it:

```csharp
var security = new SecurityConfig {
    MaxLoopIterations = 500,        // DoS protection
    MaxRecursionDepth = 5,          // StackOverflow protection
    HtmlEncodeOutput = true,        // XSS protection
    BlockedPropertyNames = new HashSet<string> { "Password", "Secret" }
};
var engine = new TemplateEngine(security);
```

---

## 📊 Performance Metrics

| Scenario | Operations / Second |
|----------|---------------------|
| **Simple Template Rendering** | ~90,000+ ops/sec |
| **Complex Component Rendering** | ~6,500+ ops/sec |
| **Property Access (Compiled)** | ~12,000,000+ ops/sec |

*Tested on .NET 8.0 / Intel i7 / Windows 11.*

---

## 🧪 Global & Local Callbacks

### Global Variables
Perfect for site-wide settings like `SiteName` or `CopyrightYear`.
```csharp
TemplateEngine.SetGlobalVariable("Year", 2026);
```

### OnBeforeIncludeRender
Fires before each component renders. Use it to inject dynamic data based on component name.
```csharp
engine.OnBeforeIncludeRender((info, eng) => {
    var data = Database.Fetch(info.Name);
    eng.SetVariable("item", data);
});
```

---

## 📄 License
This project is licensed under the MIT License - see the LICENSE file for details.
