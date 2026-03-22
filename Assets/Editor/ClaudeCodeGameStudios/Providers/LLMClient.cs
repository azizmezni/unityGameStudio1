using System;
using System.Text;
using ClaudeCodeGameStudios.Data;
using UnityEngine;
using UnityEngine.Networking;

namespace ClaudeCodeGameStudios.Providers
{
    /// <summary>
    /// Direct HTTP client for sending chat completions to any configured LLM provider.
    /// Bypasses Claude Code CLI entirely — talks to provider APIs directly.
    /// </summary>
    public static class LLMClient
    {
        [Serializable]
        private class ChatRequest
        {
            public string model;
            public ChatMessage[] messages;
            public float temperature;
            public int max_tokens;
        }

        [Serializable]
        private class ChatMessage
        {
            public string role;
            public string content;
        }

        // Anthropic uses a different request format
        [Serializable]
        private class AnthropicRequest
        {
            public string model;
            public int max_tokens;
            public AnthropicMessage[] messages;
            public string system;
        }

        [Serializable]
        private class AnthropicMessage
        {
            public string role;
            public string content;
        }

        // Google AI uses yet another format
        [Serializable]
        private class GoogleRequest
        {
            public GoogleContent[] contents;
            public GoogleSystemInstruction system_instruction;
        }

        [Serializable]
        private class GoogleContent
        {
            public string role;
            public GooglePart[] parts;
        }

        [Serializable]
        private class GooglePart
        {
            public string text;
        }

        [Serializable]
        private class GoogleSystemInstruction
        {
            public GooglePart[] parts;
        }

        // Ollama native chat format
        [Serializable]
        private class OllamaChatRequest
        {
            public string model;
            public OllamaMessage[] messages;
            public bool stream;
        }

        [Serializable]
        private class OllamaMessage
        {
            public string role;
            public string content;
        }

        /// <summary>
        /// Send a chat completion request to the configured provider.
        /// </summary>
        public static void SendChat(
            ProviderType providerType,
            string model,
            string systemPrompt,
            string userMessage,
            Action<string> onSuccess,
            Action<string> onError)
        {
            var config = LLMProviderConfig.Instance;
            var provider = config.GetProvider(providerType);
            if (provider == null)
            {
                onError?.Invoke($"Provider {providerType} not configured");
                return;
            }

            var apiKey = LLMProviderConfig.GetApiKey(providerType);

            if (provider.RequiresApiKey && string.IsNullOrEmpty(apiKey))
            {
                onError?.Invoke($"No API key set for {provider.DisplayName}. Go to Dashboard > Providers tab to add it.");
                return;
            }

            switch (providerType)
            {
                case ProviderType.Claude:
                    SendAnthropic(provider, apiKey, model, systemPrompt, userMessage, onSuccess, onError);
                    break;
                case ProviderType.GoogleAI:
                    SendGoogleAI(provider, apiKey, model, systemPrompt, userMessage, onSuccess, onError);
                    break;
                case ProviderType.OllamaLocal:
                case ProviderType.OllamaCloud:
                    SendOllama(provider, apiKey, model, systemPrompt, userMessage, onSuccess, onError);
                    break;
                default:
                    // OpenAI-compatible: OpenAI, OpenRouter, LMStudio, Groq, HuggingFace, Custom
                    SendOpenAICompatible(provider, apiKey, model, systemPrompt, userMessage, onSuccess, onError);
                    break;
            }
        }

        /// <summary>
        /// Send using OpenAI-compatible /v1/chat/completions.
        /// Works for: OpenAI, OpenRouter, LM Studio, Groq, HuggingFace, Custom.
        /// </summary>
        private static void SendOpenAICompatible(
            LLMProvider provider, string apiKey, string model,
            string systemPrompt, string userMessage,
            Action<string> onSuccess, Action<string> onError)
        {
            var url = $"{provider.Endpoint}/chat/completions";

            var request = new ChatRequest
            {
                model = model,
                temperature = 0.7f,
                max_tokens = 4096,
                messages = string.IsNullOrEmpty(systemPrompt)
                    ? new[] { new ChatMessage { role = "user", content = userMessage } }
                    : new[]
                    {
                        new ChatMessage { role = "system", content = systemPrompt },
                        new ChatMessage { role = "user", content = userMessage }
                    }
            };

            var json = JsonUtility.ToJson(request);
            var webRequest = CreatePostRequest(url, json);

            if (!string.IsNullOrEmpty(apiKey))
                webRequest.SetRequestHeader("Authorization", $"Bearer {apiKey}");

            SendRequest(webRequest, response =>
            {
                // Parse OpenAI response: { choices: [{ message: { content: "..." } }] }
                var parsed = JsonUtility.FromJson<OpenAIResponse>(response);
                if (parsed?.choices != null && parsed.choices.Length > 0)
                    onSuccess?.Invoke(parsed.choices[0].message.content);
                else
                    onError?.Invoke($"Empty response from {provider.DisplayName}");
            }, onError);
        }

        /// <summary>
        /// Send using Anthropic /v1/messages format.
        /// </summary>
        private static void SendAnthropic(
            LLMProvider provider, string apiKey, string model,
            string systemPrompt, string userMessage,
            Action<string> onSuccess, Action<string> onError)
        {
            var url = $"{provider.Endpoint}/messages";

            var request = new AnthropicRequest
            {
                model = model,
                max_tokens = 4096,
                system = systemPrompt ?? "",
                messages = new[]
                {
                    new AnthropicMessage { role = "user", content = userMessage }
                }
            };

            var json = JsonUtility.ToJson(request);
            var webRequest = CreatePostRequest(url, json);
            webRequest.SetRequestHeader("x-api-key", apiKey);
            webRequest.SetRequestHeader("anthropic-version", "2023-06-01");

            SendRequest(webRequest, response =>
            {
                // Parse Anthropic response: { content: [{ text: "..." }] }
                var parsed = JsonUtility.FromJson<AnthropicResponse>(response);
                if (parsed?.content != null && parsed.content.Length > 0)
                    onSuccess?.Invoke(parsed.content[0].text);
                else
                    onError?.Invoke($"Empty response from Claude");
            }, onError);
        }

        /// <summary>
        /// Send using Google AI generateContent format.
        /// </summary>
        private static void SendGoogleAI(
            LLMProvider provider, string apiKey, string model,
            string systemPrompt, string userMessage,
            Action<string> onSuccess, Action<string> onError)
        {
            var url = $"{provider.Endpoint}/models/{model}:generateContent?key={apiKey}";

            var request = new GoogleRequest
            {
                contents = new[]
                {
                    new GoogleContent
                    {
                        role = "user",
                        parts = new[] { new GooglePart { text = userMessage } }
                    }
                }
            };

            if (!string.IsNullOrEmpty(systemPrompt))
            {
                request.system_instruction = new GoogleSystemInstruction
                {
                    parts = new[] { new GooglePart { text = systemPrompt } }
                };
            }

            var json = JsonUtility.ToJson(request);
            var webRequest = CreatePostRequest(url, json);

            SendRequest(webRequest, response =>
            {
                var parsed = JsonUtility.FromJson<GoogleResponse>(response);
                if (parsed?.candidates != null && parsed.candidates.Length > 0 &&
                    parsed.candidates[0].content?.parts != null &&
                    parsed.candidates[0].content.parts.Length > 0)
                    onSuccess?.Invoke(parsed.candidates[0].content.parts[0].text);
                else
                    onError?.Invoke("Empty response from Google AI");
            }, onError);
        }

        /// <summary>
        /// Send using Ollama /api/chat format. Works for both local and cloud.
        /// </summary>
        private static void SendOllama(
            LLMProvider provider, string apiKey, string model,
            string systemPrompt, string userMessage,
            Action<string> onSuccess, Action<string> onError)
        {
            var url = $"{provider.Endpoint}/api/chat";

            var messages = string.IsNullOrEmpty(systemPrompt)
                ? new[] { new OllamaMessage { role = "user", content = userMessage } }
                : new[]
                {
                    new OllamaMessage { role = "system", content = systemPrompt },
                    new OllamaMessage { role = "user", content = userMessage }
                };

            var request = new OllamaChatRequest
            {
                model = model,
                messages = messages,
                stream = false
            };

            var json = JsonUtility.ToJson(request);
            var webRequest = CreatePostRequest(url, json);

            if (!string.IsNullOrEmpty(apiKey))
                webRequest.SetRequestHeader("Authorization", $"Bearer {apiKey}");

            SendRequest(webRequest, response =>
            {
                var parsed = JsonUtility.FromJson<OllamaChatResponse>(response);
                if (parsed?.message != null && !string.IsNullOrEmpty(parsed.message.content))
                    onSuccess?.Invoke(parsed.message.content);
                else
                    onError?.Invoke("Empty response from Ollama");
            }, onError);
        }

        private static UnityWebRequest CreatePostRequest(string url, string json)
        {
            var request = new UnityWebRequest(url, "POST");
            var bodyBytes = Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(bodyBytes);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 120;
            return request;
        }

        private static void SendRequest(UnityWebRequest request, Action<string> onSuccess, Action<string> onError)
        {
            var operation = request.SendWebRequest();
            operation.completed += _ =>
            {
                if (request.result != UnityWebRequest.Result.Success)
                {
                    var errorBody = request.downloadHandler?.text ?? "";
                    var errorMsg = $"HTTP {request.responseCode}: {request.error}";
                    if (!string.IsNullOrEmpty(errorBody) && errorBody.Length < 500)
                        errorMsg += $"\n{errorBody}";
                    onError?.Invoke(errorMsg);
                }
                else
                {
                    onSuccess?.Invoke(request.downloadHandler.text);
                }
                request.Dispose();
            };
        }

        // Response models for JsonUtility

        [Serializable]
        private class OpenAIResponse
        {
            public OpenAIChoice[] choices;
        }

        [Serializable]
        private class OpenAIChoice
        {
            public OpenAIChoiceMessage message;
        }

        [Serializable]
        private class OpenAIChoiceMessage
        {
            public string content;
        }

        [Serializable]
        private class AnthropicResponse
        {
            public AnthropicContentBlock[] content;
        }

        [Serializable]
        private class AnthropicContentBlock
        {
            public string text;
        }

        [Serializable]
        private class GoogleResponse
        {
            public GoogleCandidate[] candidates;
        }

        [Serializable]
        private class GoogleCandidate
        {
            public GoogleContent content;
        }

        [Serializable]
        private class OllamaChatResponse
        {
            public OllamaMessage message;
        }
    }
}
