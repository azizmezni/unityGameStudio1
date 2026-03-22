using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace ClaudeCodeGameStudios.Providers
{
    /// <summary>
    /// Tests provider connectivity and fetches available models via HTTP.
    /// Uses UnityWebRequest for async HTTP calls within the editor.
    /// </summary>
    public static class ProviderTestRunner
    {
        public static void FetchModels(LLMProvider provider, Action<LLMProvider> onComplete)
        {
            // Claude has hardcoded models — no API call needed
            if (provider.Type == ProviderType.Claude)
            {
                provider.IsConnected = true;
                provider.IsFetchingModels = false;
                provider.ConnectionError = null;
                onComplete?.Invoke(provider);
                return;
            }

            var apiKey = LLMProviderConfig.GetApiKey(provider.Type);
            var url = provider.Type == ProviderType.GoogleAI
                ? ProviderRegistry.GetModelListUrlWithAuth(provider, apiKey)
                : ProviderRegistry.GetModelListUrl(provider);

            if (string.IsNullOrEmpty(url))
            {
                provider.IsConnected = false;
                provider.IsFetchingModels = false;
                provider.ConnectionError = "No endpoint configured";
                onComplete?.Invoke(provider);
                return;
            }

            provider.IsFetchingModels = true;

            var request = UnityWebRequest.Get(url);

            // Set auth header
            var (headerName, headerValue) = ProviderRegistry.GetAuthHeader(provider, apiKey);
            if (!string.IsNullOrEmpty(headerName))
                request.SetRequestHeader(headerName, headerValue);

            var operation = request.SendWebRequest();
            operation.completed += _ =>
            {
                provider.IsFetchingModels = false;

                if (request.result != UnityWebRequest.Result.Success)
                {
                    provider.IsConnected = false;
                    provider.ConnectionError = request.error;
                    Debug.LogWarning($"[ClaudeCode] Failed to fetch models from {provider.DisplayName}: {request.error}");
                    onComplete?.Invoke(provider);
                    request.Dispose();
                    return;
                }

                provider.IsConnected = true;
                provider.ConnectionError = null;

                try
                {
                    var json = request.downloadHandler.text;
                    provider.AvailableModels = ParseModels(provider.Type, json);
                    Debug.Log($"[ClaudeCode] Fetched {provider.AvailableModels.Count} models from {provider.DisplayName}");
                }
                catch (Exception e)
                {
                    provider.ConnectionError = $"Parse error: {e.Message}";
                    Debug.LogWarning($"[ClaudeCode] Failed to parse models from {provider.DisplayName}: {e.Message}");
                }

                onComplete?.Invoke(provider);
                request.Dispose();
            };
        }

        public static void TestConnection(LLMProvider provider, Action<bool, string> onResult)
        {
            if (provider.Type == ProviderType.Claude)
            {
                onResult?.Invoke(true, "OK (hardcoded models)");
                return;
            }

            var apiKey = LLMProviderConfig.GetApiKey(provider.Type);
            var url = provider.Type == ProviderType.GoogleAI
                ? ProviderRegistry.GetModelListUrlWithAuth(provider, apiKey)
                : ProviderRegistry.GetModelListUrl(provider);

            if (string.IsNullOrEmpty(url))
            {
                onResult?.Invoke(false, "No endpoint configured");
                return;
            }

            var request = UnityWebRequest.Get(url);
            var (headerName, headerValue) = ProviderRegistry.GetAuthHeader(provider, apiKey);
            if (!string.IsNullOrEmpty(headerName))
                request.SetRequestHeader(headerName, headerValue);

            var operation = request.SendWebRequest();
            operation.completed += _ =>
            {
                var success = request.result == UnityWebRequest.Result.Success;
                var msg = success ? $"OK ({request.responseCode})" : request.error;
                onResult?.Invoke(success, msg);
                request.Dispose();
            };
        }

        private static List<ModelInfo> ParseModels(ProviderType type, string json)
        {
            return type switch
            {
                ProviderType.OllamaLocal or ProviderType.OllamaCloud => ParseOllamaModels(json),
                ProviderType.GoogleAI => ParseGoogleModels(json),
                _ => ParseOpenAICompatibleModels(json, type)
            };
        }

        /// <summary>
        /// Parses OpenAI-compatible /v1/models response.
        /// Used by: OpenAI, OpenRouter, LM Studio, Groq, HuggingFace, Custom.
        /// </summary>
        private static List<ModelInfo> ParseOpenAICompatibleModels(string json, ProviderType type)
        {
            var response = JsonUtility.FromJson<OpenAIModelsResponse>(json);
            if (response?.data == null) return new List<ModelInfo>();

            var models = new List<ModelInfo>();
            foreach (var m in response.data)
            {
                // Filter out non-chat models for OpenAI
                if (type == ProviderType.OpenAI)
                {
                    if (m.id.Contains("embedding") || m.id.Contains("whisper") ||
                        m.id.Contains("dall-e") || m.id.Contains("tts") ||
                        m.id.Contains("davinci") || m.id.Contains("babbage"))
                        continue;
                }

                models.Add(new ModelInfo
                {
                    Id = m.id,
                    DisplayName = m.id,
                    OwnedBy = m.owned_by ?? "",
                    Details = type == ProviderType.Groq ? m.owned_by : ""
                });
            }

            // Limit HuggingFace to top 50
            if (type == ProviderType.HuggingFace && models.Count > 50)
                models = models.Take(50).ToList();

            return models.OrderBy(m => m.Id).ToList();
        }

        /// <summary>
        /// Parses Ollama /api/tags response.
        /// </summary>
        private static List<ModelInfo> ParseOllamaModels(string json)
        {
            var response = JsonUtility.FromJson<OllamaTagsResponse>(json);
            if (response?.models == null) return new List<ModelInfo>();

            return response.models.Select(m => new ModelInfo
            {
                Id = m.name,
                DisplayName = m.name,
                Details = FormatBytes(m.size)
            }).OrderBy(m => m.Id).ToList();
        }

        /// <summary>
        /// Parses Google AI /v1beta/models response.
        /// </summary>
        private static List<ModelInfo> ParseGoogleModels(string json)
        {
            var response = JsonUtility.FromJson<GoogleModelsResponse>(json);
            if (response?.models == null) return new List<ModelInfo>();

            return response.models
                .Where(m => m.supportedGenerationMethods != null &&
                           m.supportedGenerationMethods.Contains("generateContent"))
                .Select(m => new ModelInfo
                {
                    Id = m.name?.Replace("models/", "") ?? "",
                    DisplayName = m.displayName ?? m.name ?? "",
                    Details = m.description ?? ""
                })
                .OrderBy(m => m.Id).ToList();
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes <= 0) return "";
            if (bytes < 1024 * 1024) return $"{bytes / 1024f:F0} KB";
            if (bytes < 1024L * 1024 * 1024) return $"{bytes / (1024f * 1024f):F1} MB";
            return $"{bytes / (1024f * 1024f * 1024f):F1} GB";
        }

        // JSON response models (Unity JsonUtility requires serializable classes)

        [Serializable]
        private class OpenAIModelsResponse
        {
            public OpenAIModel[] data;
        }

        [Serializable]
        private class OpenAIModel
        {
            public string id;
            public string owned_by;
        }

        [Serializable]
        private class OllamaTagsResponse
        {
            public OllamaModel[] models;
        }

        [Serializable]
        private class OllamaModel
        {
            public string name;
            public long size;
        }

        [Serializable]
        private class GoogleModelsResponse
        {
            public GoogleModel[] models;
        }

        [Serializable]
        private class GoogleModel
        {
            public string name;
            public string displayName;
            public string description;
            public string[] supportedGenerationMethods;
        }
    }
}
