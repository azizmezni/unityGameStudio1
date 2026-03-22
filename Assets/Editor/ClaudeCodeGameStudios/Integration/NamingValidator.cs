using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace ClaudeCodeGameStudios.Integration
{
    /// <summary>
    /// Validates C# file naming conventions on import.
    /// Files must be PascalCase. Private fields should use _camelCase.
    /// </summary>
    public class NamingValidator : AssetPostprocessor
    {
        private static readonly Regex PascalCaseFile = new(@"^[A-Z][a-zA-Z0-9]*\.cs$");
        private static readonly Regex PrivateFieldBad = new(
            @"^\s*(?:private\s+)?(?:(?:static|readonly|const)\s+)*\w+(?:<[^>]+>)?\s+([a-z][a-zA-Z0-9]*)\s*[;=]",
            RegexOptions.Multiline);
        private static readonly Regex PrivateFieldGood = new(@"^_[a-z]");

        private static void OnPostprocessAllAssets(
            string[] importedAssets, string[] deletedAssets,
            string[] movedAssets, string[] movedFromAssetPaths)
        {
            foreach (var asset in importedAssets)
            {
                if (!asset.EndsWith(".cs")) continue;
                if (asset.Contains("/Editor/ClaudeCodeGameStudios/")) continue; // Don't validate ourselves

                ValidateFileName(asset);
                ValidateFieldNaming(asset);
            }
        }

        private static void ValidateFileName(string assetPath)
        {
            var fileName = Path.GetFileName(assetPath);
            if (!PascalCaseFile.IsMatch(fileName))
            {
                Debug.LogWarning(
                    $"[NAMING] {assetPath} — File name should be PascalCase (e.g., PlayerController.cs)");
            }
        }

        private static void ValidateFieldNaming(string assetPath)
        {
            var fullPath = Path.GetFullPath(assetPath);
            if (!File.Exists(fullPath)) return;

            var content = File.ReadAllText(fullPath);
            var lines = content.Split('\n');

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                // Skip public, protected, internal fields
                if (line.Contains("public ") || line.Contains("protected ") || line.Contains("internal "))
                    continue;
                // Skip method declarations, properties, using statements
                if (line.Contains("(") || line.Contains("=>") || line.Trim().StartsWith("using "))
                    continue;

                var match = PrivateFieldBad.Match(line);
                if (match.Success)
                {
                    var fieldName = match.Groups[1].Value;
                    // Skip if it already starts with underscore or is a common exception
                    if (fieldName.StartsWith("_") || fieldName == "value") continue;

                    Debug.LogWarning(
                        $"[NAMING] {assetPath}:{i + 1} — Private field '{fieldName}' should use _camelCase (e.g., _{fieldName})");
                }
            }
        }
    }
}
