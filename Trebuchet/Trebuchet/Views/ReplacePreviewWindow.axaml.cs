using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Radoub.Formats.Logging;
using Radoub.Formats.Search;
using Radoub.UI.Services.Search;

namespace RadoubLauncher.Views;

public partial class ReplacePreviewWindow : Window
{
    private BatchReplacePreview? _preview;
    private BatchReplaceService? _batchReplaceService;
    private readonly List<CheckBox> _fileCheckBoxes = new();
    private readonly List<CheckBox> _matchCheckBoxes = new();

    public event EventHandler<BatchReplaceResult>? ReplacementComplete;

    public ReplacePreviewWindow()
    {
        InitializeComponent();
    }

    public void Initialize(
        BatchReplacePreview preview,
        string searchPattern,
        string replaceText,
        BatchReplaceService batchReplaceService)
    {
        _preview = preview;
        _batchReplaceService = batchReplaceService;

        HeaderText.Text = $"Replacing \"{searchPattern}\" \u2192 \"{replaceText}\"";

        // .nss script-source matches were found but can't be replaced here \u2014 tell the
        // user instead of showing an unexplained empty/short preview (#2341).
        if (preview.SkippedNssContentMatches > 0)
        {
            var n = preview.SkippedNssContentMatches;
            NssNoticeText.Text =
                $"\u26a0 {n} match{(n == 1 ? "" : "es")} in .nss script files {(n == 1 ? "is" : "are")} not shown \u2014 " +
                "manage NSS files in your code editor.";
            NssNotice.IsVisible = true;
        }

        BuildChangeTree(preview);
        UpdateSelectionCount();
    }

    private void BuildChangeTree(BatchReplacePreview preview)
    {
        var treeItems = new List<TreeViewItem>();

        foreach (var fileGroup in preview.FileGroups)
        {
            var fileCheckBox = new CheckBox
            {
                Content = Path.GetFileName(fileGroup.FilePath),
                IsChecked = true,
                Tag = fileGroup
            };
            fileCheckBox.IsCheckedChanged += OnFileCheckChanged;
            _fileCheckBoxes.Add(fileCheckBox);

            var fileNode = new TreeViewItem
            {
                Header = fileCheckBox,
                IsExpanded = true
            };

            foreach (var change in fileGroup.Changes)
            {
                var oldText = change.Match.FullFieldValue.Length > 50
                    ? change.Match.FullFieldValue[..50] + "..."
                    : change.Match.FullFieldValue;
                // Show the computed post-replace field value (substring substitution),
                // not the bare replacement term (#2224).
                var computed = change.ComputedNewFieldValue;
                var newText = computed.Length > 50
                    ? computed[..50] + "..."
                    : computed;

                var location = change.Match.Location?.ToString() ?? change.Match.Field.Name;

                // Visual flag for ResRef-typed field rows when running in ResRef-replace mode.
                // These are rows that would be skipped by the standard replace path but are
                // visible because allowResRefReplace=true. See spec Section 5.
                var resRefBadge = preview.AllowResRefReplace
                    && change.Match.Field.FieldType == SearchFieldType.ResRef
                    ? "\ud83d\udd17 " // \ud83d\udd17
                    : string.Empty;

                var matchCheckBox = new CheckBox
                {
                    Content = $"{resRefBadge}[{location}] {change.Match.Field.Name}: \"{oldText}\" \u2192 \"{newText}\"",
                    IsChecked = true,
                    Tag = change
                };
                matchCheckBox.IsCheckedChanged += OnMatchCheckChanged;
                _matchCheckBoxes.Add(matchCheckBox);

                var matchNode = new TreeViewItem { Header = matchCheckBox };
                fileNode.Items.Add(matchNode);
            }

            treeItems.Add(fileNode);
        }

        ChangeTree.ItemsSource = treeItems;
    }

    private void OnFileCheckChanged(object? sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox fileCheckBox || fileCheckBox.Tag is not FileChangeGroup fileGroup)
            return;

        var isChecked = fileCheckBox.IsChecked == true;
        foreach (var change in fileGroup.Changes)
        {
            change.IsSelected = isChecked;
        }

        // Update child checkboxes
        foreach (var matchCb in _matchCheckBoxes)
        {
            if (matchCb.Tag is PendingChange pc && fileGroup.Changes.Contains(pc))
            {
                matchCb.IsCheckedChanged -= OnMatchCheckChanged;
                matchCb.IsChecked = isChecked;
                matchCb.IsCheckedChanged += OnMatchCheckChanged;
            }
        }

        UpdateSelectionCount();
    }

    private void OnMatchCheckChanged(object? sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox matchCheckBox || matchCheckBox.Tag is not PendingChange change)
            return;

        change.IsSelected = matchCheckBox.IsChecked == true;
        UpdateSelectionCount();
    }

    private void UpdateSelectionCount()
    {
        if (_preview == null) return;
        var selected = _preview.SelectedChanges;
        var total = _preview.TotalChanges;
        SelectionCount.Text = $"{selected} of {total} selected";
        ExecuteButton.IsEnabled = selected > 0;
    }

    private void OnSelectAllClick(object? sender, RoutedEventArgs e)
    {
        SetAllSelected(true);
    }

    private void OnDeselectAllClick(object? sender, RoutedEventArgs e)
    {
        SetAllSelected(false);
    }

    private void SetAllSelected(bool selected)
    {
        if (_preview == null) return;

        foreach (var change in _preview.Changes)
            change.IsSelected = selected;

        foreach (var cb in _fileCheckBoxes)
        {
            cb.IsCheckedChanged -= OnFileCheckChanged;
            cb.IsChecked = selected;
            cb.IsCheckedChanged += OnFileCheckChanged;
        }

        foreach (var cb in _matchCheckBoxes)
        {
            cb.IsCheckedChanged -= OnMatchCheckChanged;
            cb.IsChecked = selected;
            cb.IsCheckedChanged += OnMatchCheckChanged;
        }

        UpdateSelectionCount();
    }

    private async void OnExecuteClick(object? sender, RoutedEventArgs e)
    {
        if (_preview == null || _batchReplaceService == null) return;

        if (_preview.SelectedChanges == 0)
            return;

        ExecuteButton.IsEnabled = false;

        try
        {
            var modulePath = Radoub.Formats.Settings.RadoubSettings.Instance.CurrentModulePath;
            var moduleName = !string.IsNullOrEmpty(modulePath)
                ? Path.GetFileName(modulePath)
                : "unknown";

            var result = await _batchReplaceService.ExecuteReplaceAsync(_preview, moduleName);
            ReplacementComplete?.Invoke(this, result);
            Close();
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Replace execution failed: {ex.Message}");
            ExecuteButton.IsEnabled = true;
        }
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
