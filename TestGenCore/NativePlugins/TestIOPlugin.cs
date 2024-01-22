using Microsoft.SemanticKernel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
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
}