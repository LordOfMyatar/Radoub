using System.Reflection;
using System.Threading;
using Avalonia.Headless.XUnit;
using Radoub.UI.Controls;
using Xunit;

namespace Radoub.UI.Tests;

/// <summary>
/// Guards #2262 finding 2: FileBrowserPanelBase._indexingCts was never
/// disposed when the control detached. Host detach during indexing
/// orphaned both the CancellationTokenSource and the running Task until
/// the next ModulePath setter. Fix: subscribe DetachedFromVisualTree and
/// call CancelIndexing() there.
/// </summary>
public class FileBrowserPanelDetachTests
{
    [Fact]
    public void OnDetachedFromVisualTree_IsOverridden()
    {
        var method = typeof(FileBrowserPanelBase).GetMethod(
            "OnDetachedFromVisualTree",
            BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.NotNull(method);
        Assert.Equal(
            typeof(FileBrowserPanelBase),
            method!.DeclaringType);
    }

    [AvaloniaFact]
    public void CancelIndexing_AfterCtsAssigned_DisposesAndNullsField()
    {
        var panel = new FileBrowserPanelBase();
        var ctsField = typeof(FileBrowserPanelBase).GetField(
            "_indexingCts",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(ctsField);

        // Simulate an in-flight indexing task by assigning a fresh CTS.
        ctsField!.SetValue(panel, new CancellationTokenSource());
        Assert.NotNull(ctsField.GetValue(panel));

        // Invoke the private CancelIndexing — same path the detach handler hits.
        var cancel = typeof(FileBrowserPanelBase).GetMethod(
            "CancelIndexing",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(cancel);
        cancel!.Invoke(panel, null);

        Assert.Null(ctsField.GetValue(panel));
    }
}
