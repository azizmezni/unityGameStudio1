using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace ClaudeCodeGameStudios.Integration
{
    /// <summary>
    /// Validates asset naming conventions and file integrity on import.
    /// Assets should use lowercase_with_underscores naming.
    /// </summary>
    public class AssetValidator : AssetPostprocessor
    {
        private static readonly Regex ValidAssetName = new(@"^[a-z0-9][a-z0-9_\-]*\.[a-z0-9]+$");
        private static readonly string[] AssetExtensions =
        {
            ".png", ".jpg", ".jpeg", ".tga", ".psd", ".exr", ".hdr",
            ".wav", ".mp3", ".ogg", ".aiff",
            ".fbx", ".obj", ".blend", ".gltf", ".glb",
            ".mat", ".shader", ".shadergraph",
            ".json", ".xml", ".csv", ".txt"
        };

        private static void OnPostprocessAllAssets(
            string[] importedAssets, string[] deletedAssets,
            string[] movedAssets, string[] movedFromAssetPaths)
        {
            foreach (var asset in importedAssets)
            {
                if (asset.Contains("/Editor/")) continue;

                var ext = Path.GetExtension(asset).ToLower();
                if (System.Array.IndexOf(AssetExtensions, ext) < 0) continue;

                ValidateAssetName(asset);

                if (ext == ".json")
                    ValidateJson(asset);
            }
        }

        private static void ValidateAssetName(string assetPath)
        {
            var fileName = Path.GetFileName(assetPath);
            if (!ValidAssetName.IsMatch(fileName))
            {
                Debug.LogWarning(
                    $"[ASSET] {assetPath} — Asset name should be lowercase_with_underscores (e.g., player_idle.png)");
            }
        }

        private static void ValidateJson(string assetPath)
        {
            var fullPath = Path.GetFullPath(assetPath);
            if (!File.Exists(fullPath)) return;

            try
            {
                var content = File.ReadAllText(fullPath);
                JsonUtility.FromJson<object>(content);
            }
            catch (System.Exception)
            {
                Debug.LogWarning($"[ASSET] {assetPath} — JSON file has invalid syntax");
            }
        }
    }
}
