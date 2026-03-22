using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace ClaudeCodeGameStudios.Data
{
    /// <summary>
    /// Parses YAML frontmatter from Markdown files.
    /// Handles simple key: value, key: "quoted value", and key: [array, values].
    /// No third-party dependencies.
    /// </summary>
    public static class FrontmatterParser
    {
        public struct ParseResult
        {
            public Dictionary<string, string> Fields;
            public string Body;
        }

        public static ParseResult Parse(string filePath)
        {
            var result = new ParseResult
            {
                Fields = new Dictionary<string, string>()
            };

            if (!File.Exists(filePath))
            {
                result.Body = "";
                return result;
            }

            var lines = File.ReadAllLines(filePath);
            int frontmatterStart = -1;
            int frontmatterEnd = -1;

            for (int i = 0; i < lines.Length; i++)
            {
                var trimmed = lines[i].Trim();
                if (trimmed == "---")
                {
                    if (frontmatterStart < 0)
                    {
                        frontmatterStart = i;
                    }
                    else
                    {
                        frontmatterEnd = i;
                        break;
                    }
                }
            }

            if (frontmatterStart < 0 || frontmatterEnd < 0)
            {
                result.Body = string.Join("\n", lines);
                return result;
            }

            // Parse frontmatter fields
            for (int i = frontmatterStart + 1; i < frontmatterEnd; i++)
            {
                var line = lines[i];
                var colonIdx = line.IndexOf(':');
                if (colonIdx <= 0) continue;

                var key = line.Substring(0, colonIdx).Trim();
                var value = line.Substring(colonIdx + 1).Trim();

                // Remove surrounding quotes
                if (value.Length >= 2 && value[0] == '"' && value[value.Length - 1] == '"')
                    value = value.Substring(1, value.Length - 2);

                result.Fields[key] = value;
            }

            // Body is everything after the closing ---
            var bodyLines = new List<string>();
            for (int i = frontmatterEnd + 1; i < lines.Length; i++)
                bodyLines.Add(lines[i]);
            result.Body = string.Join("\n", bodyLines).TrimStart('\n', '\r');

            return result;
        }

        /// <summary>
        /// Safe dictionary lookup with default value (replaces GetValueOrDefault).
        /// </summary>
        public static string GetField(Dictionary<string, string> fields, string key, string defaultValue = "")
        {
            if (fields != null && fields.TryGetValue(key, out var value))
                return value;
            return defaultValue;
        }

        /// <summary>
        /// Parses a bracketed array value like "[a, b, c]" into a string array.
        /// </summary>
        public static string[] ParseArray(string value)
        {
            if (string.IsNullOrEmpty(value)) return new string[0];

            value = value.Trim();
            if (value.StartsWith("[") && value.EndsWith("]"))
                value = value.Substring(1, value.Length - 2);

            if (string.IsNullOrWhiteSpace(value)) return new string[0];

            var items = value.Split(',');
            var result = new List<string>();
            foreach (var item in items)
            {
                var trimmed = item.Trim().Trim('"');
                if (!string.IsNullOrEmpty(trimmed))
                    result.Add(trimmed);
            }
            return result.ToArray();
        }
    }
}
