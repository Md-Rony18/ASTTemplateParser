using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace ASTTemplateParser
{
    /// <summary>
    /// Block information extracted from page template
    /// Save this to your database
    /// </summary>
    public class BlockInfo
    {
        /// <summary>
        /// Unique identifier for this block
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Block name (e.g., "slider", "about")
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Previous block name (for tracking renames)
        /// </summary>
        public string OldName { get; set; }

        /// <summary>
        /// Component path for rendering (e.g., "slider", "block/about")
        /// Use with engine.RenderFile(ComponentPath)
        /// </summary>
        public string ComponentPath { get; set; }

        /// <summary>
        /// Display order (0, 1, 2, ...)
        /// </summary>
        public int Order { get; set; }

        /// <summary>
        /// Page identifier
        /// </summary>
        public string PageName { get; set; }

        /// <summary>
        /// Block parameters - save as JSON in database
        /// Compatible with engine.SetVariables()
        /// </summary>
        public Dictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// A segment in a page template - can be either a Block or raw HTML
    /// </summary>
    public class TemplateSegment
    {
        /// <summary>
        /// Segment type: "block" or "html"
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// Display order (0, 1, 2, ...)
        /// </summary>
        public int Order { get; set; }

        /// <summary>
        /// Block information (only when Type == "block")
        /// </summary>
        public BlockInfo Block { get; set; }

        /// <summary>
        /// Raw HTML content (only when Type == "html")
        /// </summary>
        public string RawHtml { get; set; }

        /// <summary>
        /// Whether this segment is a block
        /// </summary>
        public bool IsBlock { get { return Type == "block"; } }

        /// <summary>
        /// Whether this segment is raw HTML
        /// </summary>
        public bool IsHtml { get { return Type == "html"; } }
    }

    /// <summary>
    /// Parses Block-based page templates and extracts block information
    /// High-performance with static caching (no file change detection for max speed)
    /// </summary>
    public class BlockParser
    {
        private readonly TemplateEngine _engine;

        // Static cache for maximum performance (shared across all instances)
        private static readonly ConcurrentDictionary<string, List<BlockInfo>> _cache = 
            new ConcurrentDictionary<string, List<BlockInfo>>(StringComparer.OrdinalIgnoreCase);

        // Static cache for segments (blocks + raw HTML)
        private static readonly ConcurrentDictionary<string, List<TemplateSegment>> _segmentCache = 
            new ConcurrentDictionary<string, List<TemplateSegment>>(StringComparer.OrdinalIgnoreCase);

        // Pre-compiled regex for best performance (compiled once, used many times)
        private static readonly Regex _blockRegex = new Regex(
            @"<Block\s+([^>]*?)>(.*?)</Block>",
            RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex _paramRegex = new Regex(
            @"<Param\s+name=""([^""]+)""\s+value=""([^""]*)""\s*/>",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// Creates BlockParser without TemplateEngine (use full path in ParseBlocks)
        /// </summary>
        public BlockParser() : this(null) { }

        /// <summary>
        /// Creates BlockParser with TemplateEngine (uses PagesDirectory automatically)
        /// </summary>
        /// <param name="engine">TemplateEngine with configured PagesDirectory</param>
        public BlockParser(TemplateEngine engine)
        {
            _engine = engine;
        }

        /// <summary>
        /// Parses page template and extracts block information
        /// Uses PagesDirectory from TemplateEngine
        /// Results are cached for best performance
        /// </summary>
        /// <param name="pageName">Page name (e.g., "home" → pages/home.html)</param>
        /// <returns>List of BlockInfo to save in database</returns>
        public List<BlockInfo> ParseBlocks(string pageName)
        {
            if (_engine == null)
            {
                throw new InvalidOperationException("TemplateEngine not configured. Use BlockParser(engine) constructor or ParseBlocks(fullPath, pageName) overload.");
            }

            if (string.IsNullOrEmpty(_engine.PagesDirectory))
            {
                throw new InvalidOperationException("PagesDirectory not configured. Call engine.SetPagesDirectory() first.");
            }

            // Build full path from pages directory
            var templatePath = Path.Combine(_engine.PagesDirectory, pageName);
            if (!templatePath.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
            {
                templatePath += ".html";
            }

            // Security: prevent path traversal
            ValidatePath(templatePath, _engine.PagesDirectory);

            return ParseBlocksFromFile(templatePath, pageName);
        }

        /// <summary>
        /// Parses template file and extracts block information
        /// Results are cached for best performance
        /// </summary>
        /// <param name="templatePath">Full path to template file</param>
        /// <param name="pageName">Page identifier for database</param>
        /// <returns>List of BlockInfo to save in database</returns>
        public List<BlockInfo> ParseBlocks(string templatePath, string pageName)
        {
            return ParseBlocksFromFile(templatePath, pageName);
        }

        /// <summary>
        /// Internal method to parse blocks from file with caching
        /// </summary>
        private List<BlockInfo> ParseBlocksFromFile(string templatePath, string pageName)
        {
            // Check cache first (best performance - no file I/O)
            var cacheKey = templatePath.ToLowerInvariant();
            if (_cache.TryGetValue(cacheKey, out var cached))
            {
                return cached;
            }

            // Not in cache - parse file
            if (!File.Exists(templatePath))
            {
                throw new FileNotFoundException($"Template not found: {templatePath}");
            }

            var content = File.ReadAllText(templatePath);
            var blocks = ParseBlocksFromContent(content, pageName);

            // Store in cache
            _cache[cacheKey] = blocks;

            return blocks;
        }

        /// <summary>
        /// Parses template content and extracts block information
        /// </summary>
        /// <param name="content">Template content as string</param>
        /// <param name="pageName">Page identifier for database</param>
        /// <returns>List of BlockInfo to save in database</returns>
        public List<BlockInfo> ParseBlocksFromContent(string content, string pageName)
        {
            var blocks = new List<BlockInfo>();

            // Match Block tag with all attributes (using pre-compiled regex)
            var matches = _blockRegex.Matches(content);

            int order = 0;
            foreach (Match match in matches)
            {
                var attributes = match.Groups[1].Value;
                var blockContent = match.Groups[2].Value;

                // Extract individual attributes
                string component = ExtractAttribute(attributes, "component");
                string name = ExtractAttribute(attributes, "name");
                string oldNameAttr = ExtractAttribute(attributes, "oldname");
                string oldName = string.IsNullOrEmpty(oldNameAttr) ? name : oldNameAttr;

                if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(component))
                    continue;

                // Security: validate component path (no path traversal)
                if (component.Contains("..") || component.Contains(":"))
                    continue;

                var block = new BlockInfo
                {
                    Name = name,
                    OldName = oldName,
                    ComponentPath = component,
                    Order = order++,
                    PageName = pageName,
                    Parameters = ParseParameters(blockContent)
                };

                blocks.Add(block);
            }

            return blocks;
        }

        /// <summary>
        /// Parses page template into ordered segments (blocks + raw HTML)
        /// This preserves raw HTML between blocks in correct order
        /// Results are cached for best performance
        /// </summary>
        /// <param name="pageName">Page name (e.g., "home" → pages/home.html)</param>
        /// <returns>List of TemplateSegment in order</returns>
        public List<TemplateSegment> ParseTemplateSegments(string pageName)
        {
            if (_engine == null)
            {
                throw new InvalidOperationException("TemplateEngine not configured. Use BlockParser(engine) constructor.");
            }

            if (string.IsNullOrEmpty(_engine.PagesDirectory))
            {
                throw new InvalidOperationException("PagesDirectory not configured. Call engine.SetPagesDirectory() first.");
            }

            var templatePath = Path.Combine(_engine.PagesDirectory, pageName);
            if (!templatePath.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
            {
                templatePath += ".html";
            }

            // Security: prevent path traversal
            ValidatePath(templatePath, _engine.PagesDirectory);

            // Check cache first (no file I/O)
            var cacheKey = templatePath.ToLowerInvariant();
            if (_segmentCache.TryGetValue(cacheKey, out var cached))
            {
                return cached;
            }

            if (!File.Exists(templatePath))
            {
                throw new FileNotFoundException("Template not found: " + templatePath);
            }

            var content = File.ReadAllText(templatePath);
            var segments = ParseSegmentsFromContent(content, pageName);

            // Store in cache
            _segmentCache[cacheKey] = segments;

            return segments;
        }

        /// <summary>
        /// Parses template content into ordered segments (blocks + raw HTML)
        /// </summary>
        /// <param name="content">Template content as string</param>
        /// <param name="pageName">Page identifier</param>
        /// <returns>List of TemplateSegment in order</returns>
        public List<TemplateSegment> ParseSegmentsFromContent(string content, string pageName)
        {
            var segments = new List<TemplateSegment>();
            // Use pre-compiled regex
            var matches = _blockRegex.Matches(content);

            int order = 0;
            int lastEnd = 0;

            foreach (Match match in matches)
            {
                // Capture raw HTML before this block
                if (match.Index > lastEnd)
                {
                    var rawHtml = content.Substring(lastEnd, match.Index - lastEnd).Trim();
                    if (!string.IsNullOrEmpty(rawHtml))
                    {
                        segments.Add(new TemplateSegment
                        {
                            Type = "html",
                            Order = order++,
                            RawHtml = rawHtml
                        });
                    }
                }

                // Parse block
                var attributes = match.Groups[1].Value;
                var blockContent = match.Groups[2].Value;

                string component = ExtractAttribute(attributes, "component");
                string name = ExtractAttribute(attributes, "name");
                string oldNameAttr = ExtractAttribute(attributes, "oldname");
                string oldName = string.IsNullOrEmpty(oldNameAttr) ? name : oldNameAttr;

                if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(component)
                    && !component.Contains("..") && !component.Contains(":")) // Security check
                {
                    var block = new BlockInfo
                    {
                        Name = name,
                        OldName = oldName,
                        ComponentPath = component,
                        Order = order++,
                        PageName = pageName,
                        Parameters = ParseParameters(blockContent)
                    };

                    segments.Add(new TemplateSegment
                    {
                        Type = "block",
                        Order = order - 1,
                        Block = block
                    });
                }

                lastEnd = match.Index + match.Length;
            }

            // Capture trailing raw HTML after last block
            if (lastEnd < content.Length)
            {
                var trailing = content.Substring(lastEnd).Trim();
                if (!string.IsNullOrEmpty(trailing))
                {
                    segments.Add(new TemplateSegment
                    {
                        Type = "html",
                        Order = order++,
                        RawHtml = trailing
                    });
                }
            }

            return segments;
        }

        /// <summary>
        /// Parses Param elements from block content
        /// </summary>
        private Dictionary<string, object> ParseParameters(string blockContent)
        {
            var parameters = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

            // Use pre-compiled regex
            var matches = _paramRegex.Matches(blockContent);

            foreach (Match match in matches)
            {
                parameters[match.Groups[1].Value] = match.Groups[2].Value;
            }

            return parameters;
        }

        /// <summary>
        /// Extracts an attribute value from an attribute string
        /// </summary>
        private string ExtractAttribute(string attributes, string attributeName)
        {
            // Match: attributeName="value" or attributeName='value'
            var pattern = $@"{attributeName}\s*=\s*[""']([^""']*)[""']";
            var match = Regex.Match(attributes, pattern, RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value : null;
        }

        /// <summary>
        /// Clears all cached block information
        /// Call this when templates are updated
        /// </summary>
        public static void ClearCache()
        {
            _cache.Clear();
            _segmentCache.Clear();
        }

        /// <summary>
        /// Clears cached block information for a specific page
        /// </summary>
        /// <param name="templatePath">Path to the template file to clear from cache</param>
        public static void ClearCache(string templatePath)
        {
            var cacheKey = templatePath.ToLowerInvariant();
            _cache.TryRemove(cacheKey, out _);
            _segmentCache.TryRemove(cacheKey, out _);
        }

        /// <summary>
        /// Gets the current cache count
        /// </summary>
        public static int CacheCount => _cache.Count;

        /// <summary>
        /// Gets the current segment cache count
        /// </summary>
        public static int SegmentCacheCount => _segmentCache.Count;

        /// <summary>
        /// Validates that the resolved path is within the allowed base directory
        /// Prevents path traversal attacks (e.g., ../../etc/passwd)
        /// </summary>
        private static void ValidatePath(string resolvedPath, string baseDirectory)
        {
            var fullResolved = Path.GetFullPath(resolvedPath);
            var fullBase = Path.GetFullPath(baseDirectory);

            if (!fullResolved.StartsWith(fullBase, StringComparison.OrdinalIgnoreCase))
            {
                throw new UnauthorizedAccessException(
                    "Access denied: template path is outside the allowed directory.");
            }
        }
    }
}
