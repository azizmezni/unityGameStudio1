using System;
using System.IO;
using ClaudeCodeGameStudios.Utilities;
using UnityEditor;
using UnityEngine;

namespace ClaudeCodeGameStudios.Integration
{
    /// <summary>
    /// Bridges Unity Console errors/warnings to a log file that Claude Code agents can read.
    /// Writes to production/session-state/console-bridge.log.
    /// </summary>
    [InitializeOnLoad]
    public static class ConsoleLogBridge
    {
        private const long MaxLogSizeBytes = 1024 * 1024; // 1MB rotation

        static ConsoleLogBridge()
        {
            Application.logMessageReceived -= OnLogMessage;
            Application.logMessageReceived += OnLogMessage;
        }

        private static void OnLogMessage(string message, string stackTrace, LogType type)
        {
            // Only capture errors, exceptions, and warnings
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Warning)
                return;

            // Skip our own messages
            if (message.StartsWith("[ClaudeCode]") || message.StartsWith("[NAMING]") || message.StartsWith("[ASSET]"))
                return;

            var logPath = PathResolver.ConsoleBridgeLog;
            var dir = Path.GetDirectoryName(logPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            // Rotate if too large
            if (File.Exists(logPath))
            {
                var info = new FileInfo(logPath);
                if (info.Length > MaxLogSizeBytes)
                {
                    var backupPath = logPath + ".old";
                    if (File.Exists(backupPath))
                        File.Delete(backupPath);
                    File.Move(logPath, backupPath);
                }
            }

            var level = type switch
            {
                LogType.Error => "ERROR",
                LogType.Exception => "EXCEPTION",
                LogType.Warning => "WARNING",
                _ => "INFO"
            };

            var firstStackLine = "";
            if (!string.IsNullOrEmpty(stackTrace))
            {
                var lines = stackTrace.Split('\n');
                if (lines.Length > 0)
                    firstStackLine = lines[0].Trim();
            }

            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            var entry = string.IsNullOrEmpty(firstStackLine)
                ? $"[{timestamp}] [{level}] {message}\n"
                : $"[{timestamp}] [{level}] [{firstStackLine}] {message}\n";

            try
            {
                File.AppendAllText(logPath, entry);
            }
            catch (Exception)
            {
                // Silently fail — don't create recursive log loops
            }
        }
    }
}
