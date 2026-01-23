using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Radoub.Formats.Tokens;

namespace Radoub.UI.Views
{
    /// <summary>
    /// Dialog for selecting and inserting NWN tokens.
    /// </summary>
    public partial class TokenSelectorWindow : Window
    {
        private UserColorConfig? _userColorConfig;

        /// <summary>
        /// The token string to insert. Null if cancelled.
        /// </summary>
        public string? SelectedToken { get; private set; }

        /// <summary>
        /// Initializes the token selector window.
        /// </summary>
        public TokenSelectorWindow()
        {
            InitializeComponent();
            LoadStandardTokens();
            LoadUserColorConfig();
        }

        private void LoadStandardTokens()
        {
            // Load all standard tokens into the listbox
            StandardTokenListBox.ItemsSource = TokenDefinitions.StandardTokens.ToList();
        }

        private void LoadUserColorConfig()
        {
            try
            {
                _userColorConfig = UserColorConfigLoader.LoadOrCreateDefault();
                LoadColorList();
            }
            catch (Exception)
            {
                // If config fails, create default
                _userColorConfig = UserColorConfig.CreateDefault();
                LoadColorList();
            }
        }

        private void LoadColorList()
        {
            if (_userColorConfig == null)
                return;

            var colors = new List<ColorListItem>();
            foreach (var kvp in _userColorConfig.Colors)
            {
                var hexColor = _userColorConfig.ColorHexValues.GetValueOrDefault(kvp.Key, "#FFFFFF");
                colors.Add(new ColorListItem
                {
                    Name = kvp.Key,
                    OpenToken = kvp.Value,
                    CloseToken = _userColorConfig.CloseToken,
                    HexColor = new SolidColorBrush(Color.Parse(hexColor))
                });
            }

            ColorListBox.ItemsSource = colors;
        }

        private void OnTabSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            // Guard against calls before UI is initialized
            if (TokenTabControl == null || TokenOutputTextBox == null)
                return;
            UpdateTokenOutput();
        }

        private void OnStandardTokenSelected(object? sender, SelectionChangedEventArgs e)
        {
            // Guard against calls before UI is initialized
            if (StandardTokenListBox == null || StandardExampleText == null || StandardExampleResult == null)
                return;

            var token = StandardTokenListBox.SelectedItem as string;
            if (token != null)
            {
                StandardExampleText.Text = $"Well hello little <{token}>.";
                StandardExampleResult.Text = $"Well hello little {GetExampleValue(token)}.";
            }
            UpdateTokenOutput();
        }

        private string GetExampleValue(string token)
        {
            // Return example values for preview
            return token switch
            {
                "FirstName" => "Elara",
                "LastName" => "Shadowmend",
                "FullName" => "Elara Shadowmend",
                "PlayerName" => "Player1",
                "Boy/Girl" or "boy/girl" => "Girl",
                "Brother/Sister" or "brother/sister" => "Sister",
                "He/She" or "he/she" => "She",
                "Him/Her" or "him/her" => "Her",
                "His/Her" or "his/her" => "Her",
                "His/Hers" or "his/hers" => "Hers",
                "Lad/Lass" or "lad/lass" => "Lass",
                "Lord/Lady" or "lord/lady" => "Lady",
                "Male/Female" or "male/female" => "Female",
                "Man/Woman" or "man/woman" => "Woman",
                "Master/Mistress" or "master/mistress" => "Mistress",
                "Mister/Missus" or "mister/missus" => "Missus",
                "Sir/Madam" or "sir/madam" => "Madam",
                "bitch/bastard" => "bitch",
                "Class" or "class" => "Rogue",
                "Race" or "race" => "Half-Elf",
                "Subrace" => "Moon Elf",
                "Deity" => "Selune",
                "Level" => "12",
                "Alignment" or "alignment" => "Chaotic Good",
                "Good/Evil" or "good/evil" => "Good",
                "Lawful/Chaotic" or "lawful/chaotic" => "Chaotic",
                "Law/Chaos" or "law/chaos" => "Chaos",
                "Day/Night" or "day/night" => "Night",
                "GameMonth" => "Mirtul",
                "GameTime" => "Evening",
                "GameYear" => "1372",
                "QuarterDay" or "quarterday" => "Evening",
                _ when token.StartsWith("CUSTOM") => $"[{token}]",
                _ => token
            };
        }

        private void OnHighlightTypeChanged(object? sender, RoutedEventArgs e)
        {
            // Guard against calls before UI is initialized
            if (ActionRadio == null || HighlightRadio == null)
                return;
            UpdateHighlightPreview();
            UpdateTokenOutput();
        }

        private void OnHighlightTextChanged(object? sender, TextChangedEventArgs e)
        {
            // Guard against calls before UI is initialized
            if (ActionTextBox == null)
                return;
            UpdateHighlightPreview();
            UpdateTokenOutput();
        }

        private void OnSkillCheckChanged(object? sender, SelectionChangedEventArgs e)
        {
            // Guard against calls before UI is initialized
            if (SkillCheckComboBox == null)
                return;
            UpdateHighlightPreview();
            UpdateTokenOutput();
        }

        private void UpdateHighlightPreview()
        {
            // Guard against calls before UI is initialized
            if (ActionRadio == null || HighlightRadio == null || ActionTextBox == null ||
                SkillCheckComboBox == null || HighlightPreviewRaw == null || HighlightPreviewFormatted == null)
                return;

            string type;
            string content;
            string color;

            if (ActionRadio.IsChecked == true)
            {
                type = "StartAction";
                content = string.IsNullOrWhiteSpace(ActionTextBox.Text)
                    ? "[Action text]"
                    : $"[{ActionTextBox.Text}]";
                color = "#00FF00";
            }
            else if (HighlightRadio.IsChecked == true)
            {
                type = "StartHighlight";
                content = string.IsNullOrWhiteSpace(ActionTextBox.Text)
                    ? "[Highlighted text]"
                    : $"[{ActionTextBox.Text}]";
                color = "#0080FF";
            }
            else // Check
            {
                type = "StartCheck";
                var skill = (SkillCheckComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Lore";
                content = $"[{skill}]";
                color = "#FF0000";
            }

            HighlightPreviewRaw.Text = $"<{type}>{content}</Start>";
            HighlightPreviewFormatted.Text = content;
            HighlightPreviewFormatted.Foreground = new SolidColorBrush(Color.Parse(color));
        }

        private void OnColorSelected(object? sender, SelectionChangedEventArgs e)
        {
            // Guard against calls before UI is initialized
            if (ColorListBox == null || ColorPreviewRaw == null || ColorPreviewSample == null)
                return;

            var item = ColorListBox.SelectedItem as ColorListItem;
            if (item != null)
            {
                ColorPreviewRaw.Text = $"{item.OpenToken}Your text here{item.CloseToken}";
                ColorPreviewSample.Text = "Your text here";
                ColorPreviewSample.Foreground = item.HexColor;
            }
            UpdateTokenOutput();
        }

        private void UpdateTokenOutput()
        {
            // Guard against calls before UI is initialized
            if (TokenTabControl == null || TokenOutputTextBox == null ||
                StandardTokenListBox == null || ActionRadio == null || HighlightRadio == null ||
                ActionTextBox == null || SkillCheckComboBox == null || ColorListBox == null)
                return;

            var tabIndex = TokenTabControl.SelectedIndex;

            switch (tabIndex)
            {
                case 0: // Standard
                    var standardToken = StandardTokenListBox.SelectedItem as string;
                    TokenOutputTextBox.Text = standardToken != null ? $"<{standardToken}>" : "";
                    break;

                case 1: // Highlight
                    string type;
                    string content;

                    if (ActionRadio.IsChecked == true)
                    {
                        type = "StartAction";
                        content = string.IsNullOrWhiteSpace(ActionTextBox.Text)
                            ? ""
                            : $"[{ActionTextBox.Text}]";
                    }
                    else if (HighlightRadio.IsChecked == true)
                    {
                        type = "StartHighlight";
                        content = string.IsNullOrWhiteSpace(ActionTextBox.Text)
                            ? ""
                            : $"[{ActionTextBox.Text}]";
                    }
                    else // Check
                    {
                        type = "StartCheck";
                        var skill = (SkillCheckComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
                        content = !string.IsNullOrEmpty(skill) ? $"[{skill}]" : "";
                    }

                    TokenOutputTextBox.Text = !string.IsNullOrEmpty(content)
                        ? $"<{type}>{content}</Start>"
                        : "";
                    break;

                case 2: // Custom Colors
                    var colorItem = ColorListBox.SelectedItem as ColorListItem;
                    if (colorItem != null)
                    {
                        // For custom colors, we insert open+close with cursor position in between
                        TokenOutputTextBox.Text = $"{colorItem.OpenToken}TEXT{colorItem.CloseToken}";
                    }
                    else
                    {
                        TokenOutputTextBox.Text = "";
                    }
                    break;
            }
        }

        private void OnTokenDoubleClicked(object? sender, RoutedEventArgs e)
        {
            OnOkClick(sender, e);
        }

        private void OnOkClick(object? sender, RoutedEventArgs e)
        {
            SelectedToken = TokenOutputTextBox.Text;
            if (!string.IsNullOrEmpty(SelectedToken))
            {
                Close(true);
            }
        }

        private void OnCancelClick(object? sender, RoutedEventArgs e)
        {
            SelectedToken = null;
            Close(false);
        }
    }

    /// <summary>
    /// Item for the color list display.
    /// </summary>
    internal class ColorListItem
    {
        public string Name { get; set; } = "";
        public string OpenToken { get; set; } = "";
        public string CloseToken { get; set; } = "";
        public IBrush HexColor { get; set; } = Brushes.White;
    }
}
