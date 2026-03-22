using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClaudeCodeGameStudios.Data;
using ClaudeCodeGameStudios.Utilities;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ClaudeCodeGameStudios.Tabs
{
    public class DesignDocBrowserTab
    {
        private readonly ClaudeProjectContext _ctx;

        public DesignDocBrowserTab(ClaudeProjectContext ctx)
        {
            _ctx = ctx;
        }

        public VisualElement Build()
        {
            var root = new ScrollView(ScrollViewMode.Vertical);
            root.style.paddingLeft = 12;
            root.style.paddingRight = 12;
            root.style.paddingTop = 8;

            var designDir = PathResolver.DesignDir;

            if (!Directory.Exists(designDir) || _ctx.DesignDocPaths.Count == 0)
            {
                var placeholder = new VisualElement();
                placeholder.style.alignItems = Align.Center;
                placeholder.style.paddingTop = 40;

                var title = new Label("No Design Documents");
                title.style.fontSize = 16;
                title.style.unityFontStyleAndWeight = FontStyle.Bold;
                title.style.color = new Color(0.5f, 0.5f, 0.5f);
                title.style.marginBottom = 8;
                placeholder.Add(title);

                var info = new Label(
                    "Design documents live in the design/ directory.\n" +
                    "Use /brainstorm to create a game concept,\n" +
                    "then /design-system to author GDD sections.");
                info.style.fontSize = 11;
                info.style.color = new Color(0.45f, 0.45f, 0.45f);
                info.style.unityTextAlign = TextAnchor.MiddleCenter;
                info.style.whiteSpace = WhiteSpace.Normal;
                placeholder.Add(info);

                root.Add(placeholder);
                return root;
            }

            var summary = new Label($"{_ctx.DesignDocPaths.Count} design documents");
            summary.style.fontSize = 11;
            summary.style.color = new Color(0.6f, 0.6f, 0.6f);
            summary.style.marginBottom = 8;
            root.Add(summary);

            // Build tree structure
            var tree = BuildDirectoryTree(designDir);
            RenderTree(root, tree, 0);

            return root;
        }

        private class TreeNode
        {
            public string Name;
            public string FullPath;
            public bool IsDirectory;
            public List<TreeNode> Children = new();
        }

        private TreeNode BuildDirectoryTree(string rootPath)
        {
            var node = new TreeNode
            {
                Name = Path.GetFileName(rootPath),
                FullPath = rootPath.Replace('\\', '/'),
                IsDirectory = true
            };

            if (Directory.Exists(rootPath))
            {
                foreach (var dir in Directory.GetDirectories(rootPath).OrderBy(d => d))
                {
                    node.Children.Add(BuildDirectoryTree(dir));
                }
                foreach (var file in Directory.GetFiles(rootPath, "*.md").OrderBy(f => f))
                {
                    node.Children.Add(new TreeNode
                    {
                        Name = Path.GetFileName(file),
                        FullPath = file.Replace('\\', '/'),
                        IsDirectory = false
                    });
                }
            }

            return node;
        }

        private void RenderTree(VisualElement parent, TreeNode node, int depth)
        {
            if (node.IsDirectory && node.Children.Count > 0)
            {
                var foldout = new Foldout();
                foldout.text = node.Name + "/";
                foldout.value = depth < 2;
                foldout.style.marginLeft = depth > 0 ? 8 : 0;

                var headerLabel = foldout.Q<Toggle>().Q<Label>();
                if (headerLabel != null)
                {
                    headerLabel.style.color = new Color(0.9f, 0.8f, 0.3f);
                    headerLabel.style.fontSize = 12;
                }

                foreach (var child in node.Children)
                {
                    RenderTree(foldout, child, depth + 1);
                }

                parent.Add(foldout);
            }
            else if (!node.IsDirectory)
            {
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
                row.style.paddingLeft = 16;
                row.style.paddingTop = 2;
                row.style.paddingBottom = 2;

                var fileLabel = new Label(node.Name);
                fileLabel.style.fontSize = 11;
                fileLabel.style.color = new Color(0.75f, 0.75f, 0.8f);
                fileLabel.style.flexGrow = 1;
                row.Add(fileLabel);

                // File info
                if (File.Exists(node.FullPath))
                {
                    var info = new FileInfo(node.FullPath);
                    var sizeLabel = new Label($"{info.Length / 1024f:F1} KB");
                    sizeLabel.style.fontSize = 9;
                    sizeLabel.style.color = new Color(0.5f, 0.5f, 0.5f);
                    sizeLabel.style.marginRight = 8;
                    row.Add(sizeLabel);

                    var dateLabel = new Label(info.LastWriteTime.ToString("MM/dd HH:mm"));
                    dateLabel.style.fontSize = 9;
                    dateLabel.style.color = new Color(0.5f, 0.5f, 0.5f);
                    dateLabel.style.marginRight = 8;
                    row.Add(dateLabel);
                }

                var openBtn = new Button(() =>
                {
                    // Open in default text editor
                    EditorUtility.OpenWithDefaultApp(node.FullPath);
                }) { text = "Open" };
                openBtn.style.height = 18;
                openBtn.style.fontSize = 9;
                row.Add(openBtn);

                // Make whole row clickable
                row.RegisterCallback<ClickEvent>(evt =>
                {
                    if (evt.clickCount == 2)
                        EditorUtility.OpenWithDefaultApp(node.FullPath);
                });

                parent.Add(row);
            }
        }
    }
}
