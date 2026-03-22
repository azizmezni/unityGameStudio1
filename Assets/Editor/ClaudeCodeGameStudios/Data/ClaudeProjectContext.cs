using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClaudeCodeGameStudios.Utilities;
using UnityEngine;

namespace ClaudeCodeGameStudios.Data
{
    /// <summary>
    /// Central in-memory cache of all parsed Claude Code Game Studios data.
    /// Rebuilt from source .md files on demand. Never saved to disk.
    /// </summary>
    public class ClaudeProjectContext : ScriptableObject
    {
        public List<AgentData> Agents = new();
        public List<SkillData> Skills = new();
        public SessionStateData SessionState;
        public List<SprintData> Sprints = new();
        public List<string> DesignDocPaths = new();
        public bool IsLoaded;

        private static ClaudeProjectContext _instance;

        public static ClaudeProjectContext Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = CreateInstance<ClaudeProjectContext>();
                    _instance.hideFlags = HideFlags.HideAndDontSave;
                }
                if (!_instance.IsLoaded)
                    _instance.Refresh();
                return _instance;
            }
        }

        public void Refresh()
        {
            LoadAgents();
            LoadSkills();
            RefreshSessionState();
            LoadSprints();
            LoadDesignDocs();
            IsLoaded = true;
            OnDataChanged?.Invoke();
        }

        public void RefreshSessionState()
        {
            SessionState = SessionStateData.Load();
        }

        public event Action OnDataChanged;

        private void LoadAgents()
        {
            Agents.Clear();
            var dir = PathResolver.AgentsDir;
            if (!Directory.Exists(dir)) return;

            var files = Directory.GetFiles(dir, "*.md");
            foreach (var file in files)
            {
                try
                {
                    var parsed = FrontmatterParser.Parse(file);
                    if (parsed.Fields.Count > 0)
                    {
                        var agent = AgentData.FromFrontmatter(parsed, file);
                        Agents.Add(agent);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[ClaudeCode] Failed to parse agent: {file}\n{e.Message}");
                }
            }

            // Sort: Directors first, then Leads, then Specialists, then Sub-Specialists
            Agents.Sort((a, b) => a.Tier.CompareTo(b.Tier));
        }

        private void LoadSkills()
        {
            Skills.Clear();
            var dir = PathResolver.SkillsDir;
            if (!Directory.Exists(dir)) return;

            var skillDirs = Directory.GetDirectories(dir);
            foreach (var skillDir in skillDirs)
            {
                var skillFile = Path.Combine(skillDir, "SKILL.md");
                if (!File.Exists(skillFile)) continue;

                try
                {
                    var parsed = FrontmatterParser.Parse(skillFile);
                    var name = Path.GetFileName(skillDir);
                    var skill = SkillData.FromFrontmatter(parsed, skillDir, name);
                    Skills.Add(skill);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[ClaudeCode] Failed to parse skill: {skillDir}\n{e.Message}");
                }
            }

            Skills.Sort((a, b) => a.Category.CompareTo(b.Category));
        }

        private void LoadSprints()
        {
            Sprints.Clear();
            var dir = PathResolver.SprintsDir;
            if (!Directory.Exists(dir)) return;

            var files = Directory.GetFiles(dir, "*.md");
            foreach (var file in files)
            {
                try
                {
                    Sprints.Add(SprintData.FromFile(file));
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[ClaudeCode] Failed to parse sprint: {file}\n{e.Message}");
                }
            }
        }

        private void LoadDesignDocs()
        {
            DesignDocPaths.Clear();
            var dir = PathResolver.DesignDir;
            if (!Directory.Exists(dir)) return;

            var files = Directory.GetFiles(dir, "*.md", SearchOption.AllDirectories);
            DesignDocPaths.AddRange(files.Select(f => f.Replace('\\', '/')));
            DesignDocPaths.Sort();
        }
    }
}
