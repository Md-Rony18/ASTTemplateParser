# 🧩 Component Development Guide

> **The Ultimate Handbook for Frontend Developers Building Components with AST Template Parser.**

This guide is specifically designed for developers who will be **creating new components**, designing **reusable UI elements**, and building **modular template systems**.

---

## 📑 Table of Contents

1. [Introduction to Components](#-introduction-to-components)
2. [Directory Structure & Organization](#-directory-structure--organization)
3. [Component Anatomy](#-component-anatomy)
4. [Receiving Parameters (Params)](#-receiving-parameters)
5. [Using Default Values](#-using-default-values)
6. [Accessing Nested Data](#-accessing-nested-data)
7. [Using Indexers in Components](#-using-indexers)
8. [Using Template Filters](#-using-template-filters)
9. [Conditional Rendering](#-conditional-rendering)
10. [Looping Inside Components](#-looping-inside-components)
11. [Working with Slots](#-working-with-slots)
12. [Component Types & Auto-Prefixing](#-component-types--auto-prefixing)
13. [Composing Components (Nesting)](#-composing-components-nesting)
14. [Best Practices](#-best-practices)
15. [Common Patterns & Examples](#-common-patterns--examples)
16. [Troubleshooting](#-troubleshooting)

---

## 🎯 Introduction to Components

Components are **reusable, self-contained HTML templates**. Think of them as custom HTML tags that you can use anywhere in your project.

### Why Components?

- **Reusability**: Write once, use everywhere.
- **Consistency**: Same component = same look everywhere.
- **Maintainability**: Update in one place, reflects everywhere.
- **Separation of Concerns**: Each component has a single responsibility.

### Component Lifecycle

```
┌──────────────────────────────────────────────────────────┐
│  1. PARSE: Template engine reads the component file      │
│  2. PARAMS: Parameters are resolved ({{variables}})      │
│  3. BEFORE: OnBeforeIncludeRender callback fires         │
│  4. RENDER: Component HTML is generated                  │
│  5. AFTER: OnAfterIncludeRender callback fires           │
│  6. OUTPUT: Final HTML is inserted                       │
└──────────────────────────────────────────────────────────┘
```

---

## 📁 Directory Structure & Organization

### Standard Layout

```
your-project/
├── components/
│   ├── element/           ← Small UI pieces (buttons, inputs, badges)
│   │   ├── button.html
│   │   ├── input.html
│   │   ├── badge.html
│   │   └── avatar.html
│   │
│   ├── block/             ← Larger sections (cards, heroes, sliders)
│   │   ├── card.html
│   │   ├── hero.html
│   │   ├── slider/
│   │   │   ├── default.html
│   │   │   └── slide-item.html
│   │   └── testimonial.html
│   │
│   ├── navigation/        ← Menus and navigation
│   │   ├── navbar.html
│   │   ├── sidebar.html
│   │   └── breadcrumb.html
│   │
│   ├── data/              ← Structured data (JSON-LD, meta)
│   │   ├── schema.html
│   │   └── meta-tags.html
│   │
│   └── layouts/           ← Master page layouts
│       ├── main.html
│       └── admin.html
│
└── pages/
    ├── home.html
    └── about.html
```

### Naming Conventions

| Convention | Example | Description |
|-----------|---------|-------------|
| **Lowercase filenames** | `button.html` | All component files in lowercase |
| **Kebab-case for multi-word** | `user-card.html` | Use hyphens, not underscores |
| **Folder for complex** | `slider/default.html` | Complex components get folders |
| **PascalCase in templates** | `<Element>`, `<Block>` | Template tags are PascalCase |

---

## 🔨 Component Anatomy

A component is just an `.html` file with template syntax inside.

### Basic Component Structure

**File: `components/element/button.html`**
```html
<!-- 
    Component: Button
    Purpose: Reusable button/link element
    Params: text, href, type, size
-->
<a 
    href="{{href}}" 
    class="btn btn-{{type}} btn-{{size}}"
    {{attributes}}
>
    {{text}}
</a>
```

### Key Points

1. **No wrapper required**: Your component can be just the HTML you need.
2. **All params available**: Any `<Param>` from the parent becomes a variable.
3. **Use `{{attributes}}`**: For extra HTML attributes passed from parent.
4. **Comments are stripped**: HTML comments won't appear in output.

---

## 📥 Receiving Parameters

Parameters are how parent templates pass data to your component.

### How Parents Pass Params

```html
<Element component="button">
    <Param name="text" value="Click Me" />
    <Param name="href" value="/action" />
    <Param name="type" value="primary" />
    <Param name="size" value="large" />
</Element>
```

### How Components Receive Them

**Inside `button.html`:**
```html
<a href="{{href}}" class="btn btn-{{type}} btn-{{size}}">
    {{text}}
</a>
```

### Parameter Value Types

Parents can pass different types of values:

| Parent Syntax | What Component Gets |
|--------------|---------------------|
| `value="Click Me"` | Literal string `"Click Me"` |
| `value="{{ButtonText}}"` | Value of `ButtonText` variable |
| `value="ButtonText"` | Value of `ButtonText` if it exists |
| `value="Hello {{Name}}!"` | Interpolated string |
| `value="{{User.Name}}"` | Nested property value |

---

## 🛡️ Using Default Values

Always design components to handle missing parameters gracefully.

### Setting Defaults in Parent

```html
<Element component="button">
    <Param name="text" value="{{ButtonText}}" default="Submit" />
    <Param name="type" value="{{ButtonType}}" default="primary" />
    <Param name="size" value="{{ButtonSize}}" default="medium" />
</Element>
```

### Handling Missing Params in Component

**Method 1: Using If conditions**
```html
<a href="{{href}}" class="btn btn-{{type}}">
    <If condition="icon">
        <i class="icon-{{icon}}"></i>
    </If>
    {{text}}
</a>
```

**Method 2: CSS-based fallback**
```html
<!-- Empty class won't break CSS -->
<a href="{{href}}" class="btn btn-{{type}} {{extraClass}}">
    {{text}}
</a>
```

### Default Value Priority

| Scenario | Result |
|----------|--------|
| `value="Save"` | Always "Save" |
| `value="{{Text}}"` (Text = "Hello") | "Hello" |
| `value="{{Text}}"` (Text = null) | Uses `default` |
| `value="{{Text}}"` (Text = "") | Uses `default` |

---

## 🔗 Accessing Nested Data

Components can access deeply nested properties from objects passed to them.

### Passing Complex Objects

```csharp
engine.SetVariable("Product", new {
    Name = "Laptop",
    Price = 999.99,
    Category = new {
        Name = "Electronics",
        Slug = "electronics"
    },
    Images = new string[] { "/img/1.jpg", "/img/2.jpg" }
});
```

### Accessing in Component

```html
<div class="product-card">
    <h3>{{Product.Name}}</h3>
    <p class="price">${{Product.Price}}</p>
    <span class="category">{{Product.Category.Name}}</span>
    <img src="{{Product.Images[0]}}" alt="{{Product.Name}}" />
</div>
```

---

## 🎯 Using Indexers

*(NEW in v2.0.1)*

Access arrays, lists, and dictionaries directly in your components.

### Array Access

```html
<div class="gallery">
    <img src="{{Images[0]}}" class="main-image" />
    <div class="thumbnails">
        <img src="{{Images[1]}}" class="thumb" />
        <img src="{{Images[2]}}" class="thumb" />
    </div>
</div>
```

### Dictionary Access

```html
<div class="i18n-text">
    <h1>{{Translations["title"]}}</h1>
    <p>{{Translations["description"]}}</p>
</div>
```

### Dynamic Indexing

```html
<!-- If 'currentIndex' is a variable -->
<img src="{{Slides[currentIndex].Image}}" />
```

---

## 🧪 Using Template Filters

*(NEW in v2.0.3)*

Filters transform data directly in your template using the pipe `|` syntax.

### Built-in Filters

```html
<!-- Text transformations -->
<h1>{{Title | uppercase}}</h1>
<p class="slug">{{Title | lowercase}}</p>

<!-- Date formatting -->
<time>{{CreatedAt | date:"dd MMM yyyy"}}</time>
<span>{{UpdatedAt | date:"HH:mm"}}</span>

<!-- Currency formatting -->
<span class="price">{{Price | currency:"en-US"}}</span>
<span class="price-bd">{{Price | currency:"bn-BD"}}</span>
```

### Chaining Filters

```html
<!-- Apply multiple transformations -->
<p class="meta">{{Category | lowercase}}</p>
<h2>{{EventMonth | date:"MMMM" | uppercase}}</h2>
```

### Best Practice: Format in Component

Keep formatting logic inside components, not in parent templates:

**✅ Good (formatting in component):**
```html
<!-- components/element/price.html -->
<span class="price {{class}}">
    {{amount | currency:"en-US"}}
</span>
```

**❌ Bad (formatting in parent):**
```html
<Element component="price">
    <Param name="amount" value="{{Product.Price | currency:'en-US'}}" />
</Element>
```

---

## 🔀 Conditional Rendering

Use `<If>`, `<ElseIf>`, and `<Else>` to show/hide parts of your component.

### Basic Conditionals

```html
<div class="alert alert-{{type}}">
    <If condition="icon">
        <i class="alert-icon icon-{{icon}}"></i>
    </If>
    
    <If condition="title">
        <strong class="alert-title">{{title}}</strong>
    </If>
    
    <p class="alert-message">{{message}}</p>
    
    <If condition="dismissible">
        <button class="close" aria-label="Close">&times;</button>
    </If>
</div>
```

### Conditional Classes

```html
<button class="btn 
    btn-{{type}} 
    <If condition="size">btn-{{size}}</If>
    <If condition="block">btn-block</If>
    <If condition="disabled">disabled</If>
">
    {{text}}
</button>
```

### Smart Truthiness

The engine treats these as **FALSE**:
- `null`
- `""` (empty string)
- `0` (zero)
- `[]` (empty list)
- `false`

**Example:**
```html
<If condition="Items">
    <!-- Only renders if Items exists AND has items -->
    <ul>
        <ForEach var="item" in="Items">
            <li>{{item.Name}}</li>
        </ForEach>
    </ul>
<Else>
    <p class="empty-state">No items found.</p>
</If>
```

---

## 🔄 Looping Inside Components

Use `<ForEach>` to iterate over collections.

### Basic Loop

```html
<!-- components/navigation/menu.html -->
<nav class="main-menu">
    <ul>
        <ForEach var="item" in="Items">
            <li class="menu-item">
                <a href="{{item.Url}}">{{item.Title}}</a>
            </li>
        </ForEach>
    </ul>
</nav>
```

### Loop with Index Checking

```html
<!-- components/block/slider.html -->
<div class="slider">
    <ForEach var="slide" in="Slides">
        <div class="slide">
            <img src="{{slide.Image}}" alt="{{slide.Title}}" />
            <h3>{{slide.Title}}</h3>
        </div>
    </ForEach>
</div>
```

### Nested Loops

```html
<!-- components/block/mega-menu.html -->
<div class="mega-menu">
    <ForEach var="category" in="Categories">
        <div class="menu-column">
            <h4>{{category.Name}}</h4>
            <ul>
                <ForEach var="link" in="category.Links">
                    <li><a href="{{link.Url}}">{{link.Title}}</a></li>
                </ForEach>
            </ul>
        </div>
    </ForEach>
</div>
```

---

## 🎰 Working with Slots

Slots allow your component to accept content from the parent template.

### Default Slot

**Component:**
```html
<!-- components/block/card.html -->
<div class="card">
    <div class="card-header">
        <h3>{{title}}</h3>
    </div>
    <div class="card-body">
        <slot />
    </div>
</div>
```

**Usage:**
```html
<Include component="block/card">
    <Param name="title" value="My Card" />
    
    <!-- This content goes into <slot /> -->
    <p>This is the card body content!</p>
    <button>Action</button>
</Include>
```

### Named Slots

**Component:**
```html
<!-- components/block/modal.html -->
<div class="modal">
    <div class="modal-header">
        <slot name="header">
            <h3>Default Title</h3>
        </slot>
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
        <h2>Custom Header Title</h2>
    </Section>
    
    <p>This is the modal body.</p>
    
    <Section slot="footer">
        <button>Cancel</button>
        <button class="primary">Save</button>
    </Section>
</Include>
```

### Slot Fallback Content

If no content is provided for a slot, the slot's inner content is used as fallback:

```html
<slot name="icon">
    <!-- This shows if parent doesn't provide icon slot -->
    <i class="default-icon"></i>
</slot>
```

---

## 🏷️ Component Types & Auto-Prefixing

Use type-specific tags for cleaner templates:

| Tag | Auto-Prefix | Use For |
|-----|-------------|---------|
| `<Element>` | `element/` | Small UI pieces (buttons, inputs) |
| `<Block>` | `block/` | Larger sections (cards, heroes) |
| `<Nav>` | `navigation/` | Menus, breadcrumbs |
| `<Data>` | `data/` | Structured data, meta |
| `<Include>` | (none) | Custom paths |

### Examples

```html
<!-- Uses element/button.html -->
<Element component="button">
    <Param name="text" value="Click" />
</Element>

<!-- Uses block/hero.html -->
<Block component="hero">
    <Param name="title" value="Welcome" />
</Block>

<!-- Uses navigation/navbar.html -->
<Nav component="navbar">
    <Param name="items" value="{{MenuItems}}" />
</Nav>

<!-- Uses data/schema.html -->
<Data component="schema">
    <Param name="type" value="Article" />
</Data>
```

---

## 🔗 Composing Components (Nesting)

Components can include other components.

### Example: Card with Button

**`components/block/product-card.html`:**
```html
<div class="product-card">
    <img src="{{image}}" alt="{{title}}" />
    <h3>{{title}}</h3>
    <p class="price">{{price | currency:"en-US"}}</p>
    
    <!-- Include another component -->
    <Element component="button">
        <Param name="text" value="Add to Cart" />
        <Param name="type" value="primary" />
        <Param name="href" value="/cart/add/{{productId}}" />
    </Element>
</div>
```

### Example: Page with Multiple Components

**`pages/home.html`:**
```html
<Element template="home">
    <!-- Header -->
    <Element component="header">
        <Param name="title" value="{{PageTitle}}" />
    </Element>
    
    <!-- Hero Section -->
    <Block component="hero">
        <Param name="title" value="Welcome to Our Site" />
        <Param name="subtitle" value="{{HeroSubtitle}}" />
    </Block>
    
    <!-- Product Grid -->
    <div class="products-grid">
        <ForEach var="product" in="Products">
            <Block component="product-card">
                <Param name="title" value="{{product.Name}}" />
                <Param name="price" value="{{product.Price}}" />
                <Param name="image" value="{{product.Image}}" />
                <Param name="productId" value="{{product.Id}}" />
            </Block>
        </ForEach>
    </div>
    
    <!-- Footer -->
    <Block component="footer" />
</Element>
```

---

## ✅ Best Practices

### 1. Keep Components Focused
Each component should do ONE thing well.

```
✅ button.html    → Just a button
✅ input.html     → Just an input
❌ form.html      → Too many responsibilities
```

### 2. Use Semantic Param Names

```html
<!-- ✅ Good -->
<Param name="title" value="..." />
<Param name="subtitle" value="..." />
<Param name="backgroundImage" value="..." />

<!-- ❌ Bad -->
<Param name="t" value="..." />
<Param name="bg" value="..." />
```

### 3. Always Handle Empty States

```html
<If condition="Items">
    <ForEach var="item" in="Items">...</ForEach>
<Else>
    <p class="empty">No items available.</p>
</If>
```

### 4. Use Defaults for Critical Params

```html
<Param name="type" value="{{ButtonType}}" default="primary" />
```

### 5. Comment Your Components

```html
<!-- 
    Component: Product Card
    Version: 1.2
    Params:
      - title (required): Product name
      - price (required): Product price (number)
      - image (optional): Product image URL
      - productId (required): For cart link
-->
<div class="product-card">...</div>
```

### 6. Keep Styling External

Components should define structure, not inline styles:

```html
<!-- ✅ Good: Use classes -->
<button class="btn btn-{{type}}">{{text}}</button>

<!-- ❌ Bad: Inline styles -->
<button style="background: blue; color: white;">{{text}}</button>
```

---

## 📋 Common Patterns & Examples

### Pattern 1: Button with Icon

```html
<!-- components/element/icon-button.html -->
<button class="btn btn-{{type}} {{class}}">
    <If condition="iconLeft">
        <i class="icon icon-{{iconLeft}}"></i>
    </If>
    <span>{{text}}</span>
    <If condition="iconRight">
        <i class="icon icon-{{iconRight}}"></i>
    </If>
</button>
```

### Pattern 2: Avatar with Fallback

```html
<!-- components/element/avatar.html -->
<div class="avatar avatar-{{size}}">
    <If condition="image">
        <img src="{{image}}" alt="{{name}}" />
    <Else>
        <span class="initials">{{initials}}</span>
    </If>
</div>
```

### Pattern 3: Responsive Image

```html
<!-- components/element/image.html -->
<picture class="responsive-image">
    <If condition="srcMobile">
        <source media="(max-width: 768px)" srcset="{{srcMobile}}" />
    </If>
    <img src="{{src}}" alt="{{alt}}" loading="{{loading}}" />
</picture>
```

### Pattern 4: Breadcrumb Navigation

```html
<!-- components/navigation/breadcrumb.html -->
<nav aria-label="Breadcrumb">
    <ol class="breadcrumb">
        <ForEach var="item" in="Items">
            <li class="breadcrumb-item">
                <If condition="item.Url">
                    <a href="{{item.Url}}">{{item.Title}}</a>
                <Else>
                    <span>{{item.Title}}</span>
                </If>
            </li>
        </ForEach>
    </ol>
</nav>
```

### Pattern 5: Recursive Menu (Using Define/Render)

```html
<!-- components/navigation/tree-menu.html -->
<Define name="menuNode">
    <li class="menu-item">
        <a href="{{node.Url}}">{{node.Title}}</a>
        <If condition="node.Children">
            <ul class="submenu">
                <ForEach var="child" in="node.Children">
                    <Render name="menuNode" node="child" />
                </ForEach>
            </ul>
        </If>
    </li>
</Define>

<nav class="tree-nav">
    <ul>
        <ForEach var="item" in="Items">
            <Render name="menuNode" node="item" />
        </ForEach>
    </ul>
</nav>
```

---

## ❓ Troubleshooting

### "Variable not found"

**Problem:** `{{SomeVar}}` shows nothing or error.

**Solutions:**
1. Check spelling of variable name
2. Verify param was passed from parent
3. Use `<If condition="SomeVar">` to check existence

### "Component not loading"

**Problem:** Component doesn't render.

**Solutions:**
1. Check file path is correct
2. Verify file extension is `.html`
3. Check `SetComponentsDirectory()` path
4. For type-specific tags, verify folder structure

### "Infinite loop error"

**Problem:** MaxLoopIterations exceeded.

**Solutions:**
1. Check your `<ForEach>` isn't looping over itself
2. Verify collection data is finite
3. Increase `MaxLoopIterations` in SecurityConfig if needed

### "Recursion depth exceeded"

**Problem:** MaxRecursionDepth error with nested components.

**Solutions:**
1. Check for circular component references
2. Increase `MaxRecursionDepth` if needed
3. Flatten component hierarchy

---

## 📄 Quick Reference Card

```
┌────────────────────────────────────────────────────────────┐
│  VARIABLES     │  {{name}}, {{obj.prop}}, {{arr[0]}}       │
├────────────────────────────────────────────────────────────┤
│  FILTERS       │  {{ val | uppercase }}, {{ val | date:"format" }} │
├────────────────────────────────────────────────────────────┤
│  CONDITIONALS  │  <If condition="...">...<Else>...</If>    │
├────────────────────────────────────────────────────────────┤
│  LOOPS         │  <ForEach var="x" in="Items">...</ForEach>│
├────────────────────────────────────────────────────────────┤
│  SLOTS         │  <slot />, <slot name="header" />         │
├────────────────────────────────────────────────────────────┤
│  TAGS          │  <Element>, <Block>, <Nav>, <Data>        │
└────────────────────────────────────────────────────────────┘
```

---

Created by **Md_Rony_Mondol** | AST Template Parser © 2026
