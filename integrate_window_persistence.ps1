# PowerShell script to integrate WindowPersistenceManager into MainWindow
$filePath = "Parley\Parley\Views\MainWindow.axaml.cs"
$content = Get-Content $filePath -Raw

# 1. Add field declaration
$oldFields = "        private readonly DebugAndLoggingHandler _debugAndLoggingHandler; // Handles debug and logging operations

        // DEBOUNCED AUTO-SAVE: Timer for file auto-save after inactivity"
$newFields = "        private readonly DebugAndLoggingHandler _debugAndLoggingHandler; // Handles debug and logging operations
        private readonly WindowPersistenceManager _windowPersistenceManager; // Manages window and panel persistence

        // DEBOUNCED AUTO-SAVE: Timer for file auto-save after inactivity"
$content = $content.Replace($oldFields, $newFields)

# 2. Remove old _isRestoringPosition field (now in service)
$content = $content -replace '        private bool _isRestoringPosition = false;\r?\n', ''

# 3. Add service initialization
$oldInit = "                setStatusMessage: msg => _viewModel.StatusMessage = msg);

            DebugLogger.Initialize(this);"
$newInit = "                setStatusMessage: msg => _viewModel.StatusMessage = msg);
            _windowPersistenceManager = new WindowPersistenceManager(
                window: this,
                findControl: this.FindControl<Control>);

            DebugLogger.Initialize(this);"
$content = $content.Replace($oldInit, $newInit)

# 4. Replace LoadAnimationValues call
$content = $content.Replace("            LoadAnimationValues();", "            _windowPersistenceManager.LoadAnimationValues();")

# 5. Replace window event registration in Opened handler
# Need to replace the entire position restore logic within the Opened event
$oldOpened = @'
            this.Opened += async (s, e) =>
            {
                // Restore window position from settings (after window is open and screens are available)
                _isRestoringPosition = true;
                var settings = SettingsService.Instance;
                UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Restoring window position: Left={settings.WindowLeft}, Top={settings.WindowTop}, Current={Position.X},{Position.Y}");

                if (settings.WindowLeft >= 0 && settings.WindowTop >= 0)
                {
                    var targetPos = new PixelPoint((int)settings.WindowLeft, (int)settings.WindowTop);

                    // Validate position is on a visible screen
                    if (IsPositionOnScreen(targetPos))
                    {
                        Position = targetPos;
                        UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Position restored to ({targetPos.X}, {targetPos.Y})");
                    }
                    else
                    {
                        UnifiedLogger.LogApplication(LogLevel.WARN, $"Saved position ({targetPos.X}, {targetPos.Y}) is off-screen, using default");
                    }
                }

                // Allow position saving after a short delay (to avoid saving the restore itself)
                await Task.Delay(500);
                _isRestoringPosition = false;

                PopulateRecentFilesMenu();
'@

$newOpened = @'
            this.Opened += async (s, e) =>
            {
                // Restore window position from settings
                await _windowPersistenceManager.RestoreWindowPositionAsync();

                PopulateRecentFilesMenu();
'@

$content = $content.Replace($oldOpened, $newOpened)

# 6. Replace PositionChanged event handler
$content = $content.Replace("            this.PositionChanged += OnWindowPositionChanged;", "            this.PositionChanged += (s, e) => _windowPersistenceManager.SaveWindowPosition();")

# 7. Replace PropertyChanged event handler
$oldPropertyChanged = "            this.PropertyChanged += OnWindowPropertyChanged;"
$newPropertyChanged = @'
            this.PropertyChanged += (s, e) =>
            {
                if (!_windowPersistenceManager.IsRestoringPosition)
                {
                    if (e.Property.Name == nameof(Width) || e.Property.Name == nameof(Height))
                    {
                        _windowPersistenceManager.SaveWindowPosition();
                    }
                }
            };
'@
$content = $content.Replace($oldPropertyChanged, $newPropertyChanged)

# 8. Replace OnWindowLoaded
$oldWindowLoaded = @'
        private void OnWindowLoaded(object? sender, RoutedEventArgs e)
        {
            // Controls are now available, restore settings
            RestoreDebugSettings();
            RestorePanelSizes();
        }
'@

$newWindowLoaded = @'
        private void OnWindowLoaded(object? sender, RoutedEventArgs e)
        {
            // Controls are now available, restore settings
            _windowPersistenceManager.RestoreDebugSettings();
            _windowPersistenceManager.RestorePanelSizes();
        }
'@
$content = $content.Replace($oldWindowLoaded, $newWindowLoaded)

# 9. Replace OnWindowClosing SaveWindowPosition call
$content = $content.Replace("                SaveWindowPosition();", "                _windowPersistenceManager.SaveWindowPosition();")

# 10. Delete old methods
# Delete RestorePanelSizes
$content = $content -replace 'private void RestorePanelSizes\(\)[\s\S]*?// Watch for splitter changes[\s\S]*?mainContentGrid.PropertyChanged \+= OnMainContentGridPropertyChanged;[\s\S]*?\}[\s\S]*?\}[\s\S]*?\r?\n', ''

# Delete OnMainContentGridPropertyChanged
$content = $content -replace 'private void OnMainContentGridPropertyChanged\(object\? sender, AvaloniaPropertyChangedEventArgs e\)[\s\S]*?SavePanelSizes\(\);[\s\S]*?\}[\s\S]*?\r?\n', ''

# Delete SavePanelSizes
$content = $content -replace 'private void SavePanelSizes\(\)[\s\S]*?if \(topLeftPanelRow.Height.IsAbsolute\)[\s\S]*?\{[\s\S]*?SettingsService.Instance.TopLeftPanelHeight = topLeftPanelRow.Height.Value;[\s\S]*?\}[\s\S]*?\}[\s\S]*?\}[\s\S]*?\r?\n', ''

# Delete RestoreDebugSettings
$content = $content -replace 'private void RestoreDebugSettings\(\)[\s\S]*?showDebugMenuItem.Header = debugTab.IsVisible \? "Hide _Debug Console" : "Show _Debug Console";[\s\S]*?\}[\s\S]*?\}[\s\S]*?\}[\s\S]*?\r?\n', ''

# Delete OnWindowPositionChanged
$content = $content -replace 'private void OnWindowPositionChanged\(object\? sender, PixelPointEventArgs e\)[\s\S]*?SaveWindowPosition\(\);[\s\S]*?\}[\s\S]*?\r?\n', ''

# Delete OnWindowPropertyChanged
$content = $content -replace 'private void OnWindowPropertyChanged\(object\? sender, AvaloniaPropertyChangedEventArgs e\)[\s\S]*?SaveWindowPosition\(\);[\s\S]*?\}[\s\S]*?\}[\s\S]*?\r?\n', ''

# Delete SaveWindowPosition
$content = $content -replace 'private void SaveWindowPosition\(\)[\s\S]*?settings.WindowHeight = Height;[\s\S]*?\}[\s\S]*?\}[\s\S]*?\r?\n', ''

# Delete IsPositionOnScreen
$content = $content -replace 'private bool IsPositionOnScreen\(PixelPoint position\)[\s\S]*?UnifiedLogger.LogApplication\(LogLevel.DEBUG, \$"  Position is OFF all screens"\);[\s\S]*?return false;[\s\S]*?\}[\s\S]*?\r?\n', ''

# Delete LoadAnimationValues
$content = $content -replace 'private void LoadAnimationValues\(\)[\s\S]*?UnifiedLogger.LogApplication\(LogLevel.ERROR, \$"Failed to load animation values: \{ex.Message\}"\);[\s\S]*?\}[\s\S]*?\}[\s\S]*?\r?\n', ''

# Write back
$content | Set-Content $filePath -NoNewline

Write-Host "WindowPersistenceManager integration completed!"
