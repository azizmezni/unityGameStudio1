using ClaudeCodeGameStudios.Data;
using ClaudeCodeGameStudios.Utilities;
using UnityEngine;
using UnityEngine.UIElements;

namespace ClaudeCodeGameStudios.Tabs
{
    public class SessionStateTab
    {
        private readonly ClaudeProjectContext _ctx;

        public SessionStateTab(ClaudeProjectContext ctx)
        {
            _ctx = ctx;
        }

        public VisualElement Build()
        {
            var root = new ScrollView(ScrollViewMode.Vertical);
            root.style.paddingLeft = 12;
            root.style.paddingRight = 12;
            root.style.paddingTop = 8;

            _ctx.RefreshSessionState();
            var state = _ctx.SessionState;

            if (state == null || !state.Exists)
            {
                var placeholder = new VisualElement();
                placeholder.style.alignItems = Align.Center;
                placeholder.style.paddingTop = 40;

                var icon = new Label("No Active Session");
                icon.style.fontSize = 16;
                icon.style.unityFontStyleAndWeight = FontStyle.Bold;
                icon.style.color = new Color(0.5f, 0.5f, 0.5f);
                icon.style.marginBottom = 8;
                placeholder.Add(icon);

                var info = new Label(
                    "Session state appears when Claude Code is running.\n" +
                    "The file production/session-state/active.md tracks\n" +
                    "current task, progress, and focus area.");
                info.style.fontSize = 11;
                info.style.color = new Color(0.45f, 0.45f, 0.45f);
                info.style.unityTextAlign = TextAnchor.MiddleCenter;
                info.style.whiteSpace = WhiteSpace.Normal;
                placeholder.Add(info);

                root.Add(placeholder);
                return root;
            }

            // Breadcrumb bar
            var breadcrumbBar = new VisualElement();
            breadcrumbBar.style.backgroundColor = new Color(0.18f, 0.25f, 0.35f);
            StyleHelper.SetBorderRadius(breadcrumbBar.style, 6);
            breadcrumbBar.style.paddingLeft = 12;
            breadcrumbBar.style.paddingRight = 12;
            breadcrumbBar.style.paddingTop = 10;
            breadcrumbBar.style.paddingBottom = 10;
            breadcrumbBar.style.marginBottom = 12;

            var breadcrumbTitle = new Label("Current Focus");
            breadcrumbTitle.style.fontSize = 10;
            breadcrumbTitle.style.color = new Color(0.5f, 0.7f, 0.9f);
            breadcrumbTitle.style.marginBottom = 4;
            breadcrumbBar.Add(breadcrumbTitle);

            var breadcrumb = new Label(state.Breadcrumb);
            breadcrumb.style.fontSize = 14;
            breadcrumb.style.unityFontStyleAndWeight = FontStyle.Bold;
            breadcrumb.style.color = Color.white;
            breadcrumbBar.Add(breadcrumb);

            // Individual fields if available
            if (!string.IsNullOrEmpty(state.Epic) || !string.IsNullOrEmpty(state.Feature) || !string.IsNullOrEmpty(state.Task))
            {
                var fieldsRow = new VisualElement();
                fieldsRow.style.flexDirection = FlexDirection.Row;
                fieldsRow.style.marginTop = 6;

                if (!string.IsNullOrEmpty(state.Epic))
                    fieldsRow.Add(CreateStatusBadge("Epic", state.Epic, new Color(0.9f, 0.6f, 0.2f)));
                if (!string.IsNullOrEmpty(state.Feature))
                    fieldsRow.Add(CreateStatusBadge("Feature", state.Feature, new Color(0.3f, 0.8f, 0.4f)));
                if (!string.IsNullOrEmpty(state.Task))
                    fieldsRow.Add(CreateStatusBadge("Task", state.Task, new Color(0.3f, 0.6f, 1f)));

                breadcrumbBar.Add(fieldsRow);
            }

            root.Add(breadcrumbBar);

            // Last modified
            var modifiedLabel = new Label($"Last updated: {state.LastModified:yyyy-MM-dd HH:mm:ss}");
            modifiedLabel.style.fontSize = 10;
            modifiedLabel.style.color = new Color(0.5f, 0.5f, 0.5f);
            modifiedLabel.style.marginBottom = 8;
            root.Add(modifiedLabel);

            // Raw content
            var contentHeader = new Label("Session State Content");
            contentHeader.style.fontSize = 12;
            contentHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
            contentHeader.style.color = new Color(0.8f, 0.8f, 0.8f);
            contentHeader.style.marginBottom = 4;
            root.Add(contentHeader);

            var content = new Label(state.RawContent);
            content.style.fontSize = 11;
            content.style.color = new Color(0.7f, 0.7f, 0.75f);
            content.style.whiteSpace = WhiteSpace.Normal;
            content.style.backgroundColor = new Color(0.18f, 0.18f, 0.22f);
            StyleHelper.SetBorderRadius(content.style, 4);
            content.style.paddingLeft = 10;
            content.style.paddingRight = 10;
            content.style.paddingTop = 8;
            content.style.paddingBottom = 8;
            root.Add(content);

            // Auto-refresh schedule
            root.schedule.Execute(() =>
            {
                _ctx.RefreshSessionState();
            }).Every(5000);

            return root;
        }

        private VisualElement CreateStatusBadge(string label, string value, Color color)
        {
            var badge = new VisualElement();
            badge.style.marginRight = 8;

            var labelEl = new Label(label);
            labelEl.style.fontSize = 9;
            labelEl.style.color = color;
            badge.Add(labelEl);

            var valueEl = new Label(value);
            valueEl.style.fontSize = 11;
            valueEl.style.color = Color.white;
            badge.Add(valueEl);

            return badge;
        }
    }
}
