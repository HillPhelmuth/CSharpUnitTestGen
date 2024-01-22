using ChatComponents;
using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using Markdig;
using TestGenCore;
using Microsoft.JSInterop;
using Radzen.Blazor;

namespace CSharpUnitTestGen.Components;

public partial class UnitTestGen : ComponentBase
{
    private ChatView? _chatView;
    private RadzenCard? _card;
    private string _output = "";
    private bool _isBusy;
    [Inject]
    private UnitTestGeneratorService UnitTestGeneratorService { get; set; } = default!;
    [Inject]
    private IJSRuntime JsRuntime { get; set; } = default!;
    private class CodeInputForm
    {
        public string? Code { get; set; }
    }
    private CodeInputForm _codeInputForm = new();
    private async void Submit(CodeInputForm codeInputForm)
    {
        _isBusy = true;
        StateHasChanged();
        await Task.Delay(1);
        var code = codeInputForm.Code;
        var outStream = UnitTestGeneratorService.GenerateUnitTestStream(code);
        await foreach (var item in outStream)
        {
            _output += item;
            StateHasChanged();
        }
        //_output = await UnitTestGeneratorService.GenerateUnitTest(code);
        await JsRuntime.InvokeVoidAsync("addCodeStyle", _card.Element);
        _isBusy = false;
        StateHasChanged();
    }
    private static string MarkdownToHtml(string markdown)
    {
        var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
        return Markdown.ToHtml(markdown, pipeline);
    }
}