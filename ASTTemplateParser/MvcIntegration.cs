using System;
using System.Collections.Generic;
using System.IO;
using System.Web;
using System.Web.Mvc;

namespace ASTTemplateParser.Mvc
{
    /// <summary>
    /// ASP.NET MVC 5 Integration for AST Template Parser
    /// Provides ViewEngine, HtmlHelper extensions, and ActionResult support
    /// </summary>
    public static class MvcIntegration
    {
        private static TemplateEngine _engine;
        private static string _viewsPath;
        private static string _componentsPath;
        private static string _layoutsPath;

        /// <summary>
        /// Initialize the template engine for MVC
        /// Call this in Application_Start
        /// </summary>
        public static void Initialize(string viewsPath = null, SecurityConfig security = null)
        {
            var basePath = AppDomain.CurrentDomain.BaseDirectory;
            
            _viewsPath = viewsPath ?? Path.Combine(basePath, "Views", "Templates");
            _componentsPath = Path.Combine(basePath, "Views", "Components");
            _layoutsPath = Path.Combine(basePath, "Views", "Layouts");

            _engine = new TemplateEngine(security ?? SecurityConfig.Default);
            _engine.SetComponentsDirectory(_componentsPath);
            _engine.SetLayoutsDirectory(_layoutsPath);

            // Configure security for web context
            if (security == null)
            {
                var config = SecurityConfig.Default;
                config.AllowedTemplatePaths.Add(_viewsPath);
                config.AllowedTemplatePaths.Add(_componentsPath);
                config.AllowedTemplatePaths.Add(_layoutsPath);
            }
        }

        /// <summary>
        /// Gets the template engine instance
        /// </summary>
        public static TemplateEngine Engine => _engine ?? throw new InvalidOperationException(
            "MvcIntegration not initialized. Call MvcIntegration.Initialize() in Application_Start.");

        /// <summary>
        /// Renders a template with the given model
        /// </summary>
        public static string RenderTemplate(string templatePath, object model = null)
        {
            var fullPath = Path.Combine(_viewsPath, templatePath);
            if (!fullPath.EndsWith(".html"))
                fullPath += ".html";

            var engine = new TemplateEngine();
            engine.SetComponentsDirectory(_componentsPath);
            engine.SetLayoutsDirectory(_layoutsPath);

            if (model != null)
            {
                engine.SetVariableFromObject("Model", model);
            }

            return engine.RenderFile(fullPath);
        }

        /// <summary>
        /// Renders a template string with the given variables
        /// </summary>
        public static string RenderString(string template, object model = null)
        {
            var engine = new TemplateEngine();
            engine.SetComponentsDirectory(_componentsPath);
            
            if (model != null)
            {
                engine.SetVariableFromObject("Model", model);
            }

            return engine.Render(template);
        }
    }

    /// <summary>
    /// ActionResult for returning template-rendered content
    /// </summary>
    public class TemplateResult : ActionResult
    {
        public string TemplatePath { get; set; }
        public object Model { get; set; }
        public string ContentType { get; set; } = "text/html";

        public TemplateResult(string templatePath, object model = null)
        {
            TemplatePath = templatePath;
            Model = model;
        }

        public override void ExecuteResult(ControllerContext context)
        {
            var response = context.HttpContext.Response;
            response.ContentType = ContentType;

            var html = MvcIntegration.RenderTemplate(TemplatePath, Model);
            response.Write(html);
        }
    }

    /// <summary>
    /// Base controller with template rendering support
    /// </summary>
    public abstract class TemplateController : Controller
    {
        /// <summary>
        /// Returns a template view result
        /// </summary>
        protected TemplateResult Template(string templatePath, object model = null)
        {
            return new TemplateResult(templatePath, model);
        }

        /// <summary>
        /// Returns a template view using controller/action naming convention
        /// </summary>
        protected TemplateResult Template(object model = null)
        {
            var controllerName = ControllerContext.RouteData.Values["controller"].ToString();
            var actionName = ControllerContext.RouteData.Values["action"].ToString();
            var templatePath = $"{controllerName}/{actionName}";
            
            return new TemplateResult(templatePath, model);
        }

        /// <summary>
        /// Renders a component and returns as content result
        /// </summary>
        protected ContentResult Component(string componentPath, object model = null)
        {
            var engine = MvcIntegration.Engine;
            
            if (model != null)
            {
                var props = model.GetType().GetProperties();
                foreach (var prop in props)
                {
                    engine.SetVariable(prop.Name, prop.GetValue(model));
                }
            }

            // Load and render component
            var html = engine.Render($"<Include component=\"{componentPath}\" />");
            
            return Content(html, "text/html");
        }
    }

    /// <summary>
    /// HtmlHelper extensions for template rendering in Razor views
    /// </summary>
    public static class TemplateHtmlHelper
    {
        /// <summary>
        /// Renders a template component inline
        /// </summary>
        public static IHtmlString RenderComponent(this HtmlHelper helper, string componentPath, object model = null)
        {
            var engine = new TemplateEngine();
            engine.SetComponentsDirectory(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Views", "Components"));

            if (model != null)
            {
                var props = model.GetType().GetProperties();
                foreach (var prop in props)
                {
                    engine.SetVariable(prop.Name, prop.GetValue(model));
                }
            }

            var template = $"<Include component=\"{componentPath}\" />";
            var html = engine.Render(template);

            return new HtmlString(html);
        }

        /// <summary>
        /// Renders a template string with model
        /// </summary>
        public static IHtmlString RenderTemplate(this HtmlHelper helper, string template, object model = null)
        {
            var html = MvcIntegration.RenderString(template, model);
            return new HtmlString(html);
        }

        /// <summary>
        /// Renders a button component
        /// </summary>
        public static IHtmlString Button(this HtmlHelper helper, string text, string href = "#", string type = "primary")
        {
            var engine = new TemplateEngine();
            engine.SetComponentsDirectory(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Views", "Components"));
            engine.SetVariable("text", text);
            engine.SetVariable("href", href);
            engine.SetVariable("type", type);

            var html = engine.Render("<Include component=\"element/button\"><Param name=\"text\" value=\"{{text}}\" /><Param name=\"href\" value=\"{{href}}\" /><Param name=\"type\" value=\"{{type}}\" /></Include>");
            return new HtmlString(html);
        }

        /// <summary>
        /// Renders a card component
        /// </summary>
        public static IHtmlString Card(this HtmlHelper helper, string title, string content, string image = null)
        {
            var engine = new TemplateEngine();
            engine.SetComponentsDirectory(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Views", "Components"));
            engine.SetVariable("title", title);
            engine.SetVariable("content", content);
            if (image != null)
                engine.SetVariable("image", image);

            var html = engine.Render("<Include component=\"block/card\"><Param name=\"title\" value=\"{{title}}\" /><Param name=\"content\" value=\"{{content}}\" /></Include>");
            return new HtmlString(html);
        }
    }

    /// <summary>
    /// Extension methods for TemplateEngine to work with MVC models
    /// </summary>
    public static class TemplateEngineExtensions
    {
        /// <summary>
        /// Sets variables from an object's properties
        /// </summary>
        public static void SetVariableFromObject(this TemplateEngine engine, string prefix, object obj)
        {
            if (obj == null) return;

            var type = obj.GetType();
            
            // For anonymous types and regular objects, set the whole object
            engine.SetVariable(prefix, obj);

            // Also set individual properties at root level for convenience
            foreach (var prop in type.GetProperties())
            {
                try
                {
                    var value = prop.GetValue(obj);
                    engine.SetVariable(prop.Name, value);
                }
                catch
                {
                    // Ignore properties that can't be read
                }
            }
        }

        /// <summary>
        /// Sets ViewBag properties as template variables
        /// </summary>
        public static void SetFromViewBag(this TemplateEngine engine, dynamic viewBag)
        {
            if (viewBag == null) return;

            var viewBagDict = (IDictionary<string, object>)viewBag;
            foreach (var kvp in viewBagDict)
            {
                engine.SetVariable(kvp.Key, kvp.Value);
            }
        }
    }
}
