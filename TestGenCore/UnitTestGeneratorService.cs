using System.Net;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Polly;
using TestGenCore.Models;
using TestGenCore.NativePlugins;

namespace TestGenCore;
#pragma warning disable
public class UnitTestGeneratorService
{
    private const string AdvisorPromptTemplate = 
        """
        Help users generate unit tests for their c# code. 
        First, find out how the user wants to provide the code they wish to test. The options are:
            - Paste code into the chat window
            - Provide a file path to a c# file
            - Provide a directory containing c# files and then selecting a file from that list
        Generate unit tests as requested using available tools. After tests are generated, ask the user if they want to make any modifications to the generated tests or if they want to save the tests to a file.
        Read and write files using available tools.
        """;
    private readonly IConfiguration _configuration;
    private readonly Kernel _kernel;
    private ChatHistory _chatHistory = [];
    public event Action? ChatReset;
    public event Action<string>? SendMessage;
    public async IAsyncEnumerable<string> ChatStream(string input, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var kernel = CreateKernel();
        var plugins = GetPlugins();
        plugins.ForEach(kernel.Plugins.Add);
        //kernel.FunctionInvoked += FunctionInvokedHandler;
        kernel.FunctionInvoking += FunctionInvokingHandler;
        await foreach (var p in ExecuteChatStream(input, kernel, cancellationToken)) yield return p;
    }

    private async IAsyncEnumerable<string> ExecuteChatStream(string input, Kernel kernel,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var settings = new OpenAIPromptExecutionSettings() { ChatSystemPrompt = AdvisorPromptTemplate, ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions, MaxTokens = 3000 };
        var chat = kernel.GetRequiredService<IChatCompletionService>();
        _chatHistory.AddSystemMessage(AdvisorPromptTemplate);
        if (!string.IsNullOrWhiteSpace(input))
            _chatHistory.AddUserMessage(input);

        var sb = new StringBuilder();
        await foreach (var update in chat.GetStreamingChatMessageContentsAsync(_chatHistory, settings, kernel, cancellationToken))
        {
            if (update.Content is null) continue;
            sb.Append(update.Content);
            yield return update.Content;

        }
        _chatHistory.AddAssistantMessage(sb.ToString());
    }


    public void Reset()
    {
        _chatHistory.Clear();
        ChatReset?.Invoke();
    }
    public UnitTestGeneratorService(IConfiguration configuration)
    {
        _configuration = configuration;       
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

        // If using Azure OpenAI, uncomment the following line and comment the previous line
        //kernelBuilder.AddAzureOpenAIChatCompletion(_configuration["AzureOpenAI:DeploymentName"]!, _configuration["AzureOpenAI:Endpoint"]!, _configuration["AzureOpenAI:ApiKey"]!);
        var kernel = kernelBuilder.Build();
        return kernel;
    }
    
    public async IAsyncEnumerable<string> GenerateUnitTestStream(string code)
    {
        var kernel = CreateKernel();
        var plugin = KernelPluginFromYamlFile("UnitTestGen.yaml");
        kernel.Plugins.Add(plugin);
        var args = new KernelArguments { ["code"] = code };
        var result = kernel.InvokeStreamingAsync<string>(plugin["UnitTestGen"], args);
        await foreach (var item in result)
        {
            yield return item;
        }
    }

    private static KernelPlugin KernelPluginFromYamlFile(string yamlFileName)
    {
        var yamlFileText = FileHelper.ExtractFromAssembly<string>(yamlFileName);
        var func = KernelFunctionYaml.FromPromptYaml(yamlFileText);
        var plugin = KernelPluginFactory.CreateFromFunctions("UnitTestGenPlugin", "Unit test generator", [func]);
        return plugin;
    }
    private static List<KernelPlugin> GetPlugins()
    {
        var ioPlugin = KernelPluginFactory.CreateFromType<TestIOPlugin>("TestIOPlugin");
        var testGenPlugin = KernelPluginFromYamlFile("UnitTestGen.yaml");
        return [ioPlugin, testGenPlugin];
    }
    private void FunctionInvokingHandler(object? sender, FunctionInvokingEventArgs e)
    {
        var plugin = e.Function.Name;
        SendMessage?.Invoke($"<div style=\"font-size:110%\">Executing <em>{plugin}</em></div>");        
    }

    private void FunctionInvokedHandler(object? sender, FunctionInvokedEventArgs e)
    {
        var result = $"<p>{e.Result}</p>";
        var resultsExpando = $"""

                              <details>
                                <summary>See Results</summary>
                                
                                <h5>Results</h5>
                                <p>
                                {result}
                                </p>
                                <br/>
                              </details>
                              """;
        SendMessage?.Invoke(resultsExpando);
        
    }
}