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

    // Flag 0x04 = unlimited uses
    public bool IsUnlimited
    {
        get => (Flags & 0x04) != 0;
        set
        {
            if (value)
                Flags = (byte)(Flags | 0x04);
            else
                Flags = (byte)(Flags & ~0x04);
            OnPropertyChanged();
            OnFlagsChanged?.Invoke(this);
        }
    }

    public Action<SpecialAbilityViewModel>? OnCasterLevelChanged { get; set; }
    public Action<SpecialAbilityViewModel>? OnFlagsChanged { get; set; }
    public ICommand? RemoveCommand { get; set; }
}
