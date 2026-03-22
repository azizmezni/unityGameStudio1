using System.Linq;
using ClaudeCodeGameStudios.Data;
using ClaudeCodeGameStudios.Utilities;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ClaudeCodeGameStudios.Tabs
{
    public class SlashCommandsTab
    {
        private readonly ClaudeProjectContext _ctx;
        private string _searchFilter = "";

        private static readonly Color[] CategoryColors =
        {
            new(0.2f, 0.7f, 0.9f),  // Setup - cyan
            new(0.9f, 0.6f, 0.2f),  // Creative - orange
            new(0.3f, 0.8f, 0.4f),  // Production - green
            new(0.6f, 0.4f, 1f),    // Code - purple
            new(1f, 0.4f, 0.4f),    // Quality - red
            new(0.9f, 0.8f, 0.2f),  // Release - yellow
            new(0.5f, 0.8f, 0.9f),  // Localization - light blue
            new(0.9f, 0.5f, 0.7f),  // Teams - pink
        };

        public SlashCommandsTab(ClaudeProjectContext ctx)
        {
            _ctx = ctx;
        }

        public VisualElement Build()
        {
            var root = new ScrollView(ScrollViewMode.Vertical);
            root.style.paddingLeft = 12;
            root.style.paddingRight = 12;
            root.style.paddingTop = 8;

            // Search bar
            var searchField = new TextField();
            searchField.value = _searchFilter;
            searchField.style.marginBottom = 8;
            searchField.RegisterValueChangedCallback(evt =>
            {
                _searchFilter = evt.newValue;
                RefreshList(root);
            });
            root.Add(searchField);

            var summary = new Label($"{_ctx.Skills.Count} slash commands loaded — Click to copy, double-click to launch");
            summary.style.fontSize = 11;
            summary.style.color = new Color(0.6f, 0.6f, 0.6f);
            summary.style.marginBottom = 8;
            root.Add(summary);

            BuildSkillList(root);
            return root;
        }

        private void RefreshList(ScrollView root)
        {
            while (root.childCount > 2)
                root.RemoveAt(2);
            BuildSkillList(root);
        }

        private void BuildSkillList(VisualElement root)
        {
            var filtered = _ctx.Skills.Where(s =>
            {
                if (string.IsNullOrEmpty(_searchFilter)) return true;
                var search = _searchFilter.ToLower();
                return s.Name.ToLower().Contains(search) ||
                       s.Description.ToLower().Contains(search) ||
                       s.CategoryLabel.ToLower().Contains(search);
            }).ToList();

            var grouped = filtered.GroupBy(s => s.Category).OrderBy(g => g.Key);

            foreach (var group in grouped)
            {
                var colorIdx = (int)group.Key;
                var color = colorIdx < CategoryColors.Length
                    ? CategoryColors[colorIdx] : Color.white;

                var foldout = new Foldout();
                foldout.text = $"{group.First().CategoryLabel} ({group.Count()})";
                foldout.value = true;
                foldout.style.marginBottom = 6;

                var headerLabel = foldout.Q<Toggle>().Q<Label>();
                if (headerLabel != null)
                {
                    headerLabel.style.fontSize = 13;
                    headerLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                    headerLabel.style.color = color;
                }

                foreach (var skill in group)
                {
                    foldout.Add(CreateSkillRow(skill, color));
                }

                root.Add(foldout);
            }
        }

        private VisualElement CreateSkillRow(SkillData skill, Color categoryColor)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.backgroundColor = new Color(0.22f, 0.22f, 0.26f);
            StyleHelper.SetBorderRadius(row.style, 4);
            row.style.paddingLeft = 10;
            row.style.paddingRight = 10;
            row.style.paddingTop = 5;
            row.style.paddingBottom = 5;
            row.style.marginBottom = 2;

            // Command name
            var cmdLabel = new Label($"/{skill.Name}");
            cmdLabel.style.fontSize = 12;
            cmdLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            cmdLabel.style.color = categoryColor;
            cmdLabel.style.width = 180;
            row.Add(cmdLabel);

            // Description
            var desc = skill.Description;
            if (desc.Length > 80) desc = desc.Substring(0, 77) + "...";
            var descLabel = new Label(desc);
            descLabel.style.fontSize = 11;
            descLabel.style.color = new Color(0.7f, 0.7f, 0.75f);
            descLabel.style.flexGrow = 1;
            descLabel.style.overflow = Overflow.Hidden;
            row.Add(descLabel);

            // Copy button
            var copyBtn = new Button(() =>
            {
                var cmd = string.IsNullOrEmpty(skill.ArgumentHint) || skill.ArgumentHint == "[no arguments]"
                    ? $"/{skill.Name}"
                    : $"/{skill.Name} {skill.ArgumentHint}";
                EditorGUIUtility.systemCopyBuffer = cmd;
                Debug.Log($"[ClaudeCode] Copied to clipboard: {cmd}");
            }) { text = "Copy" };
            copyBtn.style.height = 20;
            copyBtn.style.fontSize = 10;
            row.Add(copyBtn);

            // Run in chat (uses configured provider, not Claude CLI)
            var launchBtn = new Button(() =>
            {
                ChatWindow.OpenWithSkill(skill.Name);
            }) { text = "Chat" };
            launchBtn.style.height = 20;
            launchBtn.style.fontSize = 10;
            launchBtn.style.marginLeft = 4;
            row.Add(launchBtn);

            return row;
        }
    }
}
