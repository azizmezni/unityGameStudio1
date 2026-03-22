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
    /// No Claude Code CLI needed.
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

            // === Top bar: provider + model ===
            var topBar = new VisualElement();
            topBar.style.flexDirection = FlexDirection.Row;
            topBar.style.alignItems = Align.Center;
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

            rootVisualElement.Add(topBar);

            // === Skill bar ===
            if (!string.IsNullOrEmpty(_activeSkillName))
            {
                var skillBar = new VisualElement();
                skillBar.style.flexDirection = FlexDirection.Row;
                skillBar.style.alignItems = Align.Center;
                skillBar.style.paddingLeft = 8;
                skillBar.style.paddingRight = 8;
                skillBar.style.paddingTop = 3;
                skillBar.style.paddingBottom = 3;
                skillBar.style.backgroundColor = new Color(0.12f, 0.18f, 0.28f);

                var skillLabel = new Label($"Skill: /{_activeSkillName}  — AI will follow this skill's instructions");
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

                rootVisualElement.Add(skillBar);
            }

            // === Chat area ===
            _chatScroll = new ScrollView(ScrollViewMode.Vertical);
            _chatScroll.style.flexGrow = 1;
            _chatScroll.style.paddingLeft = 8;
            _chatScroll.style.paddingRight = 8;
            _chatScroll.style.paddingTop = 8;
            _chatContainer = _chatScroll.contentContainer;
            rootVisualElement.Add(_chatScroll);

            // Re-render history
            foreach (var entry in _entries)
                RenderEntry(entry);

            // === Input area ===
            var inputRow = new VisualElement();
            inputRow.style.flexDirection = FlexDirection.Row;
            inputRow.style.paddingLeft = 8;
            inputRow.style.paddingRight = 8;
            inputRow.style.paddingTop = 6;
            inputRow.style.paddingBottom = 6;
            inputRow.style.backgroundColor = new Color(0.10f, 0.10f, 0.13f);
            inputRow.style.borderTopWidth = 1;
            inputRow.style.borderTopColor = new Color(0.25f, 0.25f, 0.30f);

            _inputField = new TextField();
            _inputField.style.flexGrow = 1;
            _inputField.style.fontSize = 12;
            _inputField.multiline = true;
            _inputField.style.maxHeight = 100;
            _inputField.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Return && !evt.shiftKey)
                {
                    evt.StopPropagation();
                    evt.PreventDefault();
                    SendMessage();
                }
            });
            inputRow.Add(_inputField);

            _sendBtn = new Button(SendMessage) { text = "Send" };
            _sendBtn.style.height = 28;
            _sendBtn.style.marginLeft = 6;
            _sendBtn.style.fontSize = 11;
            inputRow.Add(_sendBtn);

            _stopBtn = new Button(StopAgent) { text = "Stop" };
            _stopBtn.style.height = 28;
            _stopBtn.style.marginLeft = 4;
            _stopBtn.style.fontSize = 11;
            _stopBtn.style.display = DisplayStyle.None;
            inputRow.Add(_stopBtn);

            rootVisualElement.Add(inputRow);

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
                $"/{skillName} is active. The agent will follow this skill's instructions and can read/write files, run commands, and create scripts.");
            BuildUI();
        }

        private void SendMessage()
        {
            if (_agentLoop != null && _agentLoop.IsRunning) return;

            var msg = _inputField?.value?.Trim();
            if (string.IsNullOrEmpty(msg)) return;
            _inputField.value = "";

            AddEntry(EntryType.User, "You", msg);

            // Build system prompt with tool instructions
            var systemPrompt = BuildSystemPrompt();

            // Create or continue agent loop
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

                // Truncate long results in display
                var displayResult = result;
                if (displayResult.Length > 2000)
                    displayResult = displayResult.Substring(0, 2000) + "\n... (truncated in display)";

                AddEntry(EntryType.Tool, title, displayResult);

                // Trigger Unity asset refresh if files were written
                if (call.ToolName == "write_file" || call.ToolName == "edit_file")
                {
                    EditorApplication.delayCall += () => AssetDatabase.Refresh();
                }
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

            // Tool instructions (always included)
            parts.Add(ToolExecutor.BuildToolSystemPrompt());

            // Skill prompt
            if (!string.IsNullOrEmpty(_activeSkillPrompt))
                parts.Add($"=== Active Skill Instructions ===\n{_activeSkillPrompt}");

            // Project context
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

            // For tools, make it collapsible
            if (entry.Type == EntryType.Tool)
            {
                var foldout = new Foldout();
                foldout.text = entry.Title;
                foldout.value = false; // Collapsed by default
                foldout.style.color = new Color(0.5f, 0.85f, 0.55f);

                var headerLabel = foldout.Q<Toggle>().Q<Label>();
                if (headerLabel != null)
                {
                    headerLabel.style.fontSize = 10;
                    headerLabel.style.color = new Color(0.5f, 0.85f, 0.55f);
                }

                var contentLabel = new Label(entry.Content);
                contentLabel.style.fontSize = 10;
                contentLabel.style.color = new Color(0.65f, 0.75f, 0.65f);
                contentLabel.style.whiteSpace = WhiteSpace.Normal;
                contentLabel.style.overflow = Overflow.Hidden;
                foldout.Add(contentLabel);

                block.Add(foldout);
            }
            else
            {
                // Title
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

                // Content
                var contentLabel = new Label(entry.Content);
                contentLabel.style.fontSize = 11;
                contentLabel.style.color = Color.white;
                contentLabel.style.whiteSpace = WhiteSpace.Normal;
                contentLabel.enableRichText = false;
                block.Add(contentLabel);
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
