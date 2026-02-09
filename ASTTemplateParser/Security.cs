using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;

namespace ASTTemplateParser
{
    /// <summary>
    /// MAXIMUM SECURITY configuration for template parsing
    /// All security features enabled by default with strict limits
    /// </summary>
    public sealed class SecurityConfig
    {
        #region Resource Limits - STRICT

        /// <summary>
        /// Maximum allowed template size in bytes (default: 256KB - strict)
        /// </summary>
        public int MaxTemplateSize { get; set; } = 256 * 1024;

        /// <summary>
        /// Maximum iterations for foreach loops (default: 1,000 - strict)
        /// </summary>
        public int MaxLoopIterations { get; set; } = 1000;

        /// <summary>
        /// Maximum recursion depth for nested templates (default: 20 - strict)
        /// </summary>
        public int MaxRecursionDepth { get; set; } = 20;

        /// <summary>
        /// Maximum cache entries (default: 500 - strict)
        /// </summary>
        public int MaxCacheEntries { get; set; } = 500;

        /// <summary>
        /// Maximum nesting depth for If/ForEach blocks (default: 10)
        /// </summary>
        public int MaxNestingDepth { get; set; } = 10;

        /// <summary>
        /// Maximum expression length (default: 200 chars)
        /// </summary>
        public int MaxExpressionLength { get; set; } = 200;

        /// <summary>
        /// Maximum interpolations per template (default: 500)
        /// </summary>
        public int MaxInterpolationsPerTemplate { get; set; } = 500;

        /// <summary>
        /// Maximum property access depth (default: 5 levels)
        /// </summary>
        public int MaxPropertyDepth { get; set; } = 5;

        #endregion

        #region Output Security

        /// <summary>
        /// Whether to HTML encode output by default (default: true - ALWAYS ON)
        /// </summary>
        public bool HtmlEncodeOutput { get; set; } = true;

        /// <summary>
        /// Whether to strip all HTML tags from output (default: false)
        /// Set to true for maximum XSS protection
        /// </summary>
        public bool StripHtmlTags { get; set; } = false;

        /// <summary>
        /// Whether to encode JavaScript special characters (default: false)
        /// Set to true ONLY if you are rendering inside a <script> tag.
        /// </summary>
        public bool JavaScriptEncode { get; set; } = false;

        #endregion

        #region Expression Security

        /// <summary>
        /// Use strict whitelist mode for expressions (default: false)
        /// Only allows explicitly whitelisted operators and functions
        /// </summary>
        public bool StrictExpressionMode { get; set; } = false;

        /// <summary>
        /// Whether to allow method calls in expressions (default: false - DISABLED)
        /// </summary>
        public bool AllowMethodCalls { get; set; } = false;

        /// <summary>
        /// Whether to allow indexer access like array[0] (default: true)
        /// </summary>
        public bool AllowIndexerAccess { get; set; } = true;

        #endregion

        #region Path Security

        /// <summary>
        /// Allowed base paths for template files (REQUIRED for file loading)
        /// </summary>
        public List<string> AllowedTemplatePaths { get; } = new List<string>();

        /// <summary>
        /// Allowed file extensions for templates (default: .html, .htm, .template)
        /// </summary>
        public HashSet<string> AllowedFileExtensions { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".html", ".htm", ".template", ".tpl"
        };

        #endregion

        #region Property Security

        /// <summary>
        /// Blocked property names - cannot be accessed (comprehensive list)
        /// </summary>
        public HashSet<string> BlockedPropertyNames { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // Authentication & Secrets
            "Password", "PasswordHash", "PasswordSalt", "Secret", "SecretKey",
            "ApiKey", "ApiSecret", "Token", "AccessToken", "RefreshToken",
            "BearerToken", "JwtToken", "OAuthToken", "AuthToken",
            
            // Connection & Configuration
            "ConnectionString", "ConnString", "DbConnection", "DatabasePassword",
            "PrivateKey", "PublicKey", "Certificate", "CertificateThumbprint",
            
            // Session & Auth State
            "Credential", "Credentials", "Auth", "Authentication", "Authorization",
            "Session", "SessionId", "SessionKey", "Cookie", "Cookies",
            
            // Financial
            "CreditCard", "CreditCardNumber", "CVV", "CardNumber", "AccountNumber",
            "BankAccount", "RoutingNumber", "SSN", "SocialSecurityNumber", "TaxId",
            
            // Personal Identifiable Information (PII)
            "DateOfBirth", "DOB", "DriverLicense", "PassportNumber",
            
            // System
            "Environment", "EnvironmentVariables", "Configuration", "AppSettings",
            "MachineKey", "DecryptionKey", "ValidationKey",
            
            // Internal
            "InternalId", "SystemId", "AdminPassword", "RootPassword", "MasterKey"
        };

        /// <summary>
        /// Blocked type names that cannot be accessed via reflection
        /// </summary>
        public HashSet<string> BlockedTypeNames { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "System", "Process", "File", "Directory", "Assembly", "Reflection",
            "Runtime", "Activator", "AppDomain", "Thread", "Task", "Environment",
            "Registry", "RegistryKey", "WebClient", "HttpClient", "Socket",
            "StreamReader", "StreamWriter", "FileStream", "MemoryStream",
            "SqlConnection", "SqlCommand", "DbConnection", "DbCommand"
        };

        #endregion

        /// <summary>
        /// Default global configuration with MAXIMUM security
        /// </summary>
        public static SecurityConfig Default { get; } = new SecurityConfig();

        /// <summary>
        /// Creates a paranoid security config with absolute minimum permissions
        /// </summary>
        public static SecurityConfig Paranoid => new SecurityConfig
        {
            MaxTemplateSize = 64 * 1024,        // 64KB
            MaxLoopIterations = 100,            // Very strict
            MaxRecursionDepth = 5,              // Very strict
            MaxNestingDepth = 5,                // Very strict
            MaxExpressionLength = 100,          // Very strict
            MaxInterpolationsPerTemplate = 100, // Very strict
            MaxPropertyDepth = 3,               // Very strict
            StrictExpressionMode = true,
            AllowMethodCalls = false,
            AllowIndexerAccess = true,
            StripHtmlTags = true,               // Strip ALL HTML
            HtmlEncodeOutput = true,
            JavaScriptEncode = true
        };
    }

    /// <summary>
    /// MAXIMUM SECURITY utilities for template processing
    /// Uses whitelist approach - only explicitly allowed content passes
    /// </summary>
    public static class SecurityUtils
    {
        #region Dangerous Pattern Detection

        // Comprehensive dangerous patterns - BLOCK ALL
        private static readonly Regex DangerousPatterns = new Regex(
            @"(" +
            // .NET Framework types
            @"System\.|Microsoft\.|" +
            // Process/File operations
            @"Process\.|File\.|Directory\.|Path\.|" +
            // Reflection & Dynamic
            @"Assembly\.|Reflection\.|Runtime\.|Type\.|" +
            @"Activator\.|AppDomain\.|" +
            // Dangerous methods
            @"typeof|GetType|Invoke|Execute|Compile|Eval|" +
            @"CreateInstance|GetMethod|GetProperty|GetField|" +
            // Networking
            @"WebClient|HttpClient|WebRequest|Socket|" +
            // Database
            @"SqlConnection|SqlCommand|DbConnection|OleDb|Odbc|" +
            // Script injection
            @"<script|javascript:|vbscript:|data:|" +
            // Command execution
            @"cmd\.|powershell|bash|sh\s|exec\(|shell|" +
            // Environment access
            @"Environment\.|Registry\.|RegistryKey" +
            @")",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // SQL Injection patterns
        private static readonly Regex SqlInjectionPatterns = new Regex(
            @"(\b(SELECT|INSERT|UPDATE|DELETE|DROP|UNION|ALTER|CREATE|TRUNCATE|EXEC|EXECUTE)\b|--|;|\x00)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Only these characters allowed in expressions (whitelist)
        // Includes: letters, numbers, underscore, dot, spaces, math operators, comparison, logical, quotes, comma, brackets
        private static readonly Regex SafeExpressionChars = new Regex(
            @"^[a-zA-Z0-9_\.\s\+\-\*\/\%\=\!\<\>\&\|\(\)'""\[\]\:\,]+$",
            RegexOptions.Compiled);

        // Property path pattern - only letters, numbers, underscores, dots
        private static readonly Regex SafePropertyPath = new Regex(
            @"^[a-zA-Z_][a-zA-Z0-9_]*(\.[a-zA-Z_][a-zA-Z0-9_]*)*$",
            RegexOptions.Compiled);

        #endregion

        #region Whitelisted Safe Content

        // Allowed comparison operators
        private static readonly HashSet<string> AllowedOperators = new HashSet<string>
        {
            "+", "-", "*", "/", "%",           // Arithmetic
            "==", "!=", "<", ">", "<=", ">=",  // Comparison
            "&&", "||", "!",                   // Logical (symbols)
            "and", "or", "not",                // Logical (words)
            "true", "false", "null"            // Literals
        };

        // Allowed safe functions (very limited)
        private static readonly HashSet<string> AllowedFunctions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // Math only - no string manipulation that could be exploited
            "Abs", "Ceiling", "Floor", "Round", "Max", "Min"
        };

        #endregion

        #region Expression Validation

        /// <summary>
        /// Validates an expression with MAXIMUM security
        /// Uses whitelist approach - rejects anything suspicious
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsExpressionSafe(string expression, SecurityConfig config = null)
        {
            if (string.IsNullOrEmpty(expression))
                return true;

            config = config ?? SecurityConfig.Default;

            // Check length limit
            if (expression.Length > config.MaxExpressionLength)
                return false;

            // Check for NULL bytes (injection technique)
            if (expression.Contains('\0'))
                return false;

            // Strict mode: Only allow safe characters
            if (config.StrictExpressionMode && !SafeExpressionChars.IsMatch(expression))
                return false;

            // Check for SQL injection patterns
            if (SqlInjectionPatterns.IsMatch(expression))
                return false;

            // Check for dangerous patterns
            if (DangerousPatterns.IsMatch(expression))
                return false;

            // Check for method calls if not allowed
            if (!config.AllowMethodCalls && expression.Contains("(") && !IsAllowedFunctionCall(expression))
                return false;

            // Check for indexer access if not allowed
            if (!config.AllowIndexerAccess && (expression.Contains("[") || expression.Contains("]")))
                return false;

            // Check for blocked type references (only whole words, not substrings)
            foreach (var blockedType in config.BlockedTypeNames)
            {
                // Check for Type. pattern (like System. or File.)
                var typeWithDot = blockedType + ".";
                if (expression.IndexOf(typeWithDot, StringComparison.OrdinalIgnoreCase) >= 0)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Validates an expression specifically for conditions
        /// Even stricter than general expression validation
        /// </summary>
        public static bool IsConditionSafe(string condition, SecurityConfig config = null)
        {
            if (!IsExpressionSafe(condition, config))
                return false;

            // Additional checks for conditions
            // No assignment operators
            if (condition.Contains("=") && !condition.Contains("==") && !condition.Contains("!=") &&
                !condition.Contains("<=") && !condition.Contains(">="))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Checks if a function call is in the allowed list
        /// </summary>
        private static bool IsAllowedFunctionCall(string expression)
        {
            foreach (var func in AllowedFunctions)
            {
                if (expression.Contains(func + "("))
                    return true;
            }
            return false;
        }

        #endregion

        #region Property Validation

        /// <summary>
        /// Validates a property path with MAXIMUM security
        /// </summary>
        public static bool IsPropertyPathSafe(string path, SecurityConfig config = null)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            config = config ?? SecurityConfig.Default;

            // Check for safe pattern
            if (!SafePropertyPath.IsMatch(path))
                return false;

            // Check depth limit
            var depth = path.Count(c => c == '.') + 1;
            if (depth > config.MaxPropertyDepth)
                return false;

            // Check each segment against blocked names (exact match only)
            var parts = path.Split('.');
            foreach (var part in parts)
            {
                if (config.BlockedPropertyNames.Contains(part))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Checks if a property name is safe to access (exact match only)
        /// </summary>
        public static bool IsPropertySafe(string propertyName, SecurityConfig config)
        {
            if (string.IsNullOrEmpty(propertyName))
                return false;

            // Check blocked list (exact match only)
            if (config.BlockedPropertyNames.Contains(propertyName))
                return false;

            return true;
        }

        #endregion

        #region Path Validation

        /// <summary>
        /// Validates template file path with MAXIMUM security
        /// </summary>
        public static bool IsPathSafe(string path, SecurityConfig config)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            // Check for path traversal attempts - comprehensive
            var dangerousPathPatterns = new[] { "..", "~", "%2e", "%2f", "%5c", "\\\\", "//" };
            foreach (var pattern in dangerousPathPatterns)
            {
                if (path.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0)
                    return false;
            }

            // Check file extension
            var ext = System.IO.Path.GetExtension(path);
            if (!config.AllowedFileExtensions.Contains(ext))
                return false;

            // Normalize the path
            string normalizedPath;
            try
            {
                normalizedPath = System.IO.Path.GetFullPath(path);
            }
            catch
            {
                return false;
            }

            // Must be in allowed paths
            if (config.AllowedTemplatePaths.Count == 0)
                return false; // No paths configured = deny all file access

            foreach (var basePath in config.AllowedTemplatePaths)
            {
                try
                {
                    var normalizedBase = System.IO.Path.GetFullPath(basePath);
                    if (normalizedPath.StartsWith(normalizedBase, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
                catch
                {
                    continue;
                }
            }

            return false;
        }

        #endregion

        #region Output Encoding

        /// <summary>
        /// HTML encodes a string with MAXIMUM protection against XSS
        /// </summary>
        public static string HtmlEncode(object value, SecurityConfig config = null)
        {
            if (value == null)
                return string.Empty;

            var str = value.ToString();
            if (string.IsNullOrEmpty(str))
                return string.Empty;

            config = config ?? SecurityConfig.Default;

            // Strip HTML tags if configured
            if (config.StripHtmlTags)
            {
                str = StripHtml(str);
            }

            // HTML encode
            var encoded = System.Net.WebUtility.HtmlEncode(str);

            // Additional JavaScript encoding IF AND ONLY IF enabled
            if (config.JavaScriptEncode)
            {
                encoded = JavaScriptEncode(encoded);
            }

            return encoded;
        }

        /// <summary>
        /// Strips all HTML tags from a string
        /// </summary>
        public static string StripHtml(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            // Remove all HTML tags
            return Regex.Replace(input, @"<[^>]*>", string.Empty, RegexOptions.Compiled);
        }

        /// <summary>
        /// Encodes JavaScript special characters to prevent injection
        /// </summary>
        public static string JavaScriptEncode(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            var sb = new StringBuilder(input.Length);
            foreach (char c in input)
            {
                switch (c)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '\'': sb.Append("\\'"); break;
                    case '"': sb.Append("\\\""); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default: sb.Append(c); break;
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// Sanitizes user input before using in template
        /// </summary>
        public static string SanitizeInput(string input, int maxLength = 1000)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            // Truncate to max length
            if (input.Length > maxLength)
                input = input.Substring(0, maxLength);

            // Remove null bytes
            input = input.Replace("\0", "");

            // Remove control characters (except newline, tab)
            var sb = new StringBuilder(input.Length);
            foreach (char c in input)
            {
                if (c >= 32 || c == '\n' || c == '\r' || c == '\t')
                    sb.Append(c);
            }

            return sb.ToString();
        }

        #endregion

        #region Template Validation

        /// <summary>
        /// Validates entire template structure for security issues
        /// </summary>
        public static TemplateValidationResult ValidateTemplate(string template, SecurityConfig config = null)
        {
            var result = new TemplateValidationResult { IsValid = true };
            config = config ?? SecurityConfig.Default;

            if (string.IsNullOrEmpty(template))
                return result;

            // Check size
            if (template.Length > config.MaxTemplateSize)
            {
                result.IsValid = false;
                result.Errors.Add($"Template exceeds maximum size of {config.MaxTemplateSize} bytes");
                return result;
            }

            // Count interpolations
            var interpolationCount = Regex.Matches(template, @"\{\{[^}]+\}\}").Count;
            if (interpolationCount > config.MaxInterpolationsPerTemplate)
            {
                result.IsValid = false;
                result.Errors.Add($"Template exceeds maximum {config.MaxInterpolationsPerTemplate} interpolations");
            }

            // Check nesting depth
            var maxDepth = CalculateMaxNestingDepth(template);
            if (maxDepth > config.MaxNestingDepth)
            {
                result.IsValid = false;
                result.Errors.Add($"Template exceeds maximum nesting depth of {config.MaxNestingDepth}");
            }

            // Check for script tags
            if (Regex.IsMatch(template, @"<script", RegexOptions.IgnoreCase))
            {
                result.IsValid = false;
                result.Errors.Add("Template contains script tags which are not allowed");
            }

            // Check for event handlers
            if (Regex.IsMatch(template, @"\bon\w+\s*=", RegexOptions.IgnoreCase))
            {
                result.IsValid = false;
                result.Errors.Add("Template contains inline event handlers which are not allowed");
            }

            return result;
        }

        private static int CalculateMaxNestingDepth(string template)
        {
            int depth = 0;
            int maxDepth = 0;

            var tagMatches = Regex.Matches(template, @"<(/?)(If|ForEach)", RegexOptions.IgnoreCase);
            foreach (Match match in tagMatches)
            {
                if (match.Groups[1].Value == "/")
                    depth--;
                else
                    depth++;

                if (depth > maxDepth)
                    maxDepth = depth;
            }

            return maxDepth;
        }

        #endregion
    }

    /// <summary>
    /// Result of template validation
    /// </summary>
    public class TemplateValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; } = new List<string>();
        public List<string> Warnings { get; } = new List<string>();
    }

    /// <summary>
    /// Exception thrown when security violation is detected
    /// </summary>
    public class TemplateSecurityException : Exception
    {
        public string ViolationType { get; }
        public string Details { get; }

        public TemplateSecurityException(string message, string violationType = "Unknown", string details = null)
            : base(message)
        {
            ViolationType = violationType;
            Details = details;
        }
    }

    /// <summary>
    /// Exception thrown when resource limits are exceeded
    /// </summary>
    public class TemplateLimitException : Exception
    {
        public string LimitType { get; }
        public int Limit { get; }
        public int Actual { get; }

        public TemplateLimitException(string limitType, int limit, int actual)
            : base($"Template limit exceeded: {limitType}. Limit: {limit}, Actual: {actual}")
        {
            LimitType = limitType;
            Limit = limit;
            Actual = actual;
        }
    }
}
