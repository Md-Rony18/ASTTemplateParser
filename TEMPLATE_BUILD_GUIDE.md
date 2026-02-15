# 📘 AST Template Build Guide
> Complete documentation for building CMS themes with ASTTemplateParser

---

## 📑 Table of Contents

1. [Overview](#-overview)
2. [Directory Structure](#-directory-structure)
3. [ThemeSetting.json](#-themesettingjson)
4. [Master Layout](#-master-layout-defaultcshtml)
5. [Component Architecture](#-component-architecture-4-layer-system)
   - [Element Components](#layer-1-element--atomic-ui-elements)
   - [Data Components](#layer-2-data--data-loop-templates)
   - [Block Components](#layer-3-block--section-blocks)
   - [Navigation Components](#layer-4-navigation--nav-components)
6. [Page Templates](#-page-templates)
7. [Data Templates](#-data-templates-cms-dynamic-pages)
8. [Template Tags Reference](#-template-tags-reference)
9. [Variable Reference](#-variable-reference)
10. [Naming Conventions](#-naming-conventions)
11. [Step-by-Step: Build a New Theme](#-step-by-step-build-a-new-theme)
12. [Best Practices](#-best-practices)

---

## 🏗 Overview

The AST Template System uses a **4-layer component hierarchy** to build CMS-ready website themes:

```
Page Template  →  Composes Blocks
Block          →  Composes Elements + Data
Data           →  Renders dynamic CMS content via ForEach loops
Element        →  Smallest atomic HTML unit (h1, img, button, etc.)
```

Each layer is self-contained, reusable, and CMS-editable.

---

## 📁 Directory Structure

```
ThemeName/
├── ThemeSetting.json              ← Theme configuration
├── layouts/
│   └── default.cshtml             ← Master HTML layout (Razor)
├── assets/
│   ├── css/                       ← Stylesheets
│   ├── js/                        ← JavaScript files
│   ├── img/                       ← Images
│   └── fonts/                     ← Font files
├── components/
│   ├── element/                   ← Atomic UI elements
│   │   ├── Title.html
│   │   ├── header.html
│   │   ├── semiTitle.html
│   │   ├── subTitle.html
│   │   ├── content.html
│   │   ├── button.html
│   │   ├── img.html
│   │   └── icon.html
│   ├── data/                      ← Data rendering templates
│   │   ├── slider.html
│   │   ├── blog.html
│   │   ├── blog/
│   │   │   ├── featured.html
│   │   │   ├── listing.html
│   │   │   └── recent.html
│   │   ├── event.html
│   │   ├── team/
│   │   │   ├── default.html
│   │   │   └── standard.html
│   │   ├── counter.html
│   │   ├── gallery.html
│   │   ├── project.html
│   │   ├── testimonial.html
│   │   └── brand.html
│   ├── block/                     ← Section-level blocks
│   │   ├── header/default.html
│   │   ├── slider.html
│   │   ├── about/
│   │   │   ├── default.html
│   │   │   └── standard.html
│   │   ├── blog/
│   │   │   ├── default.html       ← 3-item preview
│   │   │   ├── featured.html      ← Hero-style featured
│   │   │   ├── listing.html       ← Full grid + pagination
│   │   │   └── details.html       ← Single item detail view
│   │   ├── events/default.html
│   │   ├── teams/
│   │   │   ├── default.html
│   │   │   └── standard.html
│   │   ├── counters/default.html
│   │   ├── gallery/default.html
│   │   ├── testimonial/default.html
│   │   ├── donations/default.html
│   │   ├── projects/default.html
│   │   ├── cta/default.html
│   │   ├── contact/default.html
│   │   ├── brand/default.html
│   │   ├── whatwe/default.html
│   │   └── map/default.html
│   └── navigation/                ← Navigation components
│       ├── header/default.html    ← Top bar
│       ├── main/default.html      ← Main menu + mobile menu
│       └── footer/default.html    ← Footer
└── page template/                 ← Page-level templates
    ├── home.html
    ├── about.html
    ├── contact.html
    ├── project.html
    ├── team.html
    ├── dynamic/
    │   └── dynamic.html
    └── data/                      ← CMS data page templates
        ├── News/
        │   ├── indexes/
        │   │   └── default.html
        │   ├── details/
        │   │   └── default.html
        │   ├── categories/
        │   │   └── default.html
        │   └── tags/
        │       └── default.html
        ├── Blog/
        ├── Events/
        ├── Product/
        ├── Service/
        ├── Brand/
        ├── People/
        └── FAQ/
```

---

## ⚙ ThemeSetting.json

Theme configuration file — defines metadata, variants, pages, and demo menus.

```json
{
  "Name": "ThemeName",
  "Category": "Non-Profit",
  "ThemeTypes": [
    { "Name": "default", "IsDefault": true },
    { "Name": "standard", "IsDefault": false },
    { "Name": "premium", "IsDefault": false }
  ],
  "DemoMenuItems": [
    {
      "Name": "Team",
      "Icon": "",
      "Title": "CMS - Team",
      "Href": "/team/team"
    }
  ],
  "Pages": [
    { "Name": "Home", "Order": 1 },
    { "Name": "About", "Order": 2 },
    { "Name": "Contact", "Order": 3 }
  ]
}
```

| Field | Purpose |
|---|---|
| `Name` | Theme identifier |
| `Category` | Theme category (Non-Profit, Business, etc.) |
| `ThemeTypes` | Available style variants |
| `DemoMenuItems` | Pre-configured menu items for demo |
| `Pages` | Default pages created with theme |

---

## 🎨 Master Layout (default.cshtml)

The outer HTML shell wrapping all pages. Uses **Razor syntax**.

```html
<!doctype html>
<html class="no-js" lang="">
<head>
    <meta charset="utf-8">
    <title>@Site.Name</title>
    <meta name="viewport" content="width=device-width, initial-scale=1">
    <link rel="shortcut icon" href="/assets/img/logo/favicon.png">

    <!-- CSS -->
    <link rel="stylesheet" href="/assets/css/bootstrap.min.css">
    <link rel="stylesheet" href="/assets/css/style.css">
    <link rel="stylesheet" href="/assets/css/custom.css">
    @Html.RenderThemeStyle()
</head>
<body>
    @Html.PageDesigner()

    <header id="top-menu">
        @Nav.TopHeader
        @Nav.MainHeader
    </header>

    <main>
        @RenderBody()
        @Html.CTA("cta","cta-area theme-bg2 pt-50 pb-50")
    </main>

    @Nav.MainFooter

    @Html.InitialBaseModal()

    <!-- JS -->
    <script src="/assets/js/vendor/jquery-1.12.4.min.js"></script>
    <script src="/assets/js/bootstrap.min.js"></script>
    <script src="/assets/js/main.js"></script>
    @Html.InitialInEditorScript()
</body>
</html>
```

### Razor Tags Reference

| Tag | Purpose |
|---|---|
| `@Site.Name` | Dynamic site name |
| `@Html.RenderThemeStyle()` | Injects theme variant CSS |
| `@Html.PageDesigner()` | Visual page designer integration |
| `@Nav.TopHeader` | Top header navigation component |
| `@Nav.MainHeader` | Main navigation component |
| `@RenderBody()` | Page template content renders here |
| `@Html.CTA("cta","...")` | Global CTA section |
| `@Nav.MainFooter` | Footer navigation component |
| `@Html.InitialBaseModal()` | CMS editor modals |
| `@Html.InitialInEditorScript()` | CMS editor scripts |

---

## 🧩 Component Architecture (4-Layer System)

### Layer 1: `<Element>` — Atomic UI Elements

The smallest reusable HTML primitives. Located in `components/element/`.

#### Available Elements

**Title.html**
```html
<h1 class="edit-able inline-editor-text {{class}}" {{elementAttributes}}>{{content}}</h1>
```

**header.html**
```html
<h1 class="edit-able inline-editor-text {{class}}" {{elementAttributes}}>{{content}}</h1>
```

**semiTitle.html** (h5-style)
```html
<h5 class="edit-able inline-editor-text {{class}}" {{elementAttributes}}>{{content}}</h5>
```

**subTitle.html** (with separator)
```html
<span class="edit-able inline-editor-text semi-title {{class}}" {{elementAttributes}}>{{content}}</span>
```

**content.html** (paragraph)
```html
<p class="edit-able inline-editor-text {{class}}" {{elementAttributes}}>{{content}}</p>
```

**button.html**
```html
<a class="edit-able {{class}}" {{elementAttributes}}>
   {{text}} <span><i class="{{icon}}" id="{{identity}}"></i></span>
</a>
```

**img.html**
```html
<img class="edit-able {{class}}" src="{{imageurl}}" alt="{{altText}}" {{elementAttributes}}>
```

**icon.html**
```html
<i class="edit-able {{class}}" {{elementAttributes}}></i>
```

#### How to Call Elements

From a Block component:
```html
<Element component="header" name="unique_name_GUID_un" oldname="unique_name_GUID_un">
    <Param name="content" value="{{element.Content}}" default="Fallback Text" />
    <Param name="class" value="custom-class {{element.Class}}" />
    <Param name="elementAttributes" value="type=text" />
</Element>
```

#### Key Element Parameters

| Param | Purpose | Used In |
|---|---|---|
| `content` | Text content | Title, header, semiTitle, subTitle, content |
| `text` | Button text | button |
| `class` | CSS classes | All |
| `imageurl` | Image source | img |
| `altText` | Image alt text | img |
| `icon` | Icon class | button, icon |
| `elementAttributes` | CMS editing attributes | All |
| `identity` | Element ID | button |

#### `elementAttributes` Values

| Value | Purpose |
|---|---|
| `type=text` | Inline text editing |
| `type=html` | Rich HTML editing |
| `type=image` | Image upload/change |
| `type=button` | Button editing |
| `type=submit` | Submit button |
| `loading=lazy` | Lazy loading |
| `isicon=true` | Show icon |

---

### Layer 2: `<Data>` — Data Loop Templates

Templates that render CMS data using `<ForEach>` loops. Located in `components/data/`.

#### Example: `data/event.html`
```html
<div class="row justify-content-center">
    <ForEach var="item" in="Data.Items">
        <div class="col-xl-4 col-lg-4 col-md-6 wow fadeInUp2 animated" data-wow-delay='.3s'>
            <div class="blog blogs white-bg mb-30">
                <div class="blog__thumb mb-40">
                    <a href="{{item.FullPath}}" data-type="image"
                       data-id="{{item.Id}}" class="inline-data-editing"
                       data-property="ImagePath">
                        <img src="{{item.ImagePath}}" width="370" height="247"
                             alt="{{item.Title}}" loading="lazy">
                    </a>
                </div>
                <div class="blog__content px-4 pb-4">
                    <h3 class="blog-title mb-15">
                        <a href="{{item.FullPath}}" class="line-clamp-2">
                            {{item.Title}}
                        </a>
                    </h3>
                    <p class="mb-25 line-clamp-3">{{item.Description}}</p>
                    <ul class="blog-author">
                        <li>
                            <i class="far fa-calendar-alt"></i>
                            {{item.PublishDate | date:"MMMM dd, yyyy"}}
                        </li>
                    </ul>
                </div>
            </div>
        </div>
    </ForEach>
</div>
```

#### Key Patterns in Data Components

| Pattern | Syntax | Example |
|---|---|---|
| Loop | `<ForEach var="item" in="Data.Items">` | Iterate CMS items |
| Property | `{{item.PropertyName}}` | `{{item.Title}}` |
| Filter | `{{item.Prop \| filter:"format"}}` | `{{item.PublishDate \| date:"MMMM dd, yyyy"}}` |
| Indexer | `{{item.ImagePaths[0]}}` | Array access |
| Link | `{{item.FullPath}}` | Auto-generated URL |
| CMS Edit | `class="inline-data-editing"` | Inline editing marker |
| Image Edit | `data-type="image" data-property="ImagePath"` | Image property binding |

#### Variant Data Components

Use subdirectories for variants:
```
data/team/
├── default.html     ← Card layout (2 columns)
└── standard.html    ← Grid layout (4 columns)
```

---

### Layer 3: `<Block>` — Section Blocks

Full page sections that compose Elements + Data. Located in `components/block/`.

#### Block Structure Pattern
```html
<!--section-name-area start-->
<section class="edit-able {{sectionClass}}" {{attributes}} id="element-{{element.identity}}">
    <div class="container">
        <!-- Section Header (static — Elements) -->
        <div class="row justify-content-center">
            <div class="col-xl-8">
                <div class="section-title text-center mb-85">
                    <Element component="subtitle" name="subtitle_GUID_un" oldname="subtitle_GUID_un">
                        <Param name="content" value="{{element.Content}}" default="Section Label" />
                        <Param name="elementAttributes" value="type=text" />
                    </Element>
                    <Element component="header" name="header_GUID_un" oldname="header_GUID_un">
                        <Param name="content" value="{{element.Content}}" default="Section Title" />
                        <Param name="elementAttributes" value="type=text" />
                    </Element>
                </div>
            </div>
        </div>

        <!-- Section Content (dynamic — Data) -->
        <Data component="dataComponentName" name="DataName_GUID_un" oldname="DataName_GUID_un">
            <Param name="name" value="DataName"/>
            <Param name="datatype" value="CMSDataType" />
            <Param name="properties" value="Title,ImagePath,Description" />
            <Param name="take" value="3" />
            <Param name="sorting" value="recent" />
        </Data>
    </div>
</section>
<!--section-name-area end-->
```

#### Block `<Data>` Parameters

| Param | Purpose | Example Values |
|---|---|---|
| `name` | Data query identifier | `"Blog"`, `"Team"` |
| `datatype` | CMS content type | `"News"`, `"People"`, `"Product"`, `"Service"`, `"Banner"` |
| `properties` | Fields to fetch | `"Title,ImagePath,Description,PublishDate"` |
| `take` | Number of items | `"3"`, `"4"`, `"9"` |
| `sorting` | Sort order | `"recent"` |
| `flag` | Content flag filter | `"slider"` |
| `pagination` | Enable pagination | `"true"` |
| `skip` | Items to skip | `"1"` |

#### Block Variants

Use subdirectories for variants:
```
block/blog/
├── default.html     ← 3-item preview (for home/about pages)
├── featured.html    ← Hero-style single featured item
├── listing.html     ← Full grid with pagination
└── details.html     ← Single item detail view + sidebar
```

#### How Blocks Are Called (from Page Templates)
```html
<Block component="blog" name="blog_GUID_un" oldName="blog_GUID_un">
    <Param name="sectionClass" value="blog-area grey-bg2 pt-130 pb-100"/>
    <Param name="iscustomstyle" value="true"/>
</Block>

<!-- Call variant with path -->
<Block component="blog/featured" name="featured_GUID_un" oldName="featured_GUID_un">
    <Param name="sectionClass" value="featured-news-area pt-100 pb-70"/>
    <Param name="iscustomstyle" value="true"/>
</Block>
```

---

### Layer 4: Navigation — Nav Components

Located in `components/navigation/`. Used in layout via `@Nav.*` Razor tags.

| File | Razor Tag | Purpose |
|---|---|---|
| `header/default.html` | `@Nav.TopHeader` | Top bar (phone, email, social) |
| `main/default.html` | `@Nav.MainHeader` | Main menu + logo + mobile menu |
| `footer/default.html` | `@Nav.MainFooter` | Footer with links & contact |

#### Recursive Menu Pattern (in `main/default.html`)
```html
<!-- Menu rendering -->
<nav>
    <ul>
        <ForEach var="item" in="Nav.Main">
            <Render name="menuItem" menuNode="item" />
        </ForEach>
    </ul>
</nav>

<!-- Recursive template definition -->
<Define name="menuItem">
    <li>
        <a href="{{menuNode.Item.Href}}">
            {{menuNode.Item.Name}}
            <If condition="menuNode.HasChildren">
                <i class="far fa-chevron-down"></i>
            </If>
        </a>
        <If condition="menuNode.HasChildren">
            <ul class="submenu">
                <ForEach var="child" in="menuNode.Children">
                    <Render name="menuItem" menuNode="child" />
                </ForEach>
            </ul>
        </If>
    </li>
</Define>
```

---

## 📄 Page Templates

Located in `page template/`. Compose `<Block>` components only — no raw HTML.

### Example: `home.html`
```html
<Block component="slider" name="slider_GUID_un" oldName="slider_GUID_un">
    <Param name="iscustomstyle" value="true"/>
    <Param name="flag" value="slider"/>
</Block>
<Block component="about" name="about_GUID_un" oldName="about_GUID_un">
    <Param name="sectionClass" value="about-area grey-bg2 pos-rel pt-100 pb-100"/>
    <Param name="iscustomstyle" value="true"/>
</Block>
<Block component="projects" name="projects_GUID_un" oldName="projects_GUID_un">
    <Param name="sectionClass" value="events-grid-area pt-100 pb-90"/>
    <Param name="iscustomstyle" value="true"/>
</Block>
<Block component="events" name="events_GUID_un" oldName="events_GUID_un">
    <Param name="sectionClass" value="blog-area grey-bg2 pt-130 pb-100"/>
    <Param name="iscustomstyle" value="true"/>
</Block>
<Block component="counters" name="counters_GUID_un" oldName="counters_GUID_un">
    <Param name="sectionClass" value="counter-area theme-bg pt-130 pb-60"/>
    <Param name="iscustomstyle" value="true"/>
</Block>
<Block component="teams" name="team_GUID_un" oldName="team_GUID_un">
    <Param name="sectionClass" value="team-area pt-125 pb-100 pos-rel"/>
    <Param name="iscustomstyle" value="true"/>
    <Param name="style" value="background-image: url(assets/img/bg/01.jpg);"/>
</Block>
<Block component="gallery" name="gallery_GUID_un" oldName="gallery_GUID_un">
    <Param name="sectionClass" value="gallery-area pt-130 pb-100"/>
    <Param name="iscustomstyle" value="true"/>
</Block>
<Block component="testimonial" name="testimonial_GUID_un" oldName="testimonial_GUID_un">
    <Param name="sectionClass" value="testimonial-area grey-bg2 pos-rel pt-130 pb-130"/>
    <Param name="iscustomstyle" value="true"/>
    <Param name="flag" value="slider-item"/>
</Block>
<Block component="donations" name="donation_GUID_un" oldName="donation_GUID_un">
    <Param name="sectionClass" value="donation-area pos-rel pt-125 pb-90"/>
    <Param name="iscustomstyle" value="true"/>
    <Param name="style" value="background-image: url(assets/img/bg/02.jpg);"/>
</Block>
```

### Common Block Parameters

| Param | Purpose | Example |
|---|---|---|
| `sectionClass` | Section CSS classes | `"blog-area grey-bg2 pt-130 pb-100"` |
| `iscustomstyle` | Enable custom styling | `"true"` |
| `style` | Inline CSS styles | `"background-image: url(...);"` |
| `flag` | Content flag | `"slider"` |

### Page Templates Reference

| Page | Blocks (in order) |
|---|---|
| **home.html** | slider → about → projects → cta → events → counters → teams → gallery → testimonial → donations |
| **about.html** | header → about/standard → counters → whatwe → teams/standard → donations → blog |
| **contact.html** | header → contact |
| **project.html** | header → projects |
| **team.html** | header → teams/standard |

---

## 📊 Data Templates (CMS Dynamic Pages)

Located in `page template/data/`. The CMS routing system auto-resolves these templates based on URL.

### Directory Structure (per data type)
```
page template/data/{DataType}/
├── indexes/
│   └── default.html        ← List page (all items)
├── details/
│   └── default.html        ← Single item page
├── categories/
│   └── default.html        ← Items filtered by category
└── tags/
    └── default.html         ← Items filtered by tag
```

### URL → Template Mapping
```
/news/                        → data/News/indexes/default.html
/news/article-slug            → data/News/details/default.html
/news/category/charity        → data/News/categories/default.html
/news/tag/education           → data/News/tags/default.html
```

### Index Page Design — `indexes/default.html`
```html
<!-- ① Page Header -->
<Block component="header" name="header_news_idx_un" oldName="header_news_idx_un">
    <Param name="iscustomstyle" value="true"/>
    <Param name="sectionClass" value="{{block.Class}} page-title-area"/>
    <Param name="style" value="background-image: url(assets/img/bg/08.jpg);"/>
</Block>

<!-- ② Featured News (hero-style, 1 item) -->
<Block component="blog/featured" name="featured_news_idx_un" oldName="featured_news_idx_un">
    <Param name="sectionClass" value="featured-news-area pt-100 pb-70"/>
    <Param name="iscustomstyle" value="true"/>
</Block>

<!-- ③ News Grid Listing (remaining items + pagination) -->
<Block component="blog/listing" name="blog_listing_idx_un" oldName="blog_listing_idx_un">
    <Param name="sectionClass" value="blog-area grey-bg2 pt-130 pb-100"/>
    <Param name="iscustomstyle" value="true"/>
</Block>

<!-- ④ CTA -->
<Block component="cta" name="cta_news_idx_un" oldName="cta_news_idx_un">
    <Param name="sectionClass" value="cta-area theme-bg2 pt-50 pb-50"/>
    <Param name="iscustomstyle" value="true"/>
</Block>
```

### Details Page Design — `details/default.html`
```html
<!-- ① Page Header -->
<Block component="header" name="header_news_dtl_un" oldName="header_news_dtl_un">
    <Param name="iscustomstyle" value="true"/>
    <Param name="sectionClass" value="{{block.Class}} page-title-area"/>
    <Param name="style" value="background-image: url(assets/img/bg/08.jpg);"/>
</Block>

<!-- ② News Detail (content + sidebar) -->
<Block component="blog/details" name="blog_dtl_content_un" oldName="blog_dtl_content_un">
    <Param name="sectionClass" value="events-details-area pt-125 pb-120"/>
    <Param name="iscustomstyle" value="true"/>
</Block>

<!-- ③ Related News (3-item grid, reuse existing block!) -->
<Block component="blog" name="related_news_dtl_un" oldName="related_news_dtl_un">
    <Param name="sectionClass" value="blog-area grey-bg2 pt-130 pb-100"/>
    <Param name="iscustomstyle" value="true"/>
</Block>

<!-- ④ Stats -->
<Block component="counters" name="counters_news_dtl_un" oldName="counters_news_dtl_un">
    <Param name="sectionClass" value="counter-area theme-bg pt-130 pb-60"/>
    <Param name="iscustomstyle" value="true"/>
</Block>

<!-- ⑤ Donation CTA -->
<Block component="donations" name="donation_news_dtl_un" oldName="donation_news_dtl_un">
    <Param name="sectionClass" value="donation-area pos-rel pt-125 pb-90"/>
    <Param name="iscustomstyle" value="true"/>
    <Param name="style" value="background-image: url(assets/img/bg/02.jpg);"/>
</Block>
```

### Data Context Variables

| Template Type | Available Variables |
|---|---|
| **indexes** | `Data.Items` (list), pagination objects |
| **details** | `Data.Item` (single object) — e.g., `Data.Item.Title`, `Data.Item.ImagePath` |
| **categories** | `Data.Items` (filtered by category) |
| **tags** | `Data.Items` (filtered by tag) |

---

## 📋 Template Tags Reference

### Component Tags

| Tag | Syntax | Purpose |
|---|---|---|
| `<Block>` | `<Block component="name" name="id" oldName="id">` | Include a section block |
| `<Element>` | `<Element component="name" name="id" oldname="id">` | Include an atomic element |
| `<Data>` | `<Data component="name" name="id" oldname="id">` | Include a data loop template |
| `<Param>` | `<Param name="key" value="val" default="fallback"/>` | Pass parameter to component |

### Logic Tags

| Tag | Syntax | Purpose |
|---|---|---|
| `<ForEach>` | `<ForEach var="item" in="Data.Items">` | Loop over collection |
| `<If>` | `<If condition="expr">` | Conditional rendering |
| `<Define>` | `<Define name="templateName">` | Define reusable template fragment |
| `<Render>` | `<Render name="templateName" param="value" />` | Render a defined template |
| `<Include>` | `<Include src="path"/>` | Include external file |

### Expression Syntax

| Syntax | Example | Purpose |
|---|---|---|
| `{{variable}}` | `{{item.Title}}` | Variable interpolation |
| `{{obj.Prop}}` | `{{Data.Item.ImagePath}}` | Property access |
| `{{arr[index]}}` | `{{item.ImagePaths[0]}}` | Indexer/array access |
| `{{val \| filter}}` | `{{item.Date \| date:"MMM dd"}}` | Template filter |
| `{{block.Class}}` | `{{block.Class}}` | Block-level variable |
| `{{element.Content}}` | `{{element.Content}}` | Element-level variable |

### Available Filters

| Filter | Syntax | Output |
|---|---|---|
| `date` | `{{val \| date:"MMMM dd, yyyy"}}` | `February 15, 2026` |
| `date` | `{{val \| date:"dd MMM yyyy"}}` | `15 Feb 2026` |
| `date` | `{{val \| date:"MMM dd, yyyy"}}` | `Feb 15, 2026` |

---

## 📌 Variable Reference

### Layout Level (Razor)

| Variable | Source |
|---|---|
| `@Site.Name` | Site name from CMS |
| `@Nav.TopHeader` | Top navigation component |
| `@Nav.MainHeader` | Main navigation component |
| `@Nav.MainFooter` | Footer component |
| `@Nav.Main` | Main menu items collection |

### Block Level

| Variable | Purpose |
|---|---|
| `{{sectionClass}}` | CSS classes from Param |
| `{{attributes}}` | HTML attributes |
| `{{element.identity}}` | Block's unique ID |
| `{{element.Content}}` | CMS-editable content |
| `{{element.Class}}` | CMS-editable CSS class |
| `{{element.ImageUrl}}` | CMS-editable image |
| `{{block.Class}}` | Block's CSS class |
| `{{block.Identity}}` | Block's identity |
| `{{flag}}` | Content flag value |
| `{{iscustomstyle}}` | Custom style toggle |
| `{{style}}` | Inline style value |

### Data Loop Level

| Variable | Purpose |
|---|---|
| `{{item.Title}}` | Item title |
| `{{item.Description}}` | Item description |
| `{{item.ImagePath}}` | Item image URL |
| `{{item.FullPath}}` | Item's auto-generated URL |
| `{{item.Id}}` | Item's unique ID |
| `{{item.PublishDate}}` | Publish date |
| `{{item.Author_Text}}` | Author name |
| `{{item.Designation_Text}}` | Person's designation |
| `{{item.Category_Text}}` | Category name |
| `{{item.ImagePaths[n]}}` | Additional images (indexer) |

### Detail Page Level

| Variable | Purpose |
|---|---|
| `{{Data.Item.Title}}` | Single item's title |
| `{{Data.Item.Description}}` | Single item's description |
| `{{Data.Item.ImagePath}}` | Single item's image |
| `{{Data.Item.ImagePaths[0]}}` | Additional images |

### Navigation Level

| Variable | Purpose |
|---|---|
| `{{menuNode.Item.Href}}` | Menu item URL |
| `{{menuNode.Item.Name}}` | Menu item name |
| `{{menuNode.HasChildren}}` | Has sub-menu items |
| `{{menuNode.Children}}` | Child menu items |

---

## 🏷 Naming Conventions

### Component Names (name/oldName attributes)

Format: `descriptiveName_GUID_un`

```
Examples:
  slider_c2e8e446c39647fd902934e7e1ff3678_un
  about_us_header_5ba8452a3eec4dc8b783d44a83399ead_un
  blog_aa6e1533c000489e83b15e78dc2f9025_un
```

| Part | Purpose |
|---|---|
| `descriptiveName` | Human-readable identifier |
| `GUID` | Unique ID (32 hex chars) |
| `_un` | Suffix marker |

### File Naming

| Convention | Example |
|---|---|
| Block variants | `block/about/default.html`, `block/about/standard.html` |
| Data variants | `data/team/default.html`, `data/team/standard.html` |
| Elements | `element/Title.html`, `element/button.html` |
| Pages | `page template/home.html`, `page template/about.html` |

### CSS Class Conventions

| Class | Purpose |
|---|---|
| `edit-able` | Marks element for CMS inline editing |
| `inline-editor-text` | Text editing marker |
| `inline-data-editing` | Data item editing marker |
| `wow fadeInUp2 animated` | Scroll animation |
| `theme-bg`, `theme-bg2` | Theme color backgrounds |
| `grey-bg2` | Grey background |
| `pt-*`, `pb-*` | Padding top/bottom |

---

## 🚀 Step-by-Step: Build a New Theme

### Step 1: Create Directory Structure
```
NewTheme/
├── ThemeSetting.json
├── layouts/default.cshtml
├── assets/ (css, js, img, fonts)
├── components/ (element, data, block, navigation)
└── page template/ (pages + data)
```

### Step 2: Configure ThemeSetting.json
Define theme name, category, variants, pages.

### Step 3: Build Elements (`components/element/`)
Create atomic HTML elements: Title, header, content, button, img, etc.
Each element must include `edit-able` class and `{{elementAttributes}}`.

### Step 4: Build Data Components (`components/data/`)
Create data rendering templates with `<ForEach>` loops.
Each data template iterates `Data.Items` and renders individual cards/items.

### Step 5: Build Block Components (`components/block/`)
Create section blocks that compose Elements (static) + Data (dynamic).
Follow the standard pattern: section header + data content.

### Step 6: Build Navigation (`components/navigation/`)
Create header, main menu (with recursive `<Define>`/`<Render>`), and footer.

### Step 7: Build Master Layout (`layouts/default.cshtml`)
Wire up CSS/JS, navigation, and `@RenderBody()`.

### Step 8: Build Page Templates (`page template/`)
Compose pages using `<Block>` calls only. Start with `home.html`.

### Step 9: Build Data Templates (`page template/data/`)
Create indexes, details, categories, tags templates for each CMS data type.

### Step 10: Add Assets
Add CSS, JavaScript, images, and fonts to `assets/`.

---

## ✅ Best Practices

### Component Design
- ✅ Every editable element must have `edit-able` class
- ✅ Use `default` attribute in `<Param>` for fallback values
- ✅ Add `loading="lazy"` to all images
- ✅ Use `wow fadeInUp2 animated` for scroll animations
- ✅ Keep Element components as small atomic units

### Naming
- ✅ Use descriptive names with GUID suffix: `about_us_header_GUID_un`
- ✅ Always include both `name` and `oldname` attributes
- ✅ Use consistent casing (`oldName` in page templates, `oldname` in components)

### Page Templates
- ✅ Only use `<Block>` calls — no raw HTML in page templates
- ✅ Always include `iscustomstyle` param
- ✅ Use `sectionClass` for spacing and background classes
- ✅ Reuse existing blocks whenever possible

### Data Templates
- ✅ Use `inline-data-editing` class on data items
- ✅ Add `data-type`, `data-id`, `data-property` attributes for CMS editing
- ✅ Use `line-clamp-2`/`line-clamp-3` for text truncation
- ✅ Use template filters for date formatting

### Performance
- ✅ Limit `take` values (3-9 items per section)
- ✅ Specify image `width` and `height` attributes
- ✅ Use `loading="lazy"` on images
- ✅ Minimize CSS/JS files

### Data Type Reference

| CMS DataType | Common Properties |
|---|---|
| `News` | Title, Description, ImagePath, PublishDate, Author_Text |
| `People` | Title, Designation_Text, ImagePath, Description |
| `Product` | Title, Description, ImagePath |
| `Service` | Title, Counter_Text |
| `Banner` | Title, Description, ImagePath, Button1Text, Button2Text |
| `Brand` | ImagePath |

---

## 📐 Architecture Diagram

```
┌──────────────────────────────────────────────────────────┐
│                   default.cshtml (Layout)                │
│  ┌──────────────────────────────────────────────────┐    │
│  │ @Nav.TopHeader    → navigation/header/default    │    │
│  │ @Nav.MainHeader   → navigation/main/default      │    │
│  │                                                  │    │
│  │ @RenderBody() ──→ Page Template (home.html)      │    │
│  │   │                                              │    │
│  │   ├─ <Block component="slider">                  │    │
│  │   │    └─ block/slider.html                      │    │
│  │   │         └─ <Data component="slider">         │    │
│  │   │              └─ data/slider.html              │    │
│  │   │                   └─ <ForEach> {{item.*}}     │    │
│  │   │                                              │    │
│  │   ├─ <Block component="about">                   │    │
│  │   │    └─ block/about/default.html               │    │
│  │   │         ├─ <Element component="img">          │    │
│  │   │         │    └─ element/img.html              │    │
│  │   │         ├─ <Element component="header">       │    │
│  │   │         │    └─ element/header.html           │    │
│  │   │         └─ <Element component="button">       │    │
│  │   │              └─ element/button.html           │    │
│  │   │                                              │    │
│  │   ├─ <Block component="blog">                    │    │
│  │   │    └─ block/blog/default.html                │    │
│  │   │         ├─ <Element> (subtitle, header)       │    │
│  │   │         └─ <Data component="blog">            │    │
│  │   │              └─ data/blog.html                │    │
│  │   │                   └─ <ForEach> {{item.*}}     │    │
│  │   └─ ...more blocks                              │    │
│  │                                                  │    │
│  │ @Nav.MainFooter   → navigation/footer/default    │    │
│  └──────────────────────────────────────────────────┘    │
└──────────────────────────────────────────────────────────┘
```

---

> 📌 **Tip**: When creating a new section, always start bottom-up:
> 1. Create the **Element** (if new atomic element needed)
> 2. Create the **Data** component (ForEach rendering)
> 3. Create the **Block** (section wrapper with Elements + Data)
> 4. Add the **Block call** to your page template
