using Avalonia.Media;
using Avalonia.Media.Imaging;
using Quartermaster.Services;
using Radoub.Formats.Logging;
using Radoub.UI.Services;
using System;
using System.ComponentModel;
using System.IO;

namespace Quartermaster.ViewModels;

/// <summary>
/// View model for a feat in the feats list.
/// </summary>
public class FeatListViewModel : INotifyPropertyChanged
{
    private Bitmap? _iconBitmap;
    private bool _iconLoaded = false;
    private ItemIconService? _iconService;
    private bool _isAssigned;
    private string _statusText = "";
    private IBrush _statusColor = Brushes.Transparent;
    private IBrush _rowBackground = Brushes.Transparent;
    private double _textOpacity = 1.0;

    public ushort FeatId { get; set; }
    public string FeatName { get; set; } = "";
    public string Description { get; set; } = "";
    public FeatCategory Category { get; set; }
    public string CategoryName { get; set; } = "";

    public bool IsAssigned
    {
        get => _isAssigned;
        set
        {
            if (_isAssigned != value)
            {
                _isAssigned = value;
                OnPropertyChanged(nameof(IsAssigned));
                OnPropertyChanged(nameof(CanToggle));
                OnPropertyChanged(nameof(AssignedTooltip));
                OnAssignedChanged?.Invoke(this, value);
            }
        }
    }

    public bool IsGranted { get; set; }
    public bool IsUnavailable { get; set; }
    public bool HasPrerequisites { get; set; }
    public bool PrerequisitesMet { get; set; }

    public string StatusText
    {
        get => _statusText;
        set
        {
            if (_statusText != value)
            {
                _statusText = value;
                OnPropertyChanged(nameof(StatusText));
            }
        }
    }

    public IBrush StatusColor
    {
        get => _statusColor;
        set
        {
            if (_statusColor != value)
            {
                _statusColor = value;
                OnPropertyChanged(nameof(StatusColor));
            }
        }
    }

    public IBrush RowBackground
    {
        get => _rowBackground;
        set
        {
            if (_rowBackground != value)
            {
                _rowBackground = value;
                OnPropertyChanged(nameof(RowBackground));
            }
        }
    }

    public double TextOpacity
    {
        get => _textOpacity;
        set
        {
            if (Math.Abs(_textOpacity - value) > 0.001)
            {
                _textOpacity = value;
                OnPropertyChanged(nameof(TextOpacity));
            }
        }
    }

    /// <summary>
    /// Sets the icon service for lazy loading.
    /// </summary>
    public void SetIconService(ItemIconService? iconService)
    {
        _iconService = iconService;
    }

    /// <summary>
    /// Game icon for this feat (from feat.2da ICON column).
    /// Loaded lazily on first access.
    /// </summary>
    public Bitmap? IconBitmap
    {
        get
        {
            // Lazy load on first access
            if (!_iconLoaded && _iconService != null && _iconService.IsGameDataAvailable)
            {
                _iconLoaded = true;
                try
                {
                    _iconBitmap = _iconService.GetFeatIcon(FeatId);
                }
                catch (Exception ex) when (ex is FileNotFoundException or InvalidOperationException or ArgumentException)
                {
                    // Icon not available - expected for some feats
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Could not load feat icon for ID {FeatId}: {ex.Message}");
                }
            }
            return _iconBitmap;
        }
        set
        {
            _iconBitmap = value;
            _iconLoaded = true;
            OnPropertyChanged(nameof(IconBitmap));
            OnPropertyChanged(nameof(HasGameIcon));
        }
    }

    /// <summary>
    /// Whether we have a real game icon (not placeholder).
    /// Returns true if icon service is available (assumes icon exists to avoid triggering load).
    /// </summary>
    public bool HasGameIcon => _iconService != null && _iconService.IsGameDataAvailable;

    /// <summary>
    /// Can the checkbox be toggled? (Not a granted feat - can't remove class-granted feats)
    /// </summary>
    public bool CanToggle => !IsGranted;

    /// <summary>
    /// Tooltip for the assigned checkbox.
    /// </summary>
    public string AssignedTooltip
    {
        get
        {
            if (IsGranted) return "Granted by class - cannot remove";
            if (IsUnavailable && !IsAssigned) return "Not available to this class/race";
            return IsAssigned ? "Click to remove feat" : "Click to add feat";
        }
    }

    /// <summary>
    /// Callback when IsAssigned changes. Args: (FeatListViewModel feat, bool newValue)
    /// </summary>
    public Action<FeatListViewModel, bool>? OnAssignedChanged { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
