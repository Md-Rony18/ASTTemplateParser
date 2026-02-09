using System.Collections.Generic;

namespace ASTTemplateParser
{
    /// <summary>
    /// Base class for all AST nodes
    /// </summary>
    public abstract class AstNode
    {
        public int Position { get; set; }
        public int Line { get; set; }
        
        public abstract void Accept(IAstVisitor visitor);
    }

    /// <summary>
    /// Visitor interface for AST traversal
    /// </summary>
    public interface IAstVisitor
    {
        void Visit(RootNode node);
        void Visit(TextNode node);
        void Visit(InterpolationNode node);
        void Visit(ElementNode node);
        void Visit(DataNode node);
        void Visit(NavNode node);
        void Visit(BlockNode node);
        void Visit(IfNode node);
        void Visit(ForEachNode node);
        
        // Component System
        void Visit(IncludeNode node);
        
        // Layout System
        void Visit(LayoutNode node);
        void Visit(SectionNode node);
        void Visit(RenderSectionNode node);
        void Visit(RenderBodyNode node);
        
        // Slots
        void Visit(SlotNode node);
        
        // Template Fragments (Inline Recursion)
        void Visit(DefineNode node);
        void Visit(RenderNode node);
    }

    /// <summary>
    /// Root node containing the entire document
    /// </summary>
    public sealed class RootNode : AstNode
    {
        public List<AstNode> Children { get; } = new List<AstNode>();
        
        /// <summary>
        /// Layout to use for this template (if any)
        /// </summary>
        public string LayoutName { get; set; }
        
        /// <summary>
        /// Sections defined in this template
        /// </summary>
        public Dictionary<string, List<AstNode>> Sections { get; } = new Dictionary<string, List<AstNode>>();
        
        public override void Accept(IAstVisitor visitor) => visitor.Visit(this);
    }

    /// <summary>
    /// Plain text content
    /// </summary>
    public sealed class TextNode : AstNode
    {
        public string Content { get; set; }
        
        public TextNode(string content)
        {
            Content = content;
        }
        
        public override void Accept(IAstVisitor visitor) => visitor.Visit(this);
    }

    /// <summary>
    /// Variable interpolation: {{expression}}
    /// </summary>
    public sealed class InterpolationNode : AstNode
    {
        public string Expression { get; set; }
        
        public InterpolationNode(string expression)
        {
            Expression = expression;
        }
        
        public override void Accept(IAstVisitor visitor) => visitor.Visit(this);
    }

    /// <summary>
    /// Custom Element tag: &lt;Element ...&gt;...&lt;/Element&gt;
    /// Can be used as a component tag with auto path prefix "element/"
    /// </summary>
    public sealed class ElementNode : AstNode
    {
        public string TagName { get; set; }
        public string Attributes { get; set; }
        public List<AstNode> Children { get; } = new List<AstNode>();
        public bool IsSelfClosing { get; set; }
        
        /// <summary>
        /// Component path (relative to element/ folder when used as component)
        /// </summary>
        public string ComponentPath { get; set; }
        
        /// <summary>
        /// Unique name/identifier for the component instance (used for caching)
        /// </summary>
        public string Name { get; set; }
        public string OldName { get; set; }
        
        /// <summary>
        /// Parameters passed to the component
        /// </summary>
        public List<ParamData> Parameters { get; } = new List<ParamData>();
        
        /// <summary>
        /// Slot content to pass to the component
        /// </summary>
        public List<AstNode> SlotContent { get; } = new List<AstNode>();
        
        /// <summary>
        /// Named slots content
        /// </summary>
        public Dictionary<string, List<AstNode>> NamedSlots { get; } = new Dictionary<string, List<AstNode>>();
        
        /// <summary>
        /// Whether this Element is used as a component (has component attribute)
        /// </summary>
        public bool IsComponent => !string.IsNullOrEmpty(ComponentPath);
        
        public override void Accept(IAstVisitor visitor) => visitor.Visit(this);
    }

    /// <summary>
    /// Data tag: &lt;Data ...&gt;...&lt;/Data&gt;
    /// Can be used as a component tag with auto path prefix "data/"
    /// </summary>
    public sealed class DataNode : AstNode
    {
        public string Attributes { get; set; }
        public List<AstNode> Children { get; } = new List<AstNode>();
        
        /// <summary>
        /// Component path (relative to data/ folder when used as component)
        /// </summary>
        public string ComponentPath { get; set; }
        
        /// <summary>
        /// Unique name/identifier for the component instance (used for caching)
        /// </summary>
        public string Name { get; set; }
        public string OldName { get; set; }
        
        /// <summary>
        /// Parameters passed to the component
        /// </summary>
        public List<ParamData> Parameters { get; } = new List<ParamData>();
        
        /// <summary>
        /// Slot content to pass to the component
        /// </summary>
        public List<AstNode> SlotContent { get; } = new List<AstNode>();
        
        /// <summary>
        /// Named slots content
        /// </summary>
        public Dictionary<string, List<AstNode>> NamedSlots { get; } = new Dictionary<string, List<AstNode>>();
        
        /// <summary>
        /// Whether this Data is used as a component (has component attribute)
        /// </summary>
        public bool IsComponent => !string.IsNullOrEmpty(ComponentPath);
        
        public override void Accept(IAstVisitor visitor) => visitor.Visit(this);
    }

    /// <summary>
    /// Navigation tag: &lt;Nav ...&gt;...&lt;/Nav&gt;
    /// Can be used as a component tag with auto path prefix "navigation/"
    /// </summary>
    public sealed class NavNode : AstNode
    {
        public string Attributes { get; set; }
        public List<AstNode> Children { get; } = new List<AstNode>();
        
        /// <summary>
        /// Component path (relative to navigation/ folder when used as component)
        /// </summary>
        public string ComponentPath { get; set; }
        
        /// <summary>
        /// Unique name/identifier for the component instance (used for caching)
        /// </summary>
        public string Name { get; set; }
        public string OldName { get; set; }
        
        /// <summary>
        /// Parameters passed to the component
        /// </summary>
        public List<ParamData> Parameters { get; } = new List<ParamData>();
        
        /// <summary>
        /// Slot content to pass to the component
        /// </summary>
        public List<AstNode> SlotContent { get; } = new List<AstNode>();
        
        /// <summary>
        /// Named slots content
        /// </summary>
        public Dictionary<string, List<AstNode>> NamedSlots { get; } = new Dictionary<string, List<AstNode>>();
        
        /// <summary>
        /// Whether this Nav is used as a component (has component attribute)
        /// </summary>
        public bool IsComponent => !string.IsNullOrEmpty(ComponentPath);
        
        public override void Accept(IAstVisitor visitor) => visitor.Visit(this);
    }

    /// <summary>
    /// Block tag: &lt;Block ...&gt;...&lt;/Block&gt;
    /// Can be used as a component tag with auto path prefix "block/"
    /// </summary>
    public sealed class BlockNode : AstNode
    {
        public string Attributes { get; set; }
        public List<AstNode> Children { get; } = new List<AstNode>();
        
        /// <summary>
        /// Component path (relative to block/ folder when used as component)
        /// </summary>
        public string ComponentPath { get; set; }
        
        /// <summary>
        /// Unique name/identifier for the component instance (used for caching)
        /// </summary>
        public string Name { get; set; }
        public string OldName { get; set; }
        
        /// <summary>
        /// Parameters passed to the component
        /// </summary>
        public List<ParamData> Parameters { get; } = new List<ParamData>();
        
        /// <summary>
        /// Slot content to pass to the component
        /// </summary>
        public List<AstNode> SlotContent { get; } = new List<AstNode>();
        
        /// <summary>
        /// Named slots content
        /// </summary>
        public Dictionary<string, List<AstNode>> NamedSlots { get; } = new Dictionary<string, List<AstNode>>();
        
        /// <summary>
        /// Whether this Block is used as a component (has component attribute)
        /// </summary>
        public bool IsComponent => !string.IsNullOrEmpty(ComponentPath);
        
        public override void Accept(IAstVisitor visitor) => visitor.Visit(this);
    }

    /// <summary>
    /// Conditional block: &lt;If condition="..."&gt; ... &lt;/If&gt;
    /// </summary>
    public sealed class IfNode : AstNode
    {
        public string Condition { get; set; }
        public List<AstNode> ThenBranch { get; } = new List<AstNode>();
        public List<ElseIfBranch> ElseIfBranches { get; } = new List<ElseIfBranch>();
        public List<AstNode> ElseBranch { get; } = new List<AstNode>();
        
        public override void Accept(IAstVisitor visitor) => visitor.Visit(this);
    }

    /// <summary>
    /// Else-if branch data
    /// </summary>
    public sealed class ElseIfBranch
    {
        public string Condition { get; set; }
        public List<AstNode> Body { get; } = new List<AstNode>();
    }

    /// <summary>
    /// ForEach loop: &lt;ForEach var="item" in="collection"&gt; ... &lt;/ForEach&gt;
    /// </summary>
    public sealed class ForEachNode : AstNode
    {
        public string VariableName { get; set; }
        public string CollectionExpression { get; set; }
        public List<AstNode> Body { get; } = new List<AstNode>();
        
        public override void Accept(IAstVisitor visitor) => visitor.Visit(this);
    }

    // ============ Component System ============

    /// <summary>
    /// Parameter for component include
    /// </summary>
    public sealed class ParamData
    {
        public string Name { get; set; }
        public string Value { get; set; }
        
        /// <summary>
        /// Default value to use if Value resolves to null or empty
        /// </summary>
        public string Default { get; set; }
    }

    /// <summary>
    /// Include component: &lt;Include component="path/to/component"&gt;...&lt;/Include&gt;
    /// </summary>
    public sealed class IncludeNode : AstNode
    {
        /// <summary>
        /// Unique name/identifier for the component instance (used for caching)
        /// </summary>
        public string Name { get; set; }
        public string OldName { get; set; }
        
        /// <summary>
        /// Path to the component file (relative to components directory)
        /// </summary>
        public string ComponentPath { get; set; }
        
        /// <summary>
        /// Parameters passed to the component
        /// </summary>
        public List<ParamData> Parameters { get; } = new List<ParamData>();
        
        /// <summary>
        /// Slot content to pass to the component
        /// </summary>
        public List<AstNode> SlotContent { get; } = new List<AstNode>();
        
        /// <summary>
        /// Named slots content
        /// </summary>
        public Dictionary<string, List<AstNode>> NamedSlots { get; } = new Dictionary<string, List<AstNode>>();
        
        public override void Accept(IAstVisitor visitor) => visitor.Visit(this);
    }

    // ============ Layout System ============

    /// <summary>
    /// Layout declaration: &lt;Layout name="default"&gt;
    /// Used in page templates to specify which layout to use
    /// </summary>
    public sealed class LayoutNode : AstNode
    {
        /// <summary>
        /// Name/path of the layout file
        /// </summary>
        public string LayoutName { get; set; }
        
        /// <summary>
        /// Content of the page (body)
        /// </summary>
        public List<AstNode> Children { get; } = new List<AstNode>();
        
        public override void Accept(IAstVisitor visitor) => visitor.Visit(this);
    }

    /// <summary>
    /// Section definition: &lt;Section name="header"&gt;...&lt;/Section&gt;
    /// Defines content for a specific section in the layout
    /// </summary>
    public sealed class SectionNode : AstNode
    {
        /// <summary>
        /// Name of the section
        /// </summary>
        public string Name { get; set; }
        
        /// <summary>
        /// Content of the section
        /// </summary>
        public List<AstNode> Children { get; } = new List<AstNode>();
        
        public override void Accept(IAstVisitor visitor) => visitor.Visit(this);
    }

    /// <summary>
    /// Render section placeholder: &lt;RenderSection name="header" /&gt;
    /// Used in layouts to indicate where section content should appear
    /// </summary>
    public sealed class RenderSectionNode : AstNode
    {
        /// <summary>
        /// Name of the section to render
        /// </summary>
        public string Name { get; set; }
        
        /// <summary>
        /// Whether this section is required
        /// </summary>
        public bool Required { get; set; } = false;
        
        /// <summary>
        /// Default content if section not provided
        /// </summary>
        public List<AstNode> DefaultContent { get; } = new List<AstNode>();
        
        public override void Accept(IAstVisitor visitor) => visitor.Visit(this);
    }

    /// <summary>
    /// Render body placeholder: &lt;RenderBody /&gt;
    /// Used in layouts to indicate where page content should appear
    /// </summary>
    public sealed class RenderBodyNode : AstNode
    {
        public override void Accept(IAstVisitor visitor) => visitor.Visit(this);
    }

    // ============ Slots ============

    /// <summary>
    /// Slot definition in components: &lt;Slot name="default"&gt;...&lt;/Slot&gt;
    /// Defines where slot content should be inserted
    /// </summary>
    public sealed class SlotNode : AstNode
    {
        /// <summary>
        /// Name of the slot (default is "default")
        /// </summary>
        public string Name { get; set; } = "default";
        
        /// <summary>
        /// Default content if no slot content provided
        /// </summary>
        public List<AstNode> DefaultContent { get; } = new List<AstNode>();
        
        public override void Accept(IAstVisitor visitor) => visitor.Visit(this);
    }

    // ============ Template Fragments (Inline Recursion) ============

    /// <summary>
    /// Define a reusable template fragment: &lt;Define name="menuItem"&gt;...&lt;/Define&gt;
    /// Use with &lt;Render name="menuItem" /&gt; for inline recursion
    /// </summary>
    public sealed class DefineNode : AstNode
    {
        /// <summary>
        /// Name of the template fragment
        /// </summary>
        public string Name { get; set; }
        
        /// <summary>
        /// Content/body of the template fragment
        /// </summary>
        public List<AstNode> Body { get; } = new List<AstNode>();
        
        public override void Accept(IAstVisitor visitor) => visitor.Visit(this);
    }

    /// <summary>
    /// Render a defined template fragment: &lt;Render name="menuItem" param1="value1" /&gt;
    /// Parameters are passed as attributes
    /// </summary>
    public sealed class RenderNode : AstNode
    {
        /// <summary>
        /// Name of the template fragment to render
        /// </summary>
        public string FragmentName { get; set; }
        
        /// <summary>
        /// All attributes (including parameters to pass)
        /// </summary>
        public string Attributes { get; set; }
        
        /// <summary>
        /// Parsed parameters from attributes
        /// </summary>
        public List<ParamData> Parameters { get; } = new List<ParamData>();
        
        public override void Accept(IAstVisitor visitor) => visitor.Visit(this);
    }
}
