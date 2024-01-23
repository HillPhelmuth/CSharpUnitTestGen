using Microsoft.SemanticKernel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace TestGenCore.NativePlugins;

public class TestIOPlugin
{
    [KernelFunction, Description("Save user-approved code file (.cs) to a native file path")]
    [return:Description("Success or error message string")]
    public async Task<string> SaveCodeFile([Description("Code text to save")]string code, [Description("Directory path to output file")] string path,[Description("code file name. Must be .cs extension")] string fileName)
    {
        var codeText = code.Replace("```csharp", "").Replace("```", "").TrimStart('\n');
        try
        {
            await File.WriteAllTextAsync(Path.Combine(path, fileName), codeText);
            return "Success";
        }
        catch (Exception ex)
        {
            return $"Error: {ex}";
        }
    }
    [KernelFunction, Description("Read a c# code file (.cs) from a native file path")]
    [return: Description("The content of the requested c# file")]
    public async Task<string> ReadCodeFile([Description("Directory path to code file")] string path, [Description("code file name. Must be .cs extension")] string fileName)
    {
        try
        {
            return await File.ReadAllTextAsync(Path.Combine(path, fileName));
        }
        catch (Exception ex)
        {
            return $"Error: {ex}";
        }
    }
    [KernelFunction, Description("Retrieve a list of c# files avaiable in a specified directory")]
    [return: Description("A collection of objects representing c# file paths and c# file names")]
    public async Task<string> GetAllCsharpFiles([Description("Directory containing c# files")] string directoryPath)
    {
        try
        {
            var files = Directory.GetFiles(directoryPath, "*.cs").Select(x => new CodeFile(x, Path.GetFileName(x)));
            return JsonSerializer.Serialize(files, new JsonSerializerOptions { WriteIndented = true});
        }
        catch (Exception ex)
        {
            return $"Error: {ex}";
        }
    }
}
public record CodeFile(string Path, string Name);