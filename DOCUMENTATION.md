# AST Template Parser - Complete Documentation

A high-performance, HTML-like template parser for .NET with component system, layouts, and security features.

---

## 📑 Table of Contents

1. [Getting Started](#-getting-started)
2. [Variables & Interpolation](#-variables--interpolation)
3. [Indexers (Arrays & Dictionaries)](#-indexers-new-in-v201) *(NEW in v2.0.1)*
4. [Template Filters (Pipes)](#-template-filters-pipes) *(NEW in v2.0.3)*
5. [Global Variables](#-global-variables) *(NEW in v1.0.27)*
6. [Conditionals (If/Else)](#-conditionals-ifelse)
7. [Loops (ForEach)](#-loops-foreach)
8. [Component System](#-component-system)
9. [Type-Specific Component Tags](#type-specific-component-tags-new-in-v107) *(NEW in v1.0.7)*
10. [OnBeforeIncludeRender Event](#-onbeforeincluderender-event)
11. [OnAfterIncludeRender Event](#-onafterincluderender-event) *(NEW in v1.0.7)*
12. [Pre-Render Data Extraction](#-pre-render-data-extraction)
13. [Pages Directory & RenderPage](#-pages-directory--renderpage) *(NEW in v1.0.8)*
14. [Layout System](#-layout-system)
15. [Slots](#-slots)
16. [Security Configuration](#-security-configuration)
17. [Case-Sensitive Tags](#-case-sensitive-tags) *(NEW in v1.0.10)*
18. [Template Fragments (Define/Render)](#-template-fragments-definerender) *(NEW in v1.0.11)*
19. [Smart If Condition Truthiness](#-smart-if-condition-truthiness) *(NEW in v1.0.11)*
20. [API Reference](#-api-reference)
21. [Common Patterns](#-common-patterns)
22. [Troubleshooting](#-troubleshooting)

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

## 🎯 Indexers (NEW in v2.0.1)

You can now access **Arrays**, **Lists**, and **Dictionaries** directly in your templates using bracket syntax.

### Array & List Indexing

**In C#:**
```csharp
engine.SetVariable("Colors", new string[] { "Red", "Green", "Blue" });
engine.SetVariable("Products", new List<object> {
    new { Name = "Laptop", Price = 999 },
    new { Name = "Mouse", Price = 29 }
});
```

**In Template:**
```html
<p>First Color: {{Colors[0]}}</p>
<p>Second Product: {{Products[1].Name}}</p>
```

**Output:**
```html
<p>First Color: Red</p>
<p>Second Product: Mouse</p>
```

### Dictionary Key Access

**In C#:**
```csharp
engine.SetVariable("Settings", new Dictionary<string, string> {
    { "Theme", "Dark" },
    { "Language", "Bengali" }
});
```

**In Template:**
```html
<p>Current Theme: {{Settings["Theme"]}}</p>
<p>Language: {{Settings["Language"]}}</p>
```

### Dynamic Indexing

You can use a variable as an index:

**In C#:**
```csharp
engine.SetVariable("idx", 1);
engine.SetVariable("Items", new string[] { "A", "B", "C" });
```

**In Template:**
```html
<p>Item at index: {{Items[idx]}}</p>
```

---

## 🧪 Template Filters (Pipes) (NEW in v2.0.3)

Filters allow you to transform data directly in your template using the pipe (`|`) syntax.

### Syntax

```
{{ Expression | FilterName }}
{{ Expression | FilterName:Argument }}
{{ Expression | Filter1 | Filter2 }}
```

### Built-in Filters

| Filter | Description | Example | Output |
|--------|-------------|---------|--------|
| `uppercase` | Converts to UPPERCASE | `{{ "hello" | uppercase }}` | `HELLO` |
| `lowercase` | Converts to lowercase | `{{ "HELLO" | lowercase }}` | `hello` |
| `date` | Formats DateTime | `{{ Today | date:"dd MMM yyyy" }}` | `21 Jan 2026` |
| `currency` | Formats as currency | `{{ 1250.5 | currency:"en-US" }}` | `$1,250.50` |

### Chaining Filters

You can chain multiple filters together:

```html
{{ Title | lowercase | uppercase }}
{{ EventDate | date:"MMMM" | uppercase }}
```

### Custom Filter Registration

You can register your own custom filters in C#:

```csharp
TemplateEngine.RegisterFilter("shout", (value, args) => {
    return value?.ToString() + "!!!";
});

TemplateEngine.RegisterFilter("truncate", (value, args) => {
    int length = args.Length > 0 ? int.Parse(args[0]) : 50;
    string text = value?.ToString() ?? "";
    return text.Length > length ? text.Substring(0, length) + "..." : text;
});
```

**Usage in Template:**
```html
<p>{{ Name | shout }}</p>
<p>{{ Description | truncate:100 }}</p>
```

---

## 🌐 Global Variables

*(NEW in v1.0.27)*

Global variables are **static variables that persist across ALL TemplateEngine instances**. Set them once at application startup, and they're available in every template without needing to set them again.

### Why Use Global Variables?

- **Site-wide data**: Site name, copyright year, company info
- **Configuration**: API endpoints, feature flags, environment settings
- **Common data**: Shared navigation items, footer links, social media URLs
- **Reduce repetition**: No need to call `SetVariable()` on every engine instance

### Setting Global Variables

```csharp
// Set once at application startup (Program.cs, Global.asax, etc.)
TemplateEngine.SetGlobalVariable("SiteName", "My Awesome Website");
TemplateEngine.SetGlobalVariable("CurrentYear", DateTime.Now.Year);
TemplateEngine.SetGlobalVariable("SupportEmail", "support@example.com");

// Set multiple at once
TemplateEngine.SetGlobalVariables(new Dictionary<string, object>
{
    { "CompanyName", "ACME Corp" },
    { "CompanyAddress", "123 Business Street" },
    { "SocialLinks", new { 
        Facebook = "https://facebook.com/acme",
        Twitter = "https://twitter.com/acme"
    }}
});
```

### Using Global Variables in Templates

```html
<!-- Global variables work like regular variables -->
<footer>
    <p>&copy; {{CurrentYear}} {{CompanyName}}. All rights reserved.</p>
    <p>Contact: {{SupportEmail}}</p>
    <a href="{{SocialLinks.Facebook}}">Facebook</a>
</footer>
```

### No Engine Configuration Needed!

```csharp
// Global variables are automatic in ALL engine instances
var engine1 = new TemplateEngine();
var html1 = engine1.Render("Welcome to {{SiteName}}");  // Works!

var engine2 = new TemplateEngine();
var html2 = engine2.Render("Copyright {{CurrentYear}}");  // Also works!
```

### Variable Priority (Override Behavior)

Variables are resolved in this order (highest priority wins):

| Priority | Source | Example |
|----------|--------|---------|
| 1 (Highest) | `Render(template, additionalVars)` | Per-render override |
| 2 | `engine.SetVariable()` | Instance-level |
| 3 (Lowest) | `TemplateEngine.SetGlobalVariable()` | Global (static) |

**Example:**

```csharp
// Global
TemplateEngine.SetGlobalVariable("Title", "Global Title");

// Instance
var engine = new TemplateEngine();
engine.SetVariable("Title", "Instance Title");

// Render with additional
var html = engine.Render("{{Title}}", new Dictionary<string, object>
{
    { "Title", "Render Title" }
});

// Result: "Render Title" (highest priority wins)
```

### Global Variable API

| Method | Description |
|--------|-------------|
| `SetGlobalVariable(key, value)` | Set a single global variable |
| `SetGlobalVariables(dict)` | Set multiple global variables |
| `GetGlobalVariable(key)` | Get a global variable value |
| `HasGlobalVariable(key)` | Check if a global variable exists |
| `RemoveGlobalVariable(key)` | Remove a specific global variable |
| `ClearGlobalVariables()` | Remove ALL global variables |
| `GlobalVariableCount` | Get the count of global variables |

### Complete Example

```csharp
// Program.cs or Startup.cs
public class Program
{
    public static void Main(string[] args)
    {
        // Set global variables at startup
        TemplateEngine.SetGlobalVariables(new Dictionary<string, object>
        {
            { "SiteName", "My Website" },
            { "CurrentYear", DateTime.Now.Year },
            { "Version", "2.0.3" },
            { "IsDevelopment", Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development" }
        });
        
        // Rest of your application...
    }
}

// Anywhere in your application...
public string RenderPage(string pageTemplate)
{
    var engine = new TemplateEngine();
    engine.SetComponentsDirectory("./components");
    
    // No need to set SiteName, CurrentYear, etc. - they're global!
    engine.SetVariable("PageTitle", "About Us");
    
    return engine.Render(pageTemplate);
}
```

```html
<!-- Template uses both global and instance variables -->
<html>
<head>
    <title>{{PageTitle}} - {{SiteName}}</title>
</head>
<body>
    <header>{{SiteName}} v{{Version}}</header>
    <main>...</main>
    <footer>&copy; {{CurrentYear}}</footer>
</body>
</html>
```

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

### Param Default Values (NEW in v1.0.5!)

Use the `default` attribute to provide fallback values when a variable is null or empty:

```html
<!-- Basic default: If ButtonText is null/empty, "Click Me" is used -->
<Include component="element/button">
    <Param name="text" value="{{ButtonText}}" default="Click Me" />
    <Param name="type" value="{{ButtonType}}" default="primary" />
</Include>
```

**Default with Interpolation:**
```html
<!-- Default value can also contain variables -->
<Include component="element/header">
    <Param name="title" value="{{PageTitle}}" default="Welcome, {{UserName}}!" />
</Include>
```

**How Default Works:**

| Value | Variable State | Result |
|-------|---------------|--------|
| `value="Save"` | - | "Save" (hardcoded, default ignored) |
| `value="{{Text}}"` | Text = "Hello" | "Hello" (variable used) |
| `value="{{Text}}"` | Text = null | default value used |
| `value="{{Text}}"` | Text = "" | default value used |

**Important:** Default only applies when the resolved value is null or empty. Hardcoded values like `value="Save"` are used directly.

### Folder-Based Components (NEW in v1.0.5!)

Organize complex components in folders with `default.html`:

**Directory Structure:**
```
components/
├── element/
│   ├── button.html          ← component="element/button"
│   └── header.html          ← component="element/header"
└── block/
    ├── card.html            ← component="block/card"
    └── projects/            ← component="block/projects" (folder)
        ├── default.html     ← Main component (auto-loaded)
        ├── item.html        ← Sub-component
        └── styles.css       ← Related assets
```

**Component Resolution Order:**
1. First looks for `block/projects.html`
2. If not found, looks for `block/projects/default.html`

**Usage:**
```html
<!-- File-based component -->
<Include component="element/button">
    <Param name="text" value="Click" />
</Include>

<!-- Folder-based component (loads block/projects/default.html) -->
<Include component="block/projects">
    <Param name="title" value="My Projects" />
</Include>
```

---

## 🏷️ Type-Specific Component Tags (NEW in v1.0.7!)

Instead of using `<Include component="element/button">`, you can use **type-specific tags** that auto-prefix the component path:

| Tag | Path Prefix | Example |
|-----|-------------|---------|
| `<Element>` | `element/` | `<Element component="button">` → loads `element/button.html` |
| `<Block>` | `block/` | `<Block component="slider">` → loads `block/slider.html` |
| `<Data>` | `data/` | `<Data component="userData">` → loads `data/userData.html` |
| `<Nav>` | `navigation/` | `<Nav component="mainMenu">` → loads `navigation/mainMenu.html` |

**Before (using Include):**
```html
<Include component="element/subTitle" name="experience_years_subtitle">
    <Param name="content" value="Years Experience" />
    <Param name="attributes" value="type='text'" />
</Include>
```

**After (using type-specific tags):**
```html
<Element component="subTitle" name="experience_years_subtitle">
    <Param name="content" value="Years Experience" />
    <Param name="attributes" value="type='text'" />
</Element>
```

**Benefits:**
- More semantic and readable template code
- Auto path prefixing - less typing, fewer errors
- Clear component organization by type
- All `<Include>` features work the same way (Params, slots, caching)

**Complete Example:**
```html
<!-- Header element -->
<Element component="header" name="main_header">
    <Param name="title" value="{{PageTitle}}" />
    <Param name="logo" value="/images/logo.png" />
</Element>

<!-- Main navigation -->
<Nav component="mainMenu" name="top_nav">
    <Param name="items" value="{{MenuItems}}" />
</Nav>

<!-- Hero block -->
<Block component="hero" name="homepage_hero">
    <Param name="title" value="Welcome!" />
    <Param name="background" value="/images/hero-bg.jpg" />
</Block>

<!-- Data component for structured data -->
<Data component="schema" name="page_schema">
    <Param name="type" value="WebPage" />
    <Param name="title" value="{{PageTitle}}" />
</Data>
```

---

## 🔔 OnBeforeIncludeRender Event

*(NEW in v1.0.6!)*

Get a callback **before each Include component renders** - perfect for dynamic data loading:

```csharp
var engine = new TemplateEngine();
engine.SetComponentsDirectory("./components");

// Set callback - fires BEFORE each Include renders
engine.OnBeforeIncludeRender((info, eng) =>
{
    Console.WriteLine($"Rendering: {info.Name}");
    
    // Fetch data from cache using Include name as key
    var cachedData = YourCacheService.Get(info.Name);
    eng.SetVariable("element", cachedData);
});

// Render template - callback fires for each Include
var html = engine.Render(template);
```

| IncludeInfo Property | Description |
|---------------------|-------------|
| `Name` | Include `name` attribute (use as cache key) |
| `ComponentPath` | Component path like `block/slider` |
| `ComponentType` | Type: `"element"`, `"block"`, `"data"`, `"navigation"`, or `"include"` |
| `Parameters` | Dictionary of Param names and values |

### SetVariable Priority in Callback (NEW in v1.0.20!)

Variables set via `SetVariable()` in callback **take priority over Param values**. This enables dynamic attribute injection:

```csharp
engine.OnBeforeIncludeRender((info, eng) =>
{
    if (info.ComponentType == "element")
    {
        // Get existing attributes from Param
        var existingAttrs = info.Parameters.TryGetValue("attributes", out var val) ? val : "";
        
        // Inject additional data attributes
        var identity = GetElementIdentity(info.Name);
        var dataAttrs = $"data-identity='{identity}' data-editable='true'";
        
        // SetVariable takes priority over Param!
        eng.SetVariable("attributes", $"{existingAttrs} {dataAttrs}");
    }
});
```

**Template (components/element/subTitle.html):**
```html
<h2 class="edit-able {{class}}" {{attributes}}>{{content}}</h2>
```

**Usage:**
```html
<Element component="subTitle" name="my_subtitle">
    <Param name="class" value="highlight" />
    <Param name="content" value="Welcome!" />
    <Param name="attributes" value="id='main-title'" />
</Element>
```

**Output (with callback injection):**
```html
<h2 class="edit-able highlight" id='main-title' data-identity='abc123' data-editable='true'>Welcome!</h2>
```

---

## 🎁 OnAfterIncludeRender Event (NEW in v1.0.7!)

Get a callback **after each component renders** - perfect for wrapping output:

```csharp
engine.OnAfterIncludeRender((info, renderedHtml) =>
{
    // info.ComponentType tells you the type
    // Wrap based on component type
    if (info.ComponentType == "block")
    {
        return $"<section id=\"{info.Name}\">{renderedHtml}</section>";
    }
    return renderedHtml;
});
```

---

## 📤 Pre-Render Data Extraction

*(NEW in v1.0.4!)*

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

---

## 📄 Pages Directory & RenderPage (NEW in v1.0.8!)

Separate your page templates from components with a dedicated pages directory:

```csharp
var engine = new TemplateEngine();

// Setup both directories
engine.SetComponentsDirectory("./components");   // For reusable components
engine.SetPagesDirectory("./pages");             // For page templates

// Set your data
engine.SetVariable("PageTitle", "Home");
engine.SetVariable("UserName", "John");

// Render a page template
string html = engine.RenderPage("home");         // → pages/home.html

// Or with RenderFile using flag
string html2 = engine.RenderFile("about", isPage: true);   // → pages/about.html
string html3 = engine.RenderFile("element/button");        // → components/element/button.html
```

**Directory Structure:**
```
your-project/
├── components/              ← SetComponentsDirectory()
│   ├── element/
│   │   └── button.html
│   └── block/
│       └── footer.html
│
├── pages/                   ← SetPagesDirectory()
│   ├── home.html
│   ├── about.html
│   └── contact.html
│
└── Program.cs
```

**Page Template Example (`pages/home.html`):**
```html
<Element template="home">
    <h1>{{PageTitle}}</h1>
    
    <!-- Include components from components directory -->
    <Element component="header" name="main_header">
        <Param name="title" value="{{PageTitle}}" />
    </Element>
    
    <Block component="slider" name="hero">
        <Param name="slides" value="{{Slides}}" />
    </Block>
</Element>
```

---

## 📐 Layout System

Layouts let you define a common structure for multiple pages.

### Creating a Layout

**File: `components/layouts/main.html`**
```html
<!DOCTYPE html>
<html>
<head>
    <title>{{PageTitle}} - {{SiteName}}</title>
    <RenderSection name="head" />
</head>
<body>
    <header>
        <nav>{{SiteName}}</nav>
    </header>
    
    <main>
        <RenderBody />
    </main>
    
    <footer>
        &copy; {{CurrentYear}} {{SiteName}}
    </footer>
    
    <RenderSection name="scripts" />
</body>
</html>
```

### Using a Layout

```html
<Layout template="layouts/main">
    <Section name="head">
        <link rel="stylesheet" href="/css/home.css" />
    </Section>
    
    <h1>Welcome to the Home Page</h1>
    <p>This is the main content.</p>
    
    <Section name="scripts">
        <script src="/js/home.js"></script>
    </Section>
</Layout>
```

---

## 🎰 Slots

Slots allow components to accept content from their parent template.

### Default Slot

**Component (`components/block/card.html`):**
```html
<div class="card">
    <div class="card-header">{{title}}</div>
    <div class="card-body">
        <slot />
    </div>
</div>
```

**Usage:**
```html
<Include component="block/card">
    <Param name="title" value="My Card" />
    
    <!-- This content goes into the slot -->
    <p>This is the card body content!</p>
    <button>Click me</button>
</Include>
```

### Named Slots

**Component:**
```html
<div class="modal">
    <div class="modal-header">
        <slot name="header">Default Header</slot>
    </div>
    <div class="modal-body">
        <slot />
    </div>
    <div class="modal-footer">
        <slot name="footer">
            <button>Close</button>
        </slot>
    </div>
</div>
```

**Usage:**
```html
<Include component="block/modal">
    <Section slot="header">
        <h2>Custom Header</h2>
    </Section>
    
    <p>Modal body content here</p>
    
    <Section slot="footer">
        <button>Cancel</button>
        <button>Save</button>
    </Section>
</Include>
```

---

## 🛡️ Security Configuration

The parser includes built-in protection against common attacks.

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

## 🔤 Case-Sensitive Tags (NEW in v1.0.10!)

Template tags MUST be **PascalCase** to distinguish them from HTML tags:

| Template Tag | HTML Tag |
|-------------|----------|
| `<Element>` | `<element>` |
| `<Data>` | `<data>` |
| `<Section>` | `<section>` |
| `<Nav>` | `<nav>` |

**Correct:**
```html
<Element component="header">
    <section class="content">
        <Data component="schema">...</Data>
    </section>
</Element>
```

**Wrong (will be treated as HTML):**
```html
<element component="header">  <!-- NOT a template tag! -->
```

---

## 🔁 Template Fragments (Define/Render) (NEW in v1.0.11!)

Create reusable template fragments with inline recursion - perfect for multi-level navigation menus!

```html
<!-- Define a reusable fragment -->
<Define name="menuItem">
    <li>
        <a href="{{menuNode.Url}}">{{menuNode.Title}}</a>
        <If condition="menuNode.Children">
            <ul>
                <ForEach var="child" in="menuNode.Children">
                    <!-- Recursive call! -->
                    <Render name="menuItem" menuNode="child" />
                </ForEach>
            </ul>
        </If>
    </li>
</Define>

<!-- Use the fragment -->
<nav class="main-nav">
    <ul>
        <ForEach var="item" in="menuItems">
            <Render name="menuItem" menuNode="item" />
        </ForEach>
    </ul>
</nav>
```

**C# Setup:**
```csharp
var menuItems = new List<object>
{
    new {
        Title = "Home",
        Url = "/",
        Children = new List<object>
        {
            new { Title = "About", Url = "/about", Children = (object)null },
            new { Title = "Services", Url = "/services", Children = (object)null }
        }
    }
};

engine.SetVariable("menuItems", menuItems);
```

**Benefits:**
- ✅ Single file recursion - no separate component files needed
- ✅ Supports unlimited depth (up to MaxRecursionDepth, default: 20)
- ✅ Parameters passed via attributes
- ✅ Variable interpolation works in parameters

---

## ✅ Smart If Condition Truthiness (NEW in v1.0.11!)

If conditions now properly evaluate all types:

| Value | Result | Example |
|-------|--------|---------|
| `null` | `false` | `<If condition="item.Children">` when Children is null |
| Empty string `""` | `false` | `<If condition="name">` when name is "" |
| Empty collection `[]` | `false` | `<If condition="list">` when list has 0 items |
| Non-empty collection | `true` | `<If condition="Children">` when Children has items |
| Any non-null object | `true` | `<If condition="user">` when user exists |
| Boolean | As-is | `<If condition="isActive">` |

```html
<ul>
    <ForEach var="item" in="menuItems">
        <li>
            {{item.Title}}
            <If condition="item.Children">
                <!-- Only renders if Children is non-null AND has items -->
                <ul>
                    <ForEach var="child" in="item.Children">
                        <li>{{child.Title}}</li>
                    </ForEach>
                </ul>
            <Else>
                <!-- Renders if Children is null OR empty -->
                <span>(No sub-items)</span>
            </If>
        </li>
    </ForEach>
</ul>
```

---

## 📊 Performance Benchmarks

| Template Type | Operations/Second |
|--------------|-------------------|
| Simple (Small) | ~90,000+ ops/sec |
| Complex (Medium) | ~6,500+ ops/sec |
| Property Access | ~12,000,000+ ops/sec |

*Benchmarks conducted on .NET 8.0, Windows 11.*

---

## 📑 API Reference

### TemplateEngine Class

| Method | Description |
|--------|-------------|
| `SetVariable(key, value)` | Set instance variable |
| `SetGlobalVariable(key, value)` | Set global variable |
| `Render(template)` | Render template string |
| `RenderFile(path)` | Render from file |
| `RenderPage(name)` | Render page template |
| `RegisterFilter(name, delegate)` | Register custom filter |
| `OnBeforeIncludeRender(callback)` | Set pre-render callback |
| `OnAfterIncludeRender(callback)` | Set post-render callback |

---

## ❓ Troubleshooting

### Common Issues

**Variable not found:**
- Check variable name spelling
- Verify `SetVariable()` was called
- Check property path for typos

**Component not loading:**
- Verify `SetComponentsDirectory()` path
- Check component file exists
- Ensure correct path in `component` attribute

**Condition not working:**
- Check operator syntax (`==` not `=`)
- Verify variable has expected value
- Use Smart Truthiness for null/empty checks

---

## 📄 License

MIT License. Created by Md_Rony_Mondol. © 2026.
