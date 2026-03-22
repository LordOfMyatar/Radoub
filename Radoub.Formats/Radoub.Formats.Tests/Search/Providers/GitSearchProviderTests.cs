using Radoub.Formats.Gff;
using Radoub.Formats.Search;
using Xunit;
using static Radoub.Formats.Gff.GffFieldBuilder;

namespace Radoub.Formats.Tests.Search.Providers;

public class GitSearchProviderTests
{
    /// <summary>
    /// Build a minimal GIT-format GFF with placed instances for testing.
    /// </summary>
    private static GffFile CreateTestGit()
    {
        var rootStruct = new GffStruct
        {
            Fields = new List<GffField>
            {
                // Creature List with one creature
                MakeList("Creature List", new List<GffStruct>
                {
                    MakeInstanceStruct(4, "LOUIS_ROMAIN", "Louis Romain", "louis_romain",
                        scripts: new Dictionary<string, string>
                        {
                            ["ScriptHeartbeat"] = "nw_c2_default1",
                            ["ScriptSpawn"] = "nw_c2_default9"
                        },
                        varTable: new List<(string name, VariableType type, object value)>
                        {
                            ("nBossState", VariableType.Int, 0),
                            ("sGreeting", VariableType.String, "Welcome, adventurer")
                        })
                }),

                // Door List with one door
                MakeList("Door List", new List<GffStruct>
                {
                    MakeInstanceStruct(8, "TAVERN_DOOR", "Tavern Door", "door_wood",
                        scripts: new Dictionary<string, string>
                        {
                            ["OnOpen"] = "gc_door_open"
                        })
                }),

                // Placeable List with one placeable
                MakeList("Placeable List", new List<GffStruct>
                {
                    MakeInstanceStruct(9, "CHEST_REWARD", "Reward Chest", "plc_chest",
                        description: "A locked chest containing quest rewards.")
                }),

                // WaypointList with one waypoint (uses LocalizedName, not LocName)
                MakeList("WaypointList", new List<GffStruct>
                {
                    MakeWaypointStruct("WP_SPAWN_01", "Spawn Point Alpha", "nw_waypoint001",
                        mapNote: "Main spawn location")
                }),

                // TriggerList with one trigger
                MakeList("TriggerList", new List<GffStruct>
                {
                    MakeTriggerStruct("AREA_TRANS_01", "Area Transition", "newtransition",
                        linkedTo: "WP_SPAWN_01")
                }),

                // SoundList with one sound
                MakeList("SoundList", new List<GffStruct>
                {
                    MakeInstanceStruct(6, "AMB_TAVERN", "Tavern Ambience", "snd_tavern")
                }),

                // StoreList with one store
                MakeList("StoreList", new List<GffStruct>
                {
                    MakeInstanceStruct(11, "LOUIS_SHOP", "Louis's Shop", "louis_shop")
                }),

                // Encounter List with one encounter
                MakeList("Encounter List", new List<GffStruct>
                {
                    MakeEncounterStruct("ENC_BANDITS", "Bandit Ambush", "enc_bandits")
                })
            }
        };

        return new GffFile
        {
            FileType = "GIT ",
            FileVersion = "V3.28",
            RootStruct = rootStruct
        };
    }

    private static GffField MakeList(string label, List<GffStruct> elements)
    {
        return new GffField
        {
            Label = label,
            Type = GffField.List,
            Value = new GffList { Elements = elements }
        };
    }

    private static GffStruct MakeInstanceStruct(int structId, string tag, string locName, string templateResRef,
        Dictionary<string, string>? scripts = null, string? description = null,
        List<(string name, VariableType type, object value)>? varTable = null)
    {
        var fields = new List<GffField>
        {
            new GffField { Label = "Tag", Type = GffField.CExoString, Value = tag },
            new GffField { Label = "LocName", Type = GffField.CExoLocString, Value = new CExoLocString
            {
                LocalizedStrings = new Dictionary<uint, string> { [0] = locName }
            }},
            new GffField { Label = "TemplateResRef", Type = GffField.CResRef, Value = templateResRef }
        };

        if (description != null)
        {
            fields.Add(new GffField { Label = "Description", Type = GffField.CExoLocString, Value = new CExoLocString
            {
                LocalizedStrings = new Dictionary<uint, string> { [0] = description }
            }});
        }

        if (scripts != null)
        {
            foreach (var (scriptLabel, scriptValue) in scripts)
            {
                fields.Add(new GffField { Label = scriptLabel, Type = GffField.CResRef, Value = scriptValue });
            }
        }

        if (varTable != null)
        {
            var varElements = new List<GffStruct>();
            foreach (var (name, type, value) in varTable)
            {
                var varFields = new List<GffField>
                {
                    new GffField { Label = "Name", Type = GffField.CExoString, Value = name },
                    new GffField { Label = "Type", Type = GffField.DWORD, Value = (uint)type }
                };
                if (type == VariableType.String)
                    varFields.Add(new GffField { Label = "Value", Type = GffField.CExoString, Value = (string)value });
                else if (type == VariableType.Int)
                    varFields.Add(new GffField { Label = "Value", Type = GffField.INT, Value = (int)value });
                varElements.Add(new GffStruct { Type = 0, Fields = varFields });
            }
            fields.Add(new GffField { Label = "VarTable", Type = GffField.List, Value = new GffList { Elements = varElements } });
        }

        return new GffStruct { Type = (uint)structId, Fields = fields };
    }

    private static GffStruct MakeWaypointStruct(string tag, string localizedName, string templateResRef, string? mapNote = null)
    {
        var fields = new List<GffField>
        {
            new GffField { Label = "Tag", Type = GffField.CExoString, Value = tag },
            new GffField { Label = "LocalizedName", Type = GffField.CExoLocString, Value = new CExoLocString
            {
                LocalizedStrings = new Dictionary<uint, string> { [0] = localizedName }
            }},
            new GffField { Label = "TemplateResRef", Type = GffField.CResRef, Value = templateResRef }
        };

        if (mapNote != null)
        {
            fields.Add(new GffField { Label = "MapNote", Type = GffField.CExoLocString, Value = new CExoLocString
            {
                LocalizedStrings = new Dictionary<uint, string> { [0] = mapNote }
            }});
        }

        return new GffStruct { Type = 5, Fields = fields };
    }

    private static GffStruct MakeTriggerStruct(string tag, string localizedName, string templateResRef, string? linkedTo = null)
    {
        var fields = new List<GffField>
        {
            new GffField { Label = "Tag", Type = GffField.CExoString, Value = tag },
            new GffField { Label = "LocalizedName", Type = GffField.CExoLocString, Value = new CExoLocString
            {
                LocalizedStrings = new Dictionary<uint, string> { [0] = localizedName }
            }},
            new GffField { Label = "TemplateResRef", Type = GffField.CResRef, Value = templateResRef }
        };

        if (linkedTo != null)
        {
            fields.Add(new GffField { Label = "LinkedTo", Type = GffField.CExoString, Value = linkedTo });
        }

        return new GffStruct { Type = 1, Fields = fields };
    }

    private static GffStruct MakeEncounterStruct(string tag, string localizedName, string templateResRef)
    {
        return new GffStruct
        {
            Type = 7,
            Fields = new List<GffField>
            {
                new GffField { Label = "Tag", Type = GffField.CExoString, Value = tag },
                new GffField { Label = "LocalizedName", Type = GffField.CExoLocString, Value = new CExoLocString
                {
                    LocalizedStrings = new Dictionary<uint, string> { [0] = localizedName }
                }},
                new GffField { Label = "TemplateResRef", Type = GffField.CResRef, Value = templateResRef }
            }
        };
    }

    [Fact]
    public void Search_FindsCreatureTag()
    {
        var provider = new GitSearchProvider();
        var criteria = new SearchCriteria { Pattern = "LOUIS_ROMAIN" };

        var matches = provider.Search(CreateTestGit(), criteria);

        Assert.Contains(matches, m =>
            m.MatchedText == "LOUIS_ROMAIN" &&
            m.Location is GitMatchLocation loc && loc.InstanceType == "Creature");
    }

    [Fact]
    public void Search_FindsCreatureName()
    {
        var provider = new GitSearchProvider();
        var criteria = new SearchCriteria { Pattern = "Louis Romain" };

        var matches = provider.Search(CreateTestGit(), criteria);

        Assert.Contains(matches, m =>
            m.MatchedText == "Louis Romain" &&
            m.Location is GitMatchLocation loc && loc.InstanceType == "Creature");
    }

    [Fact]
    public void Search_FindsCreatureScript()
    {
        var provider = new GitSearchProvider();
        var criteria = new SearchCriteria { Pattern = "nw_c2_default1" };

        var matches = provider.Search(CreateTestGit(), criteria);

        Assert.Contains(matches, m =>
            m.Location is GitMatchLocation loc && loc.InstanceType == "Creature");
    }

    [Fact]
    public void Search_FindsCreatureVarTableName()
    {
        var provider = new GitSearchProvider();
        var criteria = new SearchCriteria { Pattern = "nBossState" };

        var matches = provider.Search(CreateTestGit(), criteria);

        Assert.Contains(matches, m =>
            m.Location is GitMatchLocation loc && loc.InstanceType == "Creature");
    }

    [Fact]
    public void Search_FindsCreatureVarTableStringValue()
    {
        var provider = new GitSearchProvider();
        var criteria = new SearchCriteria { Pattern = "adventurer" };

        var matches = provider.Search(CreateTestGit(), criteria);

        Assert.Contains(matches, m =>
            m.Location is GitMatchLocation loc && loc.InstanceType == "Creature");
    }

    [Fact]
    public void Search_FindsDoorTag()
    {
        var provider = new GitSearchProvider();
        var criteria = new SearchCriteria { Pattern = "TAVERN_DOOR" };

        var matches = provider.Search(CreateTestGit(), criteria);

        Assert.Contains(matches, m =>
            m.Location is GitMatchLocation loc && loc.InstanceType == "Door");
    }

    [Fact]
    public void Search_FindsDoorScript()
    {
        var provider = new GitSearchProvider();
        var criteria = new SearchCriteria { Pattern = "gc_door_open" };

        var matches = provider.Search(CreateTestGit(), criteria);

        Assert.Contains(matches, m =>
            m.Location is GitMatchLocation loc && loc.InstanceType == "Door");
    }

    [Fact]
    public void Search_FindsPlaceableDescription()
    {
        var provider = new GitSearchProvider();
        var criteria = new SearchCriteria { Pattern = "quest rewards" };

        var matches = provider.Search(CreateTestGit(), criteria);

        Assert.Contains(matches, m =>
            m.Location is GitMatchLocation loc && loc.InstanceType == "Placeable");
    }

    [Fact]
    public void Search_FindsWaypointName()
    {
        var provider = new GitSearchProvider();
        var criteria = new SearchCriteria { Pattern = "Spawn Point Alpha" };

        var matches = provider.Search(CreateTestGit(), criteria);

        Assert.Contains(matches, m =>
            m.Location is GitMatchLocation loc && loc.InstanceType == "Waypoint");
    }

    [Fact]
    public void Search_FindsWaypointMapNote()
    {
        var provider = new GitSearchProvider();
        var criteria = new SearchCriteria { Pattern = "Main spawn" };

        var matches = provider.Search(CreateTestGit(), criteria);

        Assert.Contains(matches, m =>
            m.Location is GitMatchLocation loc && loc.InstanceType == "Waypoint");
    }

    [Fact]
    public void Search_FindsTriggerLinkedTo()
    {
        var provider = new GitSearchProvider();
        var criteria = new SearchCriteria { Pattern = "WP_SPAWN_01" };

        var matches = provider.Search(CreateTestGit(), criteria);

        // Should find in both Trigger LinkedTo and Waypoint Tag
        Assert.Contains(matches, m =>
            m.Location is GitMatchLocation loc && loc.InstanceType == "Trigger");
    }

    [Fact]
    public void Search_FindsSoundTag()
    {
        var provider = new GitSearchProvider();
        var criteria = new SearchCriteria { Pattern = "AMB_TAVERN" };

        var matches = provider.Search(CreateTestGit(), criteria);

        Assert.Contains(matches, m =>
            m.Location is GitMatchLocation loc && loc.InstanceType == "Sound");
    }

    [Fact]
    public void Search_FindsStoreTag()
    {
        var provider = new GitSearchProvider();
        var criteria = new SearchCriteria { Pattern = "LOUIS_SHOP" };

        var matches = provider.Search(CreateTestGit(), criteria);

        Assert.Contains(matches, m =>
            m.Location is GitMatchLocation loc && loc.InstanceType == "Store");
    }

    [Fact]
    public void Search_FindsEncounterTag()
    {
        var provider = new GitSearchProvider();
        var criteria = new SearchCriteria { Pattern = "ENC_BANDITS" };

        var matches = provider.Search(CreateTestGit(), criteria);

        Assert.Contains(matches, m =>
            m.Location is GitMatchLocation loc && loc.InstanceType == "Encounter");
    }

    [Fact]
    public void Search_Location_HasInstanceTypeAndIndex()
    {
        var provider = new GitSearchProvider();
        var criteria = new SearchCriteria { Pattern = "LOUIS_ROMAIN" };

        var matches = provider.Search(CreateTestGit(), criteria);

        var match = matches.First(m => m.Location is GitMatchLocation loc && loc.InstanceType == "Creature");
        var loc = (GitMatchLocation)match.Location!;
        Assert.Equal(0, loc.InstanceIndex);
        Assert.Equal("LOUIS_ROMAIN", loc.InstanceTag);
    }

    [Fact]
    public void Search_Location_DisplayPath()
    {
        var provider = new GitSearchProvider();
        var criteria = new SearchCriteria { Pattern = "CHEST_REWARD" };

        var matches = provider.Search(CreateTestGit(), criteria);

        var match = Assert.Single(matches);
        var loc = Assert.IsType<GitMatchLocation>(match.Location);
        Assert.Contains("Placeable", loc.DisplayPath);
        Assert.Contains("#0", loc.DisplayPath);
    }

    [Fact]
    public void Search_AcrossAllInstanceTypes()
    {
        var provider = new GitSearchProvider();
        // TemplateResRef search — every instance has one
        var criteria = new SearchCriteria { Pattern = "louis", CaseSensitive = false };

        var matches = provider.Search(CreateTestGit(), criteria);

        // Creature: tag + name + resref, Store: tag + name + resref
        Assert.True(matches.Count >= 4);
    }

    [Fact]
    public void FileType_IsGit()
    {
        var provider = new GitSearchProvider();
        Assert.Equal(Radoub.Formats.Common.ResourceTypes.Git, provider.FileType);
    }

    [Fact]
    public void Extensions_ContainsGit()
    {
        var provider = new GitSearchProvider();
        Assert.Contains(".git", provider.Extensions);
    }
}
