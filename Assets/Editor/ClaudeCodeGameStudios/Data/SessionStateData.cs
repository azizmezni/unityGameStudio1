using System;
using System.IO;
using System.Text.RegularExpressions;
using ClaudeCodeGameStudios.Utilities;

namespace ClaudeCodeGameStudios.Data
{
    [Serializable]
    public class SessionStateData
    {
        public string Epic;
        public string Feature;
        public string Task;
        public string RawContent;
        public DateTime LastModified;
        public bool Exists;

        private static readonly Regex StatusBlockRegex = new(
            @"<!--\s*STATUS\s*-->(.*?)<!--\s*/STATUS\s*-->",
            RegexOptions.Singleline | RegexOptions.Compiled);

        private static readonly Regex FieldRegex = new(
            @"^\s*(Epic|Feature|Task)\s*:\s*(.+)$",
            RegexOptions.Multiline | RegexOptions.Compiled);

        public static SessionStateData Load()
        {
            var data = new SessionStateData();
            var filePath = PathResolver.ActiveSessionFile;

            if (!File.Exists(filePath))
            {
                data.Exists = false;
                return data;
            }

            data.Exists = true;
            data.RawContent = File.ReadAllText(filePath);
            data.LastModified = File.GetLastWriteTime(filePath);

            var statusMatch = StatusBlockRegex.Match(data.RawContent);
            if (statusMatch.Success)
            {
                var block = statusMatch.Groups[1].Value;
                var fieldMatches = FieldRegex.Matches(block);
                foreach (Match m in fieldMatches)
                {
                    var key = m.Groups[1].Value.Trim();
                    var value = m.Groups[2].Value.Trim();
                    switch (key)
                    {
                        case "Epic": data.Epic = value; break;
                        case "Feature": data.Feature = value; break;
                        case "Task": data.Task = value; break;
                    }
                }
            }

            return data;
        }

        public string Breadcrumb
        {
            get
            {
                var parts = new System.Collections.Generic.List<string>();
                if (!string.IsNullOrEmpty(Epic)) parts.Add(Epic);
                if (!string.IsNullOrEmpty(Feature)) parts.Add(Feature);
                if (!string.IsNullOrEmpty(Task)) parts.Add(Task);
                return parts.Count > 0 ? string.Join(" > ", parts) : "No active focus";
            }
        }
    }
}
