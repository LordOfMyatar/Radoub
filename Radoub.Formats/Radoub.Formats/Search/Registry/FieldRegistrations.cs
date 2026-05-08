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
        RegisterUtp(registry);
        RegisterUtd(registry);
        RegisterJrl(registry);
        RegisterAre(registry);
        RegisterIfo(registry);
        RegisterGit(registry);
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
                Description = "Sound file reference",
                IsReplaceable = false
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
            new FieldDefinition { Name = "Template ResRef", GffPath = "TemplateResRef", FieldType = SearchFieldType.ResRef, Category = SearchFieldCategory.Identity, Description = "Blueprint resource reference", IsReplaceable = false },
            new FieldDefinition { Name = "Comment", GffPath = "Comment", FieldType = SearchFieldType.Text, Category = SearchFieldCategory.Metadata, Description = "Toolset comment" },
            new FieldDefinition { Name = "Subrace", GffPath = "Subrace", FieldType = SearchFieldType.Text, Category = SearchFieldCategory.Identity, Description = "Creature subrace" },
            new FieldDefinition { Name = "Deity", GffPath = "Deity", FieldType = SearchFieldType.Text, Category = SearchFieldCategory.Identity, Description = "Creature deity" },
            new FieldDefinition { Name = "Conversation", GffPath = "Conversation", FieldType = SearchFieldType.ResRef, Category = SearchFieldCategory.Metadata, Description = "Default conversation file", IsReplaceable = false },
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
            new FieldDefinition { Name = "Template ResRef", GffPath = "TemplateResRef", FieldType = SearchFieldType.ResRef, Category = SearchFieldCategory.Identity, Description = "Blueprint resource reference", IsReplaceable = false },
            new FieldDefinition { Name = "Comment", GffPath = "Comment", FieldType = SearchFieldType.Text, Category = SearchFieldCategory.Metadata, Description = "Toolset comment" }
        );
    }

    private static void RegisterUtm(SearchFieldRegistry registry)
    {
        registry.RegisterFileType(ResourceTypes.Utm,
            new FieldDefinition { Name = "Name", GffPath = "LocName", FieldType = SearchFieldType.LocString, Category = SearchFieldCategory.Content, Description = "Store name" },
            new FieldDefinition { Name = "Tag", GffPath = "Tag", FieldType = SearchFieldType.Tag, Category = SearchFieldCategory.Identity, Description = "Store tag" },
            new FieldDefinition { Name = "ResRef", GffPath = "ResRef", FieldType = SearchFieldType.ResRef, Category = SearchFieldCategory.Identity, Description = "Store resource reference", IsReplaceable = false },
            new FieldDefinition { Name = "Comment", GffPath = "Comment", FieldType = SearchFieldType.Text, Category = SearchFieldCategory.Metadata, Description = "Toolset comment" },
            new FieldDefinition { Name = "OnOpenStore", GffPath = "OnOpenStore", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "Script when store opened" },
            new FieldDefinition { Name = "OnStoreClosed", GffPath = "OnStoreClosed", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "Script when store closed" },
            new FieldDefinition { Name = "Local Variables", GffPath = "VarTable", FieldType = SearchFieldType.Variable, Category = SearchFieldCategory.Variable, Description = "Local variable names and string values" }
        );
    }

    private static void RegisterUtp(SearchFieldRegistry registry)
    {
        registry.RegisterFileType(ResourceTypes.Utp,
            new FieldDefinition { Name = "Name", GffPath = "LocName", FieldType = SearchFieldType.LocString, Category = SearchFieldCategory.Content, Description = "Placeable name" },
            new FieldDefinition { Name = "Description", GffPath = "Description", FieldType = SearchFieldType.LocString, Category = SearchFieldCategory.Content, Description = "Placeable description" },
            new FieldDefinition { Name = "Tag", GffPath = "Tag", FieldType = SearchFieldType.Tag, Category = SearchFieldCategory.Identity, Description = "Placeable tag" },
            new FieldDefinition { Name = "ResRef", GffPath = "TemplateResRef", FieldType = SearchFieldType.ResRef, Category = SearchFieldCategory.Identity, Description = "Placeable resource reference", IsReplaceable = false },
            new FieldDefinition { Name = "Comment", GffPath = "Comment", FieldType = SearchFieldType.Text, Category = SearchFieldCategory.Metadata, Description = "Toolset comment" },
            new FieldDefinition { Name = "Conversation", GffPath = "Conversation", FieldType = SearchFieldType.ResRef, Category = SearchFieldCategory.Metadata, Description = "Conversation file reference", IsReplaceable = false },
            new FieldDefinition { Name = "OnClosed", GffPath = "OnClosed", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "OnClosed event script" },
            new FieldDefinition { Name = "OnDamaged", GffPath = "OnDamaged", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "OnDamaged event script" },
            new FieldDefinition { Name = "OnDeath", GffPath = "OnDeath", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "OnDeath event script" },
            new FieldDefinition { Name = "OnDisarm", GffPath = "OnDisarm", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "OnDisarm event script" },
            new FieldDefinition { Name = "OnHeartbeat", GffPath = "OnHeartbeat", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "OnHeartbeat event script" },
            new FieldDefinition { Name = "OnInvDisturbed", GffPath = "OnInvDisturbed", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "OnInventoryDisturbed event script" },
            new FieldDefinition { Name = "OnLock", GffPath = "OnLock", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "OnLock event script" },
            new FieldDefinition { Name = "OnMeleeAttacked", GffPath = "OnMeleeAttacked", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "OnPhysicalAttacked event script" },
            new FieldDefinition { Name = "OnOpen", GffPath = "OnOpen", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "OnOpen event script" },
            new FieldDefinition { Name = "OnSpellCastAt", GffPath = "OnSpellCastAt", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "OnSpellCastAt event script" },
            new FieldDefinition { Name = "OnTrapTriggered", GffPath = "OnTrapTriggered", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "OnTrapTriggered event script" },
            new FieldDefinition { Name = "OnUnlock", GffPath = "OnUnlock", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "OnUnlock event script" },
            new FieldDefinition { Name = "OnUserDefined", GffPath = "OnUserDefined", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "OnUserDefined event script" },
            new FieldDefinition { Name = "OnUsed", GffPath = "OnUsed", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "OnUsed event script" },
            new FieldDefinition { Name = "Local Variables", GffPath = "VarTable", FieldType = SearchFieldType.Variable, Category = SearchFieldCategory.Variable, Description = "Local variable names and string values" }
        );
    }

    private static void RegisterUtd(SearchFieldRegistry registry)
    {
        registry.RegisterFileType(ResourceTypes.Utd,
            new FieldDefinition { Name = "Name", GffPath = "LocName", FieldType = SearchFieldType.LocString, Category = SearchFieldCategory.Content, Description = "Door name" },
            new FieldDefinition { Name = "Description", GffPath = "Description", FieldType = SearchFieldType.LocString, Category = SearchFieldCategory.Content, Description = "Door description" },
            new FieldDefinition { Name = "Tag", GffPath = "Tag", FieldType = SearchFieldType.Tag, Category = SearchFieldCategory.Identity, Description = "Door tag" },
            new FieldDefinition { Name = "ResRef", GffPath = "TemplateResRef", FieldType = SearchFieldType.ResRef, Category = SearchFieldCategory.Identity, Description = "Door resource reference", IsReplaceable = false },
            new FieldDefinition { Name = "Comment", GffPath = "Comment", FieldType = SearchFieldType.Text, Category = SearchFieldCategory.Metadata, Description = "Toolset comment" },
            new FieldDefinition { Name = "Conversation", GffPath = "Conversation", FieldType = SearchFieldType.ResRef, Category = SearchFieldCategory.Metadata, Description = "Conversation file reference", IsReplaceable = false },
            new FieldDefinition { Name = "LinkedTo", GffPath = "LinkedTo", FieldType = SearchFieldType.Tag, Category = SearchFieldCategory.Metadata, Description = "Area transition target tag" },
            new FieldDefinition { Name = "OnClick", GffPath = "OnClick", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "OnAreaTransitionClick event script" },
            new FieldDefinition { Name = "OnClosed", GffPath = "OnClosed", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "OnClosed event script" },
            new FieldDefinition { Name = "OnDamaged", GffPath = "OnDamaged", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "OnDamaged event script" },
            new FieldDefinition { Name = "OnDeath", GffPath = "OnDeath", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "OnDeath event script" },
            new FieldDefinition { Name = "OnDisarm", GffPath = "OnDisarm", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "OnDisarm event script" },
            new FieldDefinition { Name = "OnFailToOpen", GffPath = "OnFailToOpen", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "OnFailToOpen event script" },
            new FieldDefinition { Name = "OnHeartbeat", GffPath = "OnHeartbeat", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "OnHeartbeat event script" },
            new FieldDefinition { Name = "OnLock", GffPath = "OnLock", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "OnLock event script" },
            new FieldDefinition { Name = "OnMeleeAttacked", GffPath = "OnMeleeAttacked", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "OnPhysicalAttacked event script" },
            new FieldDefinition { Name = "OnOpen", GffPath = "OnOpen", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "OnOpen event script" },
            new FieldDefinition { Name = "OnSpellCastAt", GffPath = "OnSpellCastAt", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "OnSpellCastAt event script" },
            new FieldDefinition { Name = "OnTrapTriggered", GffPath = "OnTrapTriggered", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "OnTrapTriggered event script" },
            new FieldDefinition { Name = "OnUnlock", GffPath = "OnUnlock", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "OnUnlock event script" },
            new FieldDefinition { Name = "OnUserDefined", GffPath = "OnUserDefined", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "OnUserDefined event script" },
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

    private static void RegisterGit(SearchFieldRegistry registry)
    {
        registry.RegisterFileType(ResourceTypes.Git,
            new FieldDefinition { Name = "Tag", GffPath = "Tag", FieldType = SearchFieldType.Tag, Category = SearchFieldCategory.Identity, Description = "Instance tag" },
            new FieldDefinition { Name = "Name", GffPath = "LocName", FieldType = SearchFieldType.LocString, Category = SearchFieldCategory.Content, Description = "Instance name" },
            new FieldDefinition { Name = "Template ResRef", GffPath = "TemplateResRef", FieldType = SearchFieldType.ResRef, Category = SearchFieldCategory.Identity, Description = "Blueprint resource reference", IsReplaceable = false },
            new FieldDefinition { Name = "Local Variables", GffPath = "VarTable", FieldType = SearchFieldType.Variable, Category = SearchFieldCategory.Variable, Description = "Instance local variables" }
        );
    }

    private static void RegisterAre(SearchFieldRegistry registry)
    {
        registry.RegisterFileType(ResourceTypes.Are,
            new FieldDefinition { Name = "Name", GffPath = "Name", FieldType = SearchFieldType.LocString, Category = SearchFieldCategory.Content, Description = "Area name" },
            new FieldDefinition { Name = "Tag", GffPath = "Tag", FieldType = SearchFieldType.Tag, Category = SearchFieldCategory.Identity, Description = "Area scripting tag" },
            new FieldDefinition { Name = "ResRef", GffPath = "ResRef", FieldType = SearchFieldType.ResRef, Category = SearchFieldCategory.Identity, Description = "Area resource reference", IsReplaceable = false },
            new FieldDefinition { Name = "Comments", GffPath = "Comments", FieldType = SearchFieldType.Text, Category = SearchFieldCategory.Metadata, Description = "Toolset comments" },
            new FieldDefinition { Name = "OnEnter", GffPath = "OnEnter", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "OnEnter event script" },
            new FieldDefinition { Name = "OnExit", GffPath = "OnExit", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "OnExit event script" },
            new FieldDefinition { Name = "OnHeartbeat", GffPath = "OnHeartbeat", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "OnHeartbeat event script" },
            new FieldDefinition { Name = "OnUserDefined", GffPath = "OnUserDefined", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "OnUserDefined event script" }
        );
    }

    private static void RegisterIfo(SearchFieldRegistry registry)
    {
        registry.RegisterFileType(ResourceTypes.Ifo,
            new FieldDefinition { Name = "Module Name", GffPath = "Mod_Name", FieldType = SearchFieldType.LocString, Category = SearchFieldCategory.Content, Description = "Module display name" },
            new FieldDefinition { Name = "Module Description", GffPath = "Mod_Description", FieldType = SearchFieldType.LocString, Category = SearchFieldCategory.Content, Description = "Module description" },
            new FieldDefinition { Name = "Tag", GffPath = "Mod_Tag", FieldType = SearchFieldType.Tag, Category = SearchFieldCategory.Identity, Description = "Module tag" },

            // ResRef fields (#1926)
            new FieldDefinition { Name = "Entry Area", GffPath = "Mod_Entry_Area", FieldType = SearchFieldType.ResRef, Category = SearchFieldCategory.Identity, Description = "Area ResRef where players start", IsReplaceable = false },
            new FieldDefinition { Name = "Default BIC", GffPath = "Mod_DefaultBic", FieldType = SearchFieldType.ResRef, Category = SearchFieldCategory.Identity, Description = "Default character (.bic) for new players", IsReplaceable = false },
            new FieldDefinition { Name = "Start Movie", GffPath = "Mod_StartMovie", FieldType = SearchFieldType.ResRef, Category = SearchFieldCategory.Metadata, Description = "Startup movie (.bik) ResRef", IsReplaceable = false },
            new FieldDefinition { Name = "Custom TLK", GffPath = "Mod_CustomTlk", FieldType = SearchFieldType.ResRef, Category = SearchFieldCategory.Metadata, Description = "Custom TLK file ResRef", IsReplaceable = false },
            new FieldDefinition { Name = "HAK", GffPath = "Mod_Hak", FieldType = SearchFieldType.ResRef, Category = SearchFieldCategory.Metadata, Description = "HAK file ResRef (per entry in Mod_HakList)", IsReplaceable = false },

            // Module event scripts (classic NWN)
            new FieldDefinition { Name = "OnModuleLoad", GffPath = "Mod_OnModLoad", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "Script run when module loads" },
            new FieldDefinition { Name = "OnClientEnter", GffPath = "Mod_OnClientEntr", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "Script run when a client enters" },
            new FieldDefinition { Name = "OnClientLeave", GffPath = "Mod_OnClientLeav", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "Script run when a client leaves" },
            new FieldDefinition { Name = "OnHeartbeat", GffPath = "Mod_OnHeartbeat", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "Module heartbeat script" },
            new FieldDefinition { Name = "OnAcquireItem", GffPath = "Mod_OnAcquirItem", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "Script run when an item is acquired" },
            new FieldDefinition { Name = "OnActivateItem", GffPath = "Mod_OnActvtItem", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "Script run when an item is activated" },
            new FieldDefinition { Name = "OnUnacquireItem", GffPath = "Mod_OnUnAqreItem", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "Script run when an item is unacquired" },
            new FieldDefinition { Name = "OnPlayerDeath", GffPath = "Mod_OnPlrDeath", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "Script run when a player dies" },
            new FieldDefinition { Name = "OnPlayerDying", GffPath = "Mod_OnPlrDying", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "Script run when a player is dying" },
            new FieldDefinition { Name = "OnPlayerRest", GffPath = "Mod_OnPlrRest", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "Script run when a player rests" },
            new FieldDefinition { Name = "OnPlayerEquipItem", GffPath = "Mod_OnPlrEqItm", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "Script run when a player equips an item" },
            new FieldDefinition { Name = "OnPlayerUnequipItem", GffPath = "Mod_OnPlrUnEqItm", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "Script run when a player unequips an item" },
            new FieldDefinition { Name = "OnPlayerLevelUp", GffPath = "Mod_OnPlrLvlUp", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "Script run when a player levels up" },
            new FieldDefinition { Name = "OnUserDefined", GffPath = "Mod_OnUsrDefined", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "Script run on user-defined event" },
            new FieldDefinition { Name = "OnSpawnButtonDown", GffPath = "Mod_OnSpawnBtnDn", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "Script run when respawn button is pressed" },
            new FieldDefinition { Name = "OnCutsceneAbort", GffPath = "Mod_OnCutsnAbort", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "Script run when a cutscene is aborted" },

            // Module event scripts (NWN:EE)
            new FieldDefinition { Name = "OnModuleStart", GffPath = "Mod_OnModStart", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "Script run after OnModuleLoad (NWN:EE)" },
            new FieldDefinition { Name = "OnPlayerChat", GffPath = "Mod_OnPlrChat", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "Script run when a player chats (NWN:EE)" },
            new FieldDefinition { Name = "OnPlayerTarget", GffPath = "Mod_OnPlrTarget", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "Script run when a player targets something (NWN:EE)" },
            new FieldDefinition { Name = "OnPlayerGuiEvent", GffPath = "Mod_OnPlrGuiEvt", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "Script run on player GUI event (NWN:EE)" },
            new FieldDefinition { Name = "OnPlayerTileAction", GffPath = "Mod_OnPlrTileAct", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "Script run on player tile action (NWN:EE)" },
            new FieldDefinition { Name = "OnNuiEvent", GffPath = "Mod_OnNuiEvent", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "Script run on NUI event (NWN:EE 1.80+)" }
        );
    }
}
