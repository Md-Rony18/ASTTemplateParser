# AST Template Parser v3.0.5

High-performance, AST-based template engine for .NET. Designed for CMS development, multi-tenant websites, and complex data binding scenarios.

## 🚀 Key Features
- **Server-Side Data Tags (New in v3.0.5):** Dynamically extract attributes from `<Data>` and `<Block>` tags for custom backend data-fetching logic.
- **Filter Support in ForEach:** Apply filters directly to collections inside loops (e.g., `<ForEach var="item" in="Data.Items | take:5">`).
- **Native JSON Support:** Seamlessly bind `Newtonsoft.Json` objects (JObject, JArray) without manual conversion.
- **Dynamic Variable Resolution:** Support for dotted variable names (e.g., `Data.Item.Title`) and property-to-indexer fallback.
- **Ultra-Fast Rendering:** Built on Abstract Syntax Trees with intelligent caching and thread-safe execution.
- **Enterprise Security:** XSS protection (auto-encoding), path traversal guards, and property blacklisting.
- **Cross-Platform:** Supports .NET 4.8, .NET 6.0, 8.0, 9.0, and 10.0+ via .NET Standard 2.0.

## 📦 Installation
```powershell
dotnet add package ASTTemplateParser
```

## 🛠 Usage Examples

### 1. Basic Rendering
```csharp
var engine = new TemplateEngine();
engine.SetVariable("Name", "Rony");
string result = engine.Render("Hello, {{ Name }}!");
```

### 2. Server-Side Data Tags (New in v3.0.5)
```html
<Data source="database" items="5">
   <ForEach var="item" in="FetchedItems">
       <div>{{ item.Title }}</div>
   </ForEach>
</Data>
```

### 3. ForEach with Filter
```html
<ForEach var="item" in="Items | take:2">
   <div class="card">
      <h3>{{ item.Title }}</h3>
   </div>
</ForEach>
```

### 4. Custom Filter Registration
```csharp
TemplateEngine.RegisterFilter("shout", (val, args) => val?.ToString()?.ToUpper() + "!!!");
```

## 📑 Recent Updates (v3.0.5)
- **Added:** Automatic attribute extraction for `<Data>` and `<Block>` tags.
- **Performance:** Pre-compiled static Regex for zero overhead during attribute parsing.

## ⚖️ License
Licensed under the MIT License.
