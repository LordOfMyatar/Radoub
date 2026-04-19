using ItemEditor.Services;

namespace ItemEditor.Tests.Services;

public class PropertyCategoryServiceTests
{
    private static PropertyTypeInfo MakeProp(string label)
        => new PropertyTypeInfo(0, label, label, null, null, null);

    [Theory]
    [InlineData("Enhancement", "Bonus/Enhancement")]
    [InlineData("AttackBonus", "Bonus/Enhancement")]
    [InlineData("Ability", "Bonus/Enhancement")]
    [InlineData("Keen", "Bonus/Enhancement")]
    [InlineData("Mighty", "Bonus/Enhancement")]
    [InlineData("Damage", "Damage")]
    [InlineData("Massive_Criticals", "Damage")]
    [InlineData("Monster_damage", "Damage")]
    [InlineData("DamageMelee", "Damage")]
    [InlineData("Armor", "Defense/AC")]
    [InlineData("ImprovedSavingThrows", "Defense/AC")]
    [InlineData("ImprovedMagicResist", "Defense/AC")]
    [InlineData("DamageResist", "Defense/AC")]
    [InlineData("Immunity", "Defense/AC")]
    [InlineData("OnHit", "On Hit")]
    [InlineData("OnMonsterHit", "On Hit")]
    [InlineData("OnHitCastSpell", "On Hit")]
    [InlineData("CastSpell", "Cast Spell")]
    [InlineData("BonusFeats", "Cast Spell")]
    [InlineData("DecreaseAbilityScore", "Penalty/Decreased")]
    [InlineData("AttackPenalty", "Penalty/Decreased")]
    [InlineData("DamagePenalty", "Penalty/Decreased")]
    [InlineData("Damage_Vulnerability", "Penalty/Decreased")]
    [InlineData("Skill", "Skill/Ability")]
    [InlineData("UseLimitationClass", "Use Limitation")]
    [InlineData("UseLimitationRacial", "Use Limitation")]
    [InlineData("Regeneration", "Miscellaneous")]
    [InlineData("Haste", "Miscellaneous")]
    [InlineData("HolyAvenger", "Miscellaneous")]
    [InlineData("Trap", "Miscellaneous")]
    public void IsInCategory_known_stock_label_returns_true_for_expected_category(string label, string expected)
    {
        var svc = new PropertyCategoryService();
        Assert.True(svc.IsInCategory(MakeProp(label), expected));
    }

    [Fact]
    public void IsInCategory_known_stock_label_returns_false_for_other_categories()
    {
        var svc = new PropertyCategoryService();
        var prop = MakeProp("Damage");
        Assert.False(svc.IsInCategory(prop, "Defense/AC"));
        Assert.False(svc.IsInCategory(prop, "Miscellaneous"));
        Assert.False(svc.IsInCategory(prop, PropertyCategoryService.OtherCategory));
    }

    [Fact]
    public void IsInCategory_unmapped_label_returns_true_for_Other_only()
    {
        var svc = new PropertyCategoryService();
        var prop = MakeProp("CEP_CUSTOM_PROPERTY_XYZ");

        Assert.True(svc.IsInCategory(prop, PropertyCategoryService.OtherCategory));
        Assert.False(svc.IsInCategory(prop, "Damage"));
        Assert.False(svc.IsInCategory(prop, "Bonus/Enhancement"));
    }

    [Fact]
    public void IsInCategory_empty_label_treated_as_Other()
    {
        var svc = new PropertyCategoryService();
        var prop = MakeProp("");

        Assert.True(svc.IsInCategory(prop, PropertyCategoryService.OtherCategory));
        Assert.False(svc.IsInCategory(prop, "Damage"));
    }

    [Fact]
    public void IsInCategory_is_case_insensitive()
    {
        var svc = new PropertyCategoryService();
        Assert.True(svc.IsInCategory(MakeProp("enhancement"), "Bonus/Enhancement"));
        Assert.True(svc.IsInCategory(MakeProp("ENHANCEMENT"), "Bonus/Enhancement"));
        Assert.True(svc.IsInCategory(MakeProp("EnHaNcEmEnT"), "Bonus/Enhancement"));
    }

    [Fact]
    public void GetCategoryNames_hides_empty_buckets()
    {
        var svc = new PropertyCategoryService();
        var props = new[]
        {
            MakeProp("Damage"),
            MakeProp("Regeneration"),
        };

        var result = svc.GetCategoryNames(props);

        Assert.Equal(new[] { "Damage", "Miscellaneous" }, result);
    }

    [Fact]
    public void GetCategoryNames_includes_Other_when_unmapped_present()
    {
        var svc = new PropertyCategoryService();
        var props = new[]
        {
            MakeProp("Damage"),
            MakeProp("CEP_CUSTOM_UNKNOWN"),
        };

        var result = svc.GetCategoryNames(props);

        Assert.Contains(PropertyCategoryService.OtherCategory, result);
        Assert.Contains("Damage", result);
    }

    [Fact]
    public void GetCategoryNames_preserves_fixed_display_order()
    {
        var svc = new PropertyCategoryService();
        // One property per canonical bucket, listed out of order
        var props = new[]
        {
            MakeProp("CEP_CUSTOM_UNKNOWN"),      // Other
            MakeProp("Regeneration"),            // Miscellaneous
            MakeProp("UseLimitationClass"),      // Use Limitation
            MakeProp("Skill"),                   // Skill/Ability
            MakeProp("DecreaseAC"),              // Penalty/Decreased
            MakeProp("CastSpell"),               // Cast Spell
            MakeProp("OnHit"),                   // On Hit
            MakeProp("Armor"),                   // Defense/AC
            MakeProp("Damage"),                  // Damage
            MakeProp("Enhancement"),             // Bonus/Enhancement
        };

        var result = svc.GetCategoryNames(props);

        Assert.Equal(new[]
        {
            "Bonus/Enhancement",
            "Damage",
            "Defense/AC",
            "On Hit",
            "Cast Spell",
            "Penalty/Decreased",
            "Skill/Ability",
            "Use Limitation",
            "Miscellaneous",
            PropertyCategoryService.OtherCategory,
        }, result);
    }
}
