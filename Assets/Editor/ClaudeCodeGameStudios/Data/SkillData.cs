using System;
using System.Collections.Generic;

namespace ClaudeCodeGameStudios.Data
{
    public enum SkillCategory
    {
        Setup,
        Creative,
        Production,
        Code,
        Quality,
        Release,
        Localization,
        Teams
    }

    [Serializable]
    public class SkillData
    {
        public string Name;
        public string Description;
        public string ArgumentHint;
        public bool UserInvocable;
        public string[] AllowedTools;
        public SkillCategory Category;
        public string DirectoryPath;

        private static readonly Dictionary<string, SkillCategory> CategoryMap = new()
        {
            // Setup
            { "start", SkillCategory.Setup },
            { "onboard", SkillCategory.Setup },
            { "setup-engine", SkillCategory.Setup },
            { "project-stage-detect", SkillCategory.Setup },
            // Creative
            { "brainstorm", SkillCategory.Creative },
            { "design-system", SkillCategory.Creative },
            { "design-review", SkillCategory.Creative },
            { "map-systems", SkillCategory.Creative },
            { "prototype", SkillCategory.Creative },
            // Production
            { "sprint-plan", SkillCategory.Production },
            { "milestone-review", SkillCategory.Production },
            { "estimate", SkillCategory.Production },
            { "scope-check", SkillCategory.Production },
            { "gate-check", SkillCategory.Production },
            { "retrospective", SkillCategory.Production },
            // Code
            { "code-review", SkillCategory.Code },
            { "architecture-decision", SkillCategory.Code },
            { "tech-debt", SkillCategory.Code },
            { "reverse-document", SkillCategory.Code },
            // Quality
            { "bug-report", SkillCategory.Quality },
            { "playtest-report", SkillCategory.Quality },
            { "perf-profile", SkillCategory.Quality },
            { "asset-audit", SkillCategory.Quality },
            { "balance-check", SkillCategory.Quality },
            // Release
            { "release-checklist", SkillCategory.Release },
            { "launch-checklist", SkillCategory.Release },
            { "changelog", SkillCategory.Release },
            { "patch-notes", SkillCategory.Release },
            { "hotfix", SkillCategory.Release },
            // Localization
            { "localize", SkillCategory.Localization },
            // Teams
            { "team-audio", SkillCategory.Teams },
            { "team-combat", SkillCategory.Teams },
            { "team-level", SkillCategory.Teams },
            { "team-narrative", SkillCategory.Teams },
            { "team-polish", SkillCategory.Teams },
            { "team-release", SkillCategory.Teams },
            { "team-ui", SkillCategory.Teams },
        };

        public static SkillData FromFrontmatter(FrontmatterParser.ParseResult parsed, string dirPath, string name)
        {
            var skill = new SkillData
            {
                DirectoryPath = dirPath,
                Name = FrontmatterParser.GetField(parsed.Fields, "name", name),
                Description = FrontmatterParser.GetField(parsed.Fields, "description"),
                ArgumentHint = FrontmatterParser.GetField(parsed.Fields, "argument-hint"),
                AllowedTools = FrontmatterParser.ParseArray(
                    FrontmatterParser.GetField(parsed.Fields, "allowed-tools"))
            };

            var invocable = FrontmatterParser.GetField(parsed.Fields, "user-invocable", "false");
            skill.UserInvocable = invocable.ToLower() == "true";

            skill.Category = CategoryMap.TryGetValue(skill.Name, out var cat)
                ? cat : SkillCategory.Production;

            return skill;
        }

        public string CategoryLabel => Category switch
        {
            SkillCategory.Setup => "Setup & Onboarding",
            SkillCategory.Creative => "Creative & Design",
            SkillCategory.Production => "Production",
            SkillCategory.Code => "Code & Architecture",
            SkillCategory.Quality => "Quality & Testing",
            SkillCategory.Release => "Release",
            SkillCategory.Localization => "Localization",
            SkillCategory.Teams => "Team Orchestration",
            _ => "Other"
        };
    }
}
