using System.Linq;
using ClaudeCodeGameStudios.Data;
using ClaudeCodeGameStudios.Utilities;
using UnityEngine;
using UnityEngine.UIElements;

namespace ClaudeCodeGameStudios.Tabs
{
    public class SprintTrackerTab
    {
        private readonly ClaudeProjectContext _ctx;

        public SprintTrackerTab(ClaudeProjectContext ctx)
        {
            _ctx = ctx;
        }

        public VisualElement Build()
        {
            var root = new ScrollView(ScrollViewMode.Vertical);
            root.style.paddingLeft = 12;
            root.style.paddingRight = 12;
            root.style.paddingTop = 8;

            if (_ctx.Sprints.Count == 0)
            {
                var placeholder = new VisualElement();
                placeholder.style.alignItems = Align.Center;
                placeholder.style.paddingTop = 40;

                var title = new Label("No Sprints");
                title.style.fontSize = 16;
                title.style.unityFontStyleAndWeight = FontStyle.Bold;
                title.style.color = new Color(0.5f, 0.5f, 0.5f);
                title.style.marginBottom = 8;
                placeholder.Add(title);

                var info = new Label(
                    "Sprint files live in production/sprints/.\n" +
                    "Use /sprint-plan to create your first sprint.");
                info.style.fontSize = 11;
                info.style.color = new Color(0.45f, 0.45f, 0.45f);
                info.style.unityTextAlign = TextAnchor.MiddleCenter;
                info.style.whiteSpace = WhiteSpace.Normal;
                placeholder.Add(info);

                root.Add(placeholder);
                return root;
            }

            // Show most recent sprint first
            var sprints = _ctx.Sprints.OrderByDescending(s => s.SprintName).ToList();

            foreach (var sprint in sprints)
            {
                var isFirst = sprint == sprints.First();
                root.Add(CreateSprintCard(sprint, isFirst));
            }

            return root;
        }

        private VisualElement CreateSprintCard(SprintData sprint, bool expanded)
        {
            var foldout = new Foldout();
            foldout.text = sprint.SprintName;
            foldout.value = expanded;
            foldout.style.marginBottom = 8;

            var headerLabel = foldout.Q<Toggle>().Q<Label>();
            if (headerLabel != null)
            {
                headerLabel.style.fontSize = 13;
                headerLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                headerLabel.style.color = Color.white;
            }

            // Progress bar
            if (sprint.TotalTasks > 0)
            {
                var progressRow = new VisualElement();
                progressRow.style.flexDirection = FlexDirection.Row;
                progressRow.style.alignItems = Align.Center;
                progressRow.style.marginBottom = 6;

                var progressBg = new VisualElement();
                progressBg.style.height = 6;
                progressBg.style.flexGrow = 1;
                progressBg.style.backgroundColor = new Color(0.25f, 0.25f, 0.3f);
                StyleHelper.SetBorderRadius(progressBg.style, 3);

                var progressFill = new VisualElement();
                var pct = (float)sprint.CompletedTasks / sprint.TotalTasks;
                progressFill.style.height = 6;
                progressFill.style.width = new StyleLength(new Length(pct * 100, LengthUnit.Percent));
                progressFill.style.backgroundColor = pct >= 1f
                    ? new Color(0.3f, 0.9f, 0.4f)
                    : new Color(0.2f, 0.6f, 1f);
                StyleHelper.SetBorderRadius(progressFill.style, 3);
                progressBg.Add(progressFill);
                progressRow.Add(progressBg);

                var statsLabel = new Label(
                    $"  {sprint.CompletedTasks}/{sprint.TotalTasks} done" +
                    (sprint.InProgressTasks > 0 ? $", {sprint.InProgressTasks} in progress" : ""));
                statsLabel.style.fontSize = 10;
                statsLabel.style.color = new Color(0.6f, 0.6f, 0.65f);
                statsLabel.style.marginLeft = 8;
                progressRow.Add(statsLabel);

                foldout.Add(progressRow);
            }

            // Task list
            foreach (var task in sprint.Tasks)
            {
                var taskRow = new VisualElement();
                taskRow.style.flexDirection = FlexDirection.Row;
                taskRow.style.alignItems = Align.Center;
                taskRow.style.paddingLeft = 8;
                taskRow.style.paddingTop = 2;
                taskRow.style.paddingBottom = 2;

                var statusIcon = new Label(task.Status switch
                {
                    "done" => "[x]",
                    "in-progress" => "[~]",
                    _ => "[ ]"
                });
                statusIcon.style.fontSize = 11;
                statusIcon.style.color = task.Status switch
                {
                    "done" => new Color(0.3f, 0.9f, 0.4f),
                    "in-progress" => new Color(0.9f, 0.8f, 0.2f),
                    _ => new Color(0.5f, 0.5f, 0.5f)
                };
                statusIcon.style.width = 30;
                taskRow.Add(statusIcon);

                var descLabel = new Label(task.Description);
                descLabel.style.fontSize = 11;
                descLabel.style.color = task.Status == "done"
                    ? new Color(0.5f, 0.5f, 0.5f) : new Color(0.8f, 0.8f, 0.85f);
                descLabel.style.flexGrow = 1;
                descLabel.style.whiteSpace = WhiteSpace.Normal;
                if (task.Status == "done")
                    descLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
                taskRow.Add(descLabel);

                if (!string.IsNullOrEmpty(task.Assignee))
                {
                    var assignee = new Label($"@{task.Assignee}");
                    assignee.style.fontSize = 9;
                    assignee.style.color = new Color(0.5f, 0.7f, 0.9f);
                    assignee.style.marginLeft = 8;
                    taskRow.Add(assignee);
                }

                foldout.Add(taskRow);
            }

            return foldout;
        }
    }
}
