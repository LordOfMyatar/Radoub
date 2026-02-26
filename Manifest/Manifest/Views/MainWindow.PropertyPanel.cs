using Avalonia.Controls;
using Avalonia.Interactivity;
using Manifest.Services;
using Radoub.Formats.Common;
using Radoub.Formats.Gff;
using Radoub.Formats.Logging;
using Radoub.Formats.Tokens;
using System.Collections.Generic;
using System.Linq;
using Radoub.UI.Views;

namespace Manifest.Views;

/// <summary>
/// MainWindow partial: Property panel display, language selection, and token preview.
/// </summary>
public partial class MainWindow
{
    #region Property Panel

    private bool _isUpdatingPanel = false;

    private void UpdatePropertyPanel()
    {
        _isUpdatingPanel = true;
        try
        {
            CategoryProperties.IsVisible = false;
            EntryProperties.IsVisible = false;
            NoSelectionText.IsVisible = true;

            if (_selectedItem is CategoryTreeItem catItem)
            {
                CategoryProperties.IsVisible = true;
                NoSelectionText.IsVisible = false;

                CategoryTagBox.Text = catItem.Category.Tag;
                CategoryStrRefBox.Text = FormatStrRef(catItem.Category.Name.StrRef);

                // Update source label and language info
                var nameInfo = TlkService.Instance.GetLocStringInfo(catItem.Category.Name);
                CategorySourceLabel.Text = nameInfo.SourceDescription;
                PopulateLanguageComboBox(CategoryLanguageBox, catItem.Category.Name);

                // Display text for current language
                CategoryNameBox.Text = TlkService.Instance.ResolveLocString(catItem.Category.Name, _currentViewLanguage);

                SelectPriorityItem(catItem.Category.Priority);
                CategoryXPBox.Value = catItem.Category.XP;
                CategoryCommentBox.Text = catItem.Category.Comment;

                // Wire up change handlers
                WireCategoryHandlers();
            }
            else if (_selectedItem is EntryTreeItem entItem)
            {
                EntryProperties.IsVisible = true;
                NoSelectionText.IsVisible = false;

                EntryIdBox.Value = entItem.Entry.ID;
                EntryEndBox.IsChecked = entItem.Entry.End;
                EntryStrRefBox.Text = FormatStrRef(entItem.Entry.Text.StrRef);

                // Update source label and language info
                var textInfo = TlkService.Instance.GetLocStringInfo(entItem.Entry.Text);
                EntrySourceLabel.Text = textInfo.SourceDescription;
                PopulateLanguageComboBox(EntryLanguageBox, entItem.Entry.Text);

                // Display text for current language
                EntryTextBox.Text = TlkService.Instance.ResolveLocString(entItem.Entry.Text, _currentViewLanguage);

                // Update token preview
                UpdateTokenPreview();

                // Wire up change handlers
                WireEntryHandlers();
            }
        }
        finally
        {
            _isUpdatingPanel = false;
        }
    }

    private void PopulateLanguageComboBox(ComboBox comboBox, CExoLocString locString)
    {
        comboBox.Items.Clear();

        // Get all available languages for this string
        var translations = TlkService.Instance.GetAllTranslations(locString);
        var availableTlkLanguages = TlkService.Instance.GetAvailableLanguages();

        // Determine which languages to show
        var languagesToShow = new HashSet<Language>();

        // Add embedded languages
        foreach (var (combinedId, _) in locString.LocalizedStrings)
        {
            languagesToShow.Add(LanguageHelper.GetLanguage(combinedId));
        }

        // Add TLK languages if there's a valid StrRef
        if (LanguageHelper.IsValidStrRef(locString.StrRef))
        {
            foreach (var lang in availableTlkLanguages)
            {
                languagesToShow.Add(lang);
            }
        }

        // Always show English
        languagesToShow.Add(Language.English);

        // Sort and add to combo box
        foreach (var lang in languagesToShow.OrderBy(l => (int)l))
        {
            var displayName = LanguageHelper.GetDisplayName(lang);
            var hasTranslation = translations.ContainsKey(lang);
            var item = new ComboBoxItem
            {
                Content = hasTranslation ? displayName : $"{displayName} (no text)",
                Tag = lang
            };
            comboBox.Items.Add(item);

            if (lang == _currentViewLanguage)
            {
                comboBox.SelectedItem = item;
            }
        }

        // If current language not in list, select first
        if (comboBox.SelectedItem == null && comboBox.Items.Count > 0)
        {
            comboBox.SelectedIndex = 0;
        }
    }

    private static string FormatStrRef(uint strRef)
    {
        if (strRef == 0xFFFFFFFF)
            return "(none)";
        return $"{strRef} (0x{strRef:X8})";
    }

    private void SelectPriorityItem(uint priority)
    {
        foreach (var obj in CategoryPriorityBox.Items)
        {
            if (obj is ComboBoxItem item && item.Tag is string tagStr &&
                uint.TryParse(tagStr, out var tagVal) && tagVal == priority)
            {
                CategoryPriorityBox.SelectedItem = item;
                return;
            }
        }
        // Default to Medium (2) if not found
        CategoryPriorityBox.SelectedIndex = 2;
    }

    private void WireCategoryHandlers()
    {
        // Remove old handlers to prevent duplicates
        CategoryNameBox.LostFocus -= OnCategoryNameChanged;
        CategoryTagBox.LostFocus -= OnCategoryTagChanged;
        CategoryPriorityBox.SelectionChanged -= OnCategoryPriorityChanged;
        CategoryXPBox.ValueChanged -= OnCategoryXPChanged;
        CategoryCommentBox.LostFocus -= OnCategoryCommentChanged;

        // Add handlers
        CategoryNameBox.LostFocus += OnCategoryNameChanged;
        CategoryTagBox.LostFocus += OnCategoryTagChanged;
        CategoryPriorityBox.SelectionChanged += OnCategoryPriorityChanged;
        CategoryXPBox.ValueChanged += OnCategoryXPChanged;
        CategoryCommentBox.LostFocus += OnCategoryCommentChanged;
    }

    private void WireEntryHandlers()
    {
        // Remove old handlers to prevent duplicates
        EntryIdBox.ValueChanged -= OnEntryIdChanged;
        EntryEndBox.IsCheckedChanged -= OnEntryEndChanged;
        EntryTextBox.LostFocus -= OnEntryTextChanged;
        EntryTextBox.TextChanged -= OnEntryTextPreviewChanged;

        // Add handlers
        EntryIdBox.ValueChanged += OnEntryIdChanged;
        EntryEndBox.IsCheckedChanged += OnEntryEndChanged;
        EntryTextBox.LostFocus += OnEntryTextChanged;
        EntryTextBox.TextChanged += OnEntryTextPreviewChanged;
    }

    private void OnEntryTextPreviewChanged(object? sender, TextChangedEventArgs e)
    {
        // Update the token preview as the user types
        UpdateTokenPreview();
    }

    private void OnCategoryNameChanged(object? sender, RoutedEventArgs e)
    {
        if (_isUpdatingPanel || _selectedItem is not CategoryTreeItem catItem) return;

        var newName = CategoryNameBox.Text ?? "";
        if (catItem.Category.Name.GetDefault() != newName)
        {
            catItem.Category.Name.SetString(0, newName);
            MarkDirty();
            UpdateTreeItemHeader(catItem);
            UnifiedLogger.LogJournal(LogLevel.DEBUG, $"Category name changed to: {newName}");
        }
    }

    private void OnCategoryTagChanged(object? sender, RoutedEventArgs e)
    {
        if (_isUpdatingPanel || _selectedItem is not CategoryTreeItem catItem) return;

        var newTag = CategoryTagBox.Text ?? "";
        if (catItem.Category.Tag != newTag)
        {
            catItem.Category.Tag = newTag;
            MarkDirty();
            UpdateTreeItemHeader(catItem);
            UnifiedLogger.LogJournal(LogLevel.DEBUG, $"Category tag changed to: {newTag}");
        }
    }

    private void OnCategoryPriorityChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingPanel || _selectedItem is not CategoryTreeItem catItem) return;

        if (CategoryPriorityBox.SelectedItem is ComboBoxItem item &&
            item.Tag is string tagStr && uint.TryParse(tagStr, out var newPriority))
        {
            if (catItem.Category.Priority != newPriority)
            {
                catItem.Category.Priority = newPriority;
                MarkDirty();
                UnifiedLogger.LogJournal(LogLevel.DEBUG, $"Category priority changed to: {newPriority}");
            }
        }
    }

    private void OnCategoryXPChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (_isUpdatingPanel || _selectedItem is not CategoryTreeItem catItem) return;

        var newXP = (uint)(CategoryXPBox.Value ?? 0);
        if (catItem.Category.XP != newXP)
        {
            catItem.Category.XP = newXP;
            MarkDirty();
            UnifiedLogger.LogJournal(LogLevel.DEBUG, $"Category XP changed to: {newXP}");
        }
    }

    private void OnCategoryCommentChanged(object? sender, RoutedEventArgs e)
    {
        if (_isUpdatingPanel || _selectedItem is not CategoryTreeItem catItem) return;

        var newComment = CategoryCommentBox.Text ?? "";
        if (catItem.Category.Comment != newComment)
        {
            catItem.Category.Comment = newComment;
            MarkDirty();
            UnifiedLogger.LogJournal(LogLevel.DEBUG, "Category comment changed");
        }
    }

    private void OnEntryIdChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (_isUpdatingPanel || _selectedItem is not EntryTreeItem entItem) return;

        var newId = (uint)(EntryIdBox.Value ?? 0);
        if (entItem.Entry.ID != newId)
        {
            entItem.Entry.ID = newId;
            MarkDirty();
            UpdateTreeItemHeader(entItem);
            UnifiedLogger.LogJournal(LogLevel.DEBUG, $"Entry ID changed to: {newId}");
        }
    }

    private void OnEntryEndChanged(object? sender, RoutedEventArgs e)
    {
        if (_isUpdatingPanel || _selectedItem is not EntryTreeItem entItem) return;

        var newEnd = EntryEndBox.IsChecked ?? false;
        if (entItem.Entry.End != newEnd)
        {
            entItem.Entry.End = newEnd;
            MarkDirty();
            UpdateTreeItemHeader(entItem);
            UnifiedLogger.LogJournal(LogLevel.DEBUG, $"Entry End changed to: {newEnd}");
        }
    }

    private void OnEntryTextChanged(object? sender, RoutedEventArgs e)
    {
        if (_isUpdatingPanel || _selectedItem is not EntryTreeItem entItem) return;

        var newText = EntryTextBox.Text ?? "";
        var oldText = entItem.Entry.Text.GetDefault();
        if (oldText != newText)
        {
            entItem.Entry.Text.SetString(0, newText);
            MarkDirty();
            UpdateTreeItemHeader(entItem);
            UnifiedLogger.LogJournal(LogLevel.DEBUG, "Entry text changed");
        }
    }

    private void UpdateTreeItemHeader(object item)
    {
        // Find and update the tree item header
        foreach (var treeItem in JournalTree.Items.OfType<TreeViewItem>())
        {
            if (treeItem.Tag == item)
            {
                if (item is CategoryTreeItem catItem)
                    treeItem.Header = catItem.DisplayName;
                return;
            }

            // Check children for entry items
            foreach (var childItem in treeItem.Items.OfType<TreeViewItem>())
            {
                if (childItem.Tag == item && item is EntryTreeItem entItem)
                {
                    childItem.Header = entItem.DisplayName;
                    return;
                }
            }
        }
    }

    #endregion

    #region Language Selection

    private void OnCategoryLanguageChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingPanel) return;

        if (CategoryLanguageBox.SelectedItem is ComboBoxItem item && item.Tag is Language lang)
        {
            _currentViewLanguage = lang;
            if (_selectedItem is CategoryTreeItem catItem)
            {
                CategoryNameBox.Text = TlkService.Instance.ResolveLocString(catItem.Category.Name, lang);
            }
        }
    }

    private void OnEntryLanguageChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingPanel) return;

        if (EntryLanguageBox.SelectedItem is ComboBoxItem item && item.Tag is Language lang)
        {
            _currentViewLanguage = lang;
            if (_selectedItem is EntryTreeItem entItem)
            {
                EntryTextBox.Text = TlkService.Instance.ResolveLocString(entItem.Entry.Text, lang);
            }
        }
    }

    private void OnViewCategoryLanguages(object? sender, RoutedEventArgs e)
    {
        if (_selectedItem is CategoryTreeItem catItem)
        {
            ShowAllLanguagesDialog("Category Name Translations", catItem.Category.Name);
        }
    }

    private void OnViewEntryLanguages(object? sender, RoutedEventArgs e)
    {
        if (_selectedItem is EntryTreeItem entItem)
        {
            ShowAllLanguagesDialog("Entry Text Translations", entItem.Entry.Text);
        }
    }

    private void ShowAllLanguagesDialog(string title, CExoLocString locString)
    {
        var translations = TlkService.Instance.GetAllTranslations(locString);
        var info = TlkService.Instance.GetLocStringInfo(locString);

        var dialog = new Avalonia.Controls.Window
        {
            Title = title,
            Width = 500,
            Height = 400,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = true
        };

        var mainPanel = new DockPanel { Margin = new Avalonia.Thickness(15) };

        // Header with source info
        var headerPanel = new StackPanel { Margin = new Avalonia.Thickness(0, 0, 0, 10) };
        headerPanel.Children.Add(new TextBlock
        {
            Text = $"Source: {info.SourceDescription}",
            FontWeight = Avalonia.Media.FontWeight.SemiBold
        });
        if (info.HasStrRef)
        {
            headerPanel.Children.Add(new TextBlock
            {
                Text = $"StrRef: {info.StrRef}",
                Foreground = Avalonia.Media.Brushes.Gray
            });
        }
        DockPanel.SetDock(headerPanel, Avalonia.Controls.Dock.Top);
        mainPanel.Children.Add(headerPanel);

        // Language list
        var scrollViewer = new ScrollViewer();
        var listPanel = new StackPanel { Spacing = 10 };

        if (translations.Count == 0)
        {
            listPanel.Children.Add(new TextBlock
            {
                Text = "(No translations available)",
                FontStyle = Avalonia.Media.FontStyle.Italic,
                Foreground = Avalonia.Media.Brushes.Gray
            });
        }
        else
        {
            foreach (var (lang, text) in translations.OrderBy(t => (int)t.Key))
            {
                var langPanel = new StackPanel { Spacing = 3 };
                langPanel.Children.Add(new TextBlock
                {
                    Text = LanguageHelper.GetDisplayName(lang),
                    FontWeight = Avalonia.Media.FontWeight.SemiBold
                });
                langPanel.Children.Add(new TextBox
                {
                    Text = text,
                    IsReadOnly = true,
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                    MaxHeight = 100
                });
                listPanel.Children.Add(langPanel);
            }
        }

        scrollViewer.Content = listPanel;
        mainPanel.Children.Add(scrollViewer);

        // Close button
        var closeButton = new Button
        {
            Content = "Close",
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Margin = new Avalonia.Thickness(0, 10, 0, 0)
        };
        closeButton.Click += (s, e) => dialog.Close();
        DockPanel.SetDock(closeButton, Avalonia.Controls.Dock.Bottom);
        mainPanel.Children.Add(closeButton);

        dialog.Content = mainPanel;
        dialog.Show(this);  // Non-modal per guidelines
    }

    #endregion

    #region Token Preview

    private async void OnInsertTokenClick(object? sender, RoutedEventArgs e)
    {
        // Save current selection in the textbox
        var caretIndex = EntryTextBox.CaretIndex;
        var savedText = EntryTextBox.Text ?? "";

        var tokenWindow = new TokenSelectorWindow();
        var result = await tokenWindow.ShowDialog<bool>(this);

        if (result && !string.IsNullOrEmpty(tokenWindow.SelectedToken))
        {
            // Insert token at caret position
            var newText = savedText.Insert(caretIndex, tokenWindow.SelectedToken);
            EntryTextBox.Text = newText;

            // Position caret after inserted token
            EntryTextBox.CaretIndex = caretIndex + tokenWindow.SelectedToken.Length;
            EntryTextBox.Focus();

            // Update the token preview
            UpdateTokenPreview();
        }
    }

    private void UpdateTokenPreview()
    {
        // Load user color config BEFORE setting text
        // This ensures the parser is configured before processing tokens
        UserColorConfig? config = null;
        try
        {
            config = UserColorConfigLoader.Load();
        }
        catch
        {
            // Ignore config loading errors
        }

        // Always assign config (even if null) to ensure property changed fires
        EntryTokenPreview.UserColorConfig = config;

        // Then set text - parser will now use the configured color mappings
        var text = EntryTextBox.Text ?? "";
        EntryTokenPreview.TokenText = text;
    }

    #endregion
}
