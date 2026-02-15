namespace RadoubLauncher.ViewModels;

// Partial methods for property change tracking
public partial class ModuleEditorViewModel
{
    partial void OnModuleNameChanged(string value) => MarkChanged();
    partial void OnModuleDescriptionChanged(string value) => MarkChanged();
    partial void OnModuleTagChanged(string value) => MarkChanged();
    partial void OnCustomTlkChanged(string value) => MarkChanged();
    partial void OnMinGameVersionChanged(string value) => MarkChanged();
    partial void OnRequiresSoUChanged(bool value) => MarkChanged();
    partial void OnRequiresHotUChanged(bool value) => MarkChanged();
    partial void OnDawnHourChanged(byte value) => MarkChanged();
    partial void OnDuskHourChanged(byte value) => MarkChanged();
    partial void OnMinutesPerHourChanged(byte value) => MarkChanged();
    partial void OnStartYearChanged(uint value) => MarkChanged();
    partial void OnStartMonthChanged(byte value) => MarkChanged();
    partial void OnStartDayChanged(byte value) => MarkChanged();
    partial void OnStartHourChanged(byte value) => MarkChanged();
    partial void OnEntryAreaChanged(string value) => MarkChanged();
    partial void OnEntryXChanged(float value) => MarkChanged();
    partial void OnEntryYChanged(float value) => MarkChanged();
    partial void OnEntryZChanged(float value) => MarkChanged();
    partial void OnOnModuleLoadChanged(string value) => MarkChanged();
    partial void OnOnClientEnterChanged(string value) => MarkChanged();
    partial void OnOnClientLeaveChanged(string value) => MarkChanged();
    partial void OnOnHeartbeatChanged(string value) => MarkChanged();
    partial void OnOnAcquireItemChanged(string value) => MarkChanged();
    partial void OnOnActivateItemChanged(string value) => MarkChanged();
    partial void OnOnUnacquireItemChanged(string value) => MarkChanged();
    partial void OnOnPlayerDeathChanged(string value) => MarkChanged();
    partial void OnOnPlayerDyingChanged(string value) => MarkChanged();
    partial void OnOnPlayerRestChanged(string value) => MarkChanged();
    partial void OnOnPlayerEquipItemChanged(string value) => MarkChanged();
    partial void OnOnPlayerUnequipItemChanged(string value) => MarkChanged();
    partial void OnOnPlayerLevelUpChanged(string value) => MarkChanged();
    partial void OnOnUserDefinedChanged(string value) => MarkChanged();
    partial void OnOnSpawnButtonDownChanged(string value) => MarkChanged();
    partial void OnOnCutsceneAbortChanged(string value) => MarkChanged();
    partial void OnOnModuleStartChanged(string value) => MarkChanged();
    partial void OnOnPlayerChatChanged(string value) => MarkChanged();
    partial void OnOnPlayerTargetChanged(string value) => MarkChanged();
    partial void OnOnPlayerGuiEventChanged(string value) => MarkChanged();
    partial void OnOnPlayerTileActionChanged(string value) => MarkChanged();
    partial void OnOnNuiEventChanged(string value) => MarkChanged();
    partial void OnXpScaleChanged(byte value) => MarkChanged();
    partial void OnDefaultBicChanged(string value)
    {
        MarkChanged();
        // Sync to RadoubSettings so MainWindow can check if DefaultBic is set
        SyncDefaultBicToSettings();
    }

    partial void OnUseDefaultBicChanged(bool value)
    {
        MarkChanged();
        OnPropertyChanged(nameof(IsDefaultBicDropdownEnabled));

        if (!value)
        {
            // Clear DefaultBic when unchecked
            DefaultBic = string.Empty;
        }
        else if (AvailableBicFiles.Count > 0 && string.IsNullOrEmpty(DefaultBic))
        {
            // Auto-select first BIC if available and none selected
            DefaultBic = AvailableBicFiles[0];
        }

        // Sync to RadoubSettings so MainWindow can check if DefaultBic is set
        SyncDefaultBicToSettings();
    }

    partial void OnStartMovieChanged(string value) => MarkChanged();

    private void MarkChanged()
    {
        // Only mark as changed if module is loaded (not during initial population)
        if (IsModuleLoaded)
        {
            HasUnsavedChanges = true;
        }
    }
}
