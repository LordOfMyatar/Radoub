using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Parley.Views.Helpers;
using Radoub.Formats.Logging;

namespace DialogEditor.Services
{
    /// <summary>
    /// Service for loading and displaying script content previews.
    /// Extracted from ScriptBrowserController for single responsibility.
    /// </summary>
    public class ScriptPreviewService
    {
        private readonly SafeControlFinder _controls;

        public ScriptPreviewService(SafeControlFinder controls)
        {
            _controls = controls ?? throw new ArgumentNullException(nameof(controls));
        }

        /// <summary>
        /// Loads script content preview asynchronously.
        /// </summary>
        public async Task LoadScriptPreviewAsync(string scriptName, bool isCondition)
        {
            if (string.IsNullOrWhiteSpace(scriptName))
            {
                ClearScriptPreview(isCondition);
                return;
            }

            try
            {
                var previewTextBox = isCondition
                    ? _controls.Get<TextBox>("ConditionalScriptPreviewTextBox")
                    : _controls.Get<TextBox>("ActionScriptPreviewTextBox");

                if (previewTextBox == null)
                {
                    UnifiedLogger.LogApplication(LogLevel.WARN,
                        $"LoadScriptPreviewAsync: Preview TextBox not found for {(isCondition ? "conditional" : "action")} script");
                    return;
                }

                previewTextBox.Text = "Loading...";

                var scriptContent = await ScriptService.Instance.GetScriptContentAsync(scriptName);

                if (!string.IsNullOrEmpty(scriptContent))
                {
                    previewTextBox.Text = scriptContent;
                    UnifiedLogger.LogApplication(LogLevel.DEBUG,
                        $"LoadScriptPreviewAsync: Loaded preview for {(isCondition ? "conditional" : "action")} script '{scriptName}'");
                }
                else
                {
                    previewTextBox.Text = $"// Script '{scriptName}.nss' not found or could not be loaded.\n" +
                                          "// This may be a compiled game resource (.ncs) without source available.\n" +
                                          "// Use nwnnsscomp to decompile .ncs files: github.com/niv/neverwinter.nim";
                    UnifiedLogger.LogApplication(LogLevel.WARN,
                        $"LoadScriptPreviewAsync: No content for script '{scriptName}'");
                }
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR,
                    $"LoadScriptPreviewAsync: Error loading preview for '{scriptName}': {ex.Message}");
                ClearScriptPreview(isCondition);
            }
        }

        /// <summary>
        /// Clears script preview content.
        /// </summary>
        public void ClearScriptPreview(bool isCondition)
        {
            var previewTextBox = isCondition
                ? _controls.Get<TextBox>("ConditionalScriptPreviewTextBox")
                : _controls.Get<TextBox>("ActionScriptPreviewTextBox");

            if (previewTextBox != null)
            {
                previewTextBox.Text = $"// {(isCondition ? "Conditional" : "Action")} script preview will appear here";
            }
        }
    }
}
