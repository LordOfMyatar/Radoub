using Avalonia.Controls;
using Avalonia.Interactivity;

namespace ItemEditor.Views;

/// <summary>
/// Test-only fault-injection seam (#2380). When Relique is launched with --test-fault-inject, a
/// hidden "arm fault" button (AutomationId <c>Relique_TestArmFault</c>) is shown so a FlaUI test can
/// force the NEXT assigned-properties refresh to throw exactly once. That drives the
/// PropertyListMutator rollback path (#2258) end-to-end through the real View handlers — add,
/// remove, clear, and edit — which unit tests can't reach.
///
/// Production builds that don't pass the flag never arm anything: <see cref="RefreshAssignedProperties"/>
/// just does one extra bool check and calls the real refresh.
/// </summary>
public partial class MainWindow
{
    /// <summary>When true, the next <see cref="RefreshAssignedProperties"/> call throws once.</summary>
    private bool _faultArmed;

    /// <summary>True once --test-fault-inject was parsed; gates the hidden arm control.</summary>
    private bool _faultInjectEnabled;

    /// <summary>
    /// Wire the hidden arm button when the test flag is set. Called from the constructor after
    /// InitializeComponent. No-op in production.
    /// </summary>
    private void InitializeFaultInjectionSeam(bool enabled)
    {
        _faultInjectEnabled = enabled;
        var armButton = this.FindControl<Button>("TestArmFaultButton");
        if (armButton == null)
            return;

        armButton.IsVisible = enabled;
        if (enabled)
            armButton.Click += OnArmFaultClick;
    }

    private void OnArmFaultClick(object? sender, RoutedEventArgs e)
    {
        _faultArmed = true;
        UpdateStatus("Fault armed: next property refresh will throw (test seam)");
    }

    /// <summary>
    /// Refresh seam routed by every property command's refresh delegate. When armed (test-only),
    /// throws once so the mutator rolls the model back; otherwise calls the real refresh.
    /// </summary>
    private void RefreshAssignedProperties()
    {
        if (_faultArmed)
        {
            _faultArmed = false; // one-shot
            throw new System.InvalidOperationException("Injected fault (test seam #2380)");
        }
        RefreshAssignedPropertiesCore();
    }
}
