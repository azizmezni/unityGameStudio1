using System.Collections.Generic;
using System.Linq;
using ClaudeCodeGameStudios.Data;
using ClaudeCodeGameStudios.Utilities;
using UnityEngine;
using UnityEngine.UIElements;

namespace ClaudeCodeGameStudios.Tabs
{
    public class AgentRosterTab
    {
        private readonly ClaudeProjectContext _ctx;
        private bool _showNonUnity = true;
        private string _searchFilter = "";

        private static readonly Dictionary<AgentTier, Color> TierColors = new()
        {
            { AgentTier.Director, new Color(1f, 0.84f, 0f) },       // Gold
            { AgentTier.Lead, new Color(0.3f, 0.6f, 1f) },          // Blue
            { AgentTier.Specialist, new Color(0.3f, 0.85f, 0.4f) }, // Green
            { AgentTier.SubSpecialist, new Color(0.7f, 0.4f, 1f) }  // Purple
        };

        private static readonly Dictionary<string, Color> ModelColors = new()
        {
            { "opus", new Color(1f, 0.4f, 0.2f) },
            { "sonnet", new Color(0.2f, 0.7f, 1f) },
            { "haiku", new Color(0.5f, 0.9f, 0.5f) }
        };

        public AgentRosterTab(ClaudeProjectContext ctx)
        {
            _ctx = ctx;
        }

        public VisualElement Build()
        {
            var root = new ScrollView(ScrollViewMode.Vertical);
            root.style.paddingLeft = 12;
            root.style.paddingRight = 12;
            root.style.paddingTop = 8;

            // Search + filter bar
            var filterBar = new VisualElement();
            filterBar.style.flexDirection = FlexDirection.Row;
            filterBar.style.marginBottom = 8;

            var searchField = new TextField();
            searchField.value = _searchFilter;
            searchField.style.flexGrow = 1;
            searchField.style.marginRight = 8;
            searchField.RegisterValueChangedCallback(evt =>
            {
                _searchFilter = evt.newValue;
                RefreshList(root);
            });
            var placeholder = searchField.Q<Label>(className: "unity-text-element");
            searchField.Q<TextElement>().style.color = new Color(0.7f, 0.7f, 0.7f);
            filterBar.Add(searchField);

            var toggleNonUnity = new Toggle("Show non-Unity agents");
            toggleNonUnity.value = _showNonUnity;
            toggleNonUnity.RegisterValueChangedCallback(evt =>
            {
                _showNonUnity = evt.newValue;
                RefreshList(root);
            });
            filterBar.Add(toggleNonUnity);

            root.Add(filterBar);

            // Summary
            var summary = new Label($"{_ctx.Agents.Count} agents loaded");
            summary.style.fontSize = 11;
            summary.style.color = new Color(0.6f, 0.6f, 0.6f);
            summary.style.marginBottom = 8;
            root.Add(summary);

            BuildAgentList(root);
            return root;
        }

        private void RefreshList(ScrollView root)
        {
            // Remove everything after the first 2 elements (filter bar + summary)
            while (root.childCount > 2)
                root.RemoveAt(2);
            BuildAgentList(root);
        }

        private void BuildAgentList(VisualElement root)
        {
            var filtered = _ctx.Agents.Where(a =>
            {
                if (!_showNonUnity && !a.IsUnityRelevant) return false;
                if (!string.IsNullOrEmpty(_searchFilter))
                {
                    var search = _searchFilter.ToLower();
                    return a.Name.ToLower().Contains(search) ||
                           a.Description.ToLower().Contains(search);
                }
                return true;
            }).ToList();

            var grouped = filtered.GroupBy(a => a.Tier)
                .OrderBy(g => g.Key);

            foreach (var group in grouped)
            {
                var foldout = new Foldout();
                foldout.text = $"{group.Key} ({group.Count()})";
                foldout.value = true;
                foldout.style.marginBottom = 4;

                var headerLabel = foldout.Q<Toggle>().Q<Label>();
                if (headerLabel != null)
                {
                    headerLabel.style.fontSize = 13;
                    headerLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                    headerLabel.style.color = TierColors.TryGetValue(group.Key, out var tc) ? tc : Color.white;
                }

                foreach (var agent in group)
                {
                    foldout.Add(CreateAgentCard(agent));
                }

                root.Add(foldout);
            }

            if (!filtered.Any())
            {
                var empty = new Label("No agents match the current filter.");
                empty.style.color = new Color(0.5f, 0.5f, 0.5f);
                empty.style.unityTextAlign = TextAnchor.MiddleCenter;
                empty.style.paddingTop = 20;
                root.Add(empty);
            }
        }

        private VisualElement CreateAgentCard(AgentData agent)
        {
            var card = new VisualElement();
            card.style.backgroundColor = new Color(0.22f, 0.22f, 0.26f);
            StyleHelper.SetBorderRadius(card.style, 4);
            card.style.paddingLeft = 10;
            card.style.paddingRight = 10;
            card.style.paddingTop = 6;
            card.style.paddingBottom = 6;
            card.style.marginBottom = 3;

            if (!agent.IsUnityRelevant)
                card.style.opacity = 0.5f;

            var headerRow = new VisualElement();
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.alignItems = Align.Center;

            var nameLabel = new Label(agent.Name);
            nameLabel.style.fontSize = 12;
            nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            nameLabel.style.color = Color.white;
            nameLabel.style.flexGrow = 1;
            headerRow.Add(nameLabel);

            // Model badge
            var modelBadge = new Label(agent.ModelBadge);
            modelBadge.style.fontSize = 9;
            modelBadge.style.paddingLeft = 6;
            modelBadge.style.paddingRight = 6;
            modelBadge.style.paddingTop = 2;
            modelBadge.style.paddingBottom = 2;
            StyleHelper.SetBorderRadius(modelBadge.style, 3);
            modelBadge.style.backgroundColor = ModelColors.TryGetValue(agent.Model, out var mc)
                ? mc : new Color(0.4f, 0.4f, 0.4f);
            modelBadge.style.color = Color.white;
            modelBadge.style.unityFontStyleAndWeight = FontStyle.Bold;
            headerRow.Add(modelBadge);

            card.Add(headerRow);

            // Description (truncated)
            if (!string.IsNullOrEmpty(agent.Description))
            {
                var desc = agent.Description;
                if (desc.Length > 120) desc = desc.Substring(0, 117) + "...";
                var descLabel = new Label(desc);
                descLabel.style.fontSize = 10;
                descLabel.style.color = new Color(0.7f, 0.7f, 0.75f);
                descLabel.style.marginTop = 2;
                descLabel.style.whiteSpace = WhiteSpace.Normal;
                card.Add(descLabel);
            }

            // Tools row
            if (agent.Tools != null && agent.Tools.Length > 0)
            {
                var toolsRow = new VisualElement();
                toolsRow.style.flexDirection = FlexDirection.Row;
                toolsRow.style.flexWrap = Wrap.Wrap;
                toolsRow.style.marginTop = 4;

                foreach (var tool in agent.Tools)
                {
                    var toolBadge = new Label(tool);
                    toolBadge.style.fontSize = 9;
                    toolBadge.style.paddingLeft = 4;
                    toolBadge.style.paddingRight = 4;
                    toolBadge.style.paddingTop = 1;
                    toolBadge.style.paddingBottom = 1;
                    toolBadge.style.marginRight = 3;
                    StyleHelper.SetBorderRadius(toolBadge.style, 2);
                    toolBadge.style.backgroundColor = new Color(0.3f, 0.3f, 0.35f);
                    toolBadge.style.color = new Color(0.8f, 0.8f, 0.8f);
                    toolsRow.Add(toolBadge);
                }

                card.Add(toolsRow);
            }

            return card;
        }
    }
}
