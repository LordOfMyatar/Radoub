using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
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

    [AvaloniaFact]
    public void Create_DecodesThumbnailsOffTheUiThread()
    {
        // Thumbnail decode is expensive (BIF/disk → SkiaSharp). It must NOT run
        // on the UI thread or the dialog lags on open (#2291). The decode is
        // allowed (lazily, in the background) — but never on the dispatcher thread.
        var ctx = new FakePortraitBrowserContext(new[]
        {
            new PortraitEntry { Id = 1, ResRef = "po_a_", Race = -1, Sex = -1 },
            new PortraitEntry { Id = 2, ResRef = "po_b_", Race = -1, Sex = -1 },
            new PortraitEntry { Id = 3, ResRef = "po_c_", Race = -1, Sex = -1 }
        });

        var window = PortraitBrowserWindow.Create(ctx);
        Assert.Equal(3, window.PortraitCount);

        // Let the background decodes run and report which thread they used.
        ctx.WaitForDecodes(expected: 3, timeoutMs: 5000);

        Assert.True(ctx.DecodeCount > 0, "expected thumbnails to be decoded");
        Assert.False(ctx.AnyDecodeOnUiThread, "thumbnail decode ran on the UI thread (would lag dialog open)");
    }

    private sealed class FakePortraitBrowserContext : IPortraitBrowserContext
    {
        private readonly List<PortraitEntry> _portraits;
        private readonly CountdownEvent _decoded;
        private int _decodeCount;
        private volatile bool _anyOnUiThread;

        public FakePortraitBrowserContext(IEnumerable<PortraitEntry> portraits)
        {
            _portraits = portraits.ToList();
            _decoded = new CountdownEvent(_portraits.Count == 0 ? 1 : _portraits.Count);
            if (_portraits.Count == 0) _decoded.Signal();
        }

        public int DecodeCount => _decodeCount;
        public bool AnyDecodeOnUiThread => _anyOnUiThread;

        public void WaitForDecodes(int expected, int timeoutMs) => _decoded.Wait(timeoutMs);

        public string? CurrentFileDirectory => null;
        public string? NeverwinterNightsPath => null;
        public bool GameResourcesAvailable => true;

        public IEnumerable<PortraitEntry> ListPortraits() => _portraits;

        public Bitmap? GetPortraitBitmap(string resRef)
        {
            if (Dispatcher.UIThread.CheckAccess())
                _anyOnUiThread = true;
            Interlocked.Increment(ref _decodeCount);
            if (!_decoded.IsSet) _decoded.Signal();
            return null;
        }

        public string GetRaceName(int raceId) => $"Race {raceId}";
    }
}
