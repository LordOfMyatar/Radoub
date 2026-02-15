using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using RadoubLauncher.ViewModels;

namespace RadoubLauncher.Controls;

public partial class ModuleEditorPanel : UserControl
{
    private ModuleEditorViewModel? _viewModel;

    public ModuleEditorPanel()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Initialize the panel with its ViewModel and parent window reference.
    /// Called by MainWindow after embedding the panel.
    /// </summary>
    public void Initialize(ModuleEditorViewModel viewModel, Window parentWindow)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        viewModel.SetParentWindow(parentWindow);

        // Subscribe to variable added event to auto-focus the name field
        viewModel.VariableAdded += OnVariableAdded;
    }

    /// <summary>
    /// Load the current module's IFO data into the panel.
    /// </summary>
    public async Task LoadModuleAsync()
    {
        if (_viewModel == null) return;
        await _viewModel.LoadCurrentModuleAsync();
    }

    private void OnVariableAdded(object? sender, System.EventArgs e)
    {
        // Delay to let the DataGrid render the new row, then focus the Name field
        Dispatcher.UIThread.Post(() =>
        {
            // Scroll to the new item and select its first cell
            VariablesDataGrid.ScrollIntoView(VariablesDataGrid.SelectedItem, null);
            VariablesDataGrid.BeginEdit();

            // Find the Name TextBox in the selected row and focus it
            Dispatcher.UIThread.Post(() =>
            {
                var nameTextBox = FindNameTextBoxInSelectedRow();
                if (nameTextBox != null)
                {
                    nameTextBox.Focus();
                    nameTextBox.SelectAll();
                }
            }, DispatcherPriority.Background);
        }, DispatcherPriority.Background);
    }

    private TextBox? FindNameTextBoxInSelectedRow()
    {
        if (VariablesDataGrid.SelectedItem == null) return null;

        // Find the DataGridRow matching our selected item, then grab its first TextBox
        foreach (var descendant in VariablesDataGrid.GetVisualDescendants())
        {
            if (descendant is DataGridRow row && row.DataContext == VariablesDataGrid.SelectedItem)
            {
                foreach (var child in row.GetVisualDescendants())
                {
                    if (child is TextBox tb)
                        return tb;
                }
            }
        }
        return null;
    }
}
