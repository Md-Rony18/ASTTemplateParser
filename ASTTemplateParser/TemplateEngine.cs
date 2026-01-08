using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

namespace ASTTemplateParser
{
    /// <summary>
    /// Main template engine with security hardening, path validation, and cache limits
    /// </summary>
    public sealed class TemplateEngine
    {
        // Cache for compiled AST trees with LRU eviction support
        private static readonly ConcurrentDictionary<string, CacheEntry<RootNode>> _astCache =
            new ConcurrentDictionary<string, CacheEntry<RootNode>>();

        // Cache for template strings (optional file caching)
        private static readonly ConcurrentDictionary<string, TemplateCacheEntry> _templateCache =
            new ConcurrentDictionary<string, TemplateCacheEntry>();

        private static readonly object _cacheLock = new object();

        private readonly Dictionary<string, object> _variables;
        private readonly SecurityConfig _security;
        
        // Component and Layout paths
        private string _componentsDirectory;
        private string _layoutsDirectory;

        public TemplateEngine() : this(null) { }

        public TemplateEngine(SecurityConfig security)
        {
            _variables = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            _security = security ?? SecurityConfig.Default;
        }
        
        /// <summary>
        /// Sets the base directory for components
        /// </summary>
        public TemplateEngine SetComponentsDirectory(string path)
        {
            _componentsDirectory = Path.GetFullPath(path);
            return this;
        }
        
        /// <summary>
        /// Sets the base directory for layouts
        /// </summary>
        public TemplateEngine SetLayoutsDirectory(string path)
        {
            _layoutsDirectory = Path.GetFullPath(path);
            return this;
        }

        /// <summary>
        /// Sets a variable for template rendering
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TemplateEngine SetVariable(string key, object value)
        {
            // Security: Block sensitive variable names
            if (_security.BlockedPropertyNames.Contains(key))
            {
                throw new TemplateSecurityException(
                    $"Cannot set blocked variable: {key}", "BlockedVariable");
            }
            
            _variables[key] = value;
            return this;
        }

        /// <summary>
        /// Sets multiple variables at once
        /// </summary>
        public TemplateEngine SetVariables(IDictionary<string, object> variables)
        {
            foreach (var kvp in variables)
            {
                SetVariable(kvp.Key, kvp.Value);
            }
            return this;
        }

        /// <summary>
        /// Sets model as "Model" variable and extracts its properties
        /// </summary>
        public TemplateEngine SetModel(object model)
        {
            if (model != null)
            {
                _variables["Model"] = model;
                
                foreach (var prop in model.GetType().GetProperties())
                {
                    // Security: Skip sensitive properties
                    if (_security.BlockedPropertyNames.Contains(prop.Name))
                        continue;
                        
                    try
                    {
                        _variables[prop.Name] = prop.GetValue(model, null);
                    }
                    catch { }
                }
            }
            return this;
        }

        /// <summary>
        /// Renders a template string to HTML
        /// </summary>
        public string Render(string template)
        {
            if (string.IsNullOrEmpty(template))
                return string.Empty;

            // Security: Check template size
            if (template.Length > _security.MaxTemplateSize)
            {
                throw new TemplateLimitException("TemplateSize", 
                    _security.MaxTemplateSize, template.Length);
            }

            // Security: Validate template structure for security issues
            var validationResult = SecurityUtils.ValidateTemplate(template, _security);
            if (!validationResult.IsValid)
            {
                throw new TemplateSecurityException(
                    "Template failed security validation: " + string.Join("; ", validationResult.Errors),
                    "TemplateValidation");
            }

            // Enforce cache limits
            EnforceCacheLimits();

            // Get or create AST
            var cacheKey = GetTemplateHash(template);
            var cacheEntry = _astCache.GetOrAdd(cacheKey, _ => new CacheEntry<RootNode>
            {
                Value = ParseTemplate(template),
                CreatedAt = DateTime.UtcNow
            });
            
            cacheEntry.LastAccessed = DateTime.UtcNow;

            // Evaluate AST with current variables
            var evaluator = new Evaluator(_variables, _security);
            
            // Setup component loader if components directory is configured
            if (!string.IsNullOrEmpty(_componentsDirectory))
            {
                evaluator.SetComponentLoader(LoadComponent);
            }
            
            return evaluator.Evaluate(cacheEntry.Value);
        }
        
        /// <summary>
        /// Loads a component by path and returns its AST
        /// Automatically invalidates cache if file has been modified
        /// </summary>
        private RootNode LoadComponent(string componentPath)
        {
            if (string.IsNullOrEmpty(_componentsDirectory))
                return null;
                
            // Build full path
            var fullPath = Path.Combine(_componentsDirectory, componentPath);
            if (!fullPath.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
                fullPath += ".html";
                
            // Security: Ensure path is within components directory
            var normalizedPath = Path.GetFullPath(fullPath);
            if (!normalizedPath.StartsWith(_componentsDirectory, StringComparison.OrdinalIgnoreCase))
                return null;
                
            if (!File.Exists(normalizedPath))
                return null;
            
            // Get file modification time for cache validation
            var lastWriteTime = File.GetLastWriteTimeUtc(normalizedPath);
            var cacheKey = "component:" + normalizedPath.ToLowerInvariant();
            
            // Check cache - validate against file modification time
            if (_astCache.TryGetValue(cacheKey, out var cached))
            {
                // If file was modified after cache was created, invalidate cache
                if (cached.CreatedAt >= lastWriteTime)
                {
                    cached.LastAccessed = DateTime.UtcNow;
                    return cached.Value;
                }
                
                // File changed - remove from cache
                CacheEntry<RootNode> _;
                _astCache.TryRemove(cacheKey, out _);
            }
            
            // Load and parse (file is new or changed)
            var content = File.ReadAllText(normalizedPath);
            var ast = ParseTemplate(content);
            
            // Cache with current time
            _astCache[cacheKey] = new CacheEntry<RootNode>
            {
                Value = ast,
                CreatedAt = DateTime.UtcNow,
                LastAccessed = DateTime.UtcNow
            };
            
            return ast;
        }

        /// <summary>
        /// Renders a template file to HTML with path validation
        /// </summary>
        public string RenderFile(string filePath)
        {
            // Security: Validate path before loading
            ValidateFilePath(filePath);
            
            var template = LoadTemplate(filePath);
            return Render(template);
        }

        /// <summary>
        /// Validates file path against security config with MAXIMUM security
        /// </summary>
        private void ValidateFilePath(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                throw new TemplateSecurityException("File path cannot be empty", "InvalidPath");
            }

            // Security: Use comprehensive path validation
            if (!SecurityUtils.IsPathSafe(filePath, _security))
            {
                throw new TemplateSecurityException(
                    "Template path failed security validation", "PathValidation", filePath);
            }
        }

        /// <summary>
        /// Parses template into AST (can be cached for reuse)
        /// </summary>
        public static RootNode ParseTemplate(string template)
        {
            var tokenizer = new Tokenizer(template);
            var tokens = tokenizer.Tokenize();
            var parser = new Parser(tokens);
            return parser.Parse();
        }

        /// <summary>
        /// Extracts all Include name attributes from a template without rendering.
        /// NOTE: This parses the template and caches the AST, so subsequent Render() 
        /// calls will use the cached AST - NO performance penalty!
        /// Use this to get cache keys before rendering, so you can fetch cached data
        /// and call SetVariable() before the actual Render() call.
        /// </summary>
        /// <param name="template">The template string to analyze</param>
        /// <returns>List of IncludeInfo containing Name, ComponentPath, and Parameters for each Include tag</returns>
        public static List<IncludeInfo> ExtractIncludeNames(string template)
        {
            if (string.IsNullOrEmpty(template))
                return new List<IncludeInfo>();

            // Parse and cache AST (subsequent Render() will reuse this cached version)
            var cacheKey = GetTemplateHash(template);
            var cacheEntry = _astCache.GetOrAdd(cacheKey, _ => new CacheEntry<RootNode>
            {
                Value = ParseTemplate(template),
                CreatedAt = DateTime.UtcNow
            });
            cacheEntry.LastAccessed = DateTime.UtcNow;

            var result = new List<IncludeInfo>();
            ExtractIncludeNodesRecursive(cacheEntry.Value.Children, result);
            return result;
        }

        /// <summary>
        /// Prepares a template by parsing it and extracting Include names in a single operation.
        /// Returns a PreparedTemplate object that can be used for efficient rendering.
        /// This is the MOST EFFICIENT approach - parse once, render many times!
        /// </summary>
        /// <param name="template">The template string</param>
        /// <returns>PreparedTemplate containing Include info and can be passed to RenderPrepared()</returns>
        public PreparedTemplate PrepareTemplate(string template)
        {
            if (string.IsNullOrEmpty(template))
                return new PreparedTemplate { IncludeInfos = new List<IncludeInfo>() };

            // Security: Check template size
            if (template.Length > _security.MaxTemplateSize)
            {
                throw new TemplateLimitException("TemplateSize", 
                    _security.MaxTemplateSize, template.Length);
            }

            // Security: Validate template structure
            var validationResult = SecurityUtils.ValidateTemplate(template, _security);
            if (!validationResult.IsValid)
            {
                throw new TemplateSecurityException(
                    "Template failed security validation: " + string.Join("; ", validationResult.Errors),
                    "TemplateValidation");
            }

            // Enforce cache limits
            EnforceCacheLimits();

            // Parse and cache AST
            var cacheKey = GetTemplateHash(template);
            var cacheEntry = _astCache.GetOrAdd(cacheKey, _ => new CacheEntry<RootNode>
            {
                Value = ParseTemplate(template),
                CreatedAt = DateTime.UtcNow
            });
            cacheEntry.LastAccessed = DateTime.UtcNow;

            // Extract Include names
            var includeInfos = new List<IncludeInfo>();
            ExtractIncludeNodesRecursive(cacheEntry.Value.Children, includeInfos);

            return new PreparedTemplate
            {
                CacheKey = cacheKey,
                IncludeInfos = includeInfos,
                Ast = cacheEntry.Value
            };
        }

        /// <summary>
        /// Renders a prepared template. Most efficient - no reparsing needed!
        /// </summary>
        public string RenderPrepared(PreparedTemplate prepared)
        {
            if (prepared?.Ast == null)
                return string.Empty;

            var evaluator = new Evaluator(_variables, _security);
            
            if (!string.IsNullOrEmpty(_componentsDirectory))
            {
                evaluator.SetComponentLoader(LoadComponent);
            }
            
            return evaluator.Evaluate(prepared.Ast);
        }

        private static void ExtractIncludeNodesRecursive(List<AstNode> nodes, List<IncludeInfo> result)
        {
            foreach (var node in nodes)
            {
                if (node is IncludeNode includeNode)
                {
                    result.Add(new IncludeInfo
                    {
                        Name = includeNode.Name,
                        ComponentPath = includeNode.ComponentPath,
                        Parameters = includeNode.Parameters
                            .ToDictionary(p => p.Name, p => p.Value)
                    });
                    
                    // Also check slot content for nested includes
                    if (includeNode.SlotContent.Count > 0)
                        ExtractIncludeNodesRecursive(includeNode.SlotContent, result);
                    
                    foreach (var slot in includeNode.NamedSlots.Values)
                        ExtractIncludeNodesRecursive(slot, result);
                }
                else if (node is ElementNode elementNode)
                {
                    ExtractIncludeNodesRecursive(elementNode.Children, result);
                }
                else if (node is IfNode ifNode)
                {
                    ExtractIncludeNodesRecursive(ifNode.ThenBranch, result);
                    ExtractIncludeNodesRecursive(ifNode.ElseBranch, result);
                    foreach (var branch in ifNode.ElseIfBranches)
                        ExtractIncludeNodesRecursive(branch.Body, result);
                }
                else if (node is ForEachNode forEachNode)
                {
                    ExtractIncludeNodesRecursive(forEachNode.Body, result);
                }
                else if (node is DataNode dataNode)
                {
                    ExtractIncludeNodesRecursive(dataNode.Children, result);
                }
                else if (node is NavNode navNode)
                {
                    ExtractIncludeNodesRecursive(navNode.Children, result);
                }
                else if (node is BlockNode blockNode)
                {
                    ExtractIncludeNodesRecursive(blockNode.Children, result);
                }
                else if (node is SectionNode sectionNode)
                {
                    ExtractIncludeNodesRecursive(sectionNode.Children, result);
                }
                else if (node is LayoutNode layoutNode)
                {
                    ExtractIncludeNodesRecursive(layoutNode.Children, result);
                }
            }
        }

        /// <summary>
        /// Loads template with file modification caching
        /// </summary>
        private string LoadTemplate(string filePath)
        {
            var fullPath = Path.GetFullPath(filePath);
            
            // Security: Check file exists
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException("Template file not found", fullPath);
            }
            
            var lastWrite = File.GetLastWriteTimeUtc(fullPath);

            if (_templateCache.TryGetValue(fullPath, out var entry))
            {
                if (entry.LastModified == lastWrite)
                    return entry.Content;
            }

            var content = File.ReadAllText(fullPath);
            
            // Security: Check loaded content size
            if (content.Length > _security.MaxTemplateSize)
            {
                throw new TemplateLimitException("TemplateSize", 
                    _security.MaxTemplateSize, content.Length);
            }

            _templateCache[fullPath] = new TemplateCacheEntry
            {
                Content = content,
                LastModified = lastWrite
            };

            // Invalidate AST cache for this file
            var cacheKey = GetTemplateHash(content);
            CacheEntry<RootNode> _;
            _astCache.TryRemove(cacheKey, out _);

            return content;
        }

        /// <summary>
        /// Enforces cache size limits using LRU eviction
        /// </summary>
        private void EnforceCacheLimits()
        {
            if (_astCache.Count <= _security.MaxCacheEntries)
                return;

            lock (_cacheLock)
            {
                if (_astCache.Count <= _security.MaxCacheEntries)
                    return;

                // Evict oldest 20% of cache entries
                var toEvict = _astCache
                    .OrderBy(x => x.Value.LastAccessed)
                    .Take(_security.MaxCacheEntries / 5)
                    .Select(x => x.Key)
                    .ToList();

                foreach (var key in toEvict)
                {
                    CacheEntry<RootNode> _;
                    _astCache.TryRemove(key, out _);
                }
            }
        }

        /// <summary>
        /// Creates a stable hash for template caching using FNV-1a algorithm
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static string GetTemplateHash(string template)
        {
            if (template.Length < 100)
                return template;

            // FNV-1a hash - stable across app restarts, low collision rate
            unchecked
            {
                uint hash = 2166136261;
                for (int i = 0; i < template.Length; i++)
                    hash = (hash ^ template[i]) * 16777619;
                return $"{template.Length}_{hash:X8}";
            }
        }

        /// <summary>
        /// Clears all caches
        /// </summary>
        public static void ClearCaches()
        {
            _astCache.Clear();
            _templateCache.Clear();
            PropertyAccessor.ClearCache();
            Evaluator.ClearExpressionCache();
        }

        /// <summary>
        /// Gets cache statistics for monitoring
        /// </summary>
        public static CacheStatistics GetCacheStats()
        {
            return new CacheStatistics
            {
                AstCacheCount = _astCache.Count,
                TemplateCacheCount = _templateCache.Count
            };
        }

        private class TemplateCacheEntry
        {
            public string Content { get; set; }
            public DateTime LastModified { get; set; }
        }

        private class CacheEntry<T>
        {
            public T Value { get; set; }
            public DateTime CreatedAt { get; set; }
            public DateTime LastAccessed { get; set; }
        }
    }

    /// <summary>
    /// Cache statistics for monitoring
    /// </summary>
    public struct CacheStatistics
    {
        public int AstCacheCount { get; set; }
        public int TemplateCacheCount { get; set; }
    }

    /// <summary>
    /// Information about an Include tag extracted from template
    /// Use this to get cache keys before rendering
    /// </summary>
    public class IncludeInfo
    {
        /// <summary>
        /// Unique name/identifier for caching (from name attribute)
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Path to the component file
        /// </summary>
        public string ComponentPath { get; set; }

        /// <summary>
        /// Parameters passed to the component
        /// </summary>
        public Dictionary<string, string> Parameters { get; set; } = new Dictionary<string, string>();
    }

    /// <summary>
    /// A prepared template with parsed AST and extracted Include info.
    /// Use PrepareTemplate() to create this, then RenderPrepared() to render.
    /// This is the most efficient approach - parse once, render many times!
    /// </summary>
    public class PreparedTemplate
    {
        /// <summary>
        /// Cache key for this template
        /// </summary>
        public string CacheKey { get; set; }

        /// <summary>
        /// List of all Include tags with their names (cache keys)
        /// </summary>
        public List<IncludeInfo> IncludeInfos { get; set; } = new List<IncludeInfo>();

        /// <summary>
        /// Parsed AST (internal use)
        /// </summary>
        internal RootNode Ast { get; set; }
    }
}
