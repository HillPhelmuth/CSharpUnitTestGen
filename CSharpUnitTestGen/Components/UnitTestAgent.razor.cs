using ChatComponents;
using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using TestGenCore;

namespace CSharpUnitTestGen.Components
{
    public partial class UnitTestAgent : IDisposable
    {
        private ChatView? _chatView;
        private bool _isBusy;
        private CancellationTokenSource _cancellationTokenSource = new();
        [Inject]
        private UnitTestGeneratorService UnitTestGeneratorService { get; set; } = default!;
        private void Cancel() => _cancellationTokenSource.Cancel();
        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                UnitTestGeneratorService.SendMessage += HandleSendMessage;
                await ExecuteChatSequence(UnitTestGeneratorService.ChatStream("", _cancellationTokenSource.Token));
            }
            await base.OnAfterRenderAsync(firstRender);
        }
        private async void Reset()
        {
            _chatView.ChatState?.Reset();
            UnitTestGeneratorService.Reset();
            StateHasChanged();
            await ExecuteChatSequence(UnitTestGeneratorService.ChatStream("", _cancellationTokenSource.Token));
        }
        private async void HandleChatInput(UserInputRequest request)
        {
            _isBusy = true;
            StateHasChanged();
            await Task.Delay(1);
            var input = request.ChatInput ?? "";
            _chatView!.ChatState?.AddUserMessage(input);
            var chatWithPlanner = UnitTestGeneratorService.ChatStream(input, _cancellationTokenSource.Token);
            await ExecuteChatSequence(chatWithPlanner);
            _isBusy = false;
            StateHasChanged();

        }
        private async Task ExecuteChatSequence(IAsyncEnumerable<string> chatWithPlanner)
        {
            var hasStarted = false;
            var lastIsAssistantMessage = _chatView.ChatState?.ChatMessages.LastOrDefault()?.Role == Role.Assistant;
            await foreach (var text in chatWithPlanner)
            {
                if (lastIsAssistantMessage || hasStarted)
                {
                    _chatView!.ChatState!.UpdateAssistantMessage(text);
                }
                else
                {
                    _chatView!.ChatState!.AddAssistantMessage(text);
                    _chatView.ChatState.ChatMessages.LastOrDefault(x => x.Role == Role.Assistant)!.IsActiveStreaming = true;
                    hasStarted = true;
                }
            }

            var lastAsstMessage =
                _chatView.ChatState!.ChatMessages.LastOrDefault(x => x.Role == Role.Assistant);
            if (lastAsstMessage is not null)
                lastAsstMessage.IsActiveStreaming = false;
        }
        private void HandleSendMessage(string text)
        {
            var lastMessage = _chatView.ChatState?.ChatMessages.LastOrDefault();
            var lastIsAssistantMessage = lastMessage?.Role == Role.Assistant;
            if (lastIsAssistantMessage)
            {
                _chatView!.ChatState!.UpdateAssistantMessage(text);
            }
            else
            {
                _chatView!.ChatState!.AddAssistantMessage(text);
                _chatView.ChatState.ChatMessages.LastOrDefault(x => x.Role == Role.Assistant)!.IsActiveStreaming = true;
            }
        }

        public void Dispose()
        {
            UnitTestGeneratorService.SendMessage -= HandleSendMessage;
        }
    }
}
