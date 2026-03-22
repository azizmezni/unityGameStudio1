using ClaudeCodeGameStudios.Data;
using ClaudeCodeGameStudios.Tabs;
using ClaudeCodeGameStudios.Utilities;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ClaudeCodeGameStudios
{
    public class ClaudeCodeDashboard : EditorWindow
    {
        private VisualElement _tabContent;
        private VisualElement _tabBar;
        private int _activeTabIndex;

        private AgentRosterTab _agentRosterTab;
        private SlashCommandsTab _slashCommandsTab;
        private SessionStateTab _sessionStateTab;
        private DesignDocBrowserTab _designDocBrowserTab;
        private SprintTrackerTab _sprintTrackerTab;
        private ModelProviderTab _modelProviderTab;

        private static readonly string[] TabNames =
        {
            "Agents", "Commands", "Session", "Design Docs", "Sprints", "Providers"
        };

        private static readonly Color AccentColor = new(0.2f, 0.6f, 1f);
        private static readonly Color DarkBg = new(0.15f, 0.15f, 0.18f);
        private static readonly Color TabBg = new(0.2f, 0.2f, 0.24f);
        private static readonly Color TabActiveBg = new(0.28f, 0.28f, 0.34f);

        [MenuItem("Window/Claude Code Game Studios/Dashboard %#g")]
        public static void ShowWindow()
        {
            var window = GetWindow<ClaudeCodeDashboard>();
            window.titleContent = new GUIContent("Claude Code Studio");
            window.minSize = new Vector2(600, 400);
        }

        private void OnEnable()
        {
            BuildUI();
            ClaudeProjectContext.Instance.OnDataChanged += RebuildActiveTab;
        }

        private void OnDisable()
        {
            if (ClaudeProjectContext.Instance != null)
                ClaudeProjectContext.Instance.OnDataChanged -= RebuildActiveTab;
        }

        private void BuildUI()
        {
            rootVisualElement.Clear();
            rootVisualElement.style.backgroundColor = DarkBg;

            // Toolbar
            var toolbar = new VisualElement();
            toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.style.paddingLeft = 8;
            toolbar.style.paddingRight = 8;
            toolbar.style.paddingTop = 4;
            toolbar.style.paddingBottom = 4;
            toolbar.style.backgroundColor = new Color(0.12f, 0.12f, 0.14f);
            toolbar.style.borderBottomWidth = 1;
            toolbar.style.borderBottomColor = new Color(0.3f, 0.3f, 0.35f);

            var title = new Label("Claude Code Game Studios");
            title.style.fontSize = 14;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = Color.white;
            title.style.marginRight = 16;
            title.style.unityTextAlign = TextAnchor.MiddleLeft;
            toolbar.Add(title);

            // Project status
            var statusLabel = new Label(PathResolver.ClaudeDirExists
                ? "Connected" : "No .claude/ found");
            statusLabel.style.color = PathResolver.ClaudeDirExists
                ? new Color(0.4f, 0.9f, 0.4f) : new Color(0.9f, 0.4f, 0.4f);
            statusLabel.style.fontSize = 11;
            statusLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            statusLabel.style.flexGrow = 1;
            toolbar.Add(statusLabel);

            var refreshBtn = new Button(() =>
            {
                ClaudeProjectContext.Instance.Refresh();
                RebuildActiveTab();
            }) { text = "Refresh" };
            refreshBtn.style.height = 22;
            toolbar.Add(refreshBtn);

            rootVisualElement.Add(toolbar);

            // Tab bar
            _tabBar = new VisualElement();
            _tabBar.style.flexDirection = FlexDirection.Row;
            _tabBar.style.backgroundColor = TabBg;
            _tabBar.style.paddingLeft = 4;
            _tabBar.style.paddingTop = 2;

            for (int i = 0; i < TabNames.Length; i++)
            {
                var idx = i;
                var tabBtn = new Button(() => SwitchTab(idx)) { text = TabNames[i] };
                tabBtn.name = $"tab-{i}";
                tabBtn.style.marginRight = 2;
                tabBtn.style.paddingLeft = 12;
                tabBtn.style.paddingRight = 12;
                tabBtn.style.paddingTop = 6;
                tabBtn.style.paddingBottom = 6;
                tabBtn.style.borderTopLeftRadius = 4;
                tabBtn.style.borderTopRightRadius = 4;
                tabBtn.style.borderBottomLeftRadius = 0;
                tabBtn.style.borderBottomRightRadius = 0;
                tabBtn.style.backgroundColor = i == 0 ? TabActiveBg : TabBg;
                tabBtn.style.color = i == 0 ? Color.white : new Color(0.7f, 0.7f, 0.7f);
                tabBtn.style.borderBottomWidth = i == 0 ? 2 : 0;
                tabBtn.style.borderBottomColor = AccentColor;
                _tabBar.Add(tabBtn);
            }
            rootVisualElement.Add(_tabBar);

            // Tab content area
            _tabContent = new VisualElement();
            _tabContent.style.flexGrow = 1;
            _tabContent.style.overflow = Overflow.Hidden;
            rootVisualElement.Add(_tabContent);

            // Initialize tabs
            var ctx = ClaudeProjectContext.Instance;
            _agentRosterTab = new AgentRosterTab(ctx);
            _slashCommandsTab = new SlashCommandsTab(ctx);
            _sessionStateTab = new SessionStateTab(ctx);
            _designDocBrowserTab = new DesignDocBrowserTab(ctx);
            _sprintTrackerTab = new SprintTrackerTab(ctx);
            _modelProviderTab = new ModelProviderTab(ctx);

            SwitchTab(0);
        }

        private void SwitchTab(int index)
        {
            _activeTabIndex = index;

            // Update tab button styles
            for (int i = 0; i < TabNames.Length; i++)
            {
                var btn = _tabBar.Q<Button>($"tab-{i}");
                if (btn == null) continue;
                btn.style.backgroundColor = i == index ? TabActiveBg : TabBg;
                btn.style.color = i == index ? Color.white : new Color(0.7f, 0.7f, 0.7f);
                btn.style.borderBottomWidth = i == index ? 2 : 0;
                btn.style.borderBottomColor = AccentColor;
            }

            RebuildActiveTab();
        }

        private void RebuildActiveTab()
        {
            _tabContent.Clear();

            VisualElement tab = _activeTabIndex switch
            {
                0 => _agentRosterTab.Build(),
                1 => _slashCommandsTab.Build(),
                2 => _sessionStateTab.Build(),
                3 => _designDocBrowserTab.Build(),
                4 => _sprintTrackerTab.Build(),
                5 => _modelProviderTab.Build(),
                _ => new Label("Unknown tab")
            };

            _tabContent.Add(tab);
        }
    }
}
