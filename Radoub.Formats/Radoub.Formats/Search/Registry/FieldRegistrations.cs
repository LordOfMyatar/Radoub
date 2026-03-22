using Radoub.Formats.Common;

namespace Radoub.Formats.Search;

/// <summary>
/// Static registration of all known searchable fields per file type.
/// Call RegisterAll() at startup before any search operations.
/// </summary>
public static class FieldRegistrations
{
    public static void RegisterAll(SearchFieldRegistry registry)
    {
        RegisterDlg(registry);
        RegisterUtc(registry);
        RegisterUti(registry);
        RegisterUtm(registry);
        RegisterJrl(registry);
        RegisterIfo(registry);
    }

    private static void RegisterDlg(SearchFieldRegistry registry)
    {
        registry.RegisterFileType(ResourceTypes.Dlg,
            // Content
            new FieldDefinition
            {
                Name = "Text", GffPath = "Text",
                FieldType = SearchFieldType.LocString,
                Category = SearchFieldCategory.Content,
                Description = "Dialog text (entries and replies)"
            },
            // Identity
            new FieldDefinition
            {
                Name = "Speaker", GffPath = "Speaker",
                FieldType = SearchFieldType.Text,
                Category = SearchFieldCategory.Identity,
                Description = "NPC speaker tag"
            },
            // Scripts
            new FieldDefinition
            {
                Name = "Action Script", GffPath = "Script",
                FieldType = SearchFieldType.Script,
                Category = SearchFieldCategory.Script,
                Description = "Script executed when node is reached"
            },
            new FieldDefinition
            {
                Name = "Action Params", GffPath = "ActionParams",
                FieldType = SearchFieldType.ScriptParam,
                Category = SearchFieldCategory.Script,
                Description = "Parameters passed to action script"
            },
            new FieldDefinition
            {
                Name = "Condition Script", GffPath = "Active",
                FieldType = SearchFieldType.Script,
                Category = SearchFieldCategory.Script,
                Description = "Condition script on links"
            },
            new FieldDefinition
            {
                Name = "Condition Params", GffPath = "ConditionParams",
                FieldType = SearchFieldType.ScriptParam,
                Category = SearchFieldCategory.Script,
                Description = "Parameters passed to condition script"
            },
            new FieldDefinition
            {
                Name = "End Conversation", GffPath = "EndConversation",
                FieldType = SearchFieldType.Script,
                Category = SearchFieldCategory.Script,
                Description = "Script on normal conversation end"
            },
            new FieldDefinition
            {
                Name = "End Conversation Abort", GffPath = "EndConverAbort",
                FieldType = SearchFieldType.Script,
                Category = SearchFieldCategory.Script,
                Description = "Script on aborted conversation"
            },
            // Metadata
            new FieldDefinition
            {
                Name = "Sound", GffPath = "Sound",
                FieldType = SearchFieldType.ResRef,
                Category = SearchFieldCategory.Metadata,
                Description = "Sound file reference"
            },
            new FieldDefinition
            {
                Name = "Quest", GffPath = "Quest",
                FieldType = SearchFieldType.Tag,
                Category = SearchFieldCategory.Metadata,
                Description = "Quest tag for journal updates"
            },
            new FieldDefinition
            {
                Name = "Comment", GffPath = "Comment",
                FieldType = SearchFieldType.Text,
                Category = SearchFieldCategory.Metadata,
                Description = "Toolset comment (not shown in-game)"
            }
        );
    }

    private static void RegisterUtc(SearchFieldRegistry registry)
    {
        registry.RegisterFileType(ResourceTypes.Utc,
            new FieldDefinition { Name = "First Name", GffPath = "FirstName", FieldType = SearchFieldType.LocString, Category = SearchFieldCategory.Content, Description = "Creature first name" },
            new FieldDefinition { Name = "Last Name", GffPath = "LastName", FieldType = SearchFieldType.LocString, Category = SearchFieldCategory.Content, Description = "Creature last name" },
            new FieldDefinition { Name = "Description", GffPath = "Description", FieldType = SearchFieldType.LocString, Category = SearchFieldCategory.Content, Description = "Creature description" },
            new FieldDefinition { Name = "Tag", GffPath = "Tag", FieldType = SearchFieldType.Tag, Category = SearchFieldCategory.Identity, Description = "Creature tag" },
            new FieldDefinition { Name = "Template ResRef", GffPath = "TemplateResRef", FieldType = SearchFieldType.ResRef, Category = SearchFieldCategory.Identity, Description = "Blueprint resource reference" },
            new FieldDefinition { Name = "Comment", GffPath = "Comment", FieldType = SearchFieldType.Text, Category = SearchFieldCategory.Metadata, Description = "Toolset comment" },
            new FieldDefinition { Name = "Subrace", GffPath = "Subrace", FieldType = SearchFieldType.Text, Category = SearchFieldCategory.Identity, Description = "Creature subrace" },
            new FieldDefinition { Name = "Deity", GffPath = "Deity", FieldType = SearchFieldType.Text, Category = SearchFieldCategory.Identity, Description = "Creature deity" },
            new FieldDefinition { Name = "Conversation", GffPath = "Conversation", FieldType = SearchFieldType.ResRef, Category = SearchFieldCategory.Metadata, Description = "Default conversation file" },
            new FieldDefinition { Name = "Local Variables", GffPath = "VarTable", FieldType = SearchFieldType.Variable, Category = SearchFieldCategory.Variable, Description = "Local variable names and string values" },
            new FieldDefinition { Name = "ScriptAttacked", GffPath = "ScriptAttacked", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "OnPhysicalAttacked event script" },
            new FieldDefinition { Name = "ScriptDamaged", GffPath = "ScriptDamaged", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "OnDamaged event script" },
            new FieldDefinition { Name = "ScriptDeath", GffPath = "ScriptDeath", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "OnDeath event script" },
            new FieldDefinition { Name = "ScriptDialogue", GffPath = "ScriptDialogue", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "OnConversation event script" },
            new FieldDefinition { Name = "ScriptDisturbed", GffPath = "ScriptDisturbed", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "OnDisturbed event script" },
            new FieldDefinition { Name = "ScriptEndRound", GffPath = "ScriptEndRound", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "OnEndCombatRound event script" },
            new FieldDefinition { Name = "ScriptHeartbeat", GffPath = "ScriptHeartbeat", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "OnHeartbeat event script" },
            new FieldDefinition { Name = "ScriptOnBlocked", GffPath = "ScriptOnBlocked", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "OnBlocked event script" },
            new FieldDefinition { Name = "ScriptOnNotice", GffPath = "ScriptOnNotice", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "OnPerception event script" },
            new FieldDefinition { Name = "ScriptRested", GffPath = "ScriptRested", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "OnRested event script" },
            new FieldDefinition { Name = "ScriptSpawn", GffPath = "ScriptSpawn", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "OnSpawn event script" },
            new FieldDefinition { Name = "ScriptSpellAt", GffPath = "ScriptSpellAt", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "OnSpellCastAt event script" },
            new FieldDefinition { Name = "ScriptUserDefine", GffPath = "ScriptUserDefine", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "OnUserDefined event script" }
        );

        // BIC uses the same fields as UTC
        var utcFields = registry.GetSearchableFields(ResourceTypes.Utc);
        registry.RegisterFileType(ResourceTypes.Bic, utcFields.ToArray());
    }

    private static void RegisterUti(SearchFieldRegistry registry)
    {
        registry.RegisterFileType(ResourceTypes.Uti,
            new FieldDefinition { Name = "Name", GffPath = "LocalizedName", FieldType = SearchFieldType.LocString, Category = SearchFieldCategory.Content, Description = "Item name" },
            new FieldDefinition { Name = "Description", GffPath = "Description", FieldType = SearchFieldType.LocString, Category = SearchFieldCategory.Content, Description = "Unidentified description" },
            new FieldDefinition { Name = "Identified Description", GffPath = "DescIdentified", FieldType = SearchFieldType.LocString, Category = SearchFieldCategory.Content, Description = "Identified item description" },
            new FieldDefinition { Name = "Tag", GffPath = "Tag", FieldType = SearchFieldType.Tag, Category = SearchFieldCategory.Identity, Description = "Item tag" },
            new FieldDefinition { Name = "Template ResRef", GffPath = "TemplateResRef", FieldType = SearchFieldType.ResRef, Category = SearchFieldCategory.Identity, Description = "Blueprint resource reference" },
            new FieldDefinition { Name = "Comment", GffPath = "Comment", FieldType = SearchFieldType.Text, Category = SearchFieldCategory.Metadata, Description = "Toolset comment" }
        );
    }

    private static void RegisterUtm(SearchFieldRegistry registry)
    {
        registry.RegisterFileType(ResourceTypes.Utm,
            new FieldDefinition { Name = "Name", GffPath = "LocName", FieldType = SearchFieldType.LocString, Category = SearchFieldCategory.Content, Description = "Store name" },
            new FieldDefinition { Name = "Tag", GffPath = "Tag", FieldType = SearchFieldType.Tag, Category = SearchFieldCategory.Identity, Description = "Store tag" },
            new FieldDefinition { Name = "ResRef", GffPath = "ResRef", FieldType = SearchFieldType.ResRef, Category = SearchFieldCategory.Identity, Description = "Store resource reference" },
            new FieldDefinition { Name = "Comment", GffPath = "Comment", FieldType = SearchFieldType.Text, Category = SearchFieldCategory.Metadata, Description = "Toolset comment" },
            new FieldDefinition { Name = "OnOpenStore", GffPath = "OnOpenStore", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "Script when store opened" },
            new FieldDefinition { Name = "OnStoreClosed", GffPath = "OnStoreClosed", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "Script when store closed" },
            new FieldDefinition { Name = "Local Variables", GffPath = "VarTable", FieldType = SearchFieldType.Variable, Category = SearchFieldCategory.Variable, Description = "Local variable names and string values" }
        );
    }

    private static void RegisterJrl(SearchFieldRegistry registry)
    {
        registry.RegisterFileType(ResourceTypes.Jrl,
            new FieldDefinition { Name = "Category Name", GffPath = "Name", FieldType = SearchFieldType.LocString, Category = SearchFieldCategory.Content, Description = "Quest/category name" },
            new FieldDefinition { Name = "Category Tag", GffPath = "Tag", FieldType = SearchFieldType.Tag, Category = SearchFieldCategory.Identity, Description = "Quest tag" },
            new FieldDefinition { Name = "Entry Text", GffPath = "Text", FieldType = SearchFieldType.LocString, Category = SearchFieldCategory.Content, Description = "Journal entry text" },
            new FieldDefinition { Name = "Comment", GffPath = "Comment", FieldType = SearchFieldType.Text, Category = SearchFieldCategory.Metadata, Description = "Toolset comment" }
        );
    }

    private static void RegisterIfo(SearchFieldRegistry registry)
    {
        registry.RegisterFileType(ResourceTypes.Ifo,
            new FieldDefinition { Name = "Module Name", GffPath = "Mod_Name", FieldType = SearchFieldType.LocString, Category = SearchFieldCategory.Content, Description = "Module display name" },
            new FieldDefinition { Name = "Module Description", GffPath = "Mod_Description", FieldType = SearchFieldType.LocString, Category = SearchFieldCategory.Content, Description = "Module description" },
            new FieldDefinition { Name = "Tag", GffPath = "Mod_Tag", FieldType = SearchFieldType.Tag, Category = SearchFieldCategory.Identity, Description = "Module tag" }
        );
    }
}
