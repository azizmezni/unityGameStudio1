using System.Diagnostics;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace ClaudeCodeGameStudios.Utilities
{
    /// <summary>
    /// Opens a system terminal with a Claude Code command pre-filled.
    /// </summary>
    public static class TerminalLauncher
    {
        public static void Launch(string command)
        {
            var projectRoot = PathResolver.ProjectRoot;

#if UNITY_EDITOR_WIN
            LaunchWindows(projectRoot, command);
#elif UNITY_EDITOR_OSX
            LaunchMacOS(projectRoot, command);
#elif UNITY_EDITOR_LINUX
            LaunchLinux(projectRoot, command);
#else
            Debug.LogWarning("[ClaudeCode] Unsupported platform for terminal launch.");
#endif
        }

        private static void LaunchWindows(string projectRoot, string command)
        {
            // Try PowerShell first, fall back to cmd
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/k \"cd /d \"{projectRoot}\" && claude {command}\"",
                    UseShellExecute = true,
                    WorkingDirectory = projectRoot
                };
                Process.Start(psi);
                Debug.Log($"[ClaudeCode] Launched terminal: claude {command}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[ClaudeCode] Failed to launch terminal: {e.Message}");
                EditorUtility.DisplayDialog("Terminal Launch Failed",
                    $"Could not open terminal.\nMake sure 'claude' is on your PATH.\n\nCommand: claude {command}",
                    "OK");
            }
        }

        private static void LaunchMacOS(string projectRoot, string command)
        {
            try
            {
                var script = $"tell application \"Terminal\" to do script \"cd '{projectRoot}' && claude {command}\"";
                var psi = new ProcessStartInfo
                {
                    FileName = "osascript",
                    Arguments = $"-e '{script}'",
                    UseShellExecute = false
                };
                Process.Start(psi);
                Debug.Log($"[ClaudeCode] Launched terminal: claude {command}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[ClaudeCode] Failed to launch terminal: {e.Message}");
            }
        }

        private static void LaunchLinux(string projectRoot, string command)
        {
            string[] terminals = { "gnome-terminal", "xterm", "x-terminal-emulator", "konsole" };
            foreach (var term in terminals)
            {
                try
                {
                    var args = term == "gnome-terminal"
                        ? $"-- bash -c 'cd \"{projectRoot}\" && claude {command}; exec bash'"
                        : $"-e bash -c 'cd \"{projectRoot}\" && claude {command}; exec bash'";

                    var psi = new ProcessStartInfo
                    {
                        FileName = term,
                        Arguments = args,
                        UseShellExecute = false
                    };
                    Process.Start(psi);
                    Debug.Log($"[ClaudeCode] Launched {term}: claude {command}");
                    return;
                }
                catch { /* try next terminal */ }
            }
            Debug.LogError("[ClaudeCode] No supported terminal emulator found.");
        }
    }
}
