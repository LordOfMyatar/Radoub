using Avalonia.Controls;

namespace DialogEditor.Views
{
    /// <summary>
    /// Module-wide search window for searching across all DLG files.
    /// Stub — full implementation in #1843.
    /// </summary>
    public partial class ModuleSearchWindow : Window
    {
        public ModuleSearchWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Initialize with module directory and current file path.
        /// </summary>
        public void Initialize(string modulePath, string? currentFilePath)
        {
            Title = $"Search Module — {System.IO.Path.GetFileName(modulePath)}";
        }
    }
}
