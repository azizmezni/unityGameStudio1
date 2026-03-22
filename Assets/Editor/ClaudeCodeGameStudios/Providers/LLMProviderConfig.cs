using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ClaudeCodeGameStudios.Providers
{
    /// <summary>
    /// Persisted provider configuration. Saved as ScriptableObject asset.
    /// API keys are stored in EditorPrefs, not in this asset (to avoid committing secrets).
    /// </summary>
    [CreateAssetMenu(menuName = "Claude Code/Provider Config")]
    public class LLMProviderConfig : ScriptableObject
    {
        public List<LLMProvider> Providers = new();
        public List<FallbackChain> FallbackChains = new();
        public List<AgentModelOverride> AgentOverrides = new();

        // Tier defaults
        public ProviderType DirectorProvider = ProviderType.Claude;
        public string DirectorModel = "claude-opus-4-6";
        public ProviderType LeadProvider = ProviderType.Claude;
        public string LeadModel = "claude-sonnet-4-6";
        public ProviderType SpecialistProvider = ProviderType.Claude;
        public string SpecialistModel = "claude-haiku-4-5-20251001";

        private static LLMProviderConfig _instance;
        private const string AssetPath = "Assets/Editor/ClaudeCodeGameStudios/Resources/ProviderConfig.asset";

        public static LLMProviderConfig Instance
        {
            get
            {
                if (_instance != null) return _instance;

                _instance = AssetDatabase.LoadAssetAtPath<LLMProviderConfig>(AssetPath);
                if (_instance == null)
                {
                    _instance = CreateInstance<LLMProviderConfig>();
                    _instance.InitializeDefaults();

                    var dir = Path.GetDirectoryName(AssetPath);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                        Directory.CreateDirectory(dir);

                    AssetDatabase.CreateAsset(_instance, AssetPath);
                    AssetDatabase.SaveAssets();
                }

                return _instance;
            }
        }

        private void InitializeDefaults()
        {
            Providers = ProviderRegistry.CreateDefaultProviders();

            FallbackChains = new List<FallbackChain>
            {
                new()
                {
                    TierName = "Director",
                    Entries = new List<FallbackEntry>
                    {
                        new() { Provider = ProviderType.Claude, Model = "claude-opus-4-6" },
                        new() { Provider = ProviderType.OpenRouter, Model = "anthropic/claude-3-opus" }
                    }
                },
                new()
                {
                    TierName = "Lead",
                    Entries = new List<FallbackEntry>
                    {
                        new() { Provider = ProviderType.Claude, Model = "claude-sonnet-4-6" },
                        new() { Provider = ProviderType.OpenRouter, Model = "anthropic/claude-3-sonnet" }
                    }
                },
                new()
                {
                    TierName = "Specialist",
                    Entries = new List<FallbackEntry>
                    {
                        new() { Provider = ProviderType.Claude, Model = "claude-haiku-4-5-20251001" },
                        new() { Provider = ProviderType.OllamaLocal, Model = "llama3.1:8b" }
                    }
                }
            };
        }

        public void Save()
        {
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
        }

        public static string GetApiKey(ProviderType type)
        {
            return EditorPrefs.GetString($"ClaudeCode_ApiKey_{type}", "");
        }

        public static void SetApiKey(ProviderType type, string key)
        {
            EditorPrefs.SetString($"ClaudeCode_ApiKey_{type}", key);
        }

        public LLMProvider GetProvider(ProviderType type)
        {
            return Providers.Find(p => p.Type == type);
        }

        public (ProviderType provider, string model) GetAgentConfig(string agentName, Data.AgentTier tier)
        {
            // Check per-agent override first
            var overrideEntry = AgentOverrides.Find(o => o.AgentName == agentName);
            if (overrideEntry != null)
                return (overrideEntry.Provider, overrideEntry.Model);

            // Fall back to tier default
            return tier switch
            {
                Data.AgentTier.Director => (DirectorProvider, DirectorModel),
                Data.AgentTier.Lead => (LeadProvider, LeadModel),
                _ => (SpecialistProvider, SpecialistModel)
            };
        }
    }
}
