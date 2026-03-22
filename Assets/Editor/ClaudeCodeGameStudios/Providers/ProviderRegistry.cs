using System;
using System.Collections.Generic;

namespace ClaudeCodeGameStudios.Providers
{
    /// <summary>
    /// Registry of all supported LLM providers with default endpoints,
    /// authentication requirements, and hardcoded model lists where applicable.
    /// </summary>
    public static class ProviderRegistry
    {
        public static List<LLMProvider> CreateDefaultProviders()
        {
            return new List<LLMProvider>
            {
                new()
                {
                    Type = ProviderType.Claude,
                    Name = "Claude (Anthropic)",
                    Endpoint = "https://api.anthropic.com/v1",
                    RequiresApiKey = true,
                    ApiKeyPrefsKey = "ClaudeCode_ApiKey_Claude",
                    AvailableModels = GetClaudeModels()
                },
                new()
                {
                    Type = ProviderType.OpenAI,
                    Name = "OpenAI",
                    Endpoint = "https://api.openai.com/v1",
                    RequiresApiKey = true,
                    ApiKeyPrefsKey = "ClaudeCode_ApiKey_OpenAI"
                },
                new()
                {
                    Type = ProviderType.OpenRouter,
                    Name = "OpenRouter",
                    Endpoint = "https://openrouter.ai/api/v1",
                    RequiresApiKey = true,
                    ApiKeyPrefsKey = "ClaudeCode_ApiKey_OpenRouter"
                },
                new()
                {
                    Type = ProviderType.OllamaLocal,
                    Name = "Ollama (Local)",
                    Endpoint = "http://localhost:11434",
                    RequiresApiKey = false,
                    ApiKeyPrefsKey = ""
                },
                new()
                {
                    Type = ProviderType.OllamaCloud,
                    Name = "Ollama Cloud",
                    Endpoint = "https://ollama.com",
                    RequiresApiKey = true,
                    ApiKeyPrefsKey = "ClaudeCode_ApiKey_OllamaCloud"
                },
                new()
                {
                    Type = ProviderType.LMStudio,
                    Name = "LM Studio",
                    Endpoint = "http://localhost:1234/v1",
                    RequiresApiKey = false,
                    ApiKeyPrefsKey = ""
                },
                new()
                {
                    Type = ProviderType.GoogleAI,
                    Name = "Google AI",
                    Endpoint = "https://generativelanguage.googleapis.com/v1beta",
                    RequiresApiKey = true,
                    ApiKeyPrefsKey = "ClaudeCode_ApiKey_GoogleAI"
                },
                new()
                {
                    Type = ProviderType.Groq,
                    Name = "Groq",
                    Endpoint = "https://api.groq.com/openai/v1",
                    RequiresApiKey = true,
                    ApiKeyPrefsKey = "ClaudeCode_ApiKey_Groq"
                },
                new()
                {
                    Type = ProviderType.HuggingFace,
                    Name = "Hugging Face",
                    Endpoint = "https://router.huggingface.co/v1",
                    RequiresApiKey = true,
                    ApiKeyPrefsKey = "ClaudeCode_ApiKey_HuggingFace"
                },
                new()
                {
                    Type = ProviderType.Custom,
                    Name = "Custom",
                    Endpoint = "",
                    RequiresApiKey = false,
                    ApiKeyPrefsKey = "ClaudeCode_ApiKey_Custom"
                }
            };
        }

        /// <summary>
        /// Returns the model discovery URL for a provider.
        /// </summary>
        public static string GetModelListUrl(LLMProvider provider)
        {
            return provider.Type switch
            {
                ProviderType.Claude => "", // Hardcoded, no API
                ProviderType.OpenAI => $"{provider.Endpoint}/models",
                ProviderType.OpenRouter => $"{provider.Endpoint}/models",
                ProviderType.OllamaLocal => $"{provider.Endpoint}/api/tags",
                ProviderType.OllamaCloud => $"{provider.Endpoint}/api/tags",
                ProviderType.LMStudio => $"{provider.Endpoint}/models",
                ProviderType.GoogleAI => $"{provider.Endpoint}/models",
                ProviderType.Groq => $"{provider.Endpoint}/models",
                ProviderType.HuggingFace => $"{provider.Endpoint}/models",
                ProviderType.Custom => string.IsNullOrEmpty(provider.Endpoint)
                    ? "" : $"{provider.Endpoint}/models",
                _ => ""
            };
        }

        /// <summary>
        /// Returns the auth header format for a provider.
        /// </summary>
        public static (string headerName, string headerValue) GetAuthHeader(LLMProvider provider, string apiKey)
        {
            if (string.IsNullOrEmpty(apiKey)) return ("", "");

            return provider.Type switch
            {
                ProviderType.Claude => ("x-api-key", apiKey),
                ProviderType.GoogleAI => ("", ""), // Uses query param ?key=
                _ => ("Authorization", $"Bearer {apiKey}")
            };
        }

        /// <summary>
        /// For Google AI, the API key goes in the URL query param.
        /// </summary>
        public static string GetModelListUrlWithAuth(LLMProvider provider, string apiKey)
        {
            var baseUrl = GetModelListUrl(provider);
            if (provider.Type == ProviderType.GoogleAI && !string.IsNullOrEmpty(apiKey))
                return $"{baseUrl}?key={apiKey}";
            return baseUrl;
        }

        private static List<ModelInfo> GetClaudeModels()
        {
            return new List<ModelInfo>
            {
                new() { Id = "claude-opus-4-6", DisplayName = "Claude Opus 4.6", OwnedBy = "anthropic", Details = "Most capable" },
                new() { Id = "claude-sonnet-4-6", DisplayName = "Claude Sonnet 4.6", OwnedBy = "anthropic", Details = "Balanced" },
                new() { Id = "claude-haiku-4-5-20251001", DisplayName = "Claude Haiku 4.5", OwnedBy = "anthropic", Details = "Fast & light" }
            };
        }
    }
}
