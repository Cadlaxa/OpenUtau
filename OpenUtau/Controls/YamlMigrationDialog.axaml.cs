using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using AvaloniaEdit.TextMate;
using OpenUtau.App.Controls;
using OpenUtau.App.Utils;
using OpenUtau.Core;
using OpenUtau.Plugin.Builtin;
using Serilog;
using TextMateSharp.Grammars;

namespace OpenUtau.App.Views {
    public partial class YamlMigrationDialog : Window {
        public enum DiffType { Added, Local, Modified }

        private readonly string targetFilePath = string.Empty;
        private readonly string oldContent = string.Empty;
        private readonly string newTemplateContent = string.Empty;
        private readonly string targetVersion = string.Empty;
        private string initialMergedText = string.Empty;

        private TextMate.Installation oldTextMate = null!;
        private TextMate.Installation newTextMate = null!;
        private TextMate.Installation resultTextMate = null!;

        private DiffLineRenderer oldDiffRenderer = null!;
        private DiffLineRenderer newDiffRenderer = null!;
        private DiffLineRenderer resultDiffRenderer = null!;

        private readonly DispatcherTimer validateTimer = null!;
        private bool hasErrors = false;

        public bool MigrationCompleted { get; private set; } = false;

        public YamlMigrationDialog() {
            InitializeComponent();
        }

        public YamlMigrationDialog(string filePath, string oldYaml, string templateYaml, string oldVersion, string newVersion) {
            InitializeComponent();
            targetFilePath = filePath;
            oldContent = oldYaml;
            newTemplateContent = templateYaml;
            targetVersion = newVersion;

            TitleBanner.Text = $"Migration Conflict: {Path.GetFileName(filePath)} ({oldVersion} ➔ {newVersion})";
            OldVersionLabel.Text = $"Current / Local (v{oldVersion})";
            NewVersionLabel.Text = $"Incoming / Template (v{newVersion})";

            try {
                SetupSyntaxHighlighting();
            } catch (Exception ex) {
                Log.Warning(ex, "[YamlMigrationDialog] Syntax highlighting setup skipped.");
            }

            OldEditor.Document = new TextDocument(oldContent);
            NewEditor.Document = new TextDocument(newTemplateContent);

            // Compute initial auto-merge
            try {
                initialMergedText = YamlMigrator.AutoMerge(oldContent, newTemplateContent, targetVersion);
            } catch (Exception ex) {
                Log.Warning(ex, "[YamlMigrationDialog] Initial AutoMerge failed; using incoming template.");
                initialMergedText = newTemplateContent;
            }

            ResultEditor.Document = new TextDocument(initialMergedText);

            // Setup Diff Background Renderers for all 3 Windows
            SetupDiffRenderers();

            validateTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            validateTimer.Tick += (s, e) => {
                validateTimer.Stop();
                ValidateResult();
            };

            ResultEditor.TextChanged += (s, e) => {
                UpdateRevertState();
                InvalidateDiffRenderers();
                validateTimer.Stop();
                validateTimer.Start();
            };

            // Resolution Quick Actions
            UseOldButton.Click += (s, e) => {
                ResultEditor.Document.Text = oldContent;
                UpdateRevertState();
                InvalidateDiffRenderers();
            };

            UseNewButton.Click += (s, e) => {
                ResultEditor.Document.Text = newTemplateContent;
                UpdateRevertState();
                InvalidateDiffRenderers();
            };

            AutoMergeButton.Click += (s, e) => {
                try {
                    ResultEditor.Document.Text = YamlMigrator.AutoMerge(oldContent, newTemplateContent, targetVersion);
                } catch {
                    ResultEditor.Document.Text = newTemplateContent;
                }
                UpdateRevertState();
                InvalidateDiffRenderers();
            };

            // Revert restores Result back to the initial auto-merged state
            RevertButton.Click += (s, e) => {
                ResultEditor.Document.Text = initialMergedText;
                UpdateRevertState();
                InvalidateDiffRenderers();
            };

            SaveButton.Click += (s, e) => ApplyMigration();

            UpdateRevertState();
            ValidateResult();
        }

        private void SetupSyntaxHighlighting() {
            var registry = new RegistryOptions(ThemeManager.IsDarkMode ? ThemeName.DarkPlus : ThemeName.LightPlus);
            var yamlLanguage = registry.GetLanguageByExtension(".yaml").Id;
            var scope = registry.GetScopeByLanguageId(yamlLanguage);

            oldTextMate = OldEditor.InstallTextMate(registry);
            oldTextMate.SetGrammar(scope);

            newTextMate = NewEditor.InstallTextMate(registry);
            newTextMate.SetGrammar(scope);

            resultTextMate = ResultEditor.InstallTextMate(registry);
            resultTextMate.SetGrammar(scope);
        }

        private void SetupDiffRenderers() {
            var oldLinesSet = ExtractNormalizedLines(oldContent);
            var newLinesSet = ExtractNormalizedLines(newTemplateContent);

            // Left Window: Highlight local customizations
            oldDiffRenderer = new DiffLineRenderer(OldEditor, lineNum => {
                string trimmed = GetLineTrimmed(OldEditor.Document, lineNum);
                if (IsIgnoredLine(trimmed)) return null;
                return !newLinesSet.Contains(trimmed) ? DiffType.Local : null;
            });
            OldEditor.TextArea.TextView.BackgroundRenderers.Add(oldDiffRenderer);

            // Right Window: Highlight incoming template additions
            newDiffRenderer = new DiffLineRenderer(NewEditor, lineNum => {
                string trimmed = GetLineTrimmed(NewEditor.Document, lineNum);
                if (IsIgnoredLine(trimmed)) return null;
                return !oldLinesSet.Contains(trimmed) ? DiffType.Added : null;
            });
            NewEditor.TextArea.TextView.BackgroundRenderers.Add(newDiffRenderer);

            // Bottom Window: Highlight added, local, or manually modified lines
            resultDiffRenderer = new DiffLineRenderer(ResultEditor, lineNum => {
                string trimmed = GetLineTrimmed(ResultEditor.Document, lineNum);
                if (IsIgnoredLine(trimmed)) return null;

                bool inOld = oldLinesSet.Contains(trimmed);
                bool inNew = newLinesSet.Contains(trimmed);

                if (inNew && !inOld) return DiffType.Added;    // Incoming addition
                if (inOld && !inNew) return DiffType.Local;    // Local customization
                if (!inOld && !inNew) return DiffType.Modified; // Manual edit / conflict edit
                return null;
            });
            ResultEditor.TextArea.TextView.BackgroundRenderers.Add(resultDiffRenderer);
        }

        private void InvalidateDiffRenderers() {
            ResultEditor.TextArea.TextView.InvalidateLayer(resultDiffRenderer.Layer);
            OldEditor.TextArea.TextView.InvalidateLayer(oldDiffRenderer.Layer);
            NewEditor.TextArea.TextView.InvalidateLayer(newDiffRenderer.Layer);
        }

        private void UpdateRevertState() {
            RevertButton.IsEnabled = ResultEditor.Text != initialMergedText;
        }

        private static HashSet<string> ExtractNormalizedLines(string text) {
            return new HashSet<string>(
                text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
                    .Select(l => l.Trim())
                    .Where(l => !IsIgnoredLine(l)),
                StringComparer.Ordinal
            );
        }

        private static bool IsIgnoredLine(string trimmed) {
            return string.IsNullOrEmpty(trimmed) || 
                   trimmed.StartsWith("#") || 
                   trimmed.StartsWith("version:", StringComparison.OrdinalIgnoreCase);
        }

        private static string GetLineTrimmed(TextDocument doc, int lineNum) {
            if (lineNum < 1 || lineNum > doc.LineCount) return string.Empty;
            var line = doc.GetLineByNumber(lineNum);
            return doc.GetText(line.Offset, line.Length).Trim();
        }

        private async void ValidateResult() {
            string text = ResultEditor.Text;
            var diagnostics = await Task.Run(() => YamlValidator.Validate(text, typeof(SyllableBasedPhonemizer.YAMLData)));

            hasErrors = diagnostics.Any(d => d.IsError);
            int errors = diagnostics.Count(d => d.IsError);
            int warnings = diagnostics.Count - errors;

            if (errors > 0) {
                ValidationSummary.Text = $"❌ {errors} Error(s) detected. Fix errors before saving.";
                SaveButton.IsEnabled = false;
            } else if (warnings > 0) {
                ValidationSummary.Text = $"⚠️ {warnings} Warning(s) detected (can be saved).";
                SaveButton.IsEnabled = true;
            } else {
                ValidationSummary.Text = "✅ YAML is valid and ready to save.";
                SaveButton.IsEnabled = true;
            }
        }

        private void ApplyMigration() {
            if (hasErrors) return;

            try {
                File.WriteAllText(targetFilePath, ResultEditor.Text, Encoding.UTF8);
                Log.Information($"[Migration] Successfully wrote migrated YAML to '{targetFilePath}'");

                MigrationCompleted = true;
                Close(true);
            } catch (Exception ex) {
                Log.Error(ex, $"Failed to commit migration to '{targetFilePath}'");
            }
        }

        /// <summary>
        /// Renders full-width translucent backgrounds and 3px left gutter accent bars.
        /// </summary>
        public class DiffLineRenderer : IBackgroundRenderer {
            private static readonly IBrush AddedBg = new SolidColorBrush(Color.FromArgb(0x33, 0x2E, 0xCC, 0x71));
            private static readonly IBrush AddedStripe = new SolidColorBrush(Color.FromRgb(0x2E, 0xCC, 0x71));

            private static readonly IBrush LocalBg = new SolidColorBrush(Color.FromArgb(0x33, 0x34, 0x98, 0xDB));
            private static readonly IBrush LocalStripe = new SolidColorBrush(Color.FromRgb(0x34, 0x98, 0xDB));

            private static readonly IBrush ModifiedBg = new SolidColorBrush(Color.FromArgb(0x38, 0xE6, 0x7E, 0x22));
            private static readonly IBrush ModifiedStripe = new SolidColorBrush(Color.FromRgb(0xE6, 0x7E, 0x22));

            private readonly TextEditor editor;
            private readonly Func<int, DiffType?> lineDiffProvider;

            public DiffLineRenderer(TextEditor editor, Func<int, DiffType?> lineDiffProvider) {
                this.editor = editor;
                this.lineDiffProvider = lineDiffProvider;
            }

            public KnownLayer Layer => KnownLayer.Background;

            public void Draw(TextView textView, DrawingContext drawingContext) {
                if (!textView.VisualLinesValid) return;

                foreach (var visualLine in textView.VisualLines) {
                    int lineNum = visualLine.FirstDocumentLine.LineNumber;
                    var diff = lineDiffProvider(lineNum);
                    if (diff == null) continue;

                    var (bg, stripe) = diff.Value switch {
                        DiffType.Added => (AddedBg, AddedStripe),
                        DiffType.Local => (LocalBg, LocalStripe),
                        _ => (ModifiedBg, ModifiedStripe)
                    };

                    var docLine = visualLine.FirstDocumentLine;
                    foreach (var rect in BackgroundGeometryBuilder.GetRectsForSegment(textView, docLine)) {
                        // Full line background tint
                        drawingContext.DrawRectangle(bg, null, new Rect(0, rect.Y, textView.Bounds.Width, rect.Height));
                        // 3px left accent bar
                        drawingContext.DrawRectangle(stripe, null, new Rect(0, rect.Y, 3, rect.Height));
                    }
                }
            }
        }
    }
}