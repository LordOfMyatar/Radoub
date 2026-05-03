using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Radoub.Formats.Gff;
using Radoub.Formats.Uti;

namespace ItemEditor.ViewModels;

/// <summary>
/// ViewModel wrapping a UtiFile for two-way data binding.
/// Property changes update the underlying UtiFile directly.
/// </summary>
public class ItemViewModel : INotifyPropertyChanged
{
    private readonly UtiFile _uti;
    private readonly Func<uint, string?>? _tlkResolver;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ItemViewModel(UtiFile uti, Func<uint, string?>? tlkResolver = null)
    {
        _uti = uti;
        _tlkResolver = tlkResolver;
    }

    /// <summary>
    /// The underlying UTI being edited. Returned by reference (the VM does not deep-copy
    /// on every property change) so the preview controller can pass it to
    /// <c>ItemModelResolver.Resolve</c> directly.
    /// </summary>
    public UtiFile Uti => _uti;

    public string Name
    {
        get => GetLocString(_uti.LocalizedName);
        set
        {
            var current = _uti.LocalizedName?.GetDefault() ?? string.Empty;
            if (current == value) return;
            _uti.LocalizedName ??= new Radoub.Formats.Gff.CExoLocString();
            _uti.LocalizedName.SetString(0, value);
            OnPropertyChanged();
        }
    }

    public string Description
    {
        get => GetLocString(_uti.Description);
        set
        {
            var current = _uti.Description?.GetDefault() ?? string.Empty;
            if (current == value) return;
            _uti.Description ??= new CExoLocString();
            _uti.Description.SetString(0, value);
            OnPropertyChanged();
        }
    }

    public string DescIdentified
    {
        get => GetLocString(_uti.DescIdentified);
        set
        {
            var current = _uti.DescIdentified?.GetDefault() ?? string.Empty;
            if (current == value) return;
            _uti.DescIdentified ??= new CExoLocString();
            _uti.DescIdentified.SetString(0, value);
            OnPropertyChanged();
        }
    }

    public string Tag
    {
        get => _uti.Tag;
        set
        {
            if (_uti.Tag == value) return;
            _uti.Tag = value;
            OnPropertyChanged();
        }
    }

    public string ResRef
    {
        get => _uti.TemplateResRef;
        set
        {
            if (_uti.TemplateResRef == value) return;
            _uti.TemplateResRef = value;
            OnPropertyChanged();
        }
    }

    public int BaseItem
    {
        get => _uti.BaseItem;
        set
        {
            if (_uti.BaseItem == value) return;
            _uti.BaseItem = value;
            OnPropertyChanged();
        }
    }

    public uint Cost
    {
        get => _uti.Cost;
        set
        {
            if (_uti.Cost == value) return;
            _uti.Cost = value;
            OnPropertyChanged();
        }
    }

    public uint AddCost
    {
        get => _uti.AddCost;
        set
        {
            if (_uti.AddCost == value) return;
            _uti.AddCost = value;
            OnPropertyChanged();
        }
    }

    public ushort StackSize
    {
        get => _uti.StackSize;
        set
        {
            if (_uti.StackSize == value) return;
            _uti.StackSize = value;
            OnPropertyChanged();
        }
    }

    public byte Charges
    {
        get => _uti.Charges;
        set
        {
            if (_uti.Charges == value) return;
            _uti.Charges = value;
            OnPropertyChanged();
        }
    }

    public bool Plot
    {
        get => _uti.Plot;
        set
        {
            if (_uti.Plot == value) return;
            _uti.Plot = value;
            OnPropertyChanged();
        }
    }

    public bool Cursed
    {
        get => _uti.Cursed;
        set
        {
            if (_uti.Cursed == value) return;
            _uti.Cursed = value;
            OnPropertyChanged();
        }
    }

    public bool Stolen
    {
        get => _uti.Stolen;
        set
        {
            if (_uti.Stolen == value) return;
            _uti.Stolen = value;
            OnPropertyChanged();
        }
    }

    public bool Identified
    {
        get => _uti.Identified;
        set
        {
            if (_uti.Identified == value) return;
            _uti.Identified = value;
            OnPropertyChanged();
        }
    }

    public bool Dropable
    {
        get => _uti.Dropable;
        set
        {
            if (_uti.Dropable == value) return;
            _uti.Dropable = value;
            OnPropertyChanged();
        }
    }

    public string Comment
    {
        get => _uti.Comment;
        set
        {
            if (_uti.Comment == value) return;
            _uti.Comment = value;
            OnPropertyChanged();
        }
    }

    public byte PaletteID
    {
        get => _uti.PaletteID;
        set
        {
            if (_uti.PaletteID == value) return;
            _uti.PaletteID = value;
            OnPropertyChanged();
        }
    }

    // --- Model Part Properties (conditional on ModelType) ---

    public byte ModelPart1
    {
        get => _uti.ModelPart1;
        set
        {
            if (_uti.ModelPart1 == value) return;
            _uti.ModelPart1 = value;
            OnPropertyChanged();
        }
    }

    public byte ModelPart2
    {
        get => _uti.ModelPart2;
        set
        {
            if (_uti.ModelPart2 == value) return;
            _uti.ModelPart2 = value;
            OnPropertyChanged();
        }
    }

    public byte ModelPart3
    {
        get => _uti.ModelPart3;
        set
        {
            if (_uti.ModelPart3 == value) return;
            _uti.ModelPart3 = value;
            OnPropertyChanged();
        }
    }

    // --- Color Properties (conditional on ModelType 1 or 3) ---

    public byte Cloth1Color
    {
        get => _uti.Cloth1Color;
        set
        {
            if (_uti.Cloth1Color == value) return;
            _uti.Cloth1Color = value;
            OnPropertyChanged();
        }
    }

    public byte Cloth2Color
    {
        get => _uti.Cloth2Color;
        set
        {
            if (_uti.Cloth2Color == value) return;
            _uti.Cloth2Color = value;
            OnPropertyChanged();
        }
    }

    public byte Leather1Color
    {
        get => _uti.Leather1Color;
        set
        {
            if (_uti.Leather1Color == value) return;
            _uti.Leather1Color = value;
            OnPropertyChanged();
        }
    }

    public byte Leather2Color
    {
        get => _uti.Leather2Color;
        set
        {
            if (_uti.Leather2Color == value) return;
            _uti.Leather2Color = value;
            OnPropertyChanged();
        }
    }

    public byte Metal1Color
    {
        get => _uti.Metal1Color;
        set
        {
            if (_uti.Metal1Color == value) return;
            _uti.Metal1Color = value;
            OnPropertyChanged();
        }
    }

    public byte Metal2Color
    {
        get => _uti.Metal2Color;
        set
        {
            if (_uti.Metal2Color == value) return;
            _uti.Metal2Color = value;
            OnPropertyChanged();
        }
    }

    // --- Armor Part Accessors ---

    public byte GetArmorPart(string partName)
    {
        return _uti.ArmorParts.TryGetValue(partName, out var value) ? value : (byte)0;
    }

    public void SetArmorPart(string partName, byte value)
    {
        _uti.ArmorParts[partName] = value;
        OnPropertyChanged($"ArmorPart_{partName}");
    }

    private string GetLocString(CExoLocString? locString)
    {
        if (locString == null) return string.Empty;

        var embedded = locString.GetDefault();
        if (!string.IsNullOrEmpty(embedded)) return embedded;

        if (_tlkResolver != null && locString.StrRef != 0xFFFFFFFF)
            return _tlkResolver(locString.StrRef) ?? string.Empty;

        return string.Empty;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
