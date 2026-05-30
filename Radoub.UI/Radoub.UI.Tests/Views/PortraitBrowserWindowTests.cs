using System.Collections.Generic;
using System.Linq;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Radoub.UI.Services;
using Radoub.UI.Views;
using Xunit;

namespace Radoub.UI.Tests.Views;

/// <summary>
/// Tests for the shared <see cref="PortraitBrowserWindow"/> (extracted from
/// Quartermaster, #2291). Verifies the window populates from an
/// <see cref="IPortraitBrowserContext"/> rather than tool-specific services.
/// </summary>
public class PortraitBrowserWindowTests
{
    [AvaloniaFact]
    public void Create_WithContext_PopulatesPortraitsFromContext()
    {
        var ctx = new FakePortraitBrowserContext(new[]
        {
            new PortraitEntry { Id = 1, ResRef = "po_plc_c05_", Race = -1, Sex = -1 }
        });

        var window = PortraitBrowserWindow.Create(ctx);

        Assert.Equal(1, window.PortraitCount);
    }

    private sealed class FakePortraitBrowserContext : IPortraitBrowserContext
    {
        private readonly List<PortraitEntry> _portraits;

        public FakePortraitBrowserContext(IEnumerable<PortraitEntry> portraits)
            => _portraits = portraits.ToList();

        public string? CurrentFileDirectory => null;
        public string? NeverwinterNightsPath => null;
        public bool GameResourcesAvailable => true;

        public IEnumerable<PortraitEntry> ListPortraits() => _portraits;
        public Bitmap? GetPortraitBitmap(string resRef) => null;
        public string GetRaceName(int raceId) => $"Race {raceId}";
    }
}
