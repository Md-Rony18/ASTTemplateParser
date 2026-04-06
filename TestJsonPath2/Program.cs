using System;
using System.Collections.Generic;
using System.IO;
using ASTTemplateParser;

// ===== EXACT CMS FLOW REPRODUCTION =====
// Simulates PageExtension.ParseTemplate() flow

var engine = new TemplateEngine();

// Create temp directories for component resolution
var tempDir = Path.Combine(Path.GetTempPath(), "JsonPathTest_" + Guid.NewGuid().ToString("N").Substring(0, 8));
var componentsDir = Path.Combine(tempDir, "components");
var dataDir = Path.Combine(componentsDir, "data");
var sliderDir = Path.Combine(dataDir, "slider");
Directory.CreateDirectory(sliderDir);

// Create a simple component template
File.WriteAllText(Path.Combine(sliderDir, "default.html"), "<div>slider component</div>");

engine.SetComponentsDirectory(componentsDir);

// === Test 1: OnBeforeIncludeRender callback (CMS flow) ===
Console.WriteLine("=== TEST 1: OnBeforeIncludeRender callback ===");

string capturedJsonPath = "NOT_CAPTURED";
string capturedComponentType = "NOT_CAPTURED";
string capturedName = "NOT_CAPTURED";

engine.OnBeforeIncludeRender((info, eng) => {
    Console.WriteLine($"  CALLBACK FIRED: ComponentType={info.ComponentType} Name={info.Name} JsonPath={info.JsonPath ?? "NULL"} Path={info.ComponentPath}");
    if (info.ComponentType == "data")
    {
        capturedJsonPath = info.JsonPath ?? "NULL";
        capturedComponentType = info.ComponentType;
        capturedName = info.Name;
    }
});

engine.OnAfterIncludeRender((info, html) => {
    Console.WriteLine($"  AFTER CALLBACK: ComponentType={info.ComponentType} JsonPath={info.JsonPath ?? "NULL"}");
    return html;
});

var template = @"<Data component=""slider"" jsonpath=""file.setting"" name=""Slider_f88b9f6ea25e4cf1b26d16932066e1df_un"" oldname=""Slider_f88b9f6ea25e4cf1b26d16932066e1df_un"">
    <Param name=""name"" value=""Slider"" />
    <Param name=""datatype"" value=""Banner""/>
    <Param name=""properties"" value=""Title,Description,ImagePath""/>
    <Param name=""flag"" value=""slider""/>
    <Param name=""sorting"" value=""recent""/>
    <Param name=""take"" value=""3""/>
</Data>";

Console.WriteLine($"Template:\n{template}\n");

try
{
    var result = engine.Render(template);
    Console.WriteLine($"Render result: {result}");
}
catch (Exception ex)
{
    Console.WriteLine($"Render error (expected - no component loader): {ex.Message}");
}

Console.WriteLine($"\nCaptured JsonPath = {capturedJsonPath}");
Console.WriteLine($"Captured ComponentType = {capturedComponentType}");
Console.WriteLine($"Captured Name = {capturedName}");

// === Test 2: PrepareTemplate ===
Console.WriteLine("\n=== TEST 2: PrepareTemplate ===");
var prepared = engine.PrepareTemplate(template);
foreach (var info in prepared.IncludeInfos)
{
    Console.WriteLine($"IncludeInfo: Name={info.Name} JsonPath={info.JsonPath ?? "NULL"} ComponentPath={info.ComponentPath} ComponentType={info.ComponentType}");
}

// === Test 3: Render via RenderFile (simulating block component rendering) ===
Console.WriteLine("\n=== TEST 3: RenderFile (component contains <Data> tag) ===");

var blockFile = Path.Combine(componentsDir, "block", "testblock.html");
Directory.CreateDirectory(Path.GetDirectoryName(blockFile));
File.WriteAllText(blockFile, template);

try
{
    var result2 = engine.RenderFile("block/testblock");
    Console.WriteLine($"RenderFile result: {result2}");
}
catch (Exception ex)
{
    Console.WriteLine($"RenderFile error: {ex.Message}");
}

Console.WriteLine($"\nFinal Captured JsonPath = {capturedJsonPath}");

// Cleanup
try { Directory.Delete(tempDir, true); } catch { }
