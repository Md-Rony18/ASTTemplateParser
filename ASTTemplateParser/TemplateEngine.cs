using System;
using System.Collections;
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

        // Cache for raw file content used by ReadFile helper
        private static readonly ConcurrentDictionary<string, CacheEntry<string>> _fileCache =
            new ConcurrentDictionary<string, CacheEntry<string>>(StringComparer.OrdinalIgnoreCase);

        private static readonly object _cacheLock = new object();

        // Component version counter - incremented when any component file changes
        // Used by RenderCachedFile to detect stale cache entries when included components are modified
        private static long _componentVersion = 0;

        // App-level global variables - shared across ALL engine instances and ALL websites
        private static readonly ConcurrentDictionary<string, object> _globalVariables =
            new ConcurrentDictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        // Global variables version - incremented when any global variable is changed
        // Used by ComputeDataHash to detect when cached renders become stale due to global changes
        private static long _globalVariablesVersion = 0;

        // Website-scoped global variables - isolated per website ID
        // Key: websiteId, Value: that website's global variables
        private static readonly ConcurrentDictionary<int, ConcurrentDictionary<string, object>> _websiteGlobals =
            new ConcurrentDictionary<int, ConcurrentDictionary<string, object>>();

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

            // attr filter: outputs ' name="value"' only if value is not null/empty
            // Usage: <div {{ MyClass | attr:"class" }}>
            RegisterFilter("attr", (val, args) => {
                if (val == null || string.IsNullOrWhiteSpace(val.ToString())) return string.Empty;
                string attrName = args.Length > 0 ? args[0] : "data";
                return $" {attrName}=\"{val}\"";
            });

            // raw filter: bypasses HTML encoding
            RegisterFilter("raw", (val, args) => val == null ? null : new RawString(val.ToString()));
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
        
        // Dirty flag for data hash - avoids recomputing hash when variables haven't changed
        private bool _variablesDirty = true;
        private string _lastDataHash;
        
        // Website ID for website-scoped globals
        private int _websiteId;
        private ConcurrentDictionary<string, object> _myWebsiteGlobals;
        private long _lastGlobalVariablesVersion = -1;
        
        // Component, Layout, and Pages paths
        private string _componentsDirectory;
        private string _layoutsDirectory;
        private string _pagesDirectory;

        // Local fallback/override paths (per-engine instance)
        // Used in conjunction with SetLocalComponentsDirectory() / SetLocalPagesDirectory()
        private string _localComponentsDirectory;
        private string _localPagesDirectory;
        private bool _preferGlobalDirectory = false; // default: local path has priority
        
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
        /// Gets the configured local components directory path
        /// </summary>
        public string LocalComponentsDirectory => _localComponentsDirectory;

        /// <summary>
        /// Gets the configured local pages directory path
        /// </summary>
        public string LocalPagesDirectory => _localPagesDirectory;

        /// <summary>
        /// Gets whether the global directory takes priority over the local directory
        /// </summary>
        public bool PreferGlobalDirectory => _preferGlobalDirectory;
        
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
            // Each engine gets its own SecurityConfig to prevent thread-safety issues
            // when mutating AllowedTemplatePaths from SetComponentsDirectory/SetLayoutsDirectory/SetPagesDirectory
            _security = security ?? new SecurityConfig();
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
        /// Sets a local components directory path.
        /// By default (preferGlobal=false), this path takes PRIORITY over SetComponentsDirectory().
        /// Use SetDirectoryPriority(preferGlobal: true) to reverse the lookup order.
        /// </summary>
        /// <example>
        /// engine.SetComponentsDirectory(website.GetComponentPath());   // global/theme path
        /// engine.SetLocalComponentsDirectory(localThemePath);           // local/override path
        /// // Default: localThemePath is checked first
        /// </example>
        public TemplateEngine SetLocalComponentsDirectory(string path)
        {
            _localComponentsDirectory = Path.GetFullPath(path);
            if (!_security.AllowedTemplatePaths.Contains(_localComponentsDirectory))
                _security.AllowedTemplatePaths.Add(_localComponentsDirectory);
            return this;
        }

        /// <summary>
        /// Sets a local pages directory path.
        /// By default (preferGlobal=false), this path takes PRIORITY over SetPagesDirectory().
        /// Use SetDirectoryPriority(preferGlobal: true) to reverse the lookup order.
        /// </summary>
        public TemplateEngine SetLocalPagesDirectory(string path)
        {
            _localPagesDirectory = Path.GetFullPath(path);
            if (!_security.AllowedTemplatePaths.Contains(_localPagesDirectory))
                _security.AllowedTemplatePaths.Add(_localPagesDirectory);
            return this;
        }

        /// <summary>
        /// Sets the directory lookup priority for dual-path resolution.
        /// preferGlobal=false (default): Local path is checked FIRST, global path is the fallback.
        /// preferGlobal=true:  Global path (SetComponentsDirectory/SetPagesDirectory) is checked FIRST, local is the fallback.
        /// </summary>
        /// <example>
        /// // Default — local overrides global
        /// engine.SetDirectoryPriority(preferGlobal: false);
        ///
        /// // Global has precedence, local is fallback
        /// engine.SetDirectoryPriority(preferGlobal: true);
        /// </example>
        public TemplateEngine SetDirectoryPriority(bool preferGlobal)
        {
            _preferGlobalDirectory = preferGlobal;
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
            _variablesDirty = true; // Mark hash as stale
            return this;
        }

        /// <summary>
        /// Gets all variables (for internal use by Evaluator to sync callback-set variables)
        /// </summary>
        internal Dictionary<string, object> GetVariables()
        {
            return _variables;
        }

        #region Website-Scoped Engine Configuration

        /// <summary>
        /// Sets the website ID for this engine instance.
        /// This determines which website-scoped globals are used during rendering.
        /// </summary>
        public TemplateEngine SetWebsiteId(int websiteId)
        {
            _websiteId = websiteId;
            _myWebsiteGlobals = _websiteGlobals.GetOrAdd(websiteId,
                _ => new ConcurrentDictionary<string, object>(StringComparer.OrdinalIgnoreCase));
            return this;
        }

        /// <summary>
        /// Gets the current website ID for this engine instance.
        /// </summary>
        public int WebsiteId => _websiteId;

        /// <summary>
        /// Gets the count of websites that have scoped globals.
        /// </summary>
        public static int WebsiteGlobalCount => _websiteGlobals.Count;

        /// <summary>
        /// Clears website-scoped globals for ALL websites.
        /// </summary>
        public static void ClearAllWebsiteGlobals()
        {
            _websiteGlobals.Clear();
        }

        #endregion

        #region Global Variables (App-Wide or Website-Scoped)

        /// <summary>
        /// Sets a global variable. 
        /// Without websiteId: app-wide global, available in ALL websites.
        /// With websiteId: website-scoped global, available only for that specific website.
        /// </summary>
        /// <param name="key">Variable name</param>
        /// <param name="value">Variable value</param>
        /// <param name="websiteId">Optional. If provided, variable is scoped to this website only.</param>
        /// <example>
        /// // App-wide global (all websites)
        /// TemplateEngine.SetGlobalVariable("CurrentYear", DateTime.Now.Year);
        /// 
        /// // Website-scoped global (only this website)
        /// TemplateEngine.SetGlobalVariable("website", websiteViewModel, websiteId: 5);
        /// TemplateEngine.SetGlobalVariable("owner", ownerViewModel, websiteId: 5);
        /// </example>
        public static void SetGlobalVariable(string key, object value, int websiteId = 0)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentNullException(nameof(key));
            
            if (SecurityConfig.Default.BlockedPropertyNames.Contains(key))
            {
                throw new TemplateSecurityException(
                    $"Cannot set blocked global variable: {key}", "BlockedVariable");
            }

            if (websiteId > 0)
            {
                var websiteVars = _websiteGlobals.GetOrAdd(websiteId,
                    _ => new ConcurrentDictionary<string, object>(StringComparer.OrdinalIgnoreCase));
                websiteVars[key] = value;
            }
            else
            {
                _globalVariables[key] = value;
            }

            // Increment version to invalidate all data-aware caches
            System.Threading.Interlocked.Increment(ref _globalVariablesVersion);
        }

        /// <summary>
        /// Sets multiple global variables at once.
        /// Without websiteId: app-wide. With websiteId: website-scoped.
        /// </summary>
        public static void SetGlobalVariables(IDictionary<string, object> variables, int websiteId = 0)
        {
            if (variables == null)
                throw new ArgumentNullException(nameof(variables));
            
            foreach (var kvp in variables)
            {
                SetGlobalVariable(kvp.Key, kvp.Value, websiteId);
            }
        }

        /// <summary>
        /// Gets a global variable value. Returns null if not found.
        /// With websiteId: searches website-scoped globals first, then app-wide.
        /// </summary>
        public static object GetGlobalVariable(string key, int websiteId = 0)
        {
            if (websiteId > 0 && _websiteGlobals.TryGetValue(websiteId, out var websiteVars))
            {
                if (websiteVars.TryGetValue(key, out var wsValue))
                    return wsValue;
            }
            if (_globalVariables.TryGetValue(key, out var value))
                return value;
            return null;
        }

        /// <summary>
        /// Checks if a global variable exists.
        /// </summary>
        public static bool HasGlobalVariable(string key, int websiteId = 0)
        {
            if (websiteId > 0 && _websiteGlobals.TryGetValue(websiteId, out var websiteVars))
            {
                if (websiteVars.ContainsKey(key)) return true;
            }
            return _globalVariables.ContainsKey(key);
        }

        /// <summary>
        /// Removes a specific global variable.
        /// </summary>
        public static bool RemoveGlobalVariable(string key, int websiteId = 0)
        {
            if (websiteId > 0 && _websiteGlobals.TryGetValue(websiteId, out var websiteVars))
                return websiteVars.TryRemove(key, out _);
            return _globalVariables.TryRemove(key, out _);
        }

        /// <summary>
        /// Clears global variables.
        /// Without websiteId: clears app-wide. With websiteId: clears that website's globals.
        /// </summary>
        public static void ClearGlobalVariables(int websiteId = 0)
        {
            if (websiteId > 0)
            {
                if (_websiteGlobals.TryGetValue(websiteId, out var websiteVars))
                    websiteVars.Clear();
            }
            else
            {
                _globalVariables.Clear();
            }
        }

        /// <summary>
        /// Gets the count of global variables.
        /// Without websiteId: app-wide count. With websiteId: that website's count.
        /// </summary>
        public static int GetGlobalVariableCount(int websiteId = 0)
        {
            if (websiteId > 0 && _websiteGlobals.TryGetValue(websiteId, out var websiteVars))
                return websiteVars.Count;
            return _globalVariables.Count;
        }

        /// <summary>
        /// Gets the count of app-wide global variables.
        /// </summary>
        public static int GlobalVariableCount => _globalVariables.Count;

        /// <summary>
        /// Clears ALL variables currently set in this TemplateEngine instance.
        /// Does NOT clear global variables.
        /// </summary>
        public TemplateEngine ClearVariables()
        {
            _variables.Clear();
            _variablesDirty = true;
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
            public long ComponentVersion { get; set; }
        }

        // Render output cache - shared across ALL engine instances
        private static readonly ConcurrentDictionary<string, RenderCacheEntry> _renderCache =
            new ConcurrentDictionary<string, RenderCacheEntry>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Computes a fast hash of all variables for data-aware caching.
        /// Uses a dirty flag to avoid recomputing when variables haven't changed.
        /// When variables change, the hash changes, causing a cache miss.
        /// </summary>
        private string ComputeDataHash(IDictionary<string, object> additionalVariables)
        {
            // Fast path: if no variables changed and no additional variables, return cached hash
            // Now also checks global variables version
            if (!_variablesDirty && 
                _lastGlobalVariablesVersion == _globalVariablesVersion &&
                additionalVariables == null && 
                _lastDataHash != null)
            {
                return _lastDataHash;
            }

            unchecked
            {
                // Use XOR-based commutative hashing — order-independent, no sorting needed
                // This eliminates LINQ .OrderBy() allocations (IOrderedEnumerable + buffer array)
                int hash = 17;
                
                // Include global variables (XOR is commutative — no ordering needed)
                foreach (var kvp in _globalVariables)
                {
                    int entryHash = (kvp.Key?.GetHashCode() ?? 0) * 397 ^ GetContentAwareHashCode(kvp.Value);
                    hash ^= entryHash;
                }
                
                // Include instance variables
                foreach (var kvp in _variables)
                {
                    int entryHash = (kvp.Key?.GetHashCode() ?? 0) * 397 ^ GetContentAwareHashCode(kvp.Value);
                    hash ^= entryHash;
                }
                
                // Include additional variables
                if (additionalVariables != null)
                {
                    foreach (var kvp in additionalVariables)
                    {
                        int entryHash = (kvp.Key?.GetHashCode() ?? 0) * 397 ^ GetContentAwareHashCode(kvp.Value);
                        hash ^= entryHash;
                    }
                }
                
                // Mix in counts to distinguish {A=1} from {A=1, B=1} when XOR cancels out
                hash = hash * 31 + _globalVariables.Count;
                hash = hash * 31 + _variables.Count;
                hash = hash * 31 + (additionalVariables?.Count ?? 0);
                
                var result = hash.ToString("X8"); // 8-char hex string
                
                // Cache the hash if no additional variables (common case)
                if (additionalVariables == null)
                {
                    _lastDataHash = result;
                    _variablesDirty = false;
                    _lastGlobalVariablesVersion = _globalVariablesVersion;
                }
                
                return result;
            }
        }

        /// <summary>
        /// Computes a content-aware hash code for caching.
        /// Unlike GetHashCode(), this properly hashes collection contents
        /// instead of using reference-based identity.
        /// </summary>
        private static int GetContentAwareHashCode(object value)
        {
            if (value == null) return 0;
            if (value is string || value is ValueType) return value.GetHashCode();
            
            if (value is IEnumerable enumerable)
            {
                unchecked
                {
                    int h = 17;
                    int limit = 20; // Limit items hashed for performance
                    foreach (var item in enumerable)
                    {
                        h = h * 31 + (item?.GetHashCode() ?? 0);
                        if (--limit <= 0) break;
                    }
                    // Include count if available for better differentiation
                    if (value is ICollection col)
                        h = h * 31 + col.Count;
                    return h;
                }
            }
            
            return value.GetHashCode();
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

            // Resolve the full file path — try pages and local pages directories (priority-aware),
            // then fall back to components directories.
            string resolvedPath =
                ResolveDualPath(filePath, _preferGlobalDirectory ? _pagesDirectory : _localPagesDirectory,
                                          _preferGlobalDirectory ? _localPagesDirectory : _pagesDirectory)
                ?? ResolveDualPath(filePath, _preferGlobalDirectory ? _componentsDirectory : _localComponentsDirectory,
                                             _preferGlobalDirectory ? _localComponentsDirectory : _componentsDirectory);

            // If still not found, throw a descriptive error
            if (string.IsNullOrEmpty(resolvedPath))
            {
                var allDirs = new[] { _pagesDirectory, _localPagesDirectory, _componentsDirectory, _localComponentsDirectory }
                    .Where(d => !string.IsNullOrEmpty(d));
                if (!allDirs.Any())
                    throw new InvalidOperationException("No directory configured. Call SetPagesDirectory() or SetComponentsDirectory() first.");
                throw new FileNotFoundException($"Template not found: {filePath}. Searched in: {string.Join(", ", allDirs)}");
            }

            // Check cache with file timestamp and path validation
            if (_renderCache.TryGetValue(effectiveCacheKey, out var cached))
            {
                // Check if expired
                if (cached.ExpiresAt.HasValue && DateTime.UtcNow > cached.ExpiresAt.Value)
                {
                    _renderCache.TryRemove(effectiveCacheKey, out _);
                }
                // Check if file was modified OR if the resolved path changed (e.g., local override added)
                else
                {
                    var currentModified = File.GetLastWriteTimeUtc(resolvedPath);
                    if (cached.FilePath == resolvedPath && 
                        currentModified <= cached.FileLastModified && 
                        cached.ComponentVersion == _componentVersion)
                    {
                        // Cache hit! Return cached output
                        return cached.RenderedOutput;
                    }

                    // File changed or path changed (e.g., local overrides global) - invalidate cache
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
                FileLastModified = File.GetLastWriteTimeUtc(resolvedPath),
                ComponentVersion = _componentVersion
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
                FileLastModified = DateTime.MinValue,
                ComponentVersion = _componentVersion
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
                
                _variablesDirty = true; // Mark hash as stale
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

            // Security: Check template size (fast — just a length check)
            if (template.Length > _security.MaxTemplateSize)
            {
                throw new TemplateLimitException("TemplateSize", 
                    _security.MaxTemplateSize, template.Length);
            }

            // Enforce cache limits
            EnforceCacheLimits();

            // Get or create AST — validation runs ONLY on cache miss (first parse)
            // This eliminates 4 regex scans on every cache-hit Render() call
            var cacheKey = GetTemplateHash(template);
            var securityConfig = _security; // capture for lambda
            var cacheEntry = _astCache.GetOrAdd(cacheKey, _ =>
            {
                // Security: Validate template structure ONCE on first parse
                var validationResult = SecurityUtils.ValidateTemplate(template, securityConfig);
                if (!validationResult.IsValid)
                {
                    throw new TemplateSecurityException(
                        "Template failed security validation: " + string.Join("; ", validationResult.Errors),
                        "TemplateValidation");
                }

                return new CacheEntry<RootNode>
                {
                    Value = ParseTemplate(template),
                    CreatedAt = DateTime.UtcNow
                };
            });
            
            cacheEntry.LastAccessed = DateTime.UtcNow;

            // Use layered variable lookup to avoid allocating a merged dictionary
            // Priority: Additional (highest) → Instance → WebsiteGlobals → AppGlobals (lowest)
            var evaluatorVars = new LayeredVariableLookup(_globalVariables, _myWebsiteGlobals, _variables, additionalVariables);

            // Evaluate AST with layered variables (pass template size as hint for StringBuilder sizing)
            var evaluator = new Evaluator(evaluatorVars, _security, template.Length);
            
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
            
            var result = evaluator.Evaluate(cacheEntry.Value);
            
            // Post-process: Clean up empty attributes if enabled
            if (_security.RemoveEmptyAttributes)
            {
                result = SecurityUtils.RemoveEmptyAttributes(result);
            }
            
            return result;
        }
        
        /// <summary>
        /// Loads a component by path and returns its AST.
        /// Supports dual-path resolution: checks local and global directories based on priority flag.
        /// Automatically invalidates cache if file has been modified.
        /// Supports both file-based (block/button.html) and folder-based (block/projects/default.html) components.
        /// </summary>
        private RootNode LoadComponent(string componentPath)
        {
            // Determine search order based on priority flag:
            // preferGlobalDirectory=false (default): local dir first → global dir as fallback
            // preferGlobalDirectory=true:            global dir first → local dir as fallback
            string firstDir  = _preferGlobalDirectory ? _componentsDirectory      : _localComponentsDirectory;
            string secondDir = _preferGlobalDirectory ? _localComponentsDirectory : _componentsDirectory;

            if (string.IsNullOrEmpty(firstDir) && string.IsNullOrEmpty(secondDir))
                return null;

            // Resolve file path — try first directory, then fallback to second
            string normalizedPath = null;
            if (!string.IsNullOrEmpty(firstDir))
                normalizedPath = ResolveComponentFilePath(firstDir, componentPath);
            if (normalizedPath == null && !string.IsNullOrEmpty(secondDir))
                normalizedPath = ResolveComponentFilePath(secondDir, componentPath);

            if (string.IsNullOrEmpty(normalizedPath))
                return null;

            // Security: Ensure resolved path is within one of the allowed component directories
            bool isInAllowedDir =
                (!string.IsNullOrEmpty(_componentsDirectory) &&
                 normalizedPath.StartsWith(_componentsDirectory, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(_localComponentsDirectory) &&
                 normalizedPath.StartsWith(_localComponentsDirectory, StringComparison.OrdinalIgnoreCase));

            if (!isInAllowedDir)
                return null;

            // Get file modification time for cache validation
            var lastWriteTime = File.GetLastWriteTimeUtc(normalizedPath);
            var cacheKey = "component:" + normalizedPath.ToLowerInvariant();

            // Check cache — invalidate if file was modified after cache was created
            if (_astCache.TryGetValue(cacheKey, out var cached))
            {
                if (cached.CreatedAt >= lastWriteTime)
                {
                    cached.LastAccessed = DateTime.UtcNow;
                    return cached.Value;
                }

                // File changed — remove stale entry and bump component version
                CacheEntry<RootNode> _;
                _astCache.TryRemove(cacheKey, out _);
                System.Threading.Interlocked.Increment(ref _componentVersion);
            }

            // Load and parse (file is new or changed)
            var content = File.ReadAllText(normalizedPath);
            var ast = ParseTemplate(content);

            _astCache[cacheKey] = new CacheEntry<RootNode>
            {
                Value = ast,
                CreatedAt = DateTime.UtcNow,
                LastAccessed = DateTime.UtcNow
            };

            return ast;
        }

        /// <summary>
        /// Resolves a component file path within a base directory.
        /// Tries: (1) exact .html path, (2) path + .html, (3) folder/default.html.
        /// Returns the normalized absolute path if found, or null.
        /// </summary>
        private static string ResolveComponentFilePath(string baseDir, string componentPath)
        {
            var fullPath = Path.Combine(baseDir, componentPath);

            // Option 1: Direct file path already has .html extension
            if (fullPath.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
            {
                var normalized = Path.GetFullPath(fullPath);
                return File.Exists(normalized) ? normalized : null;
            }

            // Option 2: Add .html extension (e.g., block/button → block/button.html)
            var withExt = Path.GetFullPath(fullPath + ".html");
            if (File.Exists(withExt))
                return withExt;

            // Option 3: Folder with default.html (e.g., block/projects → block/projects/default.html)
            var folderPath = Path.GetFullPath(fullPath);
            if (Directory.Exists(folderPath))
            {
                var defaultPath = Path.Combine(folderPath, "default.html");
                if (File.Exists(defaultPath))
                    return defaultPath;
            }

            return null;
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
            string resolvedPath;

            if (isPage)
            {
                // Try both page directories based on priority flag
                resolvedPath = ResolveDualPath(filePath,
                    _preferGlobalDirectory ? _pagesDirectory      : _localPagesDirectory,
                    _preferGlobalDirectory ? _localPagesDirectory : _pagesDirectory);

                if (string.IsNullOrEmpty(resolvedPath))
                {
                    var dirs = new[] { _pagesDirectory, _localPagesDirectory }
                        .Where(d => !string.IsNullOrEmpty(d));
                    if (!dirs.Any())
                        throw new InvalidOperationException("Pages directory not configured. Call SetPagesDirectory() or SetLocalPagesDirectory() first.");
                    throw new FileNotFoundException($"Page template not found: {filePath}. Searched in: {string.Join(", ", dirs)}");
                }
            }
            else
            {
                // Try both component directories based on priority flag
                resolvedPath = ResolveDualPath(filePath,
                    _preferGlobalDirectory ? _componentsDirectory      : _localComponentsDirectory,
                    _preferGlobalDirectory ? _localComponentsDirectory : _componentsDirectory);

                if (string.IsNullOrEmpty(resolvedPath))
                {
                    var dirs = new[] { _componentsDirectory, _localComponentsDirectory }
                        .Where(d => !string.IsNullOrEmpty(d));
                    if (!dirs.Any())
                        throw new InvalidOperationException("Components directory not configured. Call SetComponentsDirectory() or SetLocalComponentsDirectory() first.");
                    var baseDir = dirs.First();
                    throw new FileNotFoundException($"Template not found: {filePath}. Searched in: {string.Join(", ", dirs)}");
                }
            }

            // Security: Validate path before loading
            ValidateFilePath(resolvedPath);

            var template = LoadTemplate(resolvedPath);
            return Render(template, additionalVariables);
        }

        /// <summary>
        /// Resolves a page path using local/global priority
        /// </summary>
        public string ResolvePagePath(string pageName)
        {
            if (string.IsNullOrEmpty(_pagesDirectory) && string.IsNullOrEmpty(_localPagesDirectory)) return null;
            return ResolveDualPath(pageName, 
                _preferGlobalDirectory ? _pagesDirectory : _localPagesDirectory,
                _preferGlobalDirectory ? _localPagesDirectory : _pagesDirectory);
        }

        /// <summary>
        /// Resolves a component path using local/global priority
        /// </summary>
        public string ResolveComponentPath(string componentName)
        {
            if (string.IsNullOrEmpty(_componentsDirectory) && string.IsNullOrEmpty(_localComponentsDirectory)) return null;
            return ResolveDualPath(componentName, 
                _preferGlobalDirectory ? _componentsDirectory : _localComponentsDirectory,
                _preferGlobalDirectory ? _localComponentsDirectory : _componentsDirectory);
        }

        /// <summary>
        /// Tries to resolve a template path from two directories in order.
        /// Returns the resolved absolute path from the first directory that contains the file, or null.
        /// </summary>
        internal string ResolveDualPath(string filePath, string firstDir, string secondDir)
        {
            if (!filePath.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
            {
                filePath += ".html";
            }

            if (!string.IsNullOrEmpty(firstDir))
            {
                var path = ResolveTemplatePath(Path.Combine(firstDir, filePath));
                if (path != null) return path;
            }
            if (!string.IsNullOrEmpty(secondDir))
            {
                var path = ResolveTemplatePath(Path.Combine(secondDir, filePath));
                if (path != null) return path;
            }
            return null;
        }
        
        /// <summary>
        /// Resolves template path with folder-based component support
        /// Priority: 1) file.html, 2) file + .html, 3) folder/default.html
        /// </summary>
        private string ResolveTemplatePath(string fullPath)
        {
            if (_pathCache.TryGetValue(fullPath, out var cachedPath))
            {
                if (File.Exists(cachedPath))
                    return cachedPath;
                // Cached path no longer exists (file moved/deleted) - remove stale entry
                _pathCache.TryRemove(fullPath, out _);
            }

            string resolvedPath = null;

            // Option 1: Direct file path with .html extension
            if (fullPath.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
            {
                var normalizedPath = Path.GetFullPath(fullPath);
                if (File.Exists(normalizedPath))
                    resolvedPath = normalizedPath;

                // Option 3 (when already .html): strip extension → check as folder/default.html
                // e.g. "components\block\about.html" → check "components\block\about\" folder
                if (resolvedPath == null)
                {
                    var pathWithoutExt = fullPath.Substring(0, fullPath.Length - 5); // remove ".html"
                    var folderPath = Path.GetFullPath(pathWithoutExt);
                    if (Directory.Exists(folderPath))
                    {
                        var defaultPath = Path.Combine(folderPath, "default.html");
                        if (File.Exists(defaultPath))
                            resolvedPath = defaultPath;
                    }
                }
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
            
            // Build component path.
            // If ComponentPath already starts with "block/" avoid creating "block/block/..." double prefix.
            var componentPath = block.ComponentPath.StartsWith("block/", StringComparison.OrdinalIgnoreCase)
                ? block.ComponentPath
                : $"block/{block.ComponentPath}";
            
            // Fire OnBeforeIncludeRender callback
            if (_onBeforeIncludeRender != null)
            {
                var includeInfo = new IncludeInfo
                {
                    Name = block.Name,
                    OldName = block.Name,
                    ComponentPath = componentPath,
                    ComponentType = "block",
                    JsonPath = null,
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
                    JsonPath = null,
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

            // Use layered variable lookup — same priority as Render():
            // Additional (highest) → Instance → WebsiteGlobals → AppGlobals (lowest)
            var evaluatorVars = new LayeredVariableLookup(_globalVariables, _myWebsiteGlobals, _variables, additionalVariables);

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
            
            var result = evaluator.Evaluate(prepared.Ast);

            // Post-process: Clean up empty attributes if enabled
            if (_security.RemoveEmptyAttributes)
            {
                result = SecurityUtils.RemoveEmptyAttributes(result);
            }

            return result;
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
                        JsonPath = includeNode.JsonPath,
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
                            JsonPath = elementNode.JsonPath,
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
                            JsonPath = dataNode.JsonPath,
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
                            JsonPath = navNode.JsonPath,
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
                            JsonPath = blockNode.JsonPath,
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
            _renderCache.Clear();
            _pathCache.Clear();
            PropertyAccessor.ClearCache();
            Evaluator.ClearExpressionCache();
            BlockParser.ClearCache();
            System.Threading.Interlocked.Increment(ref _componentVersion);
        }

        /// <summary>
        /// Gets cache statistics for monitoring
        /// </summary>
        public static CacheStatistics GetCacheStats()
        {
            return new CacheStatistics
            {
                AstCacheCount = _astCache.Count,
                TemplateCacheCount = _templateCache.Count,
                RenderCacheCount = _renderCache.Count,
                PathCacheCount = _pathCache.Count
            };
        }

        /// <summary>
        /// Pre-warms the component cache by loading and parsing ALL component files from the components directory.
        /// Call this at app startup (e.g., Application_Start or Startup.Configure) to eliminate 
        /// first-request file I/O latency. After calling this, ALL component renders are cache hits.
        /// </summary>
        /// <returns>Number of components pre-warmed</returns>
        /// <example>
        /// // In Application_Start or Startup
        /// var engine = new TemplateEngine();
        /// engine.SetComponentsDirectory("path/to/components");
        /// int count = engine.PreWarmComponents();
        /// Console.WriteLine($"Pre-warmed {count} component templates");
        /// </example>
        public int PreWarmComponents()
        {
            if (string.IsNullOrEmpty(_componentsDirectory) || !Directory.Exists(_componentsDirectory))
                return 0;

            int count = 0;
            try
            {
                var htmlFiles = Directory.GetFiles(_componentsDirectory, "*.html", SearchOption.AllDirectories);
                foreach (var file in htmlFiles)
                {
                    try
                    {
                        var normalizedPath = Path.GetFullPath(file);
                        
                        // Security: Ensure path is within components directory
                        if (!normalizedPath.StartsWith(_componentsDirectory, StringComparison.OrdinalIgnoreCase))
                            continue;

                        // Load and cache the file content
                        var lastWrite = File.GetLastWriteTimeUtc(normalizedPath);
                        var content = File.ReadAllText(normalizedPath);
                        
                        if (content.Length > _security.MaxTemplateSize)
                            continue;

                        _templateCache[normalizedPath] = new TemplateCacheEntry
                        {
                            Content = content,
                            LastModified = lastWrite
                        };

                        // Parse AST once — reuse for both content-hash key and component path key
                        var cacheKey = GetTemplateHash(content);
                        var astEntry = _astCache.GetOrAdd(cacheKey, _ => new CacheEntry<RootNode>
                        {
                            Value = ParseTemplate(content),
                            CreatedAt = DateTime.UtcNow,
                            LastAccessed = DateTime.UtcNow
                        });

                        // Reuse the already-parsed AST for the component path key (no second parse)
                        var componentCacheKey = "component:" + normalizedPath.ToLowerInvariant();
                        _astCache.GetOrAdd(componentCacheKey, _ => astEntry);

                        // Cache the resolved path
                        var relativePath = normalizedPath.Substring(_componentsDirectory.Length).TrimStart(Path.DirectorySeparatorChar);
                        var fullPathKey = Path.Combine(_componentsDirectory, relativePath);
                        _pathCache[fullPathKey] = normalizedPath;
                        
                        // Also cache without extension
                        if (fullPathKey.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
                        {
                            var withoutExt = fullPathKey.Substring(0, fullPathKey.Length - 5);
                            _pathCache[withoutExt] = normalizedPath;
                        }

                        count++;
                    }
                    catch
                    {
                        // Skip files that fail to load/parse — don't break startup
                    }
                }
            }
            catch
            {
                // Directory read failed — non-fatal
            }

            return count;
        }

        /// <summary>
        /// Pre-warms the pages cache by loading and parsing ALL page templates from the pages directory.
        /// Call this at app startup to eliminate first-request latency for page templates.
        /// </summary>
        /// <returns>Number of pages pre-warmed</returns>
        public int PreWarmPages()
        {
            if (string.IsNullOrEmpty(_pagesDirectory) || !Directory.Exists(_pagesDirectory))
                return 0;

            int count = 0;
            try
            {
                var htmlFiles = Directory.GetFiles(_pagesDirectory, "*.html", SearchOption.AllDirectories);
                foreach (var file in htmlFiles)
                {
                    try
                    {
                        var normalizedPath = Path.GetFullPath(file);
                        var lastWrite = File.GetLastWriteTimeUtc(normalizedPath);
                        var content = File.ReadAllText(normalizedPath);
                        
                        if (content.Length > _security.MaxTemplateSize)
                            continue;

                        _templateCache[normalizedPath] = new TemplateCacheEntry
                        {
                            Content = content,
                            LastModified = lastWrite
                        };

                        var cacheKey = GetTemplateHash(content);
                        _astCache.GetOrAdd(cacheKey, _ => new CacheEntry<RootNode>
                        {
                            Value = ParseTemplate(content),
                            CreatedAt = DateTime.UtcNow,
                            LastAccessed = DateTime.UtcNow
                        });

                        // Cache the resolved path
                        _pathCache[normalizedPath] = normalizedPath;

                        count++;
                    }
                    catch
                    {
                        // Skip files that fail to load/parse
                    }
                }
            }
            catch
            {
                // Directory read failed — non-fatal
            }

            return count;
        }

        /// <summary>
        /// Pre-warms ALL caches (components + pages) at once.
        /// Best called at application startup for zero-latency first requests.
        /// </summary>
        /// <returns>Total number of templates pre-warmed</returns>
        /// <example>
        /// // In Global.asax Application_Start or Startup.Configure
        /// var engine = new TemplateEngine();
        /// engine.SetComponentsDirectory("path/to/components");
        /// engine.SetPagesDirectory("path/to/pages");
        /// int total = engine.PreWarmAll();
        /// Console.WriteLine($"Pre-warmed {total} templates — ready for zero-latency serving!");
        /// </example>
        public int PreWarmAll()
        {
            return PreWarmComponents() + PreWarmPages();
        }

        /// <summary>
        /// Pre-warms ALL themes at application startup for multi-tenant CMS.
        /// Scans each theme's subdirectories for .html files and caches them.
        /// Duplicate themes are automatically skipped (static cache is shared).
        /// Call this ONCE after WebsiteManager.Load() completes.
        /// </summary>
        /// <param name="themePaths">List of theme base directory paths</param>
        /// <param name="subFolders">Which subdirectories to cache. Default: components, pages.
        /// Example: new[] { "components", "pages", "layouts" }</param>
        /// <returns>Total number of templates pre-warmed across all themes</returns>
        /// <example>
        /// // Default: components + pages
        /// TemplateEngine.PreWarmThemes(themePaths);
        /// 
        /// // Custom: components + pages + layouts
        /// TemplateEngine.PreWarmThemes(themePaths, "components", "pages", "layouts");
        /// </example>
        public static int PreWarmThemes(IEnumerable<string> themePaths, params string[] subFolders)
        {
            if (themePaths == null) return 0;

            // Default folders if none specified
            if (subFolders == null || subFolders.Length == 0)
                subFolders = new[] { "components", "pages" };

            int total = 0;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var themePath in themePaths)
            {
                if (string.IsNullOrEmpty(themePath) || !seen.Add(themePath))
                    continue;

                foreach (var folder in subFolders)
                {
                    if (string.IsNullOrEmpty(folder)) continue;

                    var dirPath = Path.Combine(themePath, folder);
                    if (Directory.Exists(dirPath))
                    {
                        total += PreWarmDirectory(dirPath);
                    }
                }
            }

            return total;
        }

        /// <summary>
        /// Pre-warms a single directory by loading and parsing all .html files into the static cache.
        /// Useful for custom directories that aren't components or pages.
        /// </summary>
        /// <param name="directoryPath">Absolute path to the directory containing .html files</param>
        /// <returns>Number of templates pre-warmed</returns>
        public static int PreWarmDirectory(string directoryPath)
        {
            if (string.IsNullOrEmpty(directoryPath) || !Directory.Exists(directoryPath))
                return 0;

            int count = 0;
            try
            {
                var htmlFiles = Directory.GetFiles(directoryPath, "*.html", SearchOption.AllDirectories);
                foreach (var file in htmlFiles)
                {
                    try
                    {
                        var normalizedPath = Path.GetFullPath(file);
                        var lastWrite = File.GetLastWriteTimeUtc(normalizedPath);
                        var content = File.ReadAllText(normalizedPath);

                        _templateCache[normalizedPath] = new TemplateCacheEntry
                        {
                            Content = content,
                            LastModified = lastWrite
                        };

                        var cacheKey = GetTemplateHash(content);
                        _astCache.GetOrAdd(cacheKey, _ => new CacheEntry<RootNode>
                        {
                            Value = ParseTemplate(content),
                            CreatedAt = DateTime.UtcNow,
                            LastAccessed = DateTime.UtcNow
                        });

                        _pathCache[normalizedPath] = normalizedPath;
                        count++;
                    }
                    catch
                    {
                        // Skip files that fail — don't break startup
                    }
                }
            }
            catch
            {
                // Directory read failed — non-fatal
            }

            return count;
        }

        private class TemplateCacheEntry
        {
            public string Content { get; set; }
            public DateTime LastModified { get; set; }
        }


        /// <summary>
        /// Reads all text from a file. Checks both local and global directories if configured.
        /// Use this to fetch external data (JSON, TXT, HTML) for your templates.
        /// Results are cached for high performance and invalidated when the file changes.
        /// </summary>
        /// <param name="relativePath">Path relative to base directory (e.g., "data/settings.json")</param>
        /// <returns>File content or an empty string if not found</returns>
        public string ReadFile(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath)) return string.Empty;

            string fullPath = null;

            // 1. Try to resolve path directly from configured directories (supporting any extension)
            var searchDirs = new List<string>();
            if (_preferGlobalDirectory)
            {
                if (!string.IsNullOrEmpty(_componentsDirectory)) searchDirs.Add(_componentsDirectory);
                if (!string.IsNullOrEmpty(_localComponentsDirectory)) searchDirs.Add(_localComponentsDirectory);
                if (!string.IsNullOrEmpty(_pagesDirectory)) searchDirs.Add(_pagesDirectory);
                if (!string.IsNullOrEmpty(_localPagesDirectory)) searchDirs.Add(_localPagesDirectory);
            }
            else
            {
                if (!string.IsNullOrEmpty(_localComponentsDirectory)) searchDirs.Add(_localComponentsDirectory);
                if (!string.IsNullOrEmpty(_componentsDirectory)) searchDirs.Add(_componentsDirectory);
                if (!string.IsNullOrEmpty(_localPagesDirectory)) searchDirs.Add(_localPagesDirectory);
                if (!string.IsNullOrEmpty(_pagesDirectory)) searchDirs.Add(_pagesDirectory);
            }

            foreach (var baseDir in searchDirs)
            {
                try {
                    var combined = Path.Combine(baseDir, relativePath);
                    var normalized = Path.GetFullPath(combined);
                    if (File.Exists(normalized))
                    {
                        fullPath = normalized;
                        break;
                    }
                } catch { }
            }

            if (fullPath == null || !File.Exists(fullPath))
                return string.Empty;

            var cacheKey = fullPath.ToLowerInvariant();
            var lastWriteTime = File.GetLastWriteTimeUtc(fullPath);

            // 2. Check cache with file modification validation
            if (_fileCache.TryGetValue(cacheKey, out var cached))
            {
                if (cached.CreatedAt >= lastWriteTime)
                {
                    cached.LastAccessed = DateTime.UtcNow;
                    return cached.Value;
                }

                // File changed - remove from cache
                CacheEntry<string> _;
                _fileCache.TryRemove(cacheKey, out _);
            }

            // 3. Load and store in cache
            var content = File.ReadAllText(fullPath);
            _fileCache[cacheKey] = new CacheEntry<string>
            {
                Value = content,
                CreatedAt = DateTime.UtcNow,
                LastAccessed = DateTime.UtcNow
            };

            return content;
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
        public int RenderCacheCount { get; set; }
        public int PathCacheCount { get; set; }
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
        /// JSON path for component data binding
        /// </summary>
        public string JsonPath { get; set; }
        
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
    /// <summary>
    /// High-performance layered variable lookup that avoids creating a merged dictionary.
    /// Implements IVariableContext for zero-allocation rendering.
    /// Priority: Additional (highest) → Instance → WebsiteGlobals → AppGlobals (lowest)
    /// </summary>
    internal class LayeredVariableLookup : IVariableContext
    {
        private readonly ConcurrentDictionary<string, object> _appGlobals;
        private readonly ConcurrentDictionary<string, object> _websiteGlobals;
        private readonly Dictionary<string, object> _instance;
        private readonly IDictionary<string, object> _additional;

        public LayeredVariableLookup(
            ConcurrentDictionary<string, object> appGlobals,
            ConcurrentDictionary<string, object> websiteGlobals,
            Dictionary<string, object> instance,
            IDictionary<string, object> additional)
        {
            _appGlobals = appGlobals;
            _websiteGlobals = websiteGlobals;
            _instance = instance;
            _additional = additional;
        }

        public bool TryGetValue(string key, out object value)
        {
            // 1. Additional (highest priority)
            if (_additional != null && _additional.TryGetValue(key, out value))
                return true;

            // 2. Instance variables
            if (_instance != null && _instance.TryGetValue(key, out value))
                return true;

            // 3. Website-scoped globals
            if (_websiteGlobals != null && _websiteGlobals.TryGetValue(key, out value))
                return true;

            // 4. App-level globals (lowest)
            if (_appGlobals != null && _appGlobals.TryGetValue(key, out value))
                return true;

            value = null;
            return false;
        }

        public object this[string key] => TryGetValue(key, out var value) ? value : null;

        public bool ContainsKey(string key)
        {
            return (_additional != null && _additional.ContainsKey(key)) ||
                   (_instance != null && _instance.ContainsKey(key)) ||
                   (_websiteGlobals != null && _websiteGlobals.ContainsKey(key)) ||
                   (_appGlobals != null && _appGlobals.ContainsKey(key));
        }

        public IVariableContext CreateChild(IDictionary<string, object> localVariables = null)
        {
            return new HierarchicalVariableContext(this, localVariables);
        }

        public Dictionary<string, object> ToDictionary()
        {
            int estimatedCapacity = (_appGlobals?.Count ?? 0) + (_websiteGlobals?.Count ?? 0) + (_instance?.Count ?? 0) + (_additional?.Count ?? 0);
            var merged = new Dictionary<string, object>(estimatedCapacity, StringComparer.OrdinalIgnoreCase);

            if (_appGlobals != null) foreach (var kvp in _appGlobals) merged[kvp.Key] = kvp.Value;
            if (_websiteGlobals != null) foreach (var kvp in _websiteGlobals) merged[kvp.Key] = kvp.Value;
            if (_instance != null) foreach (var kvp in _instance) merged[kvp.Key] = kvp.Value;
            if (_additional != null) foreach (var kvp in _additional) merged[kvp.Key] = kvp.Value;

            return merged;
        }

        public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
        {
            var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            
            if (_additional != null)
            {
                foreach (var kvp in _additional) { seenKeys.Add(kvp.Key); yield return kvp; }
            }
            
            if (_instance != null)
            {
                foreach (var kvp in _instance) { if (seenKeys.Add(kvp.Key)) yield return kvp; }
            }
            
            if (_websiteGlobals != null)
            {
                foreach (var kvp in _websiteGlobals) { if (seenKeys.Add(kvp.Key)) yield return kvp; }
            }
            
            if (_appGlobals != null)
            {
                foreach (var kvp in _appGlobals) { if (seenKeys.Add(kvp.Key)) yield return kvp; }
            }
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
