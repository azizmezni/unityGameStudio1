using System;
using System.Collections.Generic;

namespace ClaudeCodeGameStudios.Providers
{
    public enum ProviderType
    {
        Claude,
        OpenAI,
        OpenRouter,
        OllamaLocal,
        OllamaCloud,
        LMStudio,
        GoogleAI,
        Groq,
        HuggingFace,
        Custom
    }

    [Serializable]
    public class ModelInfo
    {
        public string Id;
        public string DisplayName;
        public string OwnedBy;
        public string Details; // size, pricing, etc.

        public override string ToString() =>
            string.IsNullOrEmpty(Details) ? DisplayName : $"{DisplayName} ({Details})";
    }

    [Serializable]
    public class LLMProvider
    {
        public ProviderType Type;
        public string Name;
        public string Endpoint;
        public bool RequiresApiKey;
        public string ApiKeyPrefsKey; // EditorPrefs key for API key storage
        public bool IsConnected;
        public string ConnectionError;
        public List<ModelInfo> AvailableModels = new();
        public string SelectedModel;
        public bool IsFetchingModels;

        public string DisplayName => Type switch
        {
            ProviderType.Claude => "Claude (Anthropic)",
            ProviderType.OpenAI => "OpenAI",
            ProviderType.OpenRouter => "OpenRouter",
            ProviderType.OllamaLocal => "Ollama (Local)",
            ProviderType.OllamaCloud => "Ollama Cloud",
            ProviderType.LMStudio => "LM Studio",
            ProviderType.GoogleAI => "Google AI",
            ProviderType.Groq => "Groq",
            ProviderType.HuggingFace => "Hugging Face",
            ProviderType.Custom => Name ?? "Custom",
            _ => "Unknown"
        };
    }

    [Serializable]
    public class FallbackChain
    {
        public string TierName; // "Director", "Lead", "Specialist"
        public List<FallbackEntry> Entries = new();
    }

    [Serializable]
    public class FallbackEntry
    {
        public ProviderType Provider;
        public string Model;
    }

    [Serializable]
    public class AgentModelOverride
    {
        public string AgentName;
        public ProviderType Provider;
        public string Model;
    }
}
