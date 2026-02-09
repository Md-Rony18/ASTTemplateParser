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

        // Cache for resolved template paths to avoid repeated disk I/O
        private static readonly ConcurrentDictionary<string, string> _pathCache =
            new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private static readonly object _cacheLock = new object();

        // Global variables - shared across ALL engine instances
        private static readonly ConcurrentDictionary<string, object> _globalVariables =
            new ConcurrentDictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        // Filter Registry - shared across ALL engine instances
        public delegate object FilterDelegate(object value, string[] args);
        private static readonly ConcurrentDictionary<string, FilterDelegate> _filters =
            new ConcurrentDictionary<string, FilterDelegate>(StringComparer.OrdinalIgnoreCase);

        static TemplateEngine()
        {
            RegisterBuiltInFilters();
        }

        private static void RegisterBuiltInFilters()
        {
            // uppercase
            RegisterFilter("uppercase", (val, args) => val?.ToString()?.ToUpper());
            
            // lowercase
            RegisterFilter("lowercase", (val, args) => val?.ToString()?.ToLower());
            
            // date
            RegisterFilter("date", (val, args) => {
                if (val == null) return null;
                DateTime dt;
                if (val is DateTime d) dt = d;
                else if (DateTime.TryParse(val.ToString(), out dt)) { }
                else return val;
                
                string format = args.Length > 0 ? args[0] : "dd MMM yyyy";
                return dt.ToString(format);
            });
            
            // currency
            RegisterFilter("currency", (val, args) => {
                if (val == null) return null;
                decimal amount;
                if (val is decimal d) amount = d;
                else if (decimal.TryParse(val.ToString(), out amount)) { }
                else return val;
                
                string culture = args.Length > 0 ? args[0] : "en-US";
                try {
                    return amount.ToString("C", new System.Globalization.CultureInfo(culture));
                } catch {
                    return amount.ToString("C");
                }
            });
        }

        /// <summary>
        /// Registers a custom filter for use in templates.
        /// Example: {{ Name | shout }}
        /// TemplateEngine.RegisterFilter("shout", (val, args) => val + "!!!");
        /// </summary>
        public static void RegisterFilter(string name, FilterDelegate filter)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name));
            if (filter == null) throw new ArgumentNullException(nameof(filter));
            _filters[name] = filter;
        }

        /// <summary>
        /// Invokes a registered filter. (Internal use by Evaluator)
        /// </summary>
        internal static object InvokeFilter(string name, object value, string[] args)
        {
            if (_filters.TryGetValue(name, out var filter))
                return filter(value, args);
            return value; // Return original value if filter not found
        }

        private readonly Dictionary<string, object> _variables;
        private readonly SecurityConfig _security;
        
        // Component, Layout, and Pages paths
        private string _componentsDirectory;
        private string _layoutsDirectory;
        private string _pagesDirectory;
        
        /// <summary>
        /// Gets the configured components directory path
        /// </summary>
        public string ComponentsDirectory => _componentsDirectory;
        
        /// <summary>
        /// Gets the configured layouts directory path
        /// </summary>
        public string LayoutsDirectory => _layoutsDirectory;
        
        /// <summary>
        /// Gets the security configuration for this engine instance
        /// </summary>
        public SecurityConfig Security => _security;
        
        /// <summary>
        /// Gets the configured pages directory path
        /// </summary>
        public string PagesDirectory => _pagesDirectory;
        
        /// <summary>
        /// Callback that fires BEFORE each Include component renders.
        /// Use this to set component-specific data from cache or database.
        /// Parameters: (IncludeInfo info, TemplateEngine engine)
        /// </summary>
        private Action<IncludeInfo, TemplateEngine> _onBeforeIncludeRender;
        
        /// <summary>
        /// Callback that fires AFTER each Include component renders.
        /// Use this to wrap or modify the rendered output.
        /// Parameters: (IncludeInfo info, string renderedHtml) => string wrappedHtml
        /// </summary>
        private Func<IncludeInfo, string, string> _onAfterIncludeRender;

        public TemplateEngine() : this(null) { }

        public TemplateEngine(SecurityConfig security)
        {
            _variables = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            _security = security ?? SecurityConfig.Default;
        }
        
        /// <summary>
        /// Sets a callback that fires before each Include component renders.
        /// Use this to dynamically set data for each component.
        /// </summary>
        /// <param name="callback">Action receiving IncludeInfo (with Name, ComponentPath, Parameters) and the engine to set variables</param>
        public TemplateEngine OnBeforeIncludeRender(Action<IncludeInfo, TemplateEngine> callback)
        {
            _onBeforeIncludeRender = callback;
            return this;
        }
        
        /// <summary>
        /// Sets a callback that fires after each Include component renders.
        /// Use this to wrap or modify the rendered output (e.g., add wrapper divs, debug info).
        /// </summary>
        /// <param name="callback">Func receiving IncludeInfo and rendered HTML, returns wrapped/modified HTML</param>
        /// <example>
        /// engine.OnAfterIncludeRender((info, html) => 
        ///     $"&lt;div class=\"component\" data-name=\"{info.Name}\"&gt;{html}&lt;/div&gt;");
        /// </example>
        public TemplateEngine OnAfterIncludeRender(Func<IncludeInfo, string, string> callback)
        {
            _onAfterIncludeRender = callback;
            return this;
        }
        
        /// <summary>
        /// Gets the current OnBeforeIncludeRender callback (for internal use by Evaluator)
        /// </summary>
        public Action<IncludeInfo, TemplateEngine> GetIncludeCallback() => _onBeforeIncludeRender;
        
        /// <summary>
        /// Gets the current OnAfterIncludeRender callback (for internal use by Evaluator)
        /// </summary>
        public Func<IncludeInfo, string, string> GetAfterIncludeCallback() => _onAfterIncludeRender;
        
        /// <summary>
        /// Sets the base directory for components
        /// </summary>
        public TemplateEngine SetComponentsDirectory(string path)
        {
            _componentsDirectory = Path.GetFullPath(path);
            // Auto-add to allowed paths for security validation
            if (!_security.AllowedTemplatePaths.Contains(_componentsDirectory))
                _security.AllowedTemplatePaths.Add(_componentsDirectory);
            return this;
        }
        
        /// <summary>
        /// Sets the base directory for layouts
        /// </summary>
        public TemplateEngine SetLayoutsDirectory(string path)
        {
            _layoutsDirectory = Path.GetFullPath(path);
            // Auto-add to allowed paths for security validation
            if (!_security.AllowedTemplatePaths.Contains(_layoutsDirectory))
                _security.AllowedTemplatePaths.Add(_layoutsDirectory);
            return this;
        }
        
        /// <summary>
        /// Sets the base directory for page templates
        /// </summary>
        public TemplateEngine SetPagesDirectory(string path)
        {
            _pagesDirectory = Path.GetFullPath(path);
            // Auto-add to allowed paths for security validation
            if (!_security.AllowedTemplatePaths.Contains(_pagesDirectory))
                _security.AllowedTemplatePaths.Add(_pagesDirectory);
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
        /// Gets all variables (for internal use by Evaluator to sync callback-set variables)
        /// </summary>
        internal Dictionary<string, object> GetVariables()
        {
            return _variables;
        }

        #region Global Variables (Static - Shared Across All Instances)

        /// <summary>
        /// Sets a GLOBAL variable that persists across ALL TemplateEngine instances.
        /// Global variables are available in every template render without needing to set them again.
        /// Instance variables (SetVariable) override global variables with the same name.
        /// </summary>
        /// <param name="key">Variable name</param>
        /// <param name="value">Variable value</param>
        /// <example>
        /// // Set once at application startup
        /// TemplateEngine.SetGlobalVariable("SiteName", "My Awesome Site");
        /// TemplateEngine.SetGlobalVariable("CurrentYear", DateTime.Now.Year);
        /// 
        /// // Available in ALL templates without setting again
        /// var engine1 = new TemplateEngine();
        /// engine1.Render("{{SiteName}} - {{CurrentYear}}");  // Works!
        /// 
        /// var engine2 = new TemplateEngine();
        /// engine2.Render("Copyright {{CurrentYear}}");  // Also works!
        /// </example>
        public static void SetGlobalVariable(string key, object value)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentNullException(nameof(key));
            
            // Security: Block sensitive variable names
            if (SecurityConfig.Default.BlockedPropertyNames.Contains(key))
            {
                throw new TemplateSecurityException(
                    $"Cannot set blocked global variable: {key}", "BlockedVariable");
            }
            
            _globalVariables[key] = value;
        }

        /// <summary>
        /// Sets multiple GLOBAL variables at once.
        /// </summary>
        /// <param name="variables">Dictionary of variable names and values</param>
        public static void SetGlobalVariables(IDictionary<string, object> variables)
        {
            if (variables == null)
                throw new ArgumentNullException(nameof(variables));
            
            foreach (var kvp in variables)
            {
                SetGlobalVariable(kvp.Key, kvp.Value);
            }
        }

        /// <summary>
        /// Gets a global variable value. Returns null if not found.
        /// </summary>
        /// <param name="key">Variable name</param>
        /// <returns>Variable value or null</returns>
        public static object GetGlobalVariable(string key)
        {
            if (_globalVariables.TryGetValue(key, out var value))
                return value;
            return null;
        }

        /// <summary>
        /// Checks if a global variable exists.
        /// </summary>
        /// <param name="key">Variable name</param>
        /// <returns>True if the global variable exists</returns>
        public static bool HasGlobalVariable(string key)
        {
            return _globalVariables.ContainsKey(key);
        }

        /// <summary>
        /// Removes a specific global variable.
        /// </summary>
        /// <param name="key">Variable name to remove</param>
        /// <returns>True if the variable was removed</returns>
        public static bool RemoveGlobalVariable(string key)
        {
            return _globalVariables.TryRemove(key, out _);
        }

        /// <summary>
        /// Clears ALL global variables.
        /// </summary>
        public static void ClearGlobalVariables()
        {
            _globalVariables.Clear();
        }

        /// <summary>
        /// Gets the count of global variables currently set.
        /// </summary>
        public static int GlobalVariableCount => _globalVariables.Count;

        /// <summary>
        /// Clears ALL variables currently set in this TemplateEngine instance.
        /// Does NOT clear global variables.
        /// </summary>
        public TemplateEngine ClearVariables()
        {
            _variables.Clear();
            return this;
        }

        #endregion

        #region Render Cache (Static - High Performance Output Caching)

        /// <summary>
        /// Cache entry for rendered output with file timestamp tracking
        /// </summary>
        private class RenderCacheEntry
        {
            public string RenderedOutput { get; set; }
            public DateTime CachedAt { get; set; }
            public DateTime? ExpiresAt { get; set; }
            public string FilePath { get; set; }
            public DateTime FileLastModified { get; set; }
        }

        // Render output cache - shared across ALL engine instances
        private static readonly ConcurrentDictionary<string, RenderCacheEntry> _renderCache =
            new ConcurrentDictionary<string, RenderCacheEntry>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Computes a fast hash of all variables for data-aware caching.
        /// When variables change, the hash changes, causing a cache miss.
        /// </summary>
        private string ComputeDataHash(IDictionary<string, object> additionalVariables)
        {
            unchecked
            {
                int hash = 17;
                
                // Include global variables
                foreach (var kvp in _globalVariables.OrderBy(x => x.Key))
                {
                    hash = hash * 31 + (kvp.Key?.GetHashCode() ?? 0);
                    hash = hash * 31 + (kvp.Value?.ToString()?.GetHashCode() ?? 0);
                }
                
                // Include instance variables
                foreach (var kvp in _variables.OrderBy(x => x.Key))
                {
                    hash = hash * 31 + (kvp.Key?.GetHashCode() ?? 0);
                    hash = hash * 31 + (kvp.Value?.ToString()?.GetHashCode() ?? 0);
                }
                
                // Include additional variables
                if (additionalVariables != null)
                {
                    foreach (var kvp in additionalVariables.OrderBy(x => x.Key))
                    {
                        hash = hash * 31 + (kvp.Key?.GetHashCode() ?? 0);
                        hash = hash * 31 + (kvp.Value?.ToString()?.GetHashCode() ?? 0);
                    }
                }
                
                return hash.ToString("X8"); // 8-char hex string
            }
        }

        /// <summary>
        /// Renders a template file with automatic caching. 
        /// Cache is automatically invalidated when the file is modified.
        /// When includeDataHash is true, cache also invalidates when variables change.
        /// This is the recommended method for high-performance template rendering.
        /// </summary>
        /// <param name="filePath">Relative path to the template file</param>
        /// <param name="cacheKey">Unique cache key for this render</param>
        /// <param name="expiration">Optional cache expiration time</param>
        /// <param name="includeDataHash">If true, includes variable hash in cache key for automatic data-aware invalidation</param>
        /// <param name="additionalVariables">Optional dictionary of variables for this render</param>
        /// <returns>Rendered HTML string (from cache if available)</returns>
        /// <example>
        /// // Static content - fastest (no data hash)
        /// string about = engine.RenderCachedFile("about.html", "about");
        /// 
        /// // Data-dependent - auto invalidate when variables change
        /// engine.SetVariable("User", currentUser);
        /// string profile = engine.RenderCachedFile("profile.html", "profile", includeDataHash: true);
        /// </example>
        public string RenderCachedFile(string filePath, string cacheKey, TimeSpan? expiration = null, bool includeDataHash = false, IDictionary<string, object> additionalVariables = null)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentNullException(nameof(filePath));
            if (string.IsNullOrEmpty(cacheKey))
                throw new ArgumentNullException(nameof(cacheKey));

            // Compute effective cache key (with optional data hash)
            string effectiveCacheKey = cacheKey;
            if (includeDataHash)
            {
                var dataHash = ComputeDataHash(additionalVariables);
                effectiveCacheKey = $"{cacheKey}_{dataHash}";
            }

            // Resolve the full file path
            string baseDirectory = !string.IsNullOrEmpty(_pagesDirectory) ? _pagesDirectory : _componentsDirectory;
            if (string.IsNullOrEmpty(baseDirectory))
                throw new InvalidOperationException("No directory configured. Call SetPagesDirectory() or SetComponentsDirectory() first.");

            var fullPath = Path.Combine(baseDirectory, filePath);
            string resolvedPath = ResolveTemplatePath(fullPath);
            
            if (string.IsNullOrEmpty(resolvedPath))
                throw new FileNotFoundException($"Template not found: {filePath}");

            // Check cache with file timestamp validation
            if (_renderCache.TryGetValue(effectiveCacheKey, out var cached))
            {
                // Check if expired
                if (cached.ExpiresAt.HasValue && DateTime.UtcNow > cached.ExpiresAt.Value)
                {
                    _renderCache.TryRemove(effectiveCacheKey, out _);
                }
                // Check if file was modified
                else
                {
                    var currentModified = File.GetLastWriteTimeUtc(resolvedPath);
                    if (currentModified <= cached.FileLastModified)
                    {
                        // Cache hit! Return cached output
                        return cached.RenderedOutput;
                    }
                    // File changed - invalidate cache
                    _renderCache.TryRemove(effectiveCacheKey, out _);
                }
            }

            // Cache miss - render and cache
            ValidateFilePath(resolvedPath);
            var template = LoadTemplate(resolvedPath);
            var rendered = Render(template, additionalVariables);

            // Store in cache
            var entry = new RenderCacheEntry
            {
                RenderedOutput = rendered,
                CachedAt = DateTime.UtcNow,
                ExpiresAt = expiration.HasValue ? DateTime.UtcNow.Add(expiration.Value) : (DateTime?)null,
                FilePath = resolvedPath,
                FileLastModified = File.GetLastWriteTimeUtc(resolvedPath)
            };
            _renderCache[effectiveCacheKey] = entry;

            return rendered;
        }

        /// <summary>
        /// Renders a template string with caching.
        /// When includeDataHash is true, cache automatically invalidates when variables change.
        /// </summary>
        /// <param name="template">The template string</param>
        /// <param name="cacheKey">Unique cache key for this render</param>
        /// <param name="expiration">Optional cache expiration time</param>
        /// <param name="includeDataHash">If true, includes variable hash in cache key for automatic data-aware invalidation</param>
        /// <param name="additionalVariables">Optional dictionary of variables for this render</param>
        /// <returns>Rendered HTML string (from cache if available)</returns>
        public string RenderCached(string template, string cacheKey, TimeSpan? expiration = null, bool includeDataHash = false, IDictionary<string, object> additionalVariables = null)
        {
            if (string.IsNullOrEmpty(template))
                return string.Empty;
            if (string.IsNullOrEmpty(cacheKey))
                throw new ArgumentNullException(nameof(cacheKey));

            // Compute effective cache key (with optional data hash)
            string effectiveCacheKey = cacheKey;
            if (includeDataHash)
            {
                var dataHash = ComputeDataHash(additionalVariables);
                effectiveCacheKey = $"{cacheKey}_{dataHash}";
            }

            // Check cache
            if (_renderCache.TryGetValue(effectiveCacheKey, out var cached))
            {
                // Check if expired
                if (cached.ExpiresAt.HasValue && DateTime.UtcNow > cached.ExpiresAt.Value)
                {
                    _renderCache.TryRemove(effectiveCacheKey, out _);
                }
                else
                {
                    // Cache hit!
                    return cached.RenderedOutput;
                }
            }

            // Cache miss - render and cache
            var rendered = Render(template, additionalVariables);

            var entry = new RenderCacheEntry
            {
                RenderedOutput = rendered,
                CachedAt = DateTime.UtcNow,
                ExpiresAt = expiration.HasValue ? DateTime.UtcNow.Add(expiration.Value) : (DateTime?)null,
                FilePath = null,
                FileLastModified = DateTime.MinValue
            };
            _renderCache[effectiveCacheKey] = entry;

            return rendered;
        }

        /// <summary>
        /// Invalidates (removes) a specific cache entry by key.
        /// </summary>
        /// <param name="cacheKey">The cache key to invalidate</param>
        /// <returns>True if the cache entry was removed</returns>
        public static bool InvalidateCache(string cacheKey)
        {
            return _renderCache.TryRemove(cacheKey, out _);
        }

        /// <summary>
        /// Invalidates all cache entries whose keys start with the specified prefix.
        /// Useful for invalidating related caches (e.g., all "user-*" caches).
        /// </summary>
        /// <param name="keyPrefix">The prefix to match</param>
        /// <returns>Number of cache entries removed</returns>
        public static int InvalidateCacheByPrefix(string keyPrefix)
        {
            if (string.IsNullOrEmpty(keyPrefix))
                return 0;

            int count = 0;
            var keysToRemove = _renderCache.Keys
                .Where(k => k.StartsWith(keyPrefix, StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var key in keysToRemove)
            {
                if (_renderCache.TryRemove(key, out _))
                    count++;
            }

            return count;
        }

        /// <summary>
        /// Clears ALL render cache entries.
        /// </summary>
        public static void ClearRenderCache()
        {
            _renderCache.Clear();
        }

        /// <summary>
        /// Checks if a render cache entry exists for the specified key.
        /// </summary>
        /// <param name="cacheKey">The cache key to check</param>
        /// <returns>True if a cache entry exists (may be expired)</returns>
        public static bool HasCachedRender(string cacheKey)
        {
            return _renderCache.ContainsKey(cacheKey);
        }

        /// <summary>
        /// Gets the current count of render cache entries.
        /// </summary>
        public static int RenderCacheCount => _renderCache.Count;

        /// <summary>
        /// Gets cache statistics for monitoring.
        /// </summary>
        /// <returns>Dictionary with cache statistics</returns>
        public static IDictionary<string, object> GetRenderCacheStats()
        {
            var now = DateTime.UtcNow;
            var entries = _renderCache.Values.ToList();
            
            return new Dictionary<string, object>
            {
                { "TotalEntries", entries.Count },
                { "ExpiredEntries", entries.Count(e => e.ExpiresAt.HasValue && now > e.ExpiresAt.Value) },
                { "FileBasedEntries", entries.Count(e => !string.IsNullOrEmpty(e.FilePath)) },
                { "StringBasedEntries", entries.Count(e => string.IsNullOrEmpty(e.FilePath)) },
                { "TotalCachedBytes", entries.Sum(e => e.RenderedOutput?.Length ?? 0) * 2 } // Unicode = 2 bytes per char
            };
        }

        #endregion

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
        /// <param name="template">The template string</param>
        /// <param name="additionalVariables">Optional dictionary of variables for this specific render call</param>
        public string Render(string template, IDictionary<string, object> additionalVariables = null)
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

            // Merge variables in order of priority: Global (lowest) → Instance → Additional (highest)
            var evaluatorVars = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            
            // 1. Add global variables first (lowest priority - can be overridden)
            foreach (var kvp in _globalVariables)
            {
                evaluatorVars[kvp.Key] = kvp.Value;
            }
            
            // 2. Add instance variables (override global)
            foreach (var kvp in _variables)
            {
                evaluatorVars[kvp.Key] = kvp.Value;
            }
            
            // 3. Add additional variables (highest priority - override all)
            if (additionalVariables != null)
            {
                foreach (var kvp in additionalVariables)
                {
                    evaluatorVars[kvp.Key] = kvp.Value;
                }
            }

            // Evaluate AST with merged variables
            var evaluator = new Evaluator(evaluatorVars, _security);
            
            // Setup component loader if components directory is configured
            if (!string.IsNullOrEmpty(_componentsDirectory))
            {
                evaluator.SetComponentLoader(LoadComponent);
            }
            
            // Setup include callbacks if configured
            if (_onBeforeIncludeRender != null || _onAfterIncludeRender != null)
            {
                evaluator.SetIncludeCallback(_onBeforeIncludeRender, this, _onAfterIncludeRender);
            }
            
            return evaluator.Evaluate(cacheEntry.Value);
        }
        
        /// <summary>
        /// Loads a component by path and returns its AST
        /// Automatically invalidates cache if file has been modified
        /// Supports both file-based (block/button.html) and folder-based (block/projects/default.html) components
        /// </summary>
        private RootNode LoadComponent(string componentPath)
        {
            if (string.IsNullOrEmpty(_componentsDirectory))
                return null;
                
            // Build full path
            var fullPath = Path.Combine(_componentsDirectory, componentPath);
            
            // Try to find the component file
            string normalizedPath = null;
            
            // Option 1: Direct file path with .html extension provided
            if (fullPath.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
            {
                normalizedPath = Path.GetFullPath(fullPath);
                if (!File.Exists(normalizedPath))
                    normalizedPath = null;
            }
            else
            {
                // Option 2: Add .html extension (e.g., block/button → block/button.html)
                var withExtension = fullPath + ".html";
                var normalizedWithExt = Path.GetFullPath(withExtension);
                
                if (File.Exists(normalizedWithExt))
                {
                    normalizedPath = normalizedWithExt;
                }
                else
                {
                    // Option 3: Folder with default.html (e.g., block/projects → block/projects/default.html)
                    var folderPath = Path.GetFullPath(fullPath);
                    if (Directory.Exists(folderPath))
                    {
                        var defaultPath = Path.Combine(folderPath, "default.html");
                        if (File.Exists(defaultPath))
                        {
                            normalizedPath = defaultPath;
                        }
                    }
                }
            }
            
            // No component file found
            if (string.IsNullOrEmpty(normalizedPath))
                return null;
                
            // Security: Ensure path is within components directory
            if (!normalizedPath.StartsWith(_componentsDirectory, StringComparison.OrdinalIgnoreCase))
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
        /// Supports: file.html, folder/file, folder (uses folder/default.html)
        /// </summary>
        /// <param name="filePath">Relative path to the template file</param>
        /// <param name="additionalVariables">Optional dictionary of variables for this specific render call</param>
        /// <param name="isPage">If false (default), uses components directory. If true, uses pages directory.</param>
        public string RenderFile(string filePath, IDictionary<string, object> additionalVariables = null, bool isPage = false)
        {
            // Build full path based on directory type
            string baseDirectory;
            if (isPage)
            {
                if (string.IsNullOrEmpty(_pagesDirectory))
                    throw new InvalidOperationException("Pages directory not configured. Call SetPagesDirectory() first.");
                baseDirectory = _pagesDirectory;
            }
            else
            {
                if (string.IsNullOrEmpty(_componentsDirectory))
                    throw new InvalidOperationException("Components directory not configured. Call SetComponentsDirectory() first.");
                baseDirectory = _componentsDirectory;
            }
            
            var fullPath = Path.Combine(baseDirectory, filePath);
            
            // Resolve the actual file path (supports folder-based components)
            string resolvedPath = ResolveTemplatePath(fullPath);
            
            if (string.IsNullOrEmpty(resolvedPath))
            {
                throw new FileNotFoundException($"Template not found: {filePath}. Searched in: {fullPath}.html, {fullPath}/default.html. Base: {baseDirectory}");
            }
            
            // Security: Validate path before loading
            ValidateFilePath(resolvedPath);
            
            var template = LoadTemplate(resolvedPath);
            return Render(template, additionalVariables);
        }
        
        /// <summary>
        /// Resolves template path with folder-based component support
        /// Priority: 1) file.html, 2) file + .html, 3) folder/default.html
        /// </summary>
        private string ResolveTemplatePath(string fullPath)
        {
            if (_pathCache.TryGetValue(fullPath, out var cachedPath))
                return cachedPath;

            string resolvedPath = null;

            // Option 1: Direct file path with .html extension
            if (fullPath.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
            {
                var normalizedPath = Path.GetFullPath(fullPath);
                if (File.Exists(normalizedPath))
                    resolvedPath = normalizedPath;
            }
            
            if (resolvedPath == null)
            {
                // Option 2: Add .html extension (e.g., block/button → block/button.html)
                var withExtension = fullPath + ".html";
                var normalizedWithExt = Path.GetFullPath(withExtension);
                
                if (File.Exists(normalizedWithExt))
                {
                    resolvedPath = normalizedWithExt;
                }
                else
                {
                    // Option 3: Folder with default.html (e.g., block/projects → block/projects/default.html)
                    var folderPath = Path.GetFullPath(fullPath);
                    if (Directory.Exists(folderPath))
                    {
                        var defaultPath = Path.Combine(folderPath, "default.html");
                        if (File.Exists(defaultPath))
                        {
                            resolvedPath = defaultPath;
                        }
                    }
                }
            }

            if (resolvedPath != null)
            {
                _pathCache[fullPath] = resolvedPath;
            }
            
            return resolvedPath;
        }
        
        /// <summary>
        /// Renders a page template file to HTML (shortcut for RenderFile with isPage=true)
        /// </summary>
        /// <param name="pagePath">Relative path to the page template</param>
        /// <param name="additionalVariables">Optional dictionary of variables for this specific render call</param>
        public string RenderPage(string pagePath, IDictionary<string, object> additionalVariables = null)
        {
            return RenderFile(pagePath, additionalVariables, isPage: true);
        }

        /// <summary>
        /// Clears the resolved path cache. Call this if you add new files at runtime.
        /// </summary>
        public static void ClearPathCache()
        {
            _pathCache.Clear();
        }
        
        /// <summary>
        /// Renders a block component with automatic callback support
        /// Fires OnBeforeIncludeRender before rendering and OnAfterIncludeRender after
        /// </summary>
        /// <param name="block">BlockInfo from BlockParser</param>
        /// <returns>Rendered HTML string</returns>
        public string RenderBlock(BlockInfo block)
        {
            if (block == null)
                throw new ArgumentNullException(nameof(block));
            
            // Build component path
            var componentPath = $"block/{block.ComponentPath}";
            
            // Fire OnBeforeIncludeRender callback
            if (_onBeforeIncludeRender != null)
            {
                var includeInfo = new IncludeInfo
                {
                    Name = block.Name,
                    OldName = block.Name,
                    ComponentPath = componentPath,
                    ComponentType = "block",
                    Parameters = new Dictionary<string, string>()
                };
                
                // Copy parameters - convert object to string
                foreach (var p in block.Parameters)
                {
                    includeInfo.Parameters[p.Key] = p.Value?.ToString() ?? string.Empty;
                }
                
                _onBeforeIncludeRender(includeInfo, this);
            }
            
            // Set variables from block parameters
            foreach (var param in block.Parameters)
            {
                SetVariable(param.Key, param.Value);
            }
            
            // Render the block component
            var result = RenderFile(componentPath);
            
            // Fire OnAfterIncludeRender callback
            if (_onAfterIncludeRender != null)
            {
                var includeInfo = new IncludeInfo
                {
                    Name = block.Name,
                    OldName = block.Name,
                    ComponentPath = componentPath,
                    ComponentType = "block",
                    Parameters = new Dictionary<string, string>()
                };
                
                foreach (var p in block.Parameters)
                {
                    includeInfo.Parameters[p.Key] = p.Value?.ToString() ?? string.Empty;
                }
                
                result = _onAfterIncludeRender(includeInfo, result);
            }
            
            return result;
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
        /// <param name="prepared">The prepared template object</param>
        /// <param name="additionalVariables">Optional dictionary of variables for this specific render call</param>
        public string RenderPrepared(PreparedTemplate prepared, IDictionary<string, object> additionalVariables = null)
        {
            if (prepared?.Ast == null)
                return string.Empty;

            // Merge variables in order of priority: Global (lowest) → Instance → Additional (highest)
            var evaluatorVars = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            
            // 1. Add global variables first (lowest priority - can be overridden)
            foreach (var kvp in _globalVariables)
            {
                evaluatorVars[kvp.Key] = kvp.Value;
            }
            
            // 2. Add instance variables (override global)
            foreach (var kvp in _variables)
            {
                evaluatorVars[kvp.Key] = kvp.Value;
            }
            
            // 3. Add additional variables (highest priority - override all)
            if (additionalVariables != null)
            {
                foreach (var kvp in additionalVariables)
                {
                    evaluatorVars[kvp.Key] = kvp.Value;
                }
            }

            var evaluator = new Evaluator(evaluatorVars, _security);
            
            if (!string.IsNullOrEmpty(_componentsDirectory))
            {
                evaluator.SetComponentLoader(LoadComponent);
            }
            
            // Setup include callbacks if configured
            if (_onBeforeIncludeRender != null || _onAfterIncludeRender != null)
            {
                evaluator.SetIncludeCallback(_onBeforeIncludeRender, this, _onAfterIncludeRender);
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
                        OldName = includeNode.OldName,
                        ComponentPath = includeNode.ComponentPath,
                        ComponentType = "include",
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
                    if (elementNode.IsComponent)
                    {
                        // Type-specific component with "element/" prefix
                        result.Add(new IncludeInfo
                        {
                            Name = elementNode.Name,
                            OldName = elementNode.OldName,
                            ComponentPath = $"element/{elementNode.ComponentPath}",
                            ComponentType = "element",
                            Parameters = elementNode.Parameters
                                .ToDictionary(p => p.Name, p => p.Value)
                        });
                        
                        // Check slot content for nested includes
                        if (elementNode.SlotContent.Count > 0)
                            ExtractIncludeNodesRecursive(elementNode.SlotContent, result);
                        
                        foreach (var slot in elementNode.NamedSlots.Values)
                            ExtractIncludeNodesRecursive(slot, result);
                    }
                    else
                    {
                        ExtractIncludeNodesRecursive(elementNode.Children, result);
                    }
                }
                else if (node is DataNode dataNode)
                {
                    if (dataNode.IsComponent)
                    {
                        // Type-specific component with "data/" prefix
                        result.Add(new IncludeInfo
                        {
                            Name = dataNode.Name,
                            OldName = dataNode.OldName,
                            ComponentPath = $"data/{dataNode.ComponentPath}",
                            ComponentType = "data",
                            Parameters = dataNode.Parameters
                                .ToDictionary(p => p.Name, p => p.Value)
                        });
                        
                        // Check slot content for nested includes
                        if (dataNode.SlotContent.Count > 0)
                            ExtractIncludeNodesRecursive(dataNode.SlotContent, result);
                        
                        foreach (var slot in dataNode.NamedSlots.Values)
                            ExtractIncludeNodesRecursive(slot, result);
                    }
                    else
                    {
                        ExtractIncludeNodesRecursive(dataNode.Children, result);
                    }
                }
                else if (node is NavNode navNode)
                {
                    if (navNode.IsComponent)
                    {
                        // Type-specific component with "navigation/" prefix
                        result.Add(new IncludeInfo
                        {
                            Name = navNode.Name,
                            OldName = navNode.OldName,
                            ComponentPath = $"navigation/{navNode.ComponentPath}",
                            ComponentType = "navigation",
                            Parameters = navNode.Parameters
                                .ToDictionary(p => p.Name, p => p.Value)
                        });
                        
                        // Check slot content for nested includes
                        if (navNode.SlotContent.Count > 0)
                            ExtractIncludeNodesRecursive(navNode.SlotContent, result);
                        
                        foreach (var slot in navNode.NamedSlots.Values)
                            ExtractIncludeNodesRecursive(slot, result);
                    }
                    else
                    {
                        ExtractIncludeNodesRecursive(navNode.Children, result);
                    }
                }
                else if (node is BlockNode blockNode)
                {
                    if (blockNode.IsComponent)
                    {
                        // Type-specific component with "block/" prefix
                        result.Add(new IncludeInfo
                        {
                            Name = blockNode.Name,
                            OldName = blockNode.OldName,
                            ComponentPath = $"block/{blockNode.ComponentPath}",
                            ComponentType = "block",
                            Parameters = blockNode.Parameters
                                .ToDictionary(p => p.Name, p => p.Value)
                        });
                        
                        // Check slot content for nested includes
                        if (blockNode.SlotContent.Count > 0)
                            ExtractIncludeNodesRecursive(blockNode.SlotContent, result);
                        
                        foreach (var slot in blockNode.NamedSlots.Values)
                            ExtractIncludeNodesRecursive(slot, result);
                    }
                    else
                    {
                        ExtractIncludeNodesRecursive(blockNode.Children, result);
                    }
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
        /// Previous name/identifier for the component instance (from oldname attribute)
        /// </summary>
        public string OldName { get; set; }

        /// <summary>
        /// Path to the component file
        /// </summary>
        public string ComponentPath { get; set; }
        
        /// <summary>
        /// Type of component: "element", "block", "data", "navigation", or "include"
        /// </summary>
        public string ComponentType { get; set; } = "include";

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
