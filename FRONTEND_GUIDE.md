# 🎨 Frontend Developer Guide: Building Components

Welcome! This guide is designed for frontend developers to help you build reusable UI components using the **AST Template Parser**.

---

## 🏗️ 1. Component Basics

A component is just a standard HTML file. The parser looks for variables using the `{{VariableName}}` syntax.

### Basic Component Example
File: `components/element/button.html`
```html
<a href="{{href}}" class="btn btn-{{type}}">
    {{text}}
</a>
```

### Advanced Component (with fallbacks)
You can provide default values for cases where a variable might be missing.
```html
<button class="btn {{class}}" type="{{btnType}}" default="button">
    {{label}} default="Generic Button"
</button>
```

---

## 📂 2. Directory Structure

Your components should be organized into folders. The most common structure is:

- `components/element/` - Small UI pieces (buttons, inputs, titles)
- `components/block/` - Larger UI sections (sliders, features, heroes)
- `components/navigation/` - Menus, breadcrumbs, footers

---

## 🏷️ 3. Special Component Tags

To use your components in another file, use these specific tags. They automatically know where to look!

| Tag | Look in Folder | Example Use |
|---|---|---|
| `<Element />` | `element/` | `<Element component="button" />` |
| `<Block />` | `block/` | `<Block component="hero" />` |
| `<Nav />` | `navigation/` | `<Nav component="menu" />` |

---

## 🛠️ 4. Dynamic Logic

### 🔄 Loops (ForEach)
Use `<ForEach>` to loop through arrays.
```html
<ul class="tag-list">
    <ForEach var="tag" in="Tags">
        <li class="tag">{{tag}}</li>
    </ForEach>
</ul>
```

### 🛣️ Conditionals (If/Else)
Show or hide parts of your component based on logic.
```html
<If condition="IsVisible">
    <div class="card">
        <h3>{{Title}}</h3>
        <If condition="ImageUrl">
            <img src="{{ImageUrl}}" alt="Card image">
        <Else>
            <div class="placeholder">No Image</div>
        </If>
    </div>
</If>
```

---

## 🔄 5. Multi-Level Menus (Recursion)

If you need to build nested things like dropdown menus, use `<Define>` and `<Render>`.

```html
<!-- Define how one menu item looks -->
<Define name="navLink">
    <li>
        <a href="{{item.Url}}">{{item.Title}}</a>
        
        <!-- If it has sub-items, call itself! -->
        <If condition="item.Children">
            <ul class="dropdown">
                <ForEach var="child" in="item.Children">
                    <Render name="navLink" item="child" />
                </ForEach>
            </ul>
        </If>
    </li>
</Define>

<!-- Use it -->
<ul>
    <ForEach var="node" in="MenuData">
        <Render name="navLink" item="node" />
    </ForEach>
</ul>
```

---

## 💡 Best Practices for Frontends

1. **PascalCase for Tags**: Always use uppercase for template tags (`<Element>`, `<If>`). Regular HTML tags (`<div>`, `<span>`) should be lowercase.
2. **Naming Components**: Use descriptive names. Instead of `btn1.html`, use `primary-button.html`.
3. **Avoid Complex JS in Templates**: Keep the logic in the template simple. If it's too complex, it might belong in the backend.
4. **Data Types**: You can now use standard C# objects, **Dictionaries**, and even public **Fields**. The parser will find them automatically, regardless of case.
5. **Security**: All variables are automatically HTML-encoded to prevent XSS. If you *really* need to output raw HTML, the backend dev must enable it.

---

Happy Coding! 🚀
