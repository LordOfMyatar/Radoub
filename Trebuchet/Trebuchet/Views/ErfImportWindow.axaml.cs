using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using RadoubLauncher.ViewModels;

namespace RadoubLauncher.Views;

public partial class ErfImportWindow : Window
{
    private ErfImportViewModel? _viewModel;

    public ErfImportWindow()
    {
        InitializeComponent();
    }

    public void Initialize(string moduleDirectory)
    {
        _viewModel = new ErfImportViewModel(moduleDirectory);
        DataContext = _viewModel;
    }

    private async void OnBrowseClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel == null) return;

        var storage = GetTopLevel(this)?.StorageProvider;
        if (storage == null) return;

        var erfFilter = new FilePickerFileType("ERF Archives")
        {
            Patterns = new[] { "*.erf", "*.hak", "*.mod", "*.sav" }
        };

        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select ERF Archive",
            FileTypeFilter = new[] { erfFilter },
            AllowMultiple = false
        });

        if (files.Count > 0)
        {
            var path = files[0].TryGetLocalPath();
            if (!string.IsNullOrEmpty(path))
                await _viewModel.LoadErfAsync(path);
        }
    }

    private async void OnImportClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel == null) return;

        var result = await _viewModel.ImportAsync();
        if (result != null && result.ErrorCount == 0 && result.ImportedCount > 0)
        {
            // Auto-close on successful import with no errors
        }
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel?.IsImporting == true)
        {
            _viewModel.CancelImport();
        }
        else
        {
            Close();
        }
    }
}
