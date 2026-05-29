using System.Collections.Concurrent;
using System.Reflection;
using Radoub.UI.Controls;
using Radoub.UI.Services;
using Xunit;

namespace Radoub.UI.Tests;

/// <summary>
/// Guards #2262 finding 1: the static _hakCache fields on each *BrowserPanel
/// (and HakScriptScanner) are mutated from Task.Run-scheduled scans. Single-
/// panel usage was sequential and incidentally safe, but two panel instances
/// of the same browser type (multi-window tool, Trebuchet preview + main tool
/// open) race on writes. Fix: use ConcurrentDictionary so concurrent writes
/// are safe even if upstream code parallelizes scans across panel instances.
/// </summary>
public class BrowserPanelStaticCacheConcurrencyTests
{
    [Theory]
    [InlineData(typeof(ItemBrowserPanel))]
    [InlineData(typeof(StoreBrowserPanel))]
    [InlineData(typeof(CreatureBrowserPanel))]
    [InlineData(typeof(DialogBrowserPanel))]
    [InlineData(typeof(HakScriptScanner))]
    public void StaticHakCache_IsConcurrentDictionary(System.Type panelType)
    {
        var field = panelType.GetField(
            "_hakCache",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(field);

        var fieldType = field!.FieldType;
        Assert.True(
            fieldType.IsGenericType &&
            fieldType.GetGenericTypeDefinition() == typeof(ConcurrentDictionary<,>),
            $"{panelType.Name}._hakCache must be ConcurrentDictionary<,> for thread-safe " +
            $"concurrent writes from Task.Run-scheduled scans (#2262). Currently: {fieldType}");
    }
}
