using System.Reflection;
using Radoub.Formats.Bic;
using Radoub.Formats.Utc;
using Xunit;

namespace Radoub.Formats.Tests;

/// <summary>
/// Tests for UtcFile.CopyFrom, which commits a working copy back into an existing
/// creature instance without breaking references held by editor panels.
///
/// CopyFrom round-trips through the type's own writer/reader, so copy fidelity
/// equals save fidelity and no hand-maintained field list can drift. Cross-type
/// copies are rejected — converting UTC to BIC is BicFile.FromUtcFile's job.
///
/// Supports the Level Up wizard's clone-and-commit refactor (#2676, epic #2571).
/// </summary>
public class CreatureCopyFromTests
{
    [Fact]
    public void CopyFrom_PreservesTargetInstanceIdentity()
    {
        // The whole point: panels hold a reference to the live creature, so the
        // commit must mutate in place rather than swap the object.
        var target = CreateTestUtc();
        var reference = target;
        var source = CreateTestUtc();
        source.Str = 18;

        target.CopyFrom(source);

        Assert.Same(reference, target);
        Assert.Equal(18, target.Str);
    }

    [Fact]
    public void CopyFrom_CopiesScalarFields()
    {
        var target = CreateTestUtc();
        var source = CreateTestUtc();
        source.Str = 18;
        source.HitPoints = 42;
        source.FortBonus = 7;
        source.WillBonus = 3;

        target.CopyFrom(source);

        Assert.Equal(18, target.Str);
        Assert.Equal(42, target.HitPoints);
        Assert.Equal(7, target.FortBonus);
        Assert.Equal(3, target.WillBonus);
    }

    [Fact]
    public void CopyFrom_CopiesClassList()
    {
        var target = CreateTestUtc();
        var source = CreateTestUtc();
        source.ClassList[0].ClassLevel = 5;
        source.ClassList.Add(new CreatureClass { Class = 3, ClassLevel = 2 });

        target.CopyFrom(source);

        Assert.Equal(2, target.ClassList.Count);
        Assert.Equal(5, target.ClassList[0].ClassLevel);
        Assert.Equal(3, target.ClassList[1].Class);
    }

    [Fact]
    public void CopyFrom_IsDeepCopy_MutatingSourceDoesNotAffectTarget()
    {
        var target = CreateTestUtc();
        var source = CreateTestUtc();
        source.ClassList[0].ClassLevel = 5;

        target.CopyFrom(source);
        source.ClassList[0].ClassLevel = 99;
        source.Str = 99;

        Assert.Equal(5, target.ClassList[0].ClassLevel);
        Assert.NotEqual(99, target.Str);
    }

    [Fact]
    public void CopyFrom_ReplacesTargetListsRatherThanAppending()
    {
        var target = CreateTestUtc();
        target.FeatList.Add(100);
        target.FeatList.Add(101);
        var source = CreateTestUtc();
        source.FeatList.Add(200);

        target.CopyFrom(source);

        Assert.Single(target.FeatList);
        Assert.Equal(200, target.FeatList[0]);
    }

    [Fact]
    public void CopyFrom_ClearsFieldsAbsentFromSource()
    {
        // A field set on the target but not the source must end up cleared, not
        // left behind as stale data.
        var target = CreateTestUtc();
        target.Comment = "stale comment";
        var source = CreateTestUtc();
        source.Comment = "";

        target.CopyFrom(source);

        Assert.Equal("", target.Comment);
    }

    [Fact]
    public void CopyFrom_BicToBic_PreservesPlayerOnlyFields()
    {
        // BIC carries fields UTC does not officially support. A BIC-to-BIC copy
        // must keep them.
        var target = CreateTestBic();
        var source = CreateTestBic();
        source.Age = 37;
        source.Gold = 12345u;
        source.Experience = 90000u;
        source.ReputationList.Add(50);

        target.CopyFrom(source);

        Assert.Equal(37, target.Age);
        Assert.Equal(12345u, target.Gold);
        Assert.Equal(90000u, target.Experience);
        Assert.Single(target.ReputationList);
        Assert.Equal(50, target.ReputationList[0]);
    }

    [Fact]
    public void CopyFrom_BicToBic_PreservesQuickBar()
    {
        var target = CreateTestBic();
        var source = CreateTestBic();
        source.QBList.Add(new QuickBarSlot { ObjectType = QuickBarObjectType.Empty });
        source.QBList.Add(new QuickBarSlot { ObjectType = QuickBarObjectType.Empty });

        target.CopyFrom(source);

        Assert.Equal(2, target.QBList.Count);
    }

    [Fact]
    public void CopyFrom_UtcSourceIntoBicTarget_Throws()
    {
        // Converting UTC to BIC is BicFile.FromUtcFile's job — it synthesizes
        // Experience and the QuickBar. A silent copy would leave the target's
        // player fields stale instead.
        var target = CreateTestBic();
        var source = CreateTestUtc();

        Assert.Throws<InvalidOperationException>(() => target.CopyFrom(source));
    }

    [Fact]
    public void CopyFrom_BicSourceIntoUtcTarget_Throws()
    {
        var target = CreateTestUtc();
        var source = CreateTestBic();

        Assert.Throws<InvalidOperationException>(() => target.CopyFrom(source));
    }

    [Fact]
    public void CopyFrom_DoesNotAliasSourceLists()
    {
        // The commit takes ownership of a private round-tripped copy, so mutating
        // the source afterwards must not reach the target. This pins the property
        // the clone-and-commit design depends on.
        var target = CreateTestUtc();
        var source = CreateTestUtc();
        source.FeatList.Add(200);

        target.CopyFrom(source);
        source.FeatList.Add(201);
        source.ClassList[0].ClassLevel = 99;

        Assert.Single(target.FeatList);
        Assert.Equal(1, target.ClassList[0].ClassLevel);
    }

    [Fact]
    public void CopyFrom_BicToBic_DoesNotAliasPlayerLists()
    {
        var target = CreateTestBic();
        var source = CreateTestBic();
        source.ReputationList.Add(50);

        target.CopyFrom(source);
        source.ReputationList.Add(75);

        Assert.Single(target.ReputationList);
    }

    [Fact]
    public void CopyFrom_NullSource_Throws()
    {
        var target = CreateTestUtc();

        Assert.Throws<ArgumentNullException>(() => target.CopyFrom(null!));
    }

    /// <summary>
    /// Format metadata written by the writer itself, not user data.
    /// A test value here produces an invalid GFF header.
    /// </summary>
    private static readonly HashSet<string> FormatMetadata = new()
    {
        nameof(UtcFile.FileType),
        nameof(UtcFile.FileVersion)
    };

    /// <summary>
    /// Blueprint-only fields the BIC format does not carry (BioWare Table 2.6.2,
    /// see the exclusions in BicWriter). They do not survive a BIC save, so they
    /// do not survive a BIC copy either — copy fidelity equals save fidelity.
    /// </summary>
    private static readonly HashSet<string> BlueprintOnlyFields = new()
    {
        nameof(UtcFile.TemplateResRef),
        nameof(UtcFile.Comment),
        nameof(UtcFile.ChallengeRating),
        nameof(UtcFile.Conversation)
    };

    [Theory]
    [MemberData(nameof(UtcPropertyNames))]
    public void CopyFrom_CopiesEveryUtcProperty(string propertyName)
    {
        // Drift guard: every public read-write property on UtcFile must survive
        // a CopyFrom. Adding a field to UtcFile without the writer/reader
        // handling it fails here rather than silently dropping on commit.
        AssertPropertyCopied(propertyName, CreateTestUtc, () => CreateTestUtc());
    }

    [Theory]
    [MemberData(nameof(BicPropertyNames))]
    public void CopyFrom_CopiesEveryBicProperty(string propertyName)
    {
        AssertPropertyCopied(propertyName, CreateTestBic, () => CreateTestBic());
    }

    [Theory]
    [InlineData(nameof(UtcFile.TemplateResRef))]
    [InlineData(nameof(UtcFile.Comment))]
    [InlineData(nameof(UtcFile.Conversation))]
    public void CopyFrom_BicToBic_DropsBlueprintOnlyStringFields(string propertyName)
    {
        // Pins the documented consequence of round-tripping: BIC does not persist
        // these, so a BIC-to-BIC copy clears them rather than carrying stale values.
        var target = CreateTestBic();
        var source = CreateTestBic();
        var prop = typeof(BicFile).GetProperty(propertyName)!;
        prop.SetValue(source, "blueprint-only");

        target.CopyFrom(source);

        Assert.Equal("", prop.GetValue(target));
    }

    [Fact]
    public void CopyFrom_BicToBic_DropsChallengeRating()
    {
        var target = CreateTestBic();
        var source = CreateTestBic();
        source.ChallengeRating = 7.5f;

        target.CopyFrom(source);

        Assert.Equal(0f, target.ChallengeRating);
    }

    [Fact]
    public void CopyFrom_UtcToUtc_KeepsBlueprintOnlyFields()
    {
        // The same fields DO survive a UTC-to-UTC copy — the exclusion is a
        // property of the BIC format, not of CopyFrom.
        var target = CreateTestUtc();
        var source = CreateTestUtc();
        source.Comment = "designer note";
        source.ChallengeRating = 7.5f;
        source.Conversation = "convo";

        target.CopyFrom(source);

        Assert.Equal("designer note", target.Comment);
        Assert.Equal(7.5f, target.ChallengeRating);
        Assert.Equal("convo", target.Conversation);
    }

    public static TheoryData<string> UtcPropertyNames => BuildPropertyNames(typeof(UtcFile), false);

    public static TheoryData<string> BicPropertyNames => BuildPropertyNames(typeof(BicFile), true);

    private static TheoryData<string> BuildPropertyNames(Type type, bool isBic)
    {
        var data = new TheoryData<string>();
        foreach (var prop in GetCopyableProperties(type, isBic))
            data.Add(prop.Name);
        return data;
    }

    private static IEnumerable<PropertyInfo> GetCopyableProperties(Type type, bool isBic)
    {
        return type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite)
            .Where(p => !FormatMetadata.Contains(p.Name))
            // Covered by the explicit blueprint-only tests above.
            .Where(p => !isBic || !BlueprintOnlyFields.Contains(p.Name))
            .OrderBy(p => p.Name);
    }

    /// <summary>
    /// Sets a distinct non-default value on the named property of a fresh source,
    /// copies into a fresh target, and asserts the value arrived.
    /// </summary>
    private static void AssertPropertyCopied<T>(
        string propertyName,
        Func<T> createSource,
        Func<T> createTarget) where T : UtcFile
    {
        var source = createSource();
        var target = createTarget();
        var prop = typeof(T).GetProperty(propertyName)!;

        if (!TrySetDistinctValue(prop, source, out var expected))
            return; // Property type has no simple distinct value; covered by explicit tests above.

        target.CopyFrom(source);

        var actual = prop.GetValue(target);
        Assert.Equal(expected, actual);
    }

    /// <summary>
    /// Assigns a recognisably non-default value for the simple property types the
    /// creature formats use. Returns false for collection and object types, which
    /// the explicit tests cover instead.
    /// </summary>
    private static bool TrySetDistinctValue(PropertyInfo prop, object target, out object? assigned)
    {
        assigned = null;
        var t = prop.PropertyType;

        if (t == typeof(string)) assigned = "zz";
        else if (t == typeof(byte)) assigned = (byte)7;
        else if (t == typeof(sbyte)) assigned = (sbyte)7;
        else if (t == typeof(short)) assigned = (short)77;
        else if (t == typeof(ushort)) assigned = (ushort)77;
        else if (t == typeof(int)) assigned = 77;
        else if (t == typeof(uint)) assigned = 77u;
        else if (t == typeof(float)) assigned = 7.5f;
        else if (t == typeof(bool)) assigned = true;
        else return false;

        prop.SetValue(target, assigned);
        return true;
    }

    private static BicFile CreateTestBic()
    {
        var bic = new BicFile
        {
            Str = 10, Dex = 10, Con = 10, Int = 10, Wis = 10, Cha = 10,
            Race = 6,
            Gender = 0,
            HitPoints = 8,
            MaxHitPoints = 8,
            CurrentHitPoints = 8,
            Age = 25,
            Experience = 0,
            Gold = 0
        };
        bic.FirstName.SetString(0, "TestPC");
        bic.ClassList.Add(new CreatureClass { Class = 0, ClassLevel = 1 });
        for (int i = 0; i < 28; i++) bic.SkillList.Add(0);
        return bic;
    }

    private static UtcFile CreateTestUtc()
    {
        var utc = new UtcFile
        {
            Str = 10, Dex = 10, Con = 10, Int = 10, Wis = 10, Cha = 10,
            Race = 6,
            Gender = 0,
            HitPoints = 8,
            MaxHitPoints = 8,
            CurrentHitPoints = 8
        };
        utc.FirstName.SetString(0, "TestCreature");
        utc.ClassList.Add(new CreatureClass { Class = 0, ClassLevel = 1 });
        for (int i = 0; i < 28; i++) utc.SkillList.Add(0);
        return utc;
    }
}
