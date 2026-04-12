using CommunityToolkit.Mvvm.ComponentModel;
using Radoub.Formats.Common;
using Radoub.Formats.Erf;

namespace RadoubLauncher.ViewModels;

public partial class ErfResourceViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isSelected = true;

    public string ResRef { get; }
    public ushort ResourceType { get; }
    public string TypeExtension { get; }
    public string TypeLabel { get; }
    public long Size { get; }
    public string FormattedSize { get; }
    public bool ExistsInModule { get; set; }
    public ErfResourceEntry Entry { get; }

    public ErfResourceViewModel(ErfResourceEntry entry)
    {
        Entry = entry;
        ResRef = entry.ResRef;
        ResourceType = entry.ResourceType;
        TypeExtension = ResourceTypes.GetExtension(entry.ResourceType);
        TypeLabel = GetTypeLabel(entry.ResourceType);
        Size = entry.Size;
        FormattedSize = FormatSize(entry.Size);
    }

    private static string GetTypeLabel(ushort type) => type switch
    {
        ResourceTypes.Ncs => "Script (compiled)",
        ResourceTypes.Nss => "Script (source)",
        ResourceTypes.Utc => "Creature",
        ResourceTypes.Uti => "Item",
        ResourceTypes.Utm => "Store",
        ResourceTypes.Utp => "Placeable",
        ResourceTypes.Utd => "Door",
        ResourceTypes.Utt => "Trigger",
        ResourceTypes.Ute => "Encounter",
        ResourceTypes.Utw => "Waypoint",
        ResourceTypes.Uts => "Sound",
        ResourceTypes.Dlg => "Dialog",
        ResourceTypes.Jrl => "Journal",
        ResourceTypes.Are => "Area",
        ResourceTypes.Git => "Area Instance",
        ResourceTypes.Ifo => "Module Info",
        ResourceTypes.Fac => "Factions",
        ResourceTypes.Itp => "Palette",
        ResourceTypes.TwoDA => "2DA",
        _ => ResourceTypes.GetExtension(type).TrimStart('.').ToUpperInvariant()
    };

    private static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
        _ => $"{bytes / (1024.0 * 1024.0):F1} MB"
    };
}
