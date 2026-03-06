using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Windows.Input;

namespace Quartermaster.ViewModels;

/// <summary>
/// View model for a special ability (spell-like ability) on a creature.
/// </summary>
public class SpecialAbilityViewModel : ObservableObject
{
    public ushort SpellId { get; set; }
    public string AbilityName { get; set; } = "";

    internal byte _casterLevel;
    public byte CasterLevel
    {
        get => _casterLevel;
        set
        {
            if (SetProperty(ref _casterLevel, value))
            {
                CasterLevelDisplay = $"CL {value}";
                OnCasterLevelChanged?.Invoke(this);
            }
        }
    }

    public string CasterLevelDisplay { get; set; } = "";

    private byte _flags;
    public byte Flags
    {
        get => _flags;
        set => SetProperty(ref _flags, value);
    }

    /// <summary>
    /// Uses per day, stored in SpellFlags byte.
    /// The Aurora Toolset treats this as a simple integer count.
    /// </summary>
    public byte Uses
    {
        get => _flags;
        set
        {
            if (SetProperty(ref _flags, value, nameof(Flags)))
            {
                OnPropertyChanged();
                OnFlagsChanged?.Invoke(this);
            }
        }
    }

    /// <summary>
    /// True if this ability is a spell-like ability (has class spell levels in spells.2da).
    /// False for pure monster abilities where caster level is not applicable.
    /// </summary>
    public bool IsSpellLike { get; set; } = true;

    public Action<SpecialAbilityViewModel>? OnCasterLevelChanged { get; set; }
    public Action<SpecialAbilityViewModel>? OnFlagsChanged { get; set; }
    public ICommand? RemoveCommand { get; set; }
}
