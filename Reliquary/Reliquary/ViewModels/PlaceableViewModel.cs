using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Radoub.Formats.Gff;
using Radoub.Formats.Utp;

namespace PlaceableEditor.ViewModels;

/// <summary>
/// Two-way binding facade over a <see cref="UtpFile"/>. Property setters write straight through to
/// the model (the model is the single source of truth; the VM holds no shadow copy), mirroring
/// Relique's <c>ItemViewModel</c>. Derived enablement (<see cref="IsCombatEnabled"/>,
/// <see cref="IsDamageEnabled"/>) implements design §5.1: Static disables combat, Plot disables damage.
/// </summary>
public sealed class PlaceableViewModel : INotifyPropertyChanged
{
    private readonly UtpFile _utp;

    public event PropertyChangedEventHandler? PropertyChanged;

    public PlaceableViewModel(UtpFile utp)
    {
        _utp = utp;
        Scripts = BuildScriptSlots();
    }

    /// <summary>
    /// Create a blank placeable for File → New (#2367). Defaults to a useable, non-inventory,
    /// non-static placeable — the most common starting point — with empty Name/Tag/ResRef the
    /// user fills in. Combat/physical fields are seeded with toolset-matching, game-safe values
    /// via <see cref="Services.PlaceableDefaults.Seed"/> so a bare New → Save round-trips into
    /// Aurora without a divide-by-zero (#2417).
    /// </summary>
    public static PlaceableViewModel NewPlaceable()
    {
        var utp = new UtpFile { Useable = true };
        Services.PlaceableDefaults.Seed(utp);
        return new(utp);
    }

    /// <summary>The wrapped model. Returned by reference for preview/resolution callers.</summary>
    public UtpFile Utp => _utp;

    /// <summary>Return the underlying model for the writer (mutations are already live).</summary>
    public UtpFile WriteToUtp() => _utp;

    // --- Identity ---

    public string Name
    {
        get => _utp.LocName.GetDefault();
        set
        {
            if (_utp.LocName.GetDefault() == value) return;
            _utp.LocName.SetString(0, value ?? string.Empty);
            OnPropertyChanged();
        }
    }

    public string Tag
    {
        get => _utp.Tag;
        set { if (_utp.Tag == value) return; _utp.Tag = value ?? string.Empty; OnPropertyChanged(); }
    }

    public string TemplateResRef
    {
        get => _utp.TemplateResRef;
        set { if (_utp.TemplateResRef == value) return; _utp.TemplateResRef = value ?? string.Empty; OnPropertyChanged(); }
    }

    public uint Appearance
    {
        get => _utp.Appearance;
        set { if (_utp.Appearance == value) return; _utp.Appearance = value; OnPropertyChanged(); }
    }

    public ushort PortraitId
    {
        get => _utp.PortraitId;
        set { if (_utp.PortraitId == value) return; _utp.PortraitId = value; OnPropertyChanged(); }
    }

    // --- Combat / physical ---

    public short HP
    {
        get => _utp.HP;
        set { if (_utp.HP == value) return; _utp.HP = value; OnPropertyChanged(); }
    }

    public byte Hardness
    {
        get => _utp.Hardness;
        set { if (_utp.Hardness == value) return; _utp.Hardness = value; OnPropertyChanged(); }
    }

    public byte Fort
    {
        get => _utp.Fort;
        set { if (_utp.Fort == value) return; _utp.Fort = value; OnPropertyChanged(); }
    }

    public byte Ref
    {
        get => _utp.Ref;
        set { if (_utp.Ref == value) return; _utp.Ref = value; OnPropertyChanged(); }
    }

    public byte Will
    {
        get => _utp.Will;
        set { if (_utp.Will == value) return; _utp.Will = value; OnPropertyChanged(); }
    }

    // --- Flags ---

    public bool Plot
    {
        get => _utp.Plot;
        set
        {
            if (_utp.Plot == value) return;
            _utp.Plot = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsDamageEnabled));
        }
    }

    public bool Useable
    {
        get => _utp.Useable;
        set { if (_utp.Useable == value) return; _utp.Useable = value; OnPropertyChanged(); }
    }

    public bool HasInventory
    {
        get => _utp.HasInventory;
        set { if (_utp.HasInventory == value) return; _utp.HasInventory = value; OnPropertyChanged(); }
    }

    public bool Static
    {
        get => _utp.Static;
        set
        {
            if (_utp.Static == value) return;
            _utp.Static = value;
            // Static and Useable are mutually exclusive (#2412): a static placeable is baked into
            // the area geometry and cannot be interacted with. Force Useable off when going static.
            if (value && _utp.Useable)
            {
                _utp.Useable = false;
                OnPropertyChanged(nameof(Useable));
            }
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsCombatEnabled));
            OnPropertyChanged(nameof(IsDamageEnabled));
            OnPropertyChanged(nameof(IsUseableEnabled));
        }
    }

    /// <summary>HP/Hardness/saves are editable only on a non-static placeable (design §5.1).</summary>
    public bool IsCombatEnabled => !_utp.Static;

    /// <summary>Useable is editable only on a non-static placeable (#2412); they are mutually exclusive.</summary>
    public bool IsUseableEnabled => !_utp.Static;

    /// <summary>Damage-related fields are editable only when neither Static nor Plot (design §5.1).</summary>
    public bool IsDamageEnabled => !_utp.Static && !_utp.Plot;

    // --- Behavior / advanced ---

    public uint Faction
    {
        get => _utp.Faction;
        set { if (_utp.Faction == value) return; _utp.Faction = value; OnPropertyChanged(); }
    }

    /// <summary>Palette category (placeablepal.itp). Drives which toolset folder the blueprint lands in (#2416).</summary>
    public byte PaletteID
    {
        get => _utp.PaletteID;
        set { if (_utp.PaletteID == value) return; _utp.PaletteID = value; OnPropertyChanged(); }
    }

    public string Conversation
    {
        get => _utp.Conversation;
        set { if (_utp.Conversation == value) return; _utp.Conversation = value ?? string.Empty; OnPropertyChanged(); }
    }

    /// <summary>UI shows "No Interrupt"; the model stores the inverse (<see cref="UtpFile.Interruptable"/>).</summary>
    public bool NoInterrupt
    {
        get => !_utp.Interruptable;
        set { if (!_utp.Interruptable == value) return; _utp.Interruptable = !value; OnPropertyChanged(); }
    }

    /// <summary>Initial animation state (design "Initial State").</summary>
    public byte InitialState
    {
        get => _utp.AnimationState;
        set { if (_utp.AnimationState == value) return; _utp.AnimationState = value; OnPropertyChanged(); }
    }

    /// <summary>Named animation states for the Initial State combo (#2376). Engine-fixed catalog.</summary>
    public IReadOnlyList<PlaceableAnimationState> AnimationStates => PlaceableAnimationState.All;

    /// <summary>Body-bag index (design "Treasure Model").</summary>
    public byte TreasureModel
    {
        get => _utp.BodyBag;
        set { if (_utp.BodyBag == value) return; _utp.BodyBag = value; OnPropertyChanged(); }
    }

    // --- Text (surfaced for round-trip; full TextPanel lands Sprint 6) ---

    public string Description
    {
        get => _utp.Description.GetDefault();
        set
        {
            if (_utp.Description.GetDefault() == value) return;
            _utp.Description.SetString(0, value ?? string.Empty);
            OnPropertyChanged();
        }
    }

    public string Comment
    {
        get => _utp.Comment;
        set { if (_utp.Comment == value) return; _utp.Comment = value ?? string.Empty; OnPropertyChanged(); }
    }

    // --- Variables ---

    /// <summary>Live view of the model's local variables (host wraps mutations in undoable commands).</summary>
    public List<Variable> VarTable => _utp.VarTable;

    // --- Scripts ---

    /// <summary>
    /// The 13 placeable event-handler slots, in design §5.2 column order. Each slot binds straight
    /// to a <see cref="UtpFile"/> script field — no hardcoded script data, just the engine event map.
    /// </summary>
    public ObservableCollection<ScriptSlotViewModel> Scripts { get; }

    private ObservableCollection<ScriptSlotViewModel> BuildScriptSlots()
    {
        var slots = new (string Event, string Label, System.Func<string> Get, System.Action<string> Set)[]
        {
            ("OnClosed",        "On Closed",          () => _utp.OnClosed,        v => _utp.OnClosed = v),
            ("OnDamaged",       "On Damaged",         () => _utp.OnDamaged,       v => _utp.OnDamaged = v),
            ("OnDeath",         "On Death",           () => _utp.OnDeath,         v => _utp.OnDeath = v),
            ("OnInvDisturbed",  "On Disturbed",       () => _utp.OnInvDisturbed,  v => _utp.OnInvDisturbed = v),
            ("OnHeartbeat",     "On Heartbeat",       () => _utp.OnHeartbeat,     v => _utp.OnHeartbeat = v),
            ("OnLock",          "On Lock",            () => _utp.OnLock,          v => _utp.OnLock = v),
            ("OnMeleeAttacked", "On Physical Attacked", () => _utp.OnMeleeAttacked, v => _utp.OnMeleeAttacked = v),
            ("OnOpen",          "On Open",            () => _utp.OnOpen,          v => _utp.OnOpen = v),
            ("OnSpellCastAt",   "On Spell Cast At",   () => _utp.OnSpellCastAt,   v => _utp.OnSpellCastAt = v),
            ("OnUnlock",        "On Unlock",          () => _utp.OnUnlock,        v => _utp.OnUnlock = v),
            ("OnUsed",          "On Used",            () => _utp.OnUsed,          v => _utp.OnUsed = v),
            ("OnUserDefined",   "On User Defined",    () => _utp.OnUserDefined,   v => _utp.OnUserDefined = v),
            ("OnTrapTriggered", "On Trap Triggered",  () => _utp.OnTrapTriggered, v => _utp.OnTrapTriggered = v),
        };

        var collection = new ObservableCollection<ScriptSlotViewModel>();
        foreach (var s in slots)
            collection.Add(new ScriptSlotViewModel(s.Event, s.Label, s.Get, s.Set));
        return collection;
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
