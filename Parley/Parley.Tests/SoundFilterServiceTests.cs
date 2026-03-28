using DialogEditor.Models.Sound;
using DialogEditor.Services;
using Xunit;

namespace DialogEditor.Tests;

public class SoundFilterServiceTests
{
    private static List<SoundFileInfo> CreateTestSounds()
    {
        return new List<SoundFileInfo>
        {
            new() { FileName = "vs_femelf_att1", IsMono = true, ChannelUnknown = false },
            new() { FileName = "vs_femelf_att2", IsMono = true, ChannelUnknown = false },
            new() { FileName = "mus_battle_01", IsMono = false, ChannelUnknown = false },  // Stereo
            new() { FileName = "al_mg_beam01", IsMono = true, ChannelUnknown = false },
            new() { FileName = "as_cv_bellshrt1", IsMono = false, ChannelUnknown = true },  // Unknown channels
            new() { FileName = "cs_intro_music", IsMono = false, ChannelUnknown = false },  // Stereo
        };
    }

    [Fact]
    public void ApplyFilters_NoFilters_ReturnsAllSortedAlphabetically()
    {
        var sounds = CreateTestSounds();

        var result = SoundFilterService.ApplyFilters(sounds, monoOnly: false, searchText: null);

        Assert.Equal(6, result.Count);
        Assert.Equal("al_mg_beam01", result[0].FileName);
        Assert.Equal("as_cv_bellshrt1", result[1].FileName);
        Assert.Equal("cs_intro_music", result[2].FileName);
        Assert.Equal("mus_battle_01", result[3].FileName);
        Assert.Equal("vs_femelf_att1", result[4].FileName);
        Assert.Equal("vs_femelf_att2", result[5].FileName);
    }

    [Fact]
    public void ApplyFilters_MonoOnly_ExcludesStereo()
    {
        var sounds = CreateTestSounds();

        var result = SoundFilterService.ApplyFilters(sounds, monoOnly: true, searchText: null);

        // 3 mono + 1 channel-unknown = 4
        Assert.Equal(4, result.Count);
        Assert.DoesNotContain(result, s => s.FileName == "mus_battle_01");
        Assert.DoesNotContain(result, s => s.FileName == "cs_intro_music");
    }

    [Fact]
    public void ApplyFilters_MonoOnly_IncludesChannelUnknown()
    {
        var sounds = CreateTestSounds();

        var result = SoundFilterService.ApplyFilters(sounds, monoOnly: true, searchText: null);

        Assert.Contains(result, s => s.FileName == "as_cv_bellshrt1");
    }

    [Fact]
    public void ApplyFilters_TextSearch_CaseInsensitive()
    {
        var sounds = CreateTestSounds();

        var result = SoundFilterService.ApplyFilters(sounds, monoOnly: false, searchText: "FEMELF");

        Assert.Equal(2, result.Count);
        Assert.All(result, s => Assert.Contains("femelf", s.FileName));
    }

    [Fact]
    public void ApplyFilters_TextSearch_SubstringMatch()
    {
        var sounds = CreateTestSounds();

        var result = SoundFilterService.ApplyFilters(sounds, monoOnly: false, searchText: "att");

        Assert.Equal(3, result.Count); // mus_battle_01, vs_femelf_att1, vs_femelf_att2
    }

    [Fact]
    public void ApplyFilters_MonoAndTextSearch_CombinesFilters()
    {
        var sounds = CreateTestSounds();

        // "att" matches: mus_battle_01 (stereo), vs_femelf_att1 (mono), vs_femelf_att2 (mono)
        // Mono filter removes mus_battle_01
        var result = SoundFilterService.ApplyFilters(sounds, monoOnly: true, searchText: "att");

        Assert.Equal(2, result.Count);
        Assert.All(result, s => Assert.Contains("femelf", s.FileName));
    }

    [Fact]
    public void ApplyFilters_EmptyCollection_ReturnsEmpty()
    {
        var sounds = new List<SoundFileInfo>();

        var result = SoundFilterService.ApplyFilters(sounds, monoOnly: true, searchText: "test");

        Assert.Empty(result);
    }

    [Fact]
    public void ApplyFilters_NoMatches_ReturnsEmpty()
    {
        var sounds = CreateTestSounds();

        var result = SoundFilterService.ApplyFilters(sounds, monoOnly: false, searchText: "zzz_nonexistent");

        Assert.Empty(result);
    }

    [Fact]
    public void ApplyFilters_AllFiltersUnchecked_ReturnsAll()
    {
        var sounds = CreateTestSounds();

        var result = SoundFilterService.ApplyFilters(sounds, monoOnly: false, searchText: null);

        Assert.Equal(sounds.Count, result.Count);
    }

    [Fact]
    public void ApplyFilters_WhitespaceSearch_TreatedAsNoFilter()
    {
        var sounds = CreateTestSounds();

        var result = SoundFilterService.ApplyFilters(sounds, monoOnly: false, searchText: "   ");

        Assert.Equal(sounds.Count, result.Count);
    }
}
