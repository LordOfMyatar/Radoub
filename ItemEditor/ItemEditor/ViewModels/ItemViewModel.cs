using System.ComponentModel;
using System.Runtime.CompilerServices;
using Radoub.Formats.Uti;

namespace ItemEditor.ViewModels;

/// <summary>
/// ViewModel wrapping a UtiFile for two-way data binding.
/// Property changes update the underlying UtiFile directly.
/// </summary>
public class ItemViewModel : INotifyPropertyChanged
{
    private readonly UtiFile _uti;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ItemViewModel(UtiFile uti)
    {
        _uti = uti;
    }

    public string Name
    {
        get => _uti.LocalizedName?.GetDefault() ?? string.Empty;
        set
        {
            var current = _uti.LocalizedName?.GetDefault() ?? string.Empty;
            if (current == value) return;
            _uti.LocalizedName ??= new Radoub.Formats.Gff.CExoLocString();
            _uti.LocalizedName.SetString(0, value);
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

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
