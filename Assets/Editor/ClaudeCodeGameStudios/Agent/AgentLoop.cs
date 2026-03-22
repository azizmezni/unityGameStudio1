using System;
using System.Collections.Generic;
using System.Text;
using ClaudeCodeGameStudios.Providers;
using UnityEngine;

namespace ClaudeCodeGameStudios.Agent
{
    /// <summary>
    /// Agent loop: send message → LLM responds → parse tool calls → execute →
    /// send results back → repeat until LLM responds with no tool calls.
    /// Works with ANY provider (tool use is prompt-based, not API-based).
    /// </summary>
    public class AgentLoop
    {
        private readonly ProviderType _provider;
        private readonly string _model;
        private readonly string _systemPrompt;
        private readonly List<(string role, string content)> _conversationHistory = new();
        private int _maxIterations = 15;
        private int _currentIteration;

        public event Action<string> OnTextOutput;        // LLM text message to user
        public event Action<ToolCall, string> OnToolExecuted;  // Tool name + result
        public event Action<string> OnError;             // Error message
        public event Action OnComplete;                  // Loop finished (no more tool calls)
        public event Action OnThinking;                  // LLM is processing

        public bool IsRunning { get; private set; }

        public AgentLoop(ProviderType provider, string model, string systemPrompt)
        {
            _provider = provider;
            _model = model;
            _systemPrompt = systemPrompt;
        }

        /// <summary>
        /// Start the agent loop with a user message.
        /// </summary>
        public void Run(string userMessage)
        {
            if (IsRunning) return;
            IsRunning = true;
            _currentIteration = 0;

            _conversationHistory.Add(("user", userMessage));
            SendToLLM();
        }

        /// <summary>
        /// Continue the conversation with a follow-up message.
        /// </summary>
        public void Continue(string userMessage)
        {
            if (IsRunning) return;
            IsRunning = true;
            _currentIteration = 0;

            _conversationHistory.Add(("user", userMessage));
            SendToLLM();
        }

        private void SendToLLM()
        {
            _currentIteration++;
            if (_currentIteration > _maxIterations)
            {
                OnError?.Invoke("Max iterations reached. Stopping to prevent infinite loop.");
                IsRunning = false;
                OnComplete?.Invoke();
                return;
            }

            OnThinking?.Invoke();

            // Build the full message from conversation history
            var fullMessage = BuildConversationMessage();

            LLMClient.SendChat(
                _provider,
                _model,
                _systemPrompt,
                fullMessage,
                OnLLMResponse,
                error =>
                {
                    IsRunning = false;
                    OnError?.Invoke(error);
                    OnComplete?.Invoke();
                }
            );
        }

        private string BuildConversationMessage()
        {
            // For providers without multi-turn support in our simple client,
            // we pack the conversation into a single user message.
            var sb = new StringBuilder();

            for (int i = 0; i < _conversationHistory.Count; i++)
            {
                var (role, content) = _conversationHistory[i];
                switch (role)
                {
                    case "user":
                        sb.AppendLine($"[User]: {content}");
                        break;
                    case "assistant":
                        sb.AppendLine($"[Assistant]: {content}");
                        break;
                    case "tool_result":
                        sb.AppendLine($"[Tool Result]: {content}");
                        break;
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }

        private void OnLLMResponse(string response)
        {
            // Add to history
            _conversationHistory.Add(("assistant", response));

            // Parse tool calls
            var toolCalls = ToolExecutor.ParseToolCalls(response);

            // Get text outside tool blocks
            var textContent = ToolExecutor.GetTextOutsideTools(response);
            if (!string.IsNullOrEmpty(textContent))
            {
                OnTextOutput?.Invoke(textContent);
            }

            // If no tool calls, we're done
            if (toolCalls.Count == 0)
            {
                IsRunning = false;
                OnComplete?.Invoke();
                return;
            }

            // Execute each tool and collect results
            var resultsSb = new StringBuilder();
            foreach (var call in toolCalls)
            {
                var result = ToolExecutor.Execute(call);
                OnToolExecuted?.Invoke(call, result);

                resultsSb.AppendLine($"Tool '{call.ToolName}' result:");
                resultsSb.AppendLine(result);
                resultsSb.AppendLine();
            }

            // Add tool results to history and continue the loop
            _conversationHistory.Add(("tool_result", resultsSb.ToString()));

            // Continue: send results back to LLM for next step
            SendToLLM();
        }

        public void Stop()
        {
            IsRunning = false;
        }

        public void ClearHistory()
        {
            _conversationHistory.Clear();
            _currentIteration = 0;
        }
    }
}
