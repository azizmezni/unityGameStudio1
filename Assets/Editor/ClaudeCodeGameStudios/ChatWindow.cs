using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClaudeCodeGameStudios.Agent;
using ClaudeCodeGameStudios.Data;
using ClaudeCodeGameStudios.Providers;
using ClaudeCodeGameStudios.Utilities;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ClaudeCodeGameStudios
{
    /// <summary>
    /// Full AI coding assistant inside Unity Editor.
    /// Reads/writes files, runs commands, creates scripts — uses any configured LLM provider.
    /// </summary>
    public class ChatWindow : EditorWindow
    {
        private TextField _inputField;
        private ScrollView _chatScroll;
        private VisualElement _chatContainer;
        private PopupField<string> _providerDropdown;
        private PopupField<string> _modelDropdown;
        private Label _statusLabel;
        private Button _sendBtn;
        private Button _stopBtn;
        private Button _autofixBtn;

        private ProviderType _selectedProvider;
        private string _selectedModel;
        private string _activeSkillPrompt;
        private string _activeSkillName;

        private AgentLoop _agentLoop;
        private readonly List<ChatEntry> _entries = new();

        private static readonly Color UserBg = new(0.18f, 0.30f, 0.50f);
        private static readonly Color AssistantBg = new(0.20f, 0.20f, 0.24f);
        private static readonly Color ToolBg = new(0.15f, 0.22f, 0.18f);
        private static readonly Color ErrorBg = new(0.35f, 0.12f, 0.12f);
        private static readonly Color SystemBg = new(0.12f, 0.25f, 0.20f);

        private enum EntryType { User, Assistant, Tool, Error, System }
        private class ChatEntry
        {
            public EntryType Type;
            public string Title;
            public string Content;
        }

        [MenuItem("Window/Claude Code Game Studios/Chat %#j")]
        public static void ShowWindow()
        {
            var window = GetWindow<ChatWindow>();
            window.titleContent = new GUIContent("Studio Agent");
            window.minSize = new Vector2(600, 450);
        }

        public static void OpenWithSkill(string skillName)
        {
            var window = GetWindow<ChatWindow>();
            window.titleContent = new GUIContent($"Studio Agent — /{skillName}");
            window.minSize = new Vector2(600, 450);
            window.LoadSkill(skillName);
        }

        private void OnEnable()
        {
            BuildUI();
            LoadProviderDefaults();
        }

        private void OnDisable()
        {
            _agentLoop?.Stop();
        }

        private void BuildUI()
        {
            rootVisualElement.Clear();
            rootVisualElement.style.backgroundColor = new Color(0.13f, 0.13f, 0.16f);

            // Outer container that fills the entire window
            var outer = new VisualElement();
            outer.style.position = Position.Absolute;
            outer.style.top = 0;
            outer.style.bottom = 0;
            outer.style.left = 0;
            outer.style.right = 0;
            outer.style.flexDirection = FlexDirection.Column;
            rootVisualElement.Add(outer);

            // === Top bar: provider + model (pinned top) ===
            var topBar = new VisualElement();
            topBar.style.flexDirection = FlexDirection.Row;
            topBar.style.alignItems = Align.Center;
            topBar.style.flexShrink = 0; // never shrink
            topBar.style.paddingLeft = 8;
            topBar.style.paddingRight = 8;
            topBar.style.paddingTop = 4;
            topBar.style.paddingBottom = 4;
            topBar.style.backgroundColor = new Color(0.10f, 0.10f, 0.13f);
            topBar.style.borderBottomWidth = 1;
            topBar.style.borderBottomColor = new Color(0.25f, 0.25f, 0.30f);

            topBar.Add(MakeLabel("Provider:", 11, new Color(0.5f, 0.5f, 0.55f), 4));

            var config = LLMProviderConfig.Instance;
            var providerNames = config.Providers.Select(p => p.DisplayName).ToList();
            _providerDropdown = new PopupField<string>(providerNames, 0);
            _providerDropdown.style.width = 130;
            _providerDropdown.style.fontSize = 10;
            _providerDropdown.RegisterValueChangedCallback(OnProviderChanged);
            topBar.Add(_providerDropdown);

            topBar.Add(MakeLabel("Model:", 11, new Color(0.5f, 0.5f, 0.55f), 10));

            _modelDropdown = new PopupField<string>(new List<string> { "..." }, 0);
            _modelDropdown.style.width = 180;
            _modelDropdown.style.fontSize = 10;
            _modelDropdown.RegisterValueChangedCallback(evt => _selectedModel = evt.newValue);
            topBar.Add(_modelDropdown);

            _statusLabel = new Label("Ready");
            _statusLabel.style.fontSize = 9;
            _statusLabel.style.color = new Color(0.4f, 0.8f, 0.4f);
            _statusLabel.style.flexGrow = 1;
            _statusLabel.style.unityTextAlign = TextAnchor.MiddleRight;
            topBar.Add(_statusLabel);

            var clearBtn = new Button(ClearChat) { text = "Clear" };
            clearBtn.style.height = 20;
            clearBtn.style.fontSize = 10;
            clearBtn.style.marginLeft = 6;
            topBar.Add(clearBtn);

            outer.Add(topBar);

            // === Skill bar (optional, pinned below top bar) ===
            if (!string.IsNullOrEmpty(_activeSkillName))
            {
                var skillBar = new VisualElement();
                skillBar.style.flexDirection = FlexDirection.Row;
                skillBar.style.alignItems = Align.Center;
                skillBar.style.flexShrink = 0;
                skillBar.style.paddingLeft = 8;
                skillBar.style.paddingRight = 8;
                skillBar.style.paddingTop = 3;
                skillBar.style.paddingBottom = 3;
                skillBar.style.backgroundColor = new Color(0.12f, 0.18f, 0.28f);

                var skillLabel = new Label($"Skill: /{_activeSkillName}");
                skillLabel.style.fontSize = 10;
                skillLabel.style.color = new Color(0.4f, 0.7f, 1f);
                skillLabel.style.flexGrow = 1;
                skillBar.Add(skillLabel);

                var removeBtn = new Button(() =>
                {
                    _activeSkillName = null;
                    _activeSkillPrompt = null;
                    titleContent = new GUIContent("Studio Agent");
                    BuildUI();
                }) { text = "Remove" };
                removeBtn.style.height = 16;
                removeBtn.style.fontSize = 9;
                skillBar.Add(removeBtn);

                outer.Add(skillBar);
            }

            // === Chat scroll area (takes all remaining space) ===
            _chatScroll = new ScrollView(ScrollViewMode.Vertical);
            _chatScroll.style.flexGrow = 1;    // fill available space
            _chatScroll.style.flexShrink = 1;  // shrink if needed
            _chatScroll.style.paddingLeft = 8;
            _chatScroll.style.paddingRight = 8;
            _chatScroll.style.paddingTop = 8;
            _chatContainer = _chatScroll.contentContainer;
            outer.Add(_chatScroll);

            // Re-render history
            foreach (var entry in _entries)
                RenderEntry(entry);

            // === Input area (pinned to bottom, never hidden) ===
            var inputArea = new VisualElement();
            inputArea.style.flexShrink = 0;  // NEVER shrink — always visible
            inputArea.style.backgroundColor = new Color(0.10f, 0.10f, 0.13f);
            inputArea.style.borderTopWidth = 1;
            inputArea.style.borderTopColor = new Color(0.25f, 0.25f, 0.30f);
            inputArea.style.paddingLeft = 8;
            inputArea.style.paddingRight = 8;
            inputArea.style.paddingTop = 6;
            inputArea.style.paddingBottom = 6;

            _inputField = new TextField();
            _inputField.style.flexGrow = 1;
            _inputField.style.fontSize = 12;
            _inputField.multiline = true;
            _inputField.style.minHeight = 28;
            _inputField.style.maxHeight = 80;
            _inputField.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Return && !evt.shiftKey)
                {
                    evt.StopPropagation();
                    evt.PreventDefault();
                    SendMessage();
                }
            });
            inputArea.Add(_inputField);

            // Button row below input
            var btnRow = new VisualElement();
            btnRow.style.flexDirection = FlexDirection.Row;
            btnRow.style.justifyContent = Justify.FlexEnd;
            btnRow.style.marginTop = 4;

            _sendBtn = new Button(SendMessage) { text = "Send" };
            _sendBtn.style.height = 26;
            _sendBtn.style.width = 60;
            _sendBtn.style.fontSize = 11;
            btnRow.Add(_sendBtn);

            _autofixBtn = new Button(RunAutofix) { text = "Autofix" };
            _autofixBtn.style.height = 26;
            _autofixBtn.style.width = 70;
            _autofixBtn.style.marginLeft = 4;
            _autofixBtn.style.fontSize = 11;
            _autofixBtn.style.backgroundColor = new Color(0.6f, 0.2f, 0.2f);
            _autofixBtn.style.color = Color.white;
            btnRow.Add(_autofixBtn);

            _stopBtn = new Button(StopAgent) { text = "Stop" };
            _stopBtn.style.height = 26;
            _stopBtn.style.width = 50;
            _stopBtn.style.marginLeft = 4;
            _stopBtn.style.fontSize = 11;
            _stopBtn.style.display = DisplayStyle.None;
            btnRow.Add(_stopBtn);

            inputArea.Add(btnRow);
            outer.Add(inputArea);

            _inputField.schedule.Execute(() => _inputField.Focus()).ExecuteLater(100);
        }

        private void LoadProviderDefaults()
        {
            var config = LLMProviderConfig.Instance;
            _selectedProvider = config.SpecialistProvider;
            _selectedModel = config.SpecialistModel;

            var provider = config.GetProvider(_selectedProvider);
            if (provider != null)
            {
                var idx = config.Providers.IndexOf(provider);
                if (idx >= 0 && _providerDropdown != null)
                    _providerDropdown.index = idx;
                UpdateModelDropdown(provider);
            }
        }

        private void OnProviderChanged(ChangeEvent<string> evt)
        {
            var config = LLMProviderConfig.Instance;
            var idx = config.Providers.FindIndex(p => p.DisplayName == evt.newValue);
            if (idx < 0) return;

            var provider = config.Providers[idx];
            _selectedProvider = provider.Type;

            if (provider.AvailableModels.Count == 0 && provider.Type != ProviderType.Claude)
            {
                SetStatus("Fetching models...", new Color(0.9f, 0.8f, 0.2f));
                ProviderTestRunner.FetchModels(provider, _ =>
                {
                    UpdateModelDropdown(provider);
                    SetStatus("Ready", new Color(0.4f, 0.8f, 0.4f));
                });
            }
            else
            {
                UpdateModelDropdown(provider);
            }
        }

        private void UpdateModelDropdown(LLMProvider provider)
        {
            var modelNames = provider.AvailableModels.Select(m => m.Id).ToList();
            if (modelNames.Count == 0) modelNames.Add(_selectedModel ?? "default");

            var parent = _modelDropdown.parent;
            var idx = parent.IndexOf(_modelDropdown);
            parent.Remove(_modelDropdown);

            var selectedIdx = modelNames.IndexOf(_selectedModel);
            if (selectedIdx < 0) selectedIdx = 0;

            _modelDropdown = new PopupField<string>(modelNames, selectedIdx);
            _modelDropdown.style.width = 180;
            _modelDropdown.style.fontSize = 10;
            _modelDropdown.RegisterValueChangedCallback(e => _selectedModel = e.newValue);
            parent.Insert(idx, _modelDropdown);

            _selectedModel = modelNames[selectedIdx];
        }

        private void LoadSkill(string skillName)
        {
            var skillPath = Path.Combine(PathResolver.SkillsDir, skillName, "SKILL.md");
            if (!File.Exists(skillPath))
            {
                Debug.LogWarning($"[ClaudeCode] Skill not found: {skillPath}");
                return;
            }

            var parsed = FrontmatterParser.Parse(skillPath);
            _activeSkillName = skillName;
            _activeSkillPrompt = parsed.Body;

            ClearChat();
            AddEntry(EntryType.System, "Skill Loaded",
                $"/{skillName} is active. The agent will follow this skill's instructions.");
            BuildUI();
        }

        private void SendMessage()
        {
            if (_agentLoop != null && _agentLoop.IsRunning) return;

            var msg = _inputField?.value?.Trim();
            if (string.IsNullOrEmpty(msg)) return;
            _inputField.value = "";

            AddEntry(EntryType.User, "You", msg);

            var systemPrompt = BuildSystemPrompt();

            if (_agentLoop == null || !_agentLoop.IsRunning)
            {
                _agentLoop = new AgentLoop(_selectedProvider, _selectedModel, systemPrompt);
                WireAgentEvents(_agentLoop);
            }

            SetAgentRunning(true);

            if (_entries.Count(e => e.Type == EntryType.User) <= 1)
                _agentLoop.Run(msg);
            else
                _agentLoop.Continue(msg);
        }

        private void WireAgentEvents(AgentLoop agent)
        {
            agent.OnTextOutput += text =>
            {
                AddEntry(EntryType.Assistant, _selectedProvider.ToString(), text);
            };

            agent.OnToolExecuted += (call, result) =>
            {
                var title = call.ToolName;
                if (call.Params.TryGetValue("path", out var path))
                    title += $": {path}";
                else if (call.Params.TryGetValue("command", out var cmd))
                    title += $": {cmd}";

                var displayResult = result;
                if (displayResult.Length > 2000)
                    displayResult = displayResult.Substring(0, 2000) + "\n... (truncated in display)";

                AddEntry(EntryType.Tool, title, displayResult);
            };

            agent.OnError += error =>
            {
                AddEntry(EntryType.Error, "Error", error);
                SetAgentRunning(false);
            };

            agent.OnComplete += () =>
            {
                SetAgentRunning(false);
            };

            agent.OnThinking += () =>
            {
                SetStatus($"Agent thinking... (iteration {_entries.Count(e => e.Type == EntryType.Tool)})",
                    new Color(0.9f, 0.8f, 0.2f));
            };
        }

        private void RunAutofix()
        {
            if (_agentLoop != null && _agentLoop.IsRunning) return;

            var errors = new List<string>();

            var compileErrors = CollectCompileErrors();
            if (compileErrors.Count > 0)
                errors.AddRange(compileErrors);

            var bridgeLog = PathResolver.ConsoleBridgeLog;
            if (File.Exists(bridgeLog))
            {
                var logLines = File.ReadAllLines(bridgeLog);
                var recentErrors = logLines
                    .Where(l => l.Contains("[ERROR]") || l.Contains("[EXCEPTION]"))
                    .Reverse().Take(50).Reverse().ToList();
                if (recentErrors.Count > 0)
                    errors.AddRange(recentErrors);
            }

            if (errors.Count == 0)
            {
                AddEntry(EntryType.System, "Autofix", "No errors found. Console is clean.");
                return;
            }

            var errorBlock = string.Join("\n", errors);
            var prompt =
                $"I have {errors.Count} error(s) in my Unity project. Read the relevant source files, understand each error, and fix them all.\n\n" +
                "ERRORS:\n```\n" + errorBlock + "\n```\n\n" +
                "For each error:\n" +
                "1. Read the file that has the error\n" +
                "2. Understand what's wrong\n" +
                "3. Fix it using edit_file (or write_file if the file needs major changes)\n" +
                "4. Move to the next error\n\n" +
                "Fix ALL errors. Don't just describe the fix — actually write the corrected code.";

            AddEntry(EntryType.User, "Autofix", $"Found {errors.Count} error(s). Sending to agent...");

            var systemPrompt = BuildSystemPrompt();
            _agentLoop = new AgentLoop(_selectedProvider, _selectedModel, systemPrompt);
            WireAgentEvents(_agentLoop);
            SetAgentRunning(true);
            _agentLoop.Run(prompt);
        }

        private List<string> CollectCompileErrors()
        {
            var errors = new List<string>();
            var editorLogPath = GetEditorLogPath();
            if (string.IsNullOrEmpty(editorLogPath) || !File.Exists(editorLogPath))
                return errors;

            try
            {
                using var fs = new FileStream(editorLogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(fs);
                var content = reader.ReadToEnd();
                var lines = content.Split('\n');

                var recentLines = lines.Skip(System.Math.Max(0, lines.Length - 200));
                foreach (var line in recentLines)
                {
                    var trimmed = line.Trim();
                    if (trimmed.Contains("error CS") ||
                        trimmed.Contains("error cs") ||
                        (trimmed.Contains(": error ") && trimmed.Contains(".cs")))
                    {
                        if (!errors.Contains(trimmed))
                            errors.Add(trimmed);
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[ClaudeCode] Could not read editor log: {e.Message}");
            }

            return errors;
        }

        private static string GetEditorLogPath()
        {
#if UNITY_EDITOR_WIN
            var localAppData = System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localAppData, "Unity", "Editor", "Editor.log");
#elif UNITY_EDITOR_OSX
            var home = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal);
            return Path.Combine(home, "Library", "Logs", "Unity", "Editor.log");
#elif UNITY_EDITOR_LINUX
            var home = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal);
            return Path.Combine(home, ".config", "unity3d", "Editor.log");
#else
            return "";
#endif
        }

        private void StopAgent()
        {
            _agentLoop?.Stop();
            SetAgentRunning(false);
            AddEntry(EntryType.System, "Stopped", "Agent stopped by user.");
        }

        private void SetAgentRunning(bool running)
        {
            if (_sendBtn != null)
                _sendBtn.style.display = running ? DisplayStyle.None : DisplayStyle.Flex;
            if (_autofixBtn != null)
                _autofixBtn.style.display = running ? DisplayStyle.None : DisplayStyle.Flex;
            if (_stopBtn != null)
                _stopBtn.style.display = running ? DisplayStyle.Flex : DisplayStyle.None;
            if (_inputField != null)
                _inputField.SetEnabled(!running);

            SetStatus(running ? "Agent working..." : "Ready",
                running ? new Color(0.9f, 0.8f, 0.2f) : new Color(0.4f, 0.8f, 0.4f));
        }

        private string BuildSystemPrompt()
        {
            var parts = new List<string>();
            parts.Add(ToolExecutor.BuildToolSystemPrompt());

            if (!string.IsNullOrEmpty(_activeSkillPrompt))
                parts.Add($"=== Active Skill Instructions ===\n{_activeSkillPrompt}");

            parts.Add("You are an AI coding assistant for a Unity 6.3 LTS game project.");
            parts.Add("When asked to create or modify scripts, actually write the files using the write_file or edit_file tools.");
            parts.Add("When asked to run something, use the run_command tool.");
            parts.Add("Always execute the necessary actions — don't just describe what you would do.");

            var state = SessionStateData.Load();
            if (state.Exists && !string.IsNullOrEmpty(state.Breadcrumb))
                parts.Add($"Current project focus: {state.Breadcrumb}");

            return string.Join("\n\n", parts);
        }

        private void ClearChat()
        {
            _entries.Clear();
            _chatContainer?.Clear();
            _agentLoop?.ClearHistory();
            _agentLoop = null;
        }

        private void AddEntry(EntryType type, string title, string content)
        {
            var entry = new ChatEntry { Type = type, Title = title, Content = content };
            _entries.Add(entry);
            RenderEntry(entry);
            ScrollToBottom();
        }

        private void RenderEntry(ChatEntry entry)
        {
            if (_chatContainer == null) return;

            var block = new VisualElement();
            block.style.marginBottom = 6;
            StyleHelper.SetBorderRadius(block.style, 6);

            var bgColor = entry.Type switch
            {
                EntryType.User => UserBg,
                EntryType.Assistant => AssistantBg,
                EntryType.Tool => ToolBg,
                EntryType.Error => ErrorBg,
                EntryType.System => SystemBg,
                _ => AssistantBg
            };
            block.style.backgroundColor = bgColor;
            block.style.paddingLeft = 10;
            block.style.paddingRight = 10;
            block.style.paddingTop = 6;
            block.style.paddingBottom = 6;

            // Tool entries: collapsible foldout
            if (entry.Type == EntryType.Tool)
            {
                var foldout = new Foldout();
                foldout.text = entry.Title;
                foldout.value = false;
                foldout.style.color = new Color(0.5f, 0.85f, 0.55f);

                var headerLabel = foldout.Q<Toggle>().Q<Label>();
                if (headerLabel != null)
                {
                    headerLabel.style.fontSize = 10;
                    headerLabel.style.color = new Color(0.5f, 0.85f, 0.55f);
                }

                // Selectable text field so user can copy
                var contentField = new TextField();
                contentField.value = entry.Content;
                contentField.isReadOnly = true;
                contentField.multiline = true;
                contentField.style.fontSize = 10;
                contentField.style.color = new Color(0.65f, 0.75f, 0.65f);
                contentField.style.whiteSpace = WhiteSpace.Normal;
                contentField.style.backgroundColor = new Color(0, 0, 0, 0);
                contentField.style.borderTopWidth = 0;
                contentField.style.borderBottomWidth = 0;
                contentField.style.borderLeftWidth = 0;
                contentField.style.borderRightWidth = 0;
                contentField.style.maxHeight = 300;
                foldout.Add(contentField);

                block.Add(foldout);
            }
            else
            {
                // Title label (not selectable, just a heading)
                var titleLabel = new Label(entry.Title);
                titleLabel.style.fontSize = 9;
                titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                titleLabel.style.marginBottom = 2;
                titleLabel.style.color = entry.Type switch
                {
                    EntryType.User => new Color(0.5f, 0.7f, 1f),
                    EntryType.Error => new Color(1f, 0.5f, 0.5f),
                    EntryType.System => new Color(0.5f, 0.8f, 0.6f),
                    _ => new Color(0.6f, 0.6f, 0.65f)
                };
                block.Add(titleLabel);

                // Selectable content — read-only TextField so you can select & copy
                var contentField = new TextField();
                contentField.value = entry.Content;
                contentField.isReadOnly = true;
                contentField.multiline = true;
                contentField.style.fontSize = 11;
                contentField.style.color = Color.white;
                contentField.style.whiteSpace = WhiteSpace.Normal;
                // Make it look like a label, not an input box
                contentField.style.backgroundColor = new Color(0, 0, 0, 0);
                contentField.style.borderTopWidth = 0;
                contentField.style.borderBottomWidth = 0;
                contentField.style.borderLeftWidth = 0;
                contentField.style.borderRightWidth = 0;
                contentField.style.paddingLeft = 0;
                contentField.style.paddingRight = 0;
                contentField.style.marginLeft = 0;
                contentField.style.marginRight = 0;
                block.Add(contentField);

                // Copy button for convenience
                if (entry.Type == EntryType.Assistant && entry.Content.Length > 20)
                {
                    var copyBtn = new Button(() =>
                    {
                        EditorGUIUtility.systemCopyBuffer = entry.Content;
                        Debug.Log("[ClaudeCode] Copied to clipboard.");
                    }) { text = "Copy" };
                    copyBtn.style.height = 16;
                    copyBtn.style.fontSize = 9;
                    copyBtn.style.alignSelf = Align.FlexEnd;
                    copyBtn.style.marginTop = 4;
                    block.Add(copyBtn);
                }
            }

            _chatContainer.Add(block);
        }

        private void ScrollToBottom()
        {
            _chatScroll?.schedule.Execute(() =>
            {
                if (_chatScroll != null)
                    _chatScroll.scrollOffset = new Vector2(0, float.MaxValue);
            }).ExecuteLater(50);
        }

        private void SetStatus(string text, Color color)
        {
            if (_statusLabel == null) return;
            _statusLabel.text = text;
            _statusLabel.style.color = color;
        }

        private Label MakeLabel(string text, int size, Color color, float marginLeft = 0)
        {
            var label = new Label(text);
            label.style.fontSize = size;
            label.style.color = color;
            label.style.marginLeft = marginLeft;
            label.style.marginRight = 4;
            return label;
        }
    }
}
