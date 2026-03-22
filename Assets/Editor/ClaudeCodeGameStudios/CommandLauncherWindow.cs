using System.Collections.Generic;
using System.Linq;
using ClaudeCodeGameStudios.Data;
using ClaudeCodeGameStudios.Utilities;
using static ClaudeCodeGameStudios.Utilities.StyleHelper;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ClaudeCodeGameStudios
{
    public class CommandLauncherWindow : EditorWindow
    {
        private const string RecentCommandsKey = "ClaudeCode_RecentCommands";
        private const int MaxRecentCommands = 10;

        private TextField _searchField;
        private VisualElement _resultsList;
        private List<SkillData> _allSkills;

        [MenuItem("Window/Claude Code Game Studios/Command Launcher %#k")]
        public static void ShowLauncher()
        {
            var window = CreateInstance<CommandLauncherWindow>();
            window.titleContent = new GUIContent("Claude Commands");

            // Show as centered popup
            var size = new Vector2(500, 400);
            var screenCenter = new Vector2(Screen.currentResolution.width / 2f, Screen.currentResolution.height / 2f);
            window.position = new Rect(screenCenter.x - size.x / 2, screenCenter.y - size.y / 2, size.x, size.y);
            window.ShowUtility();
        }

        private void OnEnable()
        {
            _allSkills = ClaudeProjectContext.Instance.Skills;
            BuildUI();
        }

        private void OnLostFocus()
        {
            // Close when losing focus (popup behavior)
            EditorApplication.delayCall += Close;
        }

        private void BuildUI()
        {
            rootVisualElement.style.backgroundColor = new Color(0.14f, 0.14f, 0.17f);

            // Search field
            _searchField = new TextField();
            _searchField.style.marginLeft = 8;
            _searchField.style.marginRight = 8;
            _searchField.style.marginTop = 8;
            _searchField.style.marginBottom = 4;
            _searchField.style.fontSize = 14;
            _searchField.RegisterValueChangedCallback(_ => UpdateResults());
            rootVisualElement.Add(_searchField);

            // Focus the search field
            _searchField.schedule.Execute(() => _searchField.Focus()).ExecuteLater(50);

            // Results area
            var scrollView = new ScrollView(ScrollViewMode.Vertical);
            scrollView.style.flexGrow = 1;
            _resultsList = scrollView.contentContainer;
            rootVisualElement.Add(scrollView);

            // Hint
            var hint = new Label("Type to search commands. Click to copy, Shift+Click to open in chat.");
            hint.style.fontSize = 9;
            hint.style.color = new Color(0.4f, 0.4f, 0.45f);
            hint.style.paddingLeft = 8;
            hint.style.paddingBottom = 4;
            rootVisualElement.Add(hint);

            UpdateResults();
        }

        private void UpdateResults()
        {
            _resultsList.Clear();

            var query = _searchField?.value?.ToLower() ?? "";

            // Show recent commands first when query is empty
            if (string.IsNullOrEmpty(query))
            {
                var recent = GetRecentCommands();
                if (recent.Count > 0)
                {
                    var recentHeader = new Label("Recent");
                    recentHeader.style.fontSize = 10;
                    recentHeader.style.color = new Color(0.5f, 0.5f, 0.55f);
                    recentHeader.style.paddingLeft = 10;
                    recentHeader.style.paddingTop = 4;
                    _resultsList.Add(recentHeader);

                    foreach (var cmd in recent)
                    {
                        var skill = _allSkills.FirstOrDefault(s => s.Name == cmd);
                        if (skill != null)
                            _resultsList.Add(CreateResultRow(skill, true));
                    }

                    var separator = new VisualElement();
                    separator.style.height = 1;
                    separator.style.backgroundColor = new Color(0.3f, 0.3f, 0.35f);
                    separator.style.marginTop = 4;
                    separator.style.marginBottom = 4;
                    _resultsList.Add(separator);
                }
            }

            // All commands header
            var allHeader = new Label(string.IsNullOrEmpty(query) ? "All Commands" : "Results");
            allHeader.style.fontSize = 10;
            allHeader.style.color = new Color(0.5f, 0.5f, 0.55f);
            allHeader.style.paddingLeft = 10;
            allHeader.style.paddingTop = 2;
            _resultsList.Add(allHeader);

            var filtered = _allSkills.Where(s => FuzzyMatch(s, query)).ToList();

            if (filtered.Count == 0)
            {
                var noResults = new Label("No matching commands");
                noResults.style.color = new Color(0.4f, 0.4f, 0.4f);
                noResults.style.paddingLeft = 10;
                noResults.style.paddingTop = 8;
                _resultsList.Add(noResults);
                return;
            }

            foreach (var skill in filtered)
            {
                _resultsList.Add(CreateResultRow(skill, false));
            }
        }

        private bool FuzzyMatch(SkillData skill, string query)
        {
            if (string.IsNullOrEmpty(query)) return true;
            return skill.Name.ToLower().Contains(query) ||
                   skill.Description.ToLower().Contains(query) ||
                   skill.CategoryLabel.ToLower().Contains(query);
        }

        private VisualElement CreateResultRow(SkillData skill, bool isRecent)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.paddingLeft = 10;
            row.style.paddingRight = 10;
            row.style.paddingTop = 5;
            row.style.paddingBottom = 5;

            row.RegisterCallback<MouseEnterEvent>(_ =>
                row.style.backgroundColor = new Color(0.25f, 0.25f, 0.3f));
            row.RegisterCallback<MouseLeaveEvent>(_ =>
                row.style.backgroundColor = StyleKeyword.Null);

            // Click: copy. Shift+Click: open in chat with configured provider.
            row.RegisterCallback<ClickEvent>(evt =>
            {
                AddRecentCommand(skill.Name);
                if (evt.shiftKey)
                {
                    ChatWindow.OpenWithSkill(skill.Name);
                    Close();
                }
                else
                {
                    EditorGUIUtility.systemCopyBuffer = $"/{skill.Name}";
                    Debug.Log($"[ClaudeCode] Copied: /{skill.Name}");
                    Close();
                }
            });

            var cmdLabel = new Label($"/{skill.Name}");
            cmdLabel.style.fontSize = 12;
            cmdLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            cmdLabel.style.color = new Color(0.3f, 0.7f, 1f);
            cmdLabel.style.width = 160;
            row.Add(cmdLabel);

            var desc = skill.Description;
            if (desc.Length > 60) desc = desc.Substring(0, 57) + "...";
            var descLabel = new Label(desc);
            descLabel.style.fontSize = 11;
            descLabel.style.color = new Color(0.65f, 0.65f, 0.7f);
            descLabel.style.flexGrow = 1;
            row.Add(descLabel);

            var catBadge = new Label(skill.CategoryLabel);
            catBadge.style.fontSize = 9;
            catBadge.style.paddingLeft = 4;
            catBadge.style.paddingRight = 4;
            catBadge.style.paddingTop = 1;
            catBadge.style.paddingBottom = 1;
            SetBorderRadius(catBadge.style, 3);
            catBadge.style.backgroundColor = new Color(0.3f, 0.3f, 0.35f);
            catBadge.style.color = new Color(0.7f, 0.7f, 0.7f);
            row.Add(catBadge);

            return row;
        }

        private List<string> GetRecentCommands()
        {
            var json = EditorPrefs.GetString(RecentCommandsKey, "[]");
            try
            {
                return JsonUtility.FromJson<RecentList>(
                    $"{{\"items\":{json}}}").items ?? new List<string>();
            }
            catch
            {
                return new List<string>();
            }
        }

        private void AddRecentCommand(string name)
        {
            var recent = GetRecentCommands();
            recent.Remove(name);
            recent.Insert(0, name);
            if (recent.Count > MaxRecentCommands)
                recent.RemoveRange(MaxRecentCommands, recent.Count - MaxRecentCommands);

            var json = "[" + string.Join(",", recent.Select(r => $"\"{r}\"")) + "]";
            EditorPrefs.SetString(RecentCommandsKey, json);
        }

        [System.Serializable]
        private class RecentList
        {
            public List<string> items;
        }
    }
}
