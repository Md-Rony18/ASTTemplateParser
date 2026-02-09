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
    /// Parses Block-based page templates and extracts block information
    /// High-performance with static caching (no file change detection for max speed)
    /// </summary>
    public class BlockParser
    {
        private readonly TemplateEngine _engine;

        // Static cache for maximum performance (shared across all instances)
        private static readonly ConcurrentDictionary<string, List<BlockInfo>> _cache = 
            new ConcurrentDictionary<string, List<BlockInfo>>(StringComparer.OrdinalIgnoreCase);

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

            // Match Block tag with all attributes
            var blockPattern = @"<Block\s+([^>]*?)>(.*?)</Block>";
            var matches = Regex.Matches(content, blockPattern, RegexOptions.Singleline | RegexOptions.IgnoreCase);

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
        /// Parses Param elements from block content
        /// </summary>
        private Dictionary<string, object> ParseParameters(string blockContent)
        {
            var parameters = new Dictionary<string, object>();

            var paramPattern = @"<Param\s+name=""([^""]+)""\s+value=""([^""]*)""\s*/>";
            var matches = Regex.Matches(blockContent, paramPattern, RegexOptions.IgnoreCase);

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
        }

        /// <summary>
        /// Clears cached block information for a specific page
        /// </summary>
        /// <param name="templatePath">Path to the template file to clear from cache</param>
        public static void ClearCache(string templatePath)
        {
            var cacheKey = templatePath.ToLowerInvariant();
            _cache.TryRemove(cacheKey, out _);
        }

        /// <summary>
        /// Gets the current cache count
        /// </summary>
        public static int CacheCount => _cache.Count;
    }
}
