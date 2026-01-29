using System;
using System.IO;
using System.Windows.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Quartermaster.Services;
using Radoub.Formats.Logging;
using Radoub.Formats.Utc;
using Radoub.UI.Services;

namespace Quartermaster.ViewModels;

/// <summary>
/// View model for a spell in the spells list.
/// </summary>
public class SpellListViewModel : BindableBase
{
    private bool _isKnown;
    private bool _isMemorized;
    private int _memorizedCount;
    private string _statusText = "";
    private IBrush _statusColor = Brushes.Transparent;
    private IBrush _rowBackground = Brushes.Transparent;
    private IBrush _memorizedCountColor = Brushes.Transparent;
    private double _textOpacity = 1.0;
    private Bitmap? _iconBitmap;
    private bool _iconLoaded = false;
    private ItemIconService? _iconService;

    public int SpellId { get; set; }

    /// <summary>
    /// Sets the icon service for lazy loading.
    /// </summary>
    public void SetIconService(ItemIconService? iconService)
    {
        _iconService = iconService;
    }

    /// <summary>
    /// Game icon for this spell (from spells.2da IconResRef).
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
                    _iconBitmap = _iconService.GetSpellIcon(SpellId);
                }
                catch (Exception ex) when (ex is FileNotFoundException or InvalidOperationException or ArgumentException)
                {
                    // Icon not available - expected for some spells
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Could not load spell icon for ID {SpellId}: {ex.Message}");
                }
            }
            return _iconBitmap;
        }
        set
        {
            if (_iconBitmap != value)
            {
                _iconBitmap = value;
                _iconLoaded = true;
                OnPropertyChanged(nameof(IconBitmap));
                OnPropertyChanged(nameof(HasGameIcon));
            }
        }
    }

    /// <summary>
    /// Whether we have a real game icon (not placeholder).
    /// Returns true if icon service is available (assumes icon exists to avoid triggering load).
    /// </summary>
    public bool HasGameIcon => _iconService != null && _iconService.IsGameDataAvailable;

    public string SpellName { get; set; } = "";
    public int SpellLevel { get; set; }
    public string SpellLevelDisplay { get; set; } = "";
    public int InnateLevel { get; set; }
    public string InnateLevelDisplay { get; set; } = "";
    public SpellSchool School { get; set; }
    public string SchoolName { get; set; } = "";

    public bool IsKnown
    {
        get => _isKnown;
        set
        {
            if (_isKnown != value)
            {
                _isKnown = value;
                OnPropertyChanged(nameof(IsKnown));
                OnPropertyChanged(nameof(KnownTooltip));
                OnPropertyChanged(nameof(CanToggleMemorized));
                OnPropertyChanged(nameof(MemorizedTooltip));
                OnPropertyChanged(nameof(CanIncrementMemorized));
                OnPropertyChanged(nameof(CanDecrementMemorized));
                // Notify commands that their CanExecute may have changed
                IncrementMemorizedCommand.RaiseCanExecuteChanged();
                DecrementMemorizedCommand.RaiseCanExecuteChanged();
                OnKnownChanged?.Invoke(this, value);
            }
        }
    }

    public bool IsMemorized
    {
        get => _isMemorized;
        set
        {
            if (_isMemorized != value)
            {
                _isMemorized = value;
                OnPropertyChanged(nameof(IsMemorized));
                OnPropertyChanged(nameof(MemorizedTooltip));
                OnMemorizedChanged?.Invoke(this, value);
            }
        }
    }

    /// <summary>
    /// Number of times this spell is memorized.
    /// </summary>
    public int MemorizedCount
    {
        get => _memorizedCount;
        set
        {
            if (_memorizedCount != value)
            {
                _memorizedCount = value;
                _isMemorized = value > 0;
                OnPropertyChanged(nameof(MemorizedCount));
                OnPropertyChanged(nameof(MemorizedCountDisplay));
                OnPropertyChanged(nameof(IsMemorized));
                OnPropertyChanged(nameof(CanDecrementMemorized));
                OnPropertyChanged(nameof(CanIncrementMemorized));
                // Notify commands that their CanExecute may have changed
                IncrementMemorizedCommand.RaiseCanExecuteChanged();
                DecrementMemorizedCommand.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>
    /// Display string for memorized count.
    /// </summary>
    public string MemorizedCountDisplay => _memorizedCount.ToString();

    /// <summary>
    /// Color for the memorized count display.
    /// </summary>
    public IBrush MemorizedCountColor
    {
        get => _memorizedCountColor;
        set => SetProperty(ref _memorizedCountColor, value);
    }

    /// <summary>
    /// Whether the decrement button should be enabled.
    /// </summary>
    public bool CanDecrementMemorized => !IsBlocked && IsKnown && !IsSpontaneousCaster && _memorizedCount > 0;

    /// <summary>
    /// Whether the increment button should be enabled.
    /// </summary>
    public bool CanIncrementMemorized => !IsBlocked && IsKnown && !IsSpontaneousCaster;

    /// <summary>
    /// Command to increment memorization count.
    /// </summary>
    public RelayCommand IncrementMemorizedCommand { get; }

    /// <summary>
    /// Command to decrement memorization count.
    /// </summary>
    public RelayCommand DecrementMemorizedCommand { get; }

    /// <summary>
    /// Callback when memorization count changes. Args: (SpellListViewModel spell, int delta)
    /// </summary>
    public Action<SpellListViewModel, int>? OnMemorizedCountChanged { get; set; }

    public SpellListViewModel()
    {
        IncrementMemorizedCommand = new RelayCommand(
            () => OnMemorizedCountChanged?.Invoke(this, 1),
            () => CanIncrementMemorized);

        DecrementMemorizedCommand = new RelayCommand(
            () => OnMemorizedCountChanged?.Invoke(this, -1),
            () => CanDecrementMemorized);
    }

    public bool IsBlocked { get; set; }
    public bool IsSpontaneousCaster { get; set; }
    public string BlockedReason { get; set; } = "";
    public string Description { get; set; } = "";

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public IBrush StatusColor
    {
        get => _statusColor;
        set => SetProperty(ref _statusColor, value);
    }

    public IBrush RowBackground
    {
        get => _rowBackground;
        set => SetProperty(ref _rowBackground, value);
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
    /// Whether the Known checkbox can be toggled (not blocked).
    /// </summary>
    public bool CanToggleKnown => !IsBlocked;

    /// <summary>
    /// Whether the Memorized checkbox can be toggled.
    /// Must be known, not blocked, and not a spontaneous caster.
    /// </summary>
    public bool CanToggleMemorized => !IsBlocked && IsKnown && !IsSpontaneousCaster;

    /// <summary>
    /// Tooltip for the Known checkbox.
    /// </summary>
    public string KnownTooltip => IsBlocked
        ? BlockedReason
        : (IsKnown ? "Click to remove from known spells" : "Click to add to known spells");

    /// <summary>
    /// Tooltip for the Memorized checkbox.
    /// </summary>
    public string MemorizedTooltip
    {
        get
        {
            if (IsBlocked) return BlockedReason;
            if (IsSpontaneousCaster) return "Spontaneous casters don't memorize spells";
            if (!IsKnown) return "Must know spell before memorizing";
            return IsMemorized ? "Click to remove from memorized spells" : "Click to memorize spell";
        }
    }

    /// <summary>
    /// Callback when IsKnown changes. Args: (SpellListViewModel spell, bool newValue)
    /// </summary>
    public Action<SpellListViewModel, bool>? OnKnownChanged { get; set; }

    /// <summary>
    /// Callback when IsMemorized changes. Args: (SpellListViewModel spell, bool newValue)
    /// </summary>
    public Action<SpellListViewModel, bool>? OnMemorizedChanged { get; set; }
}
