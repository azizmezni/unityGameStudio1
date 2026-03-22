using System;
using System.Collections.Generic;
using System.Linq;
using ClaudeCodeGameStudios.Data;
using ClaudeCodeGameStudios.Providers;
using ClaudeCodeGameStudios.Utilities;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ClaudeCodeGameStudios.Tabs
{
    public class ModelProviderTab
    {
        private readonly ClaudeProjectContext _ctx;
        private VisualElement _root;

        private static readonly Color CardBg = new(0.22f, 0.22f, 0.26f);
        private static readonly Color SectionBg = new(0.18f, 0.18f, 0.22f);
        private static readonly Color GreenStatus = new(0.3f, 0.9f, 0.4f);
        private static readonly Color RedStatus = new(0.9f, 0.3f, 0.3f);
        private static readonly Color YellowStatus = new(0.9f, 0.8f, 0.2f);

        public ModelProviderTab(ClaudeProjectContext ctx)
        {
            _ctx = ctx;
        }

        public VisualElement Build()
        {
            _root = new ScrollView(ScrollViewMode.Vertical);
            _root.style.paddingLeft = 12;
            _root.style.paddingRight = 12;
            _root.style.paddingTop = 8;

            var config = LLMProviderConfig.Instance;

            // Section 1: Provider Cards
            var providerHeader = CreateSectionHeader("LLM Providers", "Configure API keys and test connections");
            _root.Add(providerHeader);

            foreach (var provider in config.Providers)
            {
                _root.Add(CreateProviderCard(provider));
            }

            // Section 2: Tier Defaults
            _root.Add(CreateSectionHeader("Tier Defaults",
                "Default provider and model for each agent tier"));
            _root.Add(CreateTierDefaultsSection(config));

            // Section 3: Per-Agent Overrides
            _root.Add(CreateSectionHeader("Per-Agent Overrides",
                "Override provider/model for individual agents"));
            _root.Add(CreateAgentOverridesSection(config));

            return _root;
        }

        private VisualElement CreateSectionHeader(string title, string subtitle)
        {
            var header = new VisualElement();
            header.style.marginTop = 12;
            header.style.marginBottom = 6;

            var titleLabel = new Label(title);
            titleLabel.style.fontSize = 14;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.color = Color.white;
            header.Add(titleLabel);

            var subtitleLabel = new Label(subtitle);
            subtitleLabel.style.fontSize = 10;
            subtitleLabel.style.color = new Color(0.5f, 0.5f, 0.55f);
            header.Add(subtitleLabel);

            return header;
        }

        private VisualElement CreateProviderCard(LLMProvider provider)
        {
            var card = new VisualElement();
            card.style.backgroundColor = CardBg;
            StyleHelper.SetBorderRadius(card.style, 6);
            card.style.paddingLeft = 12;
            card.style.paddingRight = 12;
            card.style.paddingTop = 8;
            card.style.paddingBottom = 8;
            card.style.marginBottom = 4;

            // Header row: name + status
            var headerRow = new VisualElement();
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.alignItems = Align.Center;

            var nameLabel = new Label(provider.DisplayName);
            nameLabel.style.fontSize = 12;
            nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            nameLabel.style.color = Color.white;
            nameLabel.style.flexGrow = 1;
            headerRow.Add(nameLabel);

            // Status indicator
            var statusDot = new Label(provider.IsConnected ? "Connected" :
                provider.IsFetchingModels ? "Fetching..." : "Not tested");
            statusDot.style.fontSize = 9;
            statusDot.style.color = provider.IsConnected ? GreenStatus :
                provider.IsFetchingModels ? YellowStatus : new Color(0.5f, 0.5f, 0.5f);
            statusDot.style.paddingLeft = 6;
            statusDot.style.paddingRight = 6;
            statusDot.style.paddingTop = 2;
            statusDot.style.paddingBottom = 2;
            StyleHelper.SetBorderRadius(statusDot.style, 3);
            statusDot.style.backgroundColor = new Color(0.15f, 0.15f, 0.18f);
            headerRow.Add(statusDot);

            card.Add(headerRow);

            // Endpoint
            var endpointLabel = new Label(provider.Endpoint);
            endpointLabel.style.fontSize = 10;
            endpointLabel.style.color = new Color(0.5f, 0.5f, 0.55f);
            endpointLabel.style.marginTop = 2;
            card.Add(endpointLabel);

            // API Key field (if required)
            if (provider.RequiresApiKey)
            {
                var keyRow = new VisualElement();
                keyRow.style.flexDirection = FlexDirection.Row;
                keyRow.style.alignItems = Align.Center;
                keyRow.style.marginTop = 6;

                var keyLabel = new Label("API Key:");
                keyLabel.style.fontSize = 10;
                keyLabel.style.color = new Color(0.6f, 0.6f, 0.65f);
                keyLabel.style.width = 60;
                keyRow.Add(keyLabel);

                var currentKey = LLMProviderConfig.GetApiKey(provider.Type);
                var keyField = new TextField();
                keyField.isPasswordField = true;
                keyField.value = currentKey;
                keyField.style.flexGrow = 1;
                keyField.style.fontSize = 10;
                keyField.RegisterValueChangedCallback(evt =>
                {
                    LLMProviderConfig.SetApiKey(provider.Type, evt.newValue);
                });
                keyRow.Add(keyField);

                card.Add(keyRow);
            }

            // Custom endpoint (for Custom provider)
            if (provider.Type == ProviderType.Custom)
            {
                var epRow = new VisualElement();
                epRow.style.flexDirection = FlexDirection.Row;
                epRow.style.alignItems = Align.Center;
                epRow.style.marginTop = 4;

                var epLabel = new Label("Endpoint:");
                epLabel.style.fontSize = 10;
                epLabel.style.color = new Color(0.6f, 0.6f, 0.65f);
                epLabel.style.width = 60;
                epRow.Add(epLabel);

                var epField = new TextField();
                epField.value = provider.Endpoint;
                epField.style.flexGrow = 1;
                epField.style.fontSize = 10;
                epField.RegisterValueChangedCallback(evt =>
                {
                    provider.Endpoint = evt.newValue;
                    LLMProviderConfig.Instance.Save();
                });
                epRow.Add(epField);

                card.Add(epRow);
            }

            // Error message
            if (!string.IsNullOrEmpty(provider.ConnectionError))
            {
                var errorLabel = new Label(provider.ConnectionError);
                errorLabel.style.fontSize = 9;
                errorLabel.style.color = RedStatus;
                errorLabel.style.marginTop = 4;
                card.Add(errorLabel);
            }

            // Models count + action buttons
            var actionRow = new VisualElement();
            actionRow.style.flexDirection = FlexDirection.Row;
            actionRow.style.alignItems = Align.Center;
            actionRow.style.marginTop = 6;

            var modelCount = new Label(
                provider.AvailableModels.Count > 0
                    ? $"{provider.AvailableModels.Count} models available"
                    : "No models loaded");
            modelCount.style.fontSize = 10;
            modelCount.style.color = new Color(0.5f, 0.5f, 0.55f);
            modelCount.style.flexGrow = 1;
            actionRow.Add(modelCount);

            var testBtn = new Button(() =>
            {
                ProviderTestRunner.TestConnection(provider, (success, msg) =>
                {
                    provider.IsConnected = success;
                    provider.ConnectionError = success ? null : msg;
                    RebuildUI();
                });
            }) { text = "Test" };
            testBtn.style.height = 20;
            testBtn.style.fontSize = 10;
            actionRow.Add(testBtn);

            var fetchBtn = new Button(() =>
            {
                provider.IsFetchingModels = true;
                RebuildUI();
                ProviderTestRunner.FetchModels(provider, _ => RebuildUI());
            }) { text = "Fetch Models" };
            fetchBtn.style.height = 20;
            fetchBtn.style.fontSize = 10;
            fetchBtn.style.marginLeft = 4;
            actionRow.Add(fetchBtn);

            card.Add(actionRow);

            // Model list (if models are loaded, show top 10 with a "show all" toggle)
            if (provider.AvailableModels.Count > 0)
            {
                var modelsList = new VisualElement();
                modelsList.style.marginTop = 6;
                modelsList.style.backgroundColor = SectionBg;
                StyleHelper.SetBorderRadius(modelsList.style, 4);
                modelsList.style.paddingLeft = 8;
                modelsList.style.paddingRight = 8;
                modelsList.style.paddingTop = 4;
                modelsList.style.paddingBottom = 4;

                var displayModels = provider.AvailableModels.Take(15).ToList();
                foreach (var model in displayModels)
                {
                    var modelRow = new VisualElement();
                    modelRow.style.flexDirection = FlexDirection.Row;
                    modelRow.style.alignItems = Align.Center;
                    modelRow.style.paddingTop = 1;
                    modelRow.style.paddingBottom = 1;

                    var modelName = new Label(model.DisplayName);
                    modelName.style.fontSize = 10;
                    modelName.style.color = new Color(0.75f, 0.75f, 0.8f);
                    modelName.style.flexGrow = 1;
                    modelRow.Add(modelName);

                    if (!string.IsNullOrEmpty(model.Details))
                    {
                        var details = new Label(model.Details);
                        details.style.fontSize = 9;
                        details.style.color = new Color(0.45f, 0.45f, 0.5f);
                        modelRow.Add(details);
                    }

                    modelsList.Add(modelRow);
                }

                if (provider.AvailableModels.Count > 15)
                {
                    var moreLabel = new Label($"... and {provider.AvailableModels.Count - 15} more");
                    moreLabel.style.fontSize = 9;
                    moreLabel.style.color = new Color(0.4f, 0.4f, 0.45f);
                    moreLabel.style.paddingTop = 2;
                    modelsList.Add(moreLabel);
                }

                card.Add(modelsList);
            }

            return card;
        }

        private VisualElement CreateTierDefaultsSection(LLMProviderConfig config)
        {
            var section = new VisualElement();
            section.style.backgroundColor = CardBg;
            StyleHelper.SetBorderRadius(section.style, 6);
            section.style.paddingLeft = 12;
            section.style.paddingRight = 12;
            section.style.paddingTop = 8;
            section.style.paddingBottom = 8;
            section.style.marginBottom = 4;

            var providerNames = config.Providers.Select(p => p.DisplayName).ToList();
            var providerTypes = config.Providers.Select(p => p.Type).ToList();

            // Director tier
            section.Add(CreateTierRow("Directors", config.DirectorProvider, config.DirectorModel,
                providerNames, providerTypes, config,
                (p, m) => { config.DirectorProvider = p; config.DirectorModel = m; config.Save(); }));

            // Lead tier
            section.Add(CreateTierRow("Leads", config.LeadProvider, config.LeadModel,
                providerNames, providerTypes, config,
                (p, m) => { config.LeadProvider = p; config.LeadModel = m; config.Save(); }));

            // Specialist tier
            section.Add(CreateTierRow("Specialists", config.SpecialistProvider, config.SpecialistModel,
                providerNames, providerTypes, config,
                (p, m) => { config.SpecialistProvider = p; config.SpecialistModel = m; config.Save(); }));

            return section;
        }

        private VisualElement CreateTierRow(string tierName, ProviderType currentProvider,
            string currentModel, List<string> providerNames, List<ProviderType> providerTypes,
            LLMProviderConfig config, Action<ProviderType, string> onChanged)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 6;

            var tierLabel = new Label(tierName);
            tierLabel.style.fontSize = 11;
            tierLabel.style.color = Color.white;
            tierLabel.style.width = 100;
            tierLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            row.Add(tierLabel);

            // Provider dropdown
            var providerIdx = providerTypes.IndexOf(currentProvider);
            var providerDropdown = new PopupField<string>(providerNames, providerIdx >= 0 ? providerIdx : 0);
            providerDropdown.style.width = 140;
            providerDropdown.style.fontSize = 10;
            row.Add(providerDropdown);

            // Model dropdown
            var provider = config.GetProvider(currentProvider);
            var modelNames = provider?.AvailableModels.Select(m => m.Id).ToList() ?? new List<string>();
            if (modelNames.Count == 0) modelNames.Add(currentModel);

            var modelIdx = modelNames.IndexOf(currentModel);
            var modelDropdown = new PopupField<string>(modelNames, modelIdx >= 0 ? modelIdx : 0);
            modelDropdown.style.width = 200;
            modelDropdown.style.fontSize = 10;
            modelDropdown.style.marginLeft = 8;
            row.Add(modelDropdown);

            // Wire up provider change -> refresh model dropdown
            providerDropdown.RegisterValueChangedCallback(evt =>
            {
                var newProvIdx = providerNames.IndexOf(evt.newValue);
                if (newProvIdx >= 0)
                {
                    var newProvType = providerTypes[newProvIdx];
                    var newProv = config.GetProvider(newProvType);
                    var newModel = newProv?.AvailableModels.FirstOrDefault()?.Id ?? "";
                    onChanged(newProvType, newModel);
                    RebuildUI();
                }
            });

            modelDropdown.RegisterValueChangedCallback(evt =>
            {
                onChanged(currentProvider, evt.newValue);
            });

            return row;
        }

        private VisualElement CreateAgentOverridesSection(LLMProviderConfig config)
        {
            var section = new VisualElement();
            section.style.backgroundColor = CardBg;
            StyleHelper.SetBorderRadius(section.style, 6);
            section.style.paddingLeft = 12;
            section.style.paddingRight = 12;
            section.style.paddingTop = 8;
            section.style.paddingBottom = 8;

            if (_ctx.Agents.Count == 0)
            {
                var empty = new Label("No agents loaded. Check .claude/agents/ directory.");
                empty.style.fontSize = 11;
                empty.style.color = new Color(0.5f, 0.5f, 0.5f);
                section.Add(empty);
                return section;
            }

            // Column headers
            var headerRow = new VisualElement();
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.marginBottom = 4;
            headerRow.style.paddingBottom = 4;
            headerRow.style.borderBottomWidth = 1;
            headerRow.style.borderBottomColor = new Color(0.3f, 0.3f, 0.35f);

            AddHeaderLabel(headerRow, "Agent", 160);
            AddHeaderLabel(headerRow, "Tier", 80);
            AddHeaderLabel(headerRow, "Current Model", 180);
            AddHeaderLabel(headerRow, "Provider", 120);

            section.Add(headerRow);

            // Agent rows (show Unity-relevant agents only)
            foreach (var agent in _ctx.Agents.Where(a => a.IsUnityRelevant))
            {
                var (currentProvider, currentModel) = AgentModelAssignment.ParseModelValue(agent.Model);
                var providerDisplay = config.GetProvider(currentProvider)?.DisplayName ?? currentProvider.ToString();

                var agentRow = new VisualElement();
                agentRow.style.flexDirection = FlexDirection.Row;
                agentRow.style.alignItems = Align.Center;
                agentRow.style.paddingTop = 2;
                agentRow.style.paddingBottom = 2;

                var nameLabel = new Label(agent.Name);
                nameLabel.style.fontSize = 10;
                nameLabel.style.color = Color.white;
                nameLabel.style.width = 160;
                agentRow.Add(nameLabel);

                var tierLabel = new Label(agent.TierLabel);
                tierLabel.style.fontSize = 9;
                tierLabel.style.color = new Color(0.6f, 0.6f, 0.65f);
                tierLabel.style.width = 80;
                agentRow.Add(tierLabel);

                var modelLabel = new Label(agent.Model);
                modelLabel.style.fontSize = 10;
                modelLabel.style.color = new Color(0.7f, 0.7f, 0.75f);
                modelLabel.style.width = 180;
                agentRow.Add(modelLabel);

                var provLabel = new Label(providerDisplay);
                provLabel.style.fontSize = 9;
                provLabel.style.color = new Color(0.5f, 0.5f, 0.55f);
                provLabel.style.width = 120;
                agentRow.Add(provLabel);

                section.Add(agentRow);
            }

            return section;
        }

        private void AddHeaderLabel(VisualElement row, string text, float width)
        {
            var label = new Label(text);
            label.style.fontSize = 9;
            label.style.color = new Color(0.5f, 0.5f, 0.55f);
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.width = width;
            row.Add(label);
        }

        private void RebuildUI()
        {
            if (_root?.parent == null) return;
            var parent = _root.parent;
            var index = parent.IndexOf(_root);
            parent.Remove(_root);
            var newRoot = Build();
            parent.Insert(index, newRoot);
        }
    }
}
