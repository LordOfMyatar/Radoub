using Avalonia.Controls;
using Radoub.Formats.Bic;
using System.Collections.Generic;

namespace Quartermaster.Views.Panels;

/// <summary>
/// Panel for viewing and editing BIC QuickBar slots.
/// Displays 36 slots organized as 3 bars (Normal, Shift, Ctrl) × 12 slots (F1-F12).
/// </summary>
public partial class QuickBarPanel : UserControl
{
    private BicFile? _currentBic;

    public QuickBarPanel()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Load QuickBar data from a BIC file.
    /// </summary>
    public void LoadQuickBar(BicFile bic)
    {
        _currentBic = bic;

        // Build view models for each bar
        var bar1 = new List<QuickBarSlotViewModel>();
        var bar2 = new List<QuickBarSlotViewModel>();
        var bar3 = new List<QuickBarSlotViewModel>();

        for (int i = 0; i < 12; i++)
        {
            // Normal bar (slots 0-11)
            bar1.Add(CreateSlotViewModel(bic, i, $"F{i + 1}"));
            // Shift bar (slots 12-23)
            bar2.Add(CreateSlotViewModel(bic, i + 12, $"F{i + 1}"));
            // Ctrl bar (slots 24-35)
            bar3.Add(CreateSlotViewModel(bic, i + 24, $"F{i + 1}"));
        }

        QuickBar1.ItemsSource = bar1;
        QuickBar2.ItemsSource = bar2;
        QuickBar3.ItemsSource = bar3;

        UpdateSummary();
    }

    /// <summary>
    /// Clear the panel when no BIC is loaded.
    /// </summary>
    public void ClearPanel()
    {
        _currentBic = null;
        QuickBar1.ItemsSource = null;
        QuickBar2.ItemsSource = null;
        QuickBar3.ItemsSource = null;
        QuickBarSummaryText.Text = "No player character loaded";
    }

    private QuickBarSlotViewModel CreateSlotViewModel(BicFile bic, int slotIndex, string label)
    {
        if (slotIndex >= bic.QBList.Count)
        {
            return new QuickBarSlotViewModel
            {
                SlotIndex = slotIndex,
                SlotLabel = label,
                DisplayText = "",
                TooltipText = $"{label}: Empty (slot not initialized)"
            };
        }

        var slot = bic.QBList[slotIndex];
        var typeName = QuickBarObjectType.GetTypeName(slot.ObjectType);
        var displayText = GetSlotDisplayText(slot);
        var tooltipText = GetSlotTooltip(slot, label);

        return new QuickBarSlotViewModel
        {
            SlotIndex = slotIndex,
            SlotLabel = label,
            ObjectType = slot.ObjectType,
            DisplayText = displayText,
            TooltipText = tooltipText
        };
    }

    private static string GetSlotDisplayText(QuickBarSlot slot)
    {
        return slot.ObjectType switch
        {
            QuickBarObjectType.Empty => "",
            QuickBarObjectType.Item => "Item",
            QuickBarObjectType.Spell => "Spell",
            QuickBarObjectType.Skill => "Skill",
            QuickBarObjectType.Feat => "Feat",
            QuickBarObjectType.Script => "Script",
            QuickBarObjectType.Dialog => "Dialog",
            QuickBarObjectType.Attack => "Attack",
            QuickBarObjectType.Emote => "Emote",
            QuickBarObjectType.CastSpell => "Cast",
            QuickBarObjectType.ModeToggle => "Mode",
            QuickBarObjectType.PossessFamiliar => "Familiar",
            QuickBarObjectType.AssociateCommand => "Command",
            QuickBarObjectType.Examine => "Examine",
            QuickBarObjectType.Barter => "Barter",
            QuickBarObjectType.QuickChat => "Chat",
            QuickBarObjectType.CancelPolymorph => "Unmorph",
            QuickBarObjectType.SpellLikeAbility => "SLA",
            _ => $"?{slot.ObjectType}"
        };
    }

    private static string GetSlotTooltip(QuickBarSlot slot, string label)
    {
        if (slot.ObjectType == QuickBarObjectType.Empty)
            return $"{label}: Empty";

        var typeName = QuickBarObjectType.GetTypeName(slot.ObjectType);
        var details = slot.ObjectType switch
        {
            QuickBarObjectType.Spell => $"Spell ID: {slot.INTParam1}",
            QuickBarObjectType.Skill => $"Skill ID: {slot.INTParam1}",
            QuickBarObjectType.Feat => $"Feat ID: {slot.INTParam1}",
            QuickBarObjectType.ModeToggle => $"Mode: {slot.INTParam1}",
            _ => ""
        };

        return string.IsNullOrEmpty(details)
            ? $"{label}: {typeName}"
            : $"{label}: {typeName}\n{details}";
    }

    private void UpdateSummary()
    {
        if (_currentBic == null)
        {
            QuickBarSummaryText.Text = "No player character loaded";
            return;
        }

        int assignedCount = 0;
        foreach (var slot in _currentBic.QBList)
        {
            if (slot.ObjectType != QuickBarObjectType.Empty)
                assignedCount++;
        }

        QuickBarSummaryText.Text = $"{assignedCount} of 36 slots assigned";
    }
}

/// <summary>
/// View model for a single QuickBar slot.
/// </summary>
public class QuickBarSlotViewModel
{
    public int SlotIndex { get; set; }
    public string SlotLabel { get; set; } = "";
    public byte ObjectType { get; set; }
    public string DisplayText { get; set; } = "";
    public string TooltipText { get; set; } = "";
}
