namespace ASTTemplateParser
{
    /// <summary>
    /// Token types for the template lexer
    /// All control structures use HTML-like syntax
    /// </summary>
    public enum TokenType
    {
        /// <summary>Plain text content</summary>
        Text,
        
        /// <summary>Variable interpolation: {{variable}}</summary>
        Interpolation,
        
        /// <summary>Element start tag: &lt;Element ...&gt;</summary>
        ElementStart,
        
        /// <summary>Element end tag: &lt;/Element&gt;</summary>
        ElementEnd,
        
        /// <summary>Self-closing element: &lt;Element .../&gt;</summary>
        ElementSelfClosing,
        
        /// <summary>Data tag start: &lt;Data ...&gt;</summary>
        DataStart,
        
        /// <summary>Data tag end: &lt;/Data&gt;</summary>
        DataEnd,
        
        /// <summary>Navigation tag start: &lt;Nav ...&gt;</summary>
        NavStart,
        
        /// <summary>Navigation tag end: &lt;/Nav&gt;</summary>
        NavEnd,
        
        /// <summary>Block tag start: &lt;Block ...&gt;</summary>
        BlockStart,
        
        /// <summary>Block tag end: &lt;/Block&gt;</summary>
        BlockEnd,
        
        /// <summary>If tag: &lt;If condition="..."&gt;</summary>
        If,
        
        /// <summary>ElseIf tag: &lt;ElseIf condition="..."&gt;</summary>
        ElseIf,
        
        /// <summary>Else tag: &lt;Else&gt;</summary>
        Else,
        
        /// <summary>End-if: &lt;/If&gt;</summary>
        EndIf,
        
        /// <summary>ForEach tag: &lt;ForEach var="item" in="collection"&gt;</summary>
        ForEach,
        
        /// <summary>End-foreach: &lt;/ForEach&gt;</summary>
        EndForEach,

        // ============ Component System ============
        
        /// <summary>Include component: &lt;Include component="path/component" /&gt;</summary>
        Include,
        
        /// <summary>Parameter for include: &lt;Param name="title" value="..." /&gt;</summary>
        Param,
        
        /// <summary>End-include: &lt;/Include&gt;</summary>
        EndInclude,

        // ============ Layout System ============
        
        /// <summary>Layout tag: &lt;Layout name="default"&gt;</summary>
        Layout,
        
        /// <summary>End-layout: &lt;/Layout&gt;</summary>
        EndLayout,
        
        /// <summary>Section definition: &lt;Section name="header"&gt;...&lt;/Section&gt;</summary>
        Section,
        
        /// <summary>End-section: &lt;/Section&gt;</summary>
        EndSection,
        
        /// <summary>Render a section: &lt;RenderSection name="header" /&gt;</summary>
        RenderSection,
        
        /// <summary>Render body content: &lt;RenderBody /&gt;</summary>
        RenderBody,

        // ============ Slots ============
        
        /// <summary>Slot definition: &lt;Slot name="default"&gt;</summary>
        Slot,
        
        /// <summary>End-slot: &lt;/Slot&gt;</summary>
        EndSlot,

        // ============ Template Fragments (Inline Recursion) ============
        
        /// <summary>Define template fragment: &lt;Define name="menuItem"&gt;</summary>
        Define,
        
        /// <summary>End-define: &lt;/Define&gt;</summary>
        EndDefine,
        
        /// <summary>Render template fragment: &lt;Render name="menuItem" /&gt;</summary>
        Render,

        // ============ Misc ============
        
        /// <summary>Opening brace for blocks</summary>
        OpenBrace,
        
        /// <summary>Closing brace for blocks</summary>
        CloseBrace,
        
        /// <summary>End of template</summary>
        EOF
    }
}
