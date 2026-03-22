using System.IO;
using UnityEngine;

namespace ClaudeCodeGameStudios.Utilities
{
    /// <summary>
    /// Resolves paths between Unity project and Claude Code project root.
    /// The .claude/ directory lives one level above Assets/.
    /// </summary>
    public static class PathResolver
    {
        private static string _projectRoot;
        private static string _claudeDir;

        public static string ProjectRoot
        {
            get
            {
                if (_projectRoot == null)
                {
                    // Application.dataPath = .../ProjectRoot/Assets
                    _projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                    _projectRoot = _projectRoot.Replace('\\', '/');
                }
                return _projectRoot;
            }
        }

        public static string ClaudeDir => _claudeDir ??= Combine(ProjectRoot, ".claude");
        public static string AgentsDir => Combine(ClaudeDir, "agents");
        public static string SkillsDir => Combine(ClaudeDir, "skills");
        public static string ProductionDir => Combine(ProjectRoot, "production");
        public static string SessionStateDir => Combine(ProductionDir, "session-state");
        public static string SprintsDir => Combine(ProductionDir, "sprints");
        public static string DesignDir => Combine(ProjectRoot, "design");
        public static string DocsDir => Combine(ProjectRoot, "docs");

        public static string ActiveSessionFile => Combine(SessionStateDir, "active.md");
        public static string ConsoleBridgeLog => Combine(SessionStateDir, "console-bridge.log");

        public static string Combine(string basePath, string relativePath)
        {
            return Path.GetFullPath(Path.Combine(basePath, relativePath)).Replace('\\', '/');
        }

        public static bool ClaudeDirExists => Directory.Exists(ClaudeDir);

        public static string GetRelativeToProject(string absolutePath)
        {
            var normalized = absolutePath.Replace('\\', '/');
            if (normalized.StartsWith(ProjectRoot))
                return normalized.Substring(ProjectRoot.Length + 1);
            return normalized;
        }
    }
}
