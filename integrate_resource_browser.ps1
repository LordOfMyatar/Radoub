# PowerShell script to integrate ResourceBrowserManager into MainWindow
$filePath = "Parley\Parley\Views\MainWindow.axaml.cs"
$content = Get-Content $filePath -Raw

# 1. Add field declaration
$oldFields = "        private readonly NodeCreationHelper _nodeCreationHelper; // Handles smart node creation and tree navigation

        // DEBOUNCED AUTO-SAVE: Timer for file auto-save after inactivity"
$newFields = "        private readonly NodeCreationHelper _nodeCreationHelper; // Handles smart node creation and tree navigation
        private readonly ResourceBrowserManager _resourceBrowserManager; // Manages resource browser dialogs

        // DEBOUNCED AUTO-SAVE: Timer for file auto-save after inactivity"
$content = $content.Replace($oldFields, $newFields)

# 2. Remove old _recentCreatureTags field (now in service)
$oldRecentTags = "        // Session cache for recently used creature tags
        private readonly List<string> _recentCreatureTags = new();

        // Parameter autocomplete: Cache of script parameter declarations"
$newRecentTags = "        // Parameter autocomplete: Cache of script parameter declarations"
$content = $content.Replace($oldRecentTags, $newRecentTags)

# 3. Add service initialization
$oldInit = "                triggerAutoSave: TriggerDebouncedAutoSave);

            DebugLogger.Initialize(this);"
$newInit = "                triggerAutoSave: TriggerDebouncedAutoSave);
            _resourceBrowserManager = new ResourceBrowserManager(
                audioService: _audioService,
                creatureService: _creatureService,
                findControl: this.FindControl<Control>,
                setStatusMessage: msg => _viewModel.StatusMessage = msg,
                autoSaveProperty: AutoSaveProperty,
                getSelectedNode: () => _selectedNode);

            DebugLogger.Initialize(this);"
$content = $content.Replace($oldInit, $newInit)

# 4. Replace OnBrowseSoundClick
$content = $content -replace 'private async void OnBrowseSoundClick\(object\? sender, RoutedEventArgs e\)\s*\{[^}]*?\}(?=\s*private)', @'
private async void OnBrowseSoundClick(object? sender, RoutedEventArgs e)
        {
            await _resourceBrowserManager.BrowseSoundAsync(this);
        }
'@

# 5. Replace OnBrowseCreatureClick
$content = $content -replace 'private async void OnBrowseCreatureClick\(object\? sender, RoutedEventArgs e\)\s*\{[\s\S]*?catch \(Exception ex\)[\s\S]*?\}\s*\}(?=\s*private)', @'
private async void OnBrowseCreatureClick(object? sender, RoutedEventArgs e)
        {
            await _resourceBrowserManager.BrowseCreatureAsync(this);
        }
'@

# Write back
$content | Set-Content $filePath -NoNewline

Write-Host "ResourceBrowserManager integration completed!"
