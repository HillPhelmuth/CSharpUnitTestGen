using System.Net;
using System.Xml;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Polly;
using TestGenCore.Models;
using TestGenCore.NativePlugins;

namespace TestGenCore;

public class UnitTestGeneratorService
{
    private readonly IConfiguration _configuration;
    private readonly Kernel _kernel;
    private ChatHistory _chatHistory = [];
    public UnitTestGeneratorService(IConfiguration configuration)
    {
        _configuration = configuration;
        var kernel = CreateKernel();
        _kernel = kernel;
        _kernel.Plugins.AddFromType<TestIOPlugin>();
        var yamlFileText = FileHelper.ExtractFromAssembly<string>("UnitTestGen.yaml");
        
        var func = _kernel.CreateFunctionFromPromptYaml(yamlFileText);
        var plugin = KernelPluginFactory.CreateFromFunctions("UnitTestGenPlugin","Unit test generator", [func]);
        _kernel.Plugins.Add(plugin);
    }

    private Kernel CreateKernel()
    {
        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.Services.AddLogging(builder => builder.AddConsole());
        kernelBuilder.Services.ConfigureHttpClientDefaults(c =>
        {
            c.AddStandardResilienceHandler().Configure(o =>
            {
                o.Retry.ShouldHandle = args => ValueTask.FromResult(args.Outcome.Result?.StatusCode is HttpStatusCode.TooManyRequests);
                o.Retry.BackoffType = DelayBackoffType.Exponential;
                o.AttemptTimeout = new HttpTimeoutStrategyOptions { Timeout = TimeSpan.FromSeconds(60) };
                o.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(120);
                o.TotalRequestTimeout = new HttpTimeoutStrategyOptions { Timeout = TimeSpan.FromMinutes(5) };
            });
        });
        kernelBuilder.AddOpenAIChatCompletion(_configuration["OpenAI:ModelId"]!, _configuration["OpenAI:ApiKey"]!);
        var kernel = kernelBuilder.Build();
        return kernel;
    }
    public async Task<string> GenerateUnitTest(string code)
    {
        var kernel = CreateKernel();
        var yamlFileText = FileHelper.ExtractFromAssembly<string>("UnitTestGen.yaml");
        var func = kernel.CreateFunctionFromPromptYaml(yamlFileText);
        var plugin = KernelPluginFactory.CreateFromFunctions("UnitTestGenPlugin", "Unit test generator", [func]);
        kernel.Plugins.Add(plugin);
        var args = new KernelArguments { ["code"] = code };
        var result = await kernel.InvokeAsync(plugin["UnitTestGen"], args);
        return result.GetValue<string>() ?? "";
    }
    public async IAsyncEnumerable<string> GenerateUnitTestStream(string code)
    {
        var kernel = CreateKernel();
        var yamlFileText = FileHelper.ExtractFromAssembly<string>("UnitTestGen.yaml");
        var func = kernel.CreateFunctionFromPromptYaml(yamlFileText);
        var plugin = KernelPluginFactory.CreateFromFunctions("UnitTestGenPlugin", "Unit test generator", [func]);
        kernel.Plugins.Add(plugin);
        var args = new KernelArguments { ["code"] = code };
        var result = kernel.InvokeStreamingAsync<string>(plugin["UnitTestGen"], args);
        await foreach (var item in result)
        {
            yield return item;
        }
    }
}