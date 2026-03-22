using System.IO;
using System.Text.RegularExpressions;
using ClaudeCodeGameStudios.Data;
using UnityEngine;

namespace ClaudeCodeGameStudios.Providers
{
    /// <summary>
    /// Manages per-agent model assignments and writes changes back to agent .md frontmatter.
    /// </summary>
    public static class AgentModelAssignment
    {
        private static readonly Regex ModelLineRegex = new(
            @"^model:\s*(.+)$", RegexOptions.Multiline);

        /// <summary>
        /// Updates the model field in an agent's .md frontmatter file.
        /// For Claude: writes just the model tier (opus, sonnet, haiku).
        /// For other providers: writes provider/model (e.g., ollama/llama3.1:8b).
        /// </summary>
        public static bool UpdateAgentModel(AgentData agent, ProviderType provider, string model)
        {
            if (string.IsNullOrEmpty(agent.FilePath) || !File.Exists(agent.FilePath))
            {
                Debug.LogWarning($"[ClaudeCode] Agent file not found: {agent.FilePath}");
                return false;
            }

            var content = File.ReadAllText(agent.FilePath);

            // Determine the new model value
            var newModelValue = FormatModelValue(provider, model);

            // Replace the model: line in frontmatter
            if (ModelLineRegex.IsMatch(content))
            {
                content = ModelLineRegex.Replace(content, $"model: {newModelValue}", 1);
            }
            else
            {
                // If no model line exists, add it after the description line
                var descRegex = new Regex(@"^(description:\s*.+)$", RegexOptions.Multiline);
                content = descRegex.Replace(content, $"$1\nmodel: {newModelValue}", 1);
            }

            File.WriteAllText(agent.FilePath, content);
            Debug.Log($"[ClaudeCode] Updated {agent.Name} model to: {newModelValue}");

            // Update the in-memory agent data
            agent.Model = newModelValue;

            return true;
        }

        /// <summary>
        /// Formats the model value for frontmatter storage.
        /// </summary>
        public static string FormatModelValue(ProviderType provider, string model)
        {
            return provider switch
            {
                // Claude uses simple tier names
                ProviderType.Claude => model switch
                {
                    "claude-opus-4-6" => "opus",
                    "claude-sonnet-4-6" => "sonnet",
                    "claude-haiku-4-5-20251001" => "haiku",
                    _ => model
                },
                // Other providers use provider/model format
                ProviderType.OpenAI => $"openai/{model}",
                ProviderType.OpenRouter => $"openrouter/{model}",
                ProviderType.OllamaLocal => $"ollama/{model}",
                ProviderType.OllamaCloud => $"ollama-cloud/{model}",
                ProviderType.LMStudio => $"lmstudio/{model}",
                ProviderType.GoogleAI => $"google/{model}",
                ProviderType.Groq => $"groq/{model}",
                ProviderType.HuggingFace => $"huggingface/{model}",
                ProviderType.Custom => $"custom/{model}",
                _ => model
            };
        }

        /// <summary>
        /// Parses a model value from frontmatter back to provider + model.
        /// </summary>
        public static (ProviderType provider, string model) ParseModelValue(string value)
        {
            if (string.IsNullOrEmpty(value))
                return (ProviderType.Claude, "claude-sonnet-4-6");

            // Simple Claude tier names
            if (value == "opus") return (ProviderType.Claude, "claude-opus-4-6");
            if (value == "sonnet") return (ProviderType.Claude, "claude-sonnet-4-6");
            if (value == "haiku") return (ProviderType.Claude, "claude-haiku-4-5-20251001");

            // Provider/model format
            var slashIdx = value.IndexOf('/');
            if (slashIdx > 0)
            {
                var prefix = value.Substring(0, slashIdx);
                var model = value.Substring(slashIdx + 1);

                var provider = prefix switch
                {
                    "openai" => ProviderType.OpenAI,
                    "openrouter" => ProviderType.OpenRouter,
                    "ollama" => ProviderType.OllamaLocal,
                    "ollama-cloud" => ProviderType.OllamaCloud,
                    "lmstudio" => ProviderType.LMStudio,
                    "google" => ProviderType.GoogleAI,
                    "groq" => ProviderType.Groq,
                    "huggingface" => ProviderType.HuggingFace,
                    "custom" => ProviderType.Custom,
                    _ => ProviderType.Claude
                };

                return (provider, model);
            }

            // Fallback: assume Claude
            return (ProviderType.Claude, value);
        }
    }
}
