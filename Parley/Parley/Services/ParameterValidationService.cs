using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using Radoub.Formats.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DialogEditor.Services
{
    /// <summary>
    /// Handles duplicate key validation and visual feedback for script parameter UI.
    /// Extracted from ScriptParameterUIManager.cs for maintainability.
    /// Issue #287: Duplicate key validation
    /// Issue #141: Theme-aware error/success colors for colorblind accessibility
    /// </summary>
    public class ParameterValidationService
    {
        private readonly Action<string> _setStatusMessage;

        public ParameterValidationService(Action<string> setStatusMessage)
        {
            _setStatusMessage = setStatusMessage ?? throw new ArgumentNullException(nameof(setStatusMessage));
        }

        /// <summary>
        /// Validates that a key is not duplicated in the parameter panel.
        /// Issue #287: Prevents duplicate keys which would cause data loss.
        /// Red border stays until the duplicate is corrected.
        /// Issue #141: Fixed to clear ALL red borders when duplicate is resolved.
        /// </summary>
        public void ValidateDuplicateKeys(StackPanel parent, TextBox currentKeyTextBox, bool isCondition)
        {
            string currentKey = currentKeyTextBox.Text?.Trim() ?? "";

            // If key is empty, clear any warning state and revalidate all to clear orphaned red borders
            if (string.IsNullOrWhiteSpace(currentKey))
            {
                ClearDuplicateWarning(currentKeyTextBox);
                RevalidateAllKeys(parent, isCondition);
                return;
            }

            int duplicateCount = 0;
            var allKeyTextBoxes = new List<TextBox>();

            foreach (var child in parent.Children)
            {
                if (child is Grid paramGrid)
                {
                    var textBoxes = paramGrid.Children.OfType<TextBox>().ToList();
                    if (textBoxes.Count >= 1)
                    {
                        var keyTextBox = textBoxes[0];
                        string key = keyTextBox.Text?.Trim() ?? "";

                        if (key.Equals(currentKey, StringComparison.Ordinal))
                        {
                            duplicateCount++;
                            allKeyTextBoxes.Add(keyTextBox);
                        }
                    }
                }
            }

            if (duplicateCount > 1)
            {
                // Show warning - duplicate key detected (use theme error color for accessibility)
                var errorBrush = GetErrorBrush();
                currentKeyTextBox.BorderBrush = errorBrush;
                currentKeyTextBox.BorderThickness = new Thickness(2);

                // Also mark all other textboxes with the same key
                foreach (var tb in allKeyTextBoxes)
                {
                    tb.BorderBrush = errorBrush;
                    tb.BorderThickness = new Thickness(2);
                }

                _setStatusMessage($"⚠️ Duplicate key '{currentKey}' - only one value will be saved!");

                UnifiedLogger.LogApplication(LogLevel.WARN,
                    $"Duplicate key detected: '{currentKey}' appears {duplicateCount} times in {(isCondition ? "condition" : "action")} parameters");
            }
            else
            {
                // No duplicate for current key - clear warning on this textbox
                ClearDuplicateWarning(currentKeyTextBox);

                // Issue #141: Revalidate ALL keys to clear orphaned red borders
                // When a duplicate is resolved by editing one key, the other key's
                // textbox still has a red border that needs to be cleared
                RevalidateAllKeys(parent, isCondition);
            }
        }

        /// <summary>
        /// Revalidates all key textboxes in the panel to clear orphaned red borders.
        /// Issue #141: Called when a key changes to ensure previously-duplicate keys get cleared.
        /// </summary>
        public void RevalidateAllKeys(StackPanel parent, bool isCondition)
        {
            // Build a count of each key to find actual duplicates
            var keyCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            var keyTextBoxes = new Dictionary<string, List<TextBox>>(StringComparer.Ordinal);

            foreach (var child in parent.Children)
            {
                if (child is Grid paramGrid)
                {
                    var textBoxes = paramGrid.Children.OfType<TextBox>().ToList();
                    if (textBoxes.Count >= 1)
                    {
                        var keyTextBox = textBoxes[0];
                        string key = keyTextBox.Text?.Trim() ?? "";

                        if (!string.IsNullOrWhiteSpace(key))
                        {
                            if (!keyCounts.ContainsKey(key))
                            {
                                keyCounts[key] = 0;
                                keyTextBoxes[key] = new List<TextBox>();
                            }
                            keyCounts[key]++;
                            keyTextBoxes[key].Add(keyTextBox);
                        }
                        else
                        {
                            // Empty key - clear any red border
                            ClearDuplicateWarning(keyTextBox);
                        }
                    }
                }
            }

            // Now update visual state for each key
            // Get theme error brush once for efficiency
            var errorBrush = GetErrorBrush();

            foreach (var kvp in keyCounts)
            {
                string key = kvp.Key;
                int count = kvp.Value;

                if (count > 1)
                {
                    // Still a duplicate - use theme error color
                    foreach (var tb in keyTextBoxes[key])
                    {
                        tb.BorderBrush = errorBrush;
                        tb.BorderThickness = new Thickness(2);
                    }
                }
                else
                {
                    // Not a duplicate (anymore) - clear error border
                    foreach (var tb in keyTextBoxes[key])
                    {
                        ClearDuplicateWarning(tb);
                    }
                }
            }
        }

        /// <summary>
        /// Gets the theme-aware error brush for validation errors.
        /// Falls back to red if theme error color is not available.
        /// Issue #141: Uses theme colors for colorblind accessibility.
        /// </summary>
        public IBrush GetErrorBrush()
        {
            var app = Application.Current;
            if (app?.Resources.TryGetResource("ThemeError", ThemeVariant.Default, out var errorBrush) == true
                && errorBrush is IBrush brush)
            {
                return brush;
            }
            // Fallback to standard red
            return Brushes.Red;
        }

        /// <summary>
        /// Gets the theme-aware success brush for validation success feedback.
        /// Falls back to green if theme success color is not available.
        /// Issue #141: Uses theme colors for colorblind accessibility.
        /// </summary>
        public IBrush GetSuccessBrush()
        {
            var app = Application.Current;
            if (app?.Resources.TryGetResource("ThemeSuccess", ThemeVariant.Default, out var successBrush) == true
                && successBrush is IBrush brush)
            {
                return brush;
            }
            // Fallback to standard green
            return Brushes.LightGreen;
        }

        /// <summary>
        /// Clears the duplicate key warning visual state from a TextBox.
        /// </summary>
        public void ClearDuplicateWarning(TextBox textBox)
        {
            // Only clear if currently showing error border (duplicate warning)
            // Check for both theme error brush and fallback red
            if (textBox.BorderThickness.Top >= 2)
            {
                textBox.BorderBrush = null; // Reset to default theme
                textBox.BorderThickness = new Thickness(1);
            }
        }

        /// <summary>
        /// Issue #289: Checks if a parameter panel has duplicate keys.
        /// Called by MainWindow before saving to prevent data corruption.
        /// </summary>
        public bool HasDuplicateKeys(StackPanel panel)
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);

            foreach (var child in panel.Children)
            {
                if (child is Grid paramGrid)
                {
                    var textBoxes = paramGrid.Children.OfType<TextBox>().ToList();
                    if (textBoxes.Count >= 1)
                    {
                        string key = textBoxes[0].Text?.Trim() ?? "";
                        if (!string.IsNullOrWhiteSpace(key))
                        {
                            if (!keys.Add(key))
                            {
                                // Duplicate found
                                return true;
                            }
                        }
                    }
                }
            }
            return false;
        }
    }
}
