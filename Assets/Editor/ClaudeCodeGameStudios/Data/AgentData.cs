using System;

namespace ClaudeCodeGameStudios.Data
{
    public enum AgentTier
    {
        Director,
        Lead,
        Specialist,
        SubSpecialist
    }

    [Serializable]
    public class AgentData
    {
        public string Name;
        public string Description;
        public string Model;
        public string[] Tools;
        public string[] Skills;
        public AgentTier Tier;
        public bool IsUnityRelevant;
        public string FilePath;

        private static readonly string[] DirectorNames =
        {
            "creative-director", "technical-director", "producer"
        };

        private static readonly string[] LeadNames =
        {
            "lead-programmer", "art-director", "audio-director",
            "narrative-director", "qa-lead", "release-manager",
            "localization-lead", "game-designer"
        };

        private static readonly string[] NonUnityEngineAgents =
        {
            "godot-specialist", "godot-gdscript-specialist",
            "godot-shader-specialist", "godot-gdextension-specialist",
            "unreal-specialist", "ue-blueprint-specialist",
            "ue-gas-specialist", "ue-replication-specialist",
            "ue-umg-specialist"
        };

        public static AgentData FromFrontmatter(FrontmatterParser.ParseResult parsed, string filePath)
        {
            var agent = new AgentData
            {
                FilePath = filePath,
                Name = FrontmatterParser.GetField(parsed.Fields, "name"),
                Description = FrontmatterParser.GetField(parsed.Fields, "description"),
                Model = FrontmatterParser.GetField(parsed.Fields, "model", "sonnet"),
                Tools = FrontmatterParser.ParseArray(
                    FrontmatterParser.GetField(parsed.Fields, "tools")),
                Skills = FrontmatterParser.ParseArray(
                    FrontmatterParser.GetField(parsed.Fields, "skills"))
            };

            agent.Tier = ClassifyTier(agent.Name, agent.Model);
            agent.IsUnityRelevant = !Array.Exists(NonUnityEngineAgents,
                n => n == agent.Name);

            return agent;
        }

        private static AgentTier ClassifyTier(string name, string model)
        {
            if (model == "opus" || Array.Exists(DirectorNames, n => n == name))
                return AgentTier.Director;

            if (Array.Exists(LeadNames, n => n == name))
                return AgentTier.Lead;

            if (name.StartsWith("unity-") || name.StartsWith("godot-") || name.StartsWith("ue-"))
                return AgentTier.SubSpecialist;

            return AgentTier.Specialist;
        }

        public string TierLabel => Tier switch
        {
            AgentTier.Director => "Director",
            AgentTier.Lead => "Lead",
            AgentTier.SubSpecialist => "Sub-Specialist",
            _ => "Specialist"
        };

        public string ModelBadge => Model switch
        {
            "opus" => "OPUS",
            "sonnet" => "SONNET",
            "haiku" => "HAIKU",
            _ => Model?.ToUpper() ?? "?"
        };
    }
}
