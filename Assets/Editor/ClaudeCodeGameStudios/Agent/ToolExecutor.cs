using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using ClaudeCodeGameStudios.Utilities;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace ClaudeCodeGameStudios.Agent
{
    /// <summary>
    /// Executes tools requested by the LLM: file I/O, shell commands, search.
    /// All file operations are scoped to the project root for safety.
    /// </summary>
    public static class ToolExecutor
    {
        public static readonly string[] ToolDescriptions =
        {
            @"<tool name=""read_file"">Read a file. Params: path (relative to project root). Returns file contents.</tool>",
            @"<tool name=""write_file"">Write/create a file. Params: path, content. Creates parent dirs if needed. Returns confirmation.</tool>",
            @"<tool name=""edit_file"">Replace text in a file. Params: path, old_text, new_text. Returns confirmation.</tool>",
            @"<tool name=""list_files"">List files in a directory. Params: path (relative), pattern (glob, optional). Returns file list.</tool>",
            @"<tool name=""search_files"">Search file contents. Params: pattern (regex), path (directory, optional), file_pattern (glob, optional). Returns matches.</tool>",
            @"<tool name=""run_command"">Run a shell command. Params: command. Returns stdout+stderr. Timeout: 30s.</tool>"
        };

        public static string BuildToolSystemPrompt()
        {
            var sb = new StringBuilder();
            sb.AppendLine("You have access to the following tools. To use a tool, output a block like this:");
            sb.AppendLine();
            sb.AppendLine("<use_tool name=\"tool_name\">");
            sb.AppendLine("<param name=\"param_name\">value</param>");
            sb.AppendLine("</use_tool>");
            sb.AppendLine();
            sb.AppendLine("You can use multiple tools in a single response. After each tool block, I will execute it and show you the result, then you continue.");
            sb.AppendLine();
            sb.AppendLine("Available tools:");
            foreach (var desc in ToolDescriptions)
                sb.AppendLine(desc);
            sb.AppendLine();
            sb.AppendLine($"Project root: {PathResolver.ProjectRoot}");
            sb.AppendLine("All file paths are relative to the project root unless they start with / or a drive letter.");
            sb.AppendLine();
            sb.AppendLine("IMPORTANT: When writing C# scripts for Unity, always write complete, compilable files. Use proper namespaces, using directives, and Unity conventions (PascalCase classes, _camelCase private fields).");
            return sb.ToString();
        }

        /// <summary>
        /// Parse all tool calls from LLM output.
        /// </summary>
        public static List<ToolCall> ParseToolCalls(string response)
        {
            var calls = new List<ToolCall>();
            var regex = new Regex(
                @"<use_tool\s+name=""([^""]+)"">\s*(.*?)\s*</use_tool>",
                RegexOptions.Singleline);

            var paramRegex = new Regex(
                @"<param\s+name=""([^""]+)"">(.*?)</param>",
                RegexOptions.Singleline);

            foreach (Match match in regex.Matches(response))
            {
                var call = new ToolCall
                {
                    ToolName = match.Groups[1].Value,
                    Params = new Dictionary<string, string>()
                };

                foreach (Match pm in paramRegex.Matches(match.Groups[2].Value))
                {
                    call.Params[pm.Groups[1].Value] = pm.Groups[2].Value;
                }

                call.RawMatch = match.Value;
                calls.Add(call);
            }

            return calls;
        }

        /// <summary>
        /// Get the text content outside of tool calls (the LLM's message to the user).
        /// </summary>
        public static string GetTextOutsideTools(string response)
        {
            var regex = new Regex(
                @"<use_tool\s+name=""[^""]+"">\s*.*?\s*</use_tool>",
                RegexOptions.Singleline);
            var text = regex.Replace(response, "").Trim();
            return text;
        }

        /// <summary>
        /// Execute a single tool call. Returns the result string.
        /// </summary>
        public static string Execute(ToolCall call)
        {
            try
            {
                return call.ToolName switch
                {
                    "read_file" => ExecuteReadFile(call),
                    "write_file" => ExecuteWriteFile(call),
                    "edit_file" => ExecuteEditFile(call),
                    "list_files" => ExecuteListFiles(call),
                    "search_files" => ExecuteSearchFiles(call),
                    "run_command" => ExecuteRunCommand(call),
                    _ => $"Unknown tool: {call.ToolName}"
                };
            }
            catch (Exception e)
            {
                return $"Error: {e.Message}";
            }
        }

        private static string ResolvePath(string path)
        {
            if (string.IsNullOrEmpty(path)) return PathResolver.ProjectRoot;
            path = path.Trim().Trim('"');

            // If already absolute, validate it's within project
            if (Path.IsPathRooted(path))
            {
                var normalized = Path.GetFullPath(path).Replace('\\', '/');
                if (!normalized.StartsWith(PathResolver.ProjectRoot))
                    return null; // Blocked: outside project
                return normalized;
            }

            return Path.GetFullPath(Path.Combine(PathResolver.ProjectRoot, path)).Replace('\\', '/');
        }

        private static string ExecuteReadFile(ToolCall call)
        {
            var path = ResolvePath(call.Params.GetValueOrFallback("path", ""));
            if (path == null) return "Error: Path is outside project root.";
            if (!File.Exists(path)) return $"Error: File not found: {path}";

            var content = File.ReadAllText(path);
            if (content.Length > 50000)
                content = content.Substring(0, 50000) + "\n... (truncated, file too large)";
            return content;
        }

        private static string ExecuteWriteFile(ToolCall call)
        {
            var path = ResolvePath(call.Params.GetValueOrFallback("path", ""));
            if (path == null) return "Error: Path is outside project root.";

            var content = call.Params.GetValueOrFallback("content", "");
            if (string.IsNullOrEmpty(content)) return "Error: No content provided.";

            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(path, content);
            Debug.Log($"[ClaudeCode Agent] Wrote file: {path} ({content.Length} chars)");
            return $"File written: {path} ({content.Length} chars)";
        }

        private static string ExecuteEditFile(ToolCall call)
        {
            var path = ResolvePath(call.Params.GetValueOrFallback("path", ""));
            if (path == null) return "Error: Path is outside project root.";
            if (!File.Exists(path)) return $"Error: File not found: {path}";

            var oldText = call.Params.GetValueOrFallback("old_text", "");
            var newText = call.Params.GetValueOrFallback("new_text", "");
            if (string.IsNullOrEmpty(oldText)) return "Error: No old_text provided.";

            var content = File.ReadAllText(path);
            if (!content.Contains(oldText))
                return $"Error: old_text not found in file. Make sure it matches exactly (including whitespace).";

            var count = Regex.Matches(content, Regex.Escape(oldText)).Count;
            if (count > 1)
                return $"Error: old_text found {count} times. Provide more context to make it unique.";

            content = content.Replace(oldText, newText);
            File.WriteAllText(path, content);
            Debug.Log($"[ClaudeCode Agent] Edited file: {path}");
            return $"File edited: {path}";
        }

        private static string ExecuteListFiles(ToolCall call)
        {
            var path = ResolvePath(call.Params.GetValueOrFallback("path", ""));
            if (path == null) return "Error: Path is outside project root.";
            if (!Directory.Exists(path)) return $"Error: Directory not found: {path}";

            var pattern = call.Params.GetValueOrFallback("pattern", "*");
            var entries = new List<string>();

            foreach (var dir in Directory.GetDirectories(path))
                entries.Add(Path.GetFileName(dir) + "/");

            foreach (var file in Directory.GetFiles(path, pattern))
                entries.Add(Path.GetFileName(file));

            if (entries.Count == 0) return "Directory is empty.";
            if (entries.Count > 200)
                return string.Join("\n", entries.Take(200)) + $"\n... ({entries.Count - 200} more)";
            return string.Join("\n", entries);
        }

        private static string ExecuteSearchFiles(ToolCall call)
        {
            var searchPattern = call.Params.GetValueOrFallback("pattern", "");
            if (string.IsNullOrEmpty(searchPattern)) return "Error: No search pattern provided.";

            var searchPath = ResolvePath(call.Params.GetValueOrFallback("path", ""));
            if (searchPath == null) return "Error: Path is outside project root.";
            if (!Directory.Exists(searchPath)) return $"Error: Directory not found: {searchPath}";

            var filePattern = call.Params.GetValueOrFallback("file_pattern", "*.cs");
            var results = new StringBuilder();
            var matchCount = 0;
            var regex = new Regex(searchPattern, RegexOptions.IgnoreCase);

            foreach (var file in Directory.GetFiles(searchPath, filePattern, SearchOption.AllDirectories))
            {
                if (matchCount > 50) break;
                try
                {
                    var lines = File.ReadAllLines(file);
                    for (int i = 0; i < lines.Length; i++)
                    {
                        if (regex.IsMatch(lines[i]))
                        {
                            var relPath = PathResolver.GetRelativeToProject(file);
                            results.AppendLine($"{relPath}:{i + 1}: {lines[i].Trim()}");
                            matchCount++;
                            if (matchCount > 50)
                            {
                                results.AppendLine("... (truncated at 50 matches)");
                                break;
                            }
                        }
                    }
                }
                catch { /* skip unreadable files */ }
            }

            return matchCount == 0 ? "No matches found." : results.ToString();
        }

        private static string ExecuteRunCommand(ToolCall call)
        {
            var command = call.Params.GetValueOrFallback("command", "");
            if (string.IsNullOrEmpty(command)) return "Error: No command provided.";

            // Safety: block destructive commands
            var blocked = new[] { "rm -rf", "del /s", "format ", "shutdown", "reboot" };
            foreach (var b in blocked)
            {
                if (command.ToLower().Contains(b))
                    return $"Error: Blocked potentially destructive command containing '{b}'.";
            }

            try
            {
                var psi = new ProcessStartInfo
                {
                    WorkingDirectory = PathResolver.ProjectRoot,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

#if UNITY_EDITOR_WIN
                psi.FileName = "cmd.exe";
                psi.Arguments = $"/c {command}";
#else
                psi.FileName = "/bin/bash";
                psi.Arguments = $"-c \"{command.Replace("\"", "\\\"")}\"";
#endif

                var process = Process.Start(psi);
                if (process == null) return "Error: Failed to start process.";

                process.WaitForExit(60000); // 60s timeout

                var stdout = process.StandardOutput.ReadToEnd();
                var stderr = process.StandardError.ReadToEnd();
                var exitCode = process.ExitCode;

                var result = new StringBuilder();
                if (!string.IsNullOrEmpty(stdout))
                    result.AppendLine(stdout.TrimEnd());
                if (!string.IsNullOrEmpty(stderr))
                    result.AppendLine($"[stderr] {stderr.TrimEnd()}");
                result.AppendLine($"[exit code: {exitCode}]");

                var output = result.ToString();
                if (output.Length > 10000)
                    output = output.Substring(0, 10000) + "\n... (truncated)";

                Debug.Log($"[ClaudeCode Agent] Ran: {command} (exit {exitCode})");
                return output;
            }
            catch (Exception e)
            {
                return $"Error running command: {e.Message}";
            }
        }
    }

    public class ToolCall
    {
        public string ToolName;
        public Dictionary<string, string> Params;
        public string RawMatch;
    }

    public static class DictExtensions
    {
        public static string GetValueOrFallback(this Dictionary<string, string> dict, string key, string fallback)
        {
            return dict.TryGetValue(key, out var val) ? val : fallback;
        }
    }
}
