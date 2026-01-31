namespace Radoub.UI.Controls;

/// <summary>
/// Store-specific entry.
/// </summary>
public class StoreBrowserEntry : FileBrowserEntry
{
}

/// <summary>
/// Store browser panel for embedding in Fence's main window.
/// Provides .utm file list from module directory.
/// </summary>
public partial class StoreBrowserPanel : FileBrowserPanelBase
{
    public StoreBrowserPanel()
    {
        InitializeComponent();

        FileExtension = ".utm";
        SearchWatermark = "Type to filter stores...";
    }

    protected override string FormatCountLabel(int moduleCount, int hakCount, int totalCount)
    {
        if (totalCount == 0)
        {
            if (string.IsNullOrEmpty(ModulePath))
                return "No module loaded";
            return "No .utm files found";
        }

        return $"{totalCount} store{(totalCount == 1 ? "" : "s")}";
    }
}
