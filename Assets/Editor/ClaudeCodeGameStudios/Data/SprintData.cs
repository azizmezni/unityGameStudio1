using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace ClaudeCodeGameStudios.Data
{
    [Serializable]
    public class SprintTask
    {
        public string Description;
        public string Status; // "todo", "in-progress", "done"
        public string Assignee;
    }

    [Serializable]
    public class SprintData
    {
        public string SprintName;
        public string FilePath;
        public string RawContent;
        public List<SprintTask> Tasks = new();

        private static readonly Regex TaskLineRegex = new(
            @"^\s*-\s*\[([ xX~])\]\s*(.+)$",
            RegexOptions.Multiline | RegexOptions.Compiled);

        public static SprintData FromFile(string filePath)
        {
            var sprint = new SprintData
            {
                FilePath = filePath,
                SprintName = Path.GetFileNameWithoutExtension(filePath)
            };

            if (!File.Exists(filePath))
                return sprint;

            sprint.RawContent = File.ReadAllText(filePath);

            // Parse checkbox-style task lines: - [ ] todo, - [x] done, - [~] in-progress
            var matches = TaskLineRegex.Matches(sprint.RawContent);
            foreach (Match m in matches)
            {
                var checkMark = m.Groups[1].Value;
                var desc = m.Groups[2].Value.Trim();

                var task = new SprintTask
                {
                    Description = desc,
                    Status = checkMark switch
                    {
                        "x" or "X" => "done",
                        "~" => "in-progress",
                        _ => "todo"
                    }
                };

                // Try to extract assignee from @mention
                var atIdx = desc.IndexOf('@');
                if (atIdx >= 0)
                {
                    var end = desc.IndexOf(' ', atIdx);
                    task.Assignee = end > atIdx
                        ? desc.Substring(atIdx + 1, end - atIdx - 1)
                        : desc.Substring(atIdx + 1);
                }

                sprint.Tasks.Add(task);
            }

            return sprint;
        }

        public int TotalTasks => Tasks.Count;
        public int CompletedTasks => Tasks.FindAll(t => t.Status == "done").Count;
        public int InProgressTasks => Tasks.FindAll(t => t.Status == "in-progress").Count;
    }
}
