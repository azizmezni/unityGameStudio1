using System.IO;
using ClaudeCodeGameStudios.Utilities;
using UnityEditor;
using UnityEngine;

namespace ClaudeCodeGameStudios.Integration
{
    /// <summary>
    /// Validates that the project directory structure matches Claude Code Game Studios expectations.
    /// Creates missing directories with user confirmation.
    /// </summary>
    public static class DirectorySync
    {
        private static readonly string[] ExpectedDirs =
        {
            "src", "assets", "design", "design/gdd", "design/narrative",
            "design/levels", "design/balance", "docs", "docs/architecture",
            "tests", "tools", "prototypes", "production",
            "production/sprints", "production/milestones",
            "production/session-state", "production/session-logs"
        };

        [MenuItem("Window/Claude Code Game Studios/Validate Directory Structure")]
        public static void ValidateAndSync()
        {
            var root = PathResolver.ProjectRoot;
            var missing = new System.Collections.Generic.List<string>();

            foreach (var dir in ExpectedDirs)
            {
                var fullPath = PathResolver.Combine(root, dir);
                if (!Directory.Exists(fullPath))
                    missing.Add(dir);
            }

            if (missing.Count == 0)
            {
                EditorUtility.DisplayDialog("Directory Structure",
                    "All expected directories exist. Project structure is valid.",
                    "OK");
                Debug.Log("[ClaudeCode] Directory structure validation passed.");
                return;
            }

            var missingList = string.Join("\n  ", missing);
            var create = EditorUtility.DisplayDialog("Missing Directories",
                $"The following directories are missing:\n\n  {missingList}\n\n" +
                "Create them now?",
                "Create All", "Cancel");

            if (!create) return;

            foreach (var dir in missing)
            {
                var fullPath = PathResolver.Combine(root, dir);
                Directory.CreateDirectory(fullPath);

                // Add .gitkeep to empty directories
                var gitkeep = Path.Combine(fullPath, ".gitkeep");
                if (!File.Exists(gitkeep))
                    File.WriteAllText(gitkeep, "");
            }

            Debug.Log($"[ClaudeCode] Created {missing.Count} directories.");
            AssetDatabase.Refresh();
        }
    }
}
