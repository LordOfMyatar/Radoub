namespace Parley.Services;

/// <summary>
/// Centralized manager for UI state flags that control event handling behavior.
/// Replaces scattered boolean flags with explicit state tracking (#525).
/// </summary>
/// <remarks>
/// These flags prevent recursive event handling and infinite loops when:
/// - Populating properties panel from code (shouldn't trigger auto-save)
/// - Setting selection programmatically (shouldn't trigger selection changed events)
/// </remarks>
public class UiStateManager
{
    /// <summary>
    /// True when the properties panel is being populated from code.
    /// Used to prevent auto-save from triggering during programmatic updates.
    /// </summary>
    public bool IsPopulatingProperties { get; set; }

    /// <summary>
    /// True when selection is being set programmatically (not by user click).
    /// Used to prevent selection sync loops between TreeView and FlowChart.
    /// </summary>
    public bool IsSettingSelectionProgrammatically { get; set; }

    /// <summary>
    /// True when inserting a token via the Token Selector dialog.
    /// Suppresses tree refresh during the operation to prevent focus jump.
    /// </summary>
    public bool IsInsertingToken { get; set; }

    /// <summary>
    /// Begins a property population operation.
    /// Use with try/finally to ensure EndPropertiesPopulation is called.
    /// </summary>
    public void BeginPropertiesPopulation()
    {
        IsPopulatingProperties = true;
    }

    /// <summary>
    /// Ends a property population operation.
    /// </summary>
    public void EndPropertiesPopulation()
    {
        IsPopulatingProperties = false;
    }

}
