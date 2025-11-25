# BioWare Aurora Engine - Creature Format

## Chapter 2: Creature Struct

[← Back to Main Document](../Bioware_Aurora_Creature_Format.md) | [← Previous: Introduction](Ch1_Introduction.md)

---

The tables in this section describe the GFF Struct for a Creature. Some Fields are only present on Instances and others only on Blueprints. Still others are present only in toolset data or only in the savegames.

For List Fields, the tables indicate the StructID used by the List elements.

## 2.1 Common Creature Fields

### 2.1.1. Fields in All Creatures

The Table below lists the Fields that are present in all Creature Structs, regardless of whether they are found in blueprints, instances, toolset data, or game data.

**Table 2.1.1:** Fields in all Creature Structs

| **Label**        | **Type**      | **Description**                                                                                                                                                                                                                                                                                                                                                                                                                                                                          |
| ---------------- | ------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Appearance_Type  | WORD          | Index into `appearance.2da`.                                                                                                                                                                                                                                                                                                                                                                                                                                                             |
| BodyBag          | BYTE          | Index into `bodybag.2da`. Specifies the appearance of the bodybag that this Creature leaves behind after its corpse fades, if it had dropped any Items on death, and if the _Lootable_ Field is 0. See **Table 4.5.2** in the Doors and Placeable Objects document.                                                                                                                                                                                                                      |
| Cha              | BYTE          | Charisma Ability Score, before any bonuses/penalties                                                                                                                                                                                                                                                                                                                                                                                                                                     |
| ChallengeRating  | FLOAT         | Calculated Challenge Rating. See **Section 3.1. Challenge Rating**.                                                                                                                                                                                                                                                                                                                                                                                                                      |
| ClassList        | List          | List of Class Structs, having StructID 2. Must always contain at least one element, and can have up to 3 elements. See **Section 2.1.2. Fields in Class Struct**.                                                                                                                                                                                                                                                                                                                        |
| Con              | BYTE          | Constitution Ability Score, before any bonuses/penalties                                                                                                                                                                                                                                                                                                                                                                                                                                 |
| Conversation     | CResRef       | ResRef of the Conversation file for this creature. This conversation runs when a script tells the creature to run ActionStartConversation().                                                                                                                                                                                                                                                                                                                                             |
| CRAdjust         | INT           | Adjustment to the creature's Challenge Rating. To get the Creature's final Challenge Rating, add this value to the _ChallengeRating_ Field. See **Section 3.1** for more details.                                                                                                                                                                                                                                                                                                        |
| CurrentHitPoints | SHORT         | The Creature's current hit points, not counting any bonuses. This value may be higher or lower than the creature's maximum hit points. See **Section 3.4. Hit Points** for more details.                                                                                                                                                                                                                                                                                                 |
| DecayTime        | DWORD         | If the _Lootable_ Field is 1, then this is the number of milliseconds that pass before the creature's corpse fades away after all Items have been removed from the the corpse. If the _Lootable_ Field is 0, then this is the number of milliseconds that pass after the creature dies before its corpse fades away. After the corpse fades, and if the Creature had any Items that dropped on death, the corpse will be replaced by a bodybag placeable object that contains the Items. |
| Deity            | CExoString    | Name of the Creature's Deity. Not used directly by the game, but scripts can check the value of this Field.                                                                                                                                                                                                                                                                                                                                                                              |
| Description      | CExoLocString | Description of the object as seen when using the Examine action in the game.                                                                                                                                                                                                                                                                                                                                                                                                             |
| Dex              | BYTE          | Desterity Ability Score, before any bonuses/penalties                                                                                                                                                                                                                                                                                                                                                                                                                                    |
| Disarmable       | BYTE          | 1 if the Creature can be disarmed, 0if not.                                                                                                                                                                                                                                                                                                                                                                                                                                              |
| Equip_ItemList   | List          | List of EquippedItem Structs. StructID is equal to the item slot bit flag for the equipped item: HEAD 0x1, CHEST 0x2, BOOTS 0x4, ARMS 0x8, RIGHTHAND 0x10, LEFTHAND 0x20, CLOAK 0x40, LEFTRING 0x80, RIGHTRING 0x100, NECK 0x200, BELT 0x400, ARROWS 0x800, BULLETS 0x1000, BOLTS 0x2000. The Structs themselves differs between creature blueprints and instances.                                                                                                                      |
| FactionID        | WORD          | Faction ID of the Creature. This is an index into the _FactionList_ Field of the module s **repute.fac**file.                                                                                                                                                                                                                                                                                                                                                                            |
| FeatList         | List          | List of Feat Structs. StructID 1.                                                                                                                                                                                                                                                                                                                                                                                                                                                        |
| FirstName        | CExoLocString | First name.                                                                                                                                                                                                                                                                                                                                                                                                                                                                              |
| fortbonus        | SHORT         | Fortitude save bonus. Usually 0.                                                                                                                                                                                                                                                                                                                                                                                                                                                         |
| Gender           | BYTE          | Index into `gender.2da`. 0 is assumed to be male, and 1 female, by hardcoded convention. The 2da serves mainly to specify the StrRef to display the name of the gender to the user.                                                                                                                                                                                                                                                                                                      |
| GoodEvil         | BYTE          | Alignment on the Good-Evil axis. 0 is the most Evil value, and 100 is the most Good value.                                                                                                                                                                                                                                                                                                                                                                                               |
| HitPoints        | SHORT         | Base Maximum Hit Points, not considering any bonuses. See **Section 3.4** for more details.                                                                                                                                                                                                                                                                                                                                                                                              |
| Int              | BYTE          | Intelligence Ability Score, before any bonuses/penalties                                                                                                                                                                                                                                                                                                                                                                                                                                 |
| Interruptable    | BYTE          | 1 if a conversation with this creature can be interrupted, 0 otherwise                                                                                                                                                                                                                                                                                                                                                                                                                   |
| IsImmortal       | BYTE          | 1 if the Creature can never die, and can never drop below 1 Hit Point.                                                                                                                                                                                                                                                                                                                                                                                                                   |
| IsPC             | BYTE          | 1 if the Creature is a Player Character; 0 otherwise.                                                                                                                                                                                                                                                                                                                                                                                                                                    |
| ItemList         | List          | List of InventoryObjects in the creature's backpack **See****Section 3. Inventory Objects**, in the Items GFF document.                                                                                                                                                                                                                                                                                                                                                                  |
| LastName         | CExoLocString | Last name.                                                                                                                                                                                                                                                                                                                                                                                                                                                                               |
| LawfulChaotic    | BYTE          | Alignment on the Law-Chaos axis. 0 is the most Chaotic value, and 100 is the most Lawful value.                                                                                                                                                                                                                                                                                                                                                                                          |
| Lootable         | BYTE          | 1 if the Creature leaves behind a lootable corpse. 0 if the Creature leaves behind a bodybag placeable object instead.                                                                                                                                                                                                                                                                                                                                                                   |
| MaxHitPoints     | SHORT         | Maximum Hit Points, after considering all bonuses and penalties.                                                                                                                                                                                                                                                                                                                                                                                                                         |
| NaturalAC        | BYTE          | Natural AC bonus.                                                                                                                                                                                                                                                                                                                                                                                                                                                                        |
| NoPermDeath      | BYTE          | 1 if the Creature cannot permanently die. 0 if the Creature can permanently die. Permanent death is otherwise known as explosive death or chunky death. Note that this setting does not prevent the creature's corpse from fading away when it dies. Corpse fade is a separate mechanism from death itself. To prevent the corpse from fading, call the SetIsDestroyable() scripting function on the Creature with an argument of FALSE, or set the _Lootable_ Field to 1.               |
| PerceptionRange  | BYTE          | Index into `ranges.2da`. Must be 9 to 13.                                                                                                                                                                                                                                                                                                                                                                                                                                                |
| Phenotype        | INT           | Phenotype of the Creature, applicable only if its _Appearance_Type_ Field indexes a row of `appearance.2da` where the _MODELTYPE_ is "P". 0 = normal 1 = fat                                                                                                                                                                                                                                                                                                                             |
| Plot             | BYTE          | 1 if creature is Plot, 0 if not                                                                                                                                                                                                                                                                                                                                                                                                                                                          |
| PortraitId       | WORD          | Index into `portraits.2da`                                                                                                                                                                                                                                                                                                                                                                                                                                                               |
| Race             | BYTE          | Index into `racialtypes.2da`                                                                                                                                                                                                                                                                                                                                                                                                                                                             |
| refbonus         | SHORT         | bonus to Reflex saving throw.                                                                                                                                                                                                                                                                                                                                                                                                                                                            |
| ScriptAttacked   | CResRef       | OnPhysicalAttacked event                                                                                                                                                                                                                                                                                                                                                                                                                                                                 |
| ScriptDamaged    | CResRef       | OnDamaged event                                                                                                                                                                                                                                                                                                                                                                                                                                                                          |
| ScriptDeath      | CResRef       | OnDeath event                                                                                                                                                                                                                                                                                                                                                                                                                                                                            |
| ScriptDialogue   | CResRef       | OnConversation event                                                                                                                                                                                                                                                                                                                                                                                                                                                                     |
| ScriptDisturbed  | CResRef       | OnInventoryDisturbed event                                                                                                                                                                                                                                                                                                                                                                                                                                                               |
| ScriptEndRound   | CResRef       | OnEndCombatRound event                                                                                                                                                                                                                                                                                                                                                                                                                                                                   |
| ScriptHeartbeat  | CResRef       | OnHearbeat event                                                                                                                                                                                                                                                                                                                                                                                                                                                                         |
| ScriptOnBlocked  | CResRef       | OnBlocked event                                                                                                                                                                                                                                                                                                                                                                                                                                                                          |
| ScriptOnNotice   | CResRef       | OnPerception event                                                                                                                                                                                                                                                                                                                                                                                                                                                                       |
| ScriptRested     | CResRef       | OnRested event                                                                                                                                                                                                                                                                                                                                                                                                                                                                           |
| ScriptSpawn      | CResRef       | OnSpawnIn event                                                                                                                                                                                                                                                                                                                                                                                                                                                                          |
| ScriptSpellAt    | CResRef       | OnSpellCastAt event                                                                                                                                                                                                                                                                                                                                                                                                                                                                      |
| ScriptuserDefine | CResRef       | OnUserDefined event                                                                                                                                                                                                                                                                                                                                                                                                                                                                      |
| SkillList        | List          | List of Skill Structs (StructID 0). The index of the Skill Struct in the Creature's _SkillList_ matches up on a one-to-one basis with the rows in `skills.2da`. There should be the same number of elements in _SkillList_ as there are rows in `skills.2da`.                                                                                                                                                                                                                            |
| SoundSetFile     | WORD          | Index into `soundset.2da`. See **Section 7** of the _Sound Set File_ document.                                                                                                                                                                                                                                                                                                                                                                                                           |
| SpecAbilityList  | List          | List of SpecialAbility Structs (StructID 4)                                                                                                                                                                                                                                                                                                                                                                                                                                              |
| StartingPackage  | BYTE          | Index into `packages.2da`. Specifies the package that this creature levels up in when using the LevelUpHenchman() scripting function.                                                                                                                                                                                                                                                                                                                                                    |
| Str              | BYTE          | Strength Ability Score, before any bonuses/penalties                                                                                                                                                                                                                                                                                                                                                                                                                                     |
| Subrace          | CExoString    | Subrace string. Not used by game, but scripts can check this value.                                                                                                                                                                                                                                                                                                                                                                                                                      |
| Tag              | CExoString    | Tag of this object                                                                                                                                                                                                                                                                                                                                                                                                                                                                       |
| Tail             | BYTE          | Index into `tailmodel.2da`.                                                                                                                                                                                                                                                                                                                                                                                                                                                              |
| WalkRate         | INT           | Index into `creaturespeed.2da`.                                                                                                                                                                                                                                                                                                                                                                                                                                                          |
| willbonus        | SHORT         | Bonus to Will saving throw                                                                                                                                                                                                                                                                                                                                                                                                                                                               |
| Wings            | BYTE          | Index into `wingmodel.2da`.                                                                                                                                                                                                                                                                                                                                                                                                                                                              |

### 2.1.2. Fields in Class Struct

The Table below lists the Fields that are present in a Creature Class Struct.

**Table 2.1.2.1:** Fields in Class Struct (StructID 2)

| **Label**  | **Type** | **Description**                                    |
| ---------- | -------- | -------------------------------------------------- |
| Class      | INT      | Index into `classes.2da`.                          |
| ClassLevel | SHORT    | Level in the Class specified by the _Class_ Field. |

Caster classes that prepare their spells, such as Wizards and Clerics, have a list of Prepared spells, as given in the table below. Bards and Sorcerers do not have Memorized lists.

**Table 2.1.2.2:** Additional Fields in Class Struct for Casters that Prepare Spells

| **Label**                                        | **Type** | **Description**          |
| ------------------------------------------------ | -------- | ------------------------ |
| MemorizedList0 MemorizedList1 ... MemorizedList9 | List     | List of memorized spells |

Caster classes that have a limited number of known spells per level keep track of those spells in the knownspell lists, given in the table below. For player characters, the knownspell lists are present for Wizards, Bards, and Sorcerers, but not for divine casters, since divine casters automatically know all spells at each spell level open to them. For nonplayer characters, the knownspell list is only present for Bards and Sorcerers. NPC wizards do not have spellbooks, and can only use the spells in their Memorized lists.

**Table 2.1.2.3:** Additional Fields in Class Struct for Casters that Do Not Prepare Spells

| **Label**                 | **Type** | **Description**      |
| ------------------------- | -------- | -------------------- |
| KnownList0 KnownList1 ... | List     | List of known spells |
| KnownList9                |          |                      |

The MemorizedLists and KnownLists contain Spell Structs. The game and toolset differ in what Fields the Spell Structs contain. Refer to **Table 2.1.5: Toolset Fields in all Spell Structs (known and memorized) (StructID 3)** and **Table 2.5.3: Game Fields in MemorizedSpell Struct (StructID 3)** and **Table 2.5.4: Game Fields in KnownSpell Struct (StructID 3)** for details.

### 2.1.3. Fields in Other Listed Structs

The Table below lists the Fields that are present in a Creature Feat Struct found in the _FeatList_.

**Table 2.1.3.1:** Fields in Feat Struct (StructID 1)

| **Label** | **Type** | **Description**        |
| --------- | -------- | ---------------------- |
| Feat      | WORD     | Index into `feat.2da`. |

The Table below lists the Fields that are present in a Creature Skill Struct found in the _SkillList_.

**Table 2.1.3.2:** Fields in Skill Struct (StructID 0)

| **Label** | **Type** | **Description**                                                                                                                                                                                                                       |
| --------- | -------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Rank      | BYTE     | Skill Rank. The index of the Skill Struct in the Creature's _SkillList_ matches up on a one-to-one basis with the rows in `skills.2da`. There should be the same number of elements in _SkillList_ as there are rows in `skills.2da`. |

The Table below lists the Fields that are present in a Creature Special Ability Struct found in the _SpecAbilList_.

**Table 2.1.3.3:** Fields in SpecialAbility Struct (StructID 4)

| **Label**        | **Type** | **Description**                                                                                                                                            |
| ---------------- | -------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Spell            | WORD     | Index into `spells.2da`.                                                                                                                                   |
| SpellCasterLevel | BYTE     | Spell caster level to cast this spell as                                                                                                                   |
| SpellFlags       | BYTE     | Bit flags. Can have one or more of the following flags: 0x01: readied; this flag is always set by the toolset 0x02: spontaneously cast 0x04: unlimited use |

### 2.2. Creature Blueprint Fields

The Top-Level Struct in a UTC file contains all the Fields in Table 2.1.1 above, plus those in Table 2.2 below.

**Table 2.2:** Fields in Creature Blueprint Structs

| **Label**      | **Type**   | **Description**                                                                                                                                                                                                                                                                                  |
| -------------- | ---------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| Comment        | CExoString | Module designer comment.                                                                                                                                                                                                                                                                         |
| PaletteID      | BYTE       | ID of the node that the Creature Blueprint appears under in the Item palette.                                                                                                                                                                                                                    |
| TemplateResRef | CResRef    | The filename of the UTC file itself. It is an error if this is different. Certain applications check the value of this Field instead of the ResRef of the actual file. If you manually rename a UTC file outside of the toolset, then you must also update the _TemplateResRef_ Field inside it. |

**Table 2.2:** Fields in Creature Blueprint EquippedItem Structs

| **Label** | **Type** | **Description**              |
| --------- | -------- | ---------------------------- |
| EquipRes  | CResRef  | ResRef of the Equipped Item. |

### 2.3. Creature Instance Fields

A Creature Instance Struct in a GIT file contains all the Fields in Table 2.1.1, plus those in Table 2.3 below.

**Table 2.3:** Fields in Creature Instance Structs

| **Label**                     | **Type** | **Description**                                                                        |
| ----------------------------- | -------- | -------------------------------------------------------------------------------------- |
| TemplateResRef                | CResRef  | For instances, this is the ResRef of the blueprint that the instance was created from. |
| XOrientation YOrientation     | FLOAT    | x,y vector pointing in the direction of the creature's orientation                     |
| XPosition YPosition ZPosition | FLOAT    | (x,y,z) coordinates of the Creature within the Area that it is located in.             |

### 2.4. Creature Toolset Fields

Some Creature Fields are only present in blueprints and instances created in the toolset, but not in the game.

**Table 2.1.5:** Toolset Fields in all Spell Structs (known and memorized) (StructID 3)

| **Label**      | **Type** | **Description**                                                                                                                                                                                                                                                                                                                         |
| -------------- | -------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Spell          | WORD     | Index into `spells.2da`.                                                                                                                                                                                                                                                                                                                |
| SpellFlags     | BYTE     | General bit flags. Can have one or more of the following flags: 0x01: readied; this flag is always set by the toolset 0x02: spontaneously cast 0x04: unlimited use                                                                                                                                                                      |
| SpellMetaMagic | BYTE     | Metamagic type. These values look like they can be bit flags, but the game only supports one at a time. Do not add these values together to get multiple metamagic effects on a single spell, because the resulting behaviour is undefined. 0x00: none 0x01: empower 0x02: extend 0x04: maximize 0x08: quicken 0x10: silent 0x20: still |

**Table 2.1.5:** Fields in SpecialAbility Struct (StructID 4)

| **Label**        | **Type** | **Description**                                                                                                                                                      |
| ---------------- | -------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Spell            | WORD     | Index into `spells.2da`.                                                                                                                                             |
| SpellCasterLevel | BYTE     | Spell caster level to cast this spell as                                                                                                                             |
| SpellFlags       | BYTE     | Metamagic bit flags. Can have one or more of the following flags: 0x01: readied; this flag is always set by the toolset 0x02: spontaneously cast 0x04: unlimited use |

### 2.5. Creature Game Instance Fields

After a GIT file has been saved by the game, the Creature Instance Struct contains not just the Fields in Table
2.1.1 and Table 2.3, but also those Fields in Table 2.5.

INVALID_OBJECT_ID is a special constant equal to 0x7f000000 in hex, or 2130706432 in decimal.

**Table 2.5.1:** Fields in Creature Instance Structs in SaveGames

| **Label**        | **Type**   | **Description**                                                                                                         |
| ---------------- | ---------- | ----------------------------------------------------------------------------------------------------------------------- |
| ActionList       | List       | List of Actions queued up on this creature. See **Common GFF Structs** document, **Section 6**.                         |
| Age              | INT        | 0 for non-player creatures. For player characters, this is the Age entered during character creation.                   |
| AmbientAnimState | BYTE       |                                                                                                                         |
| AnimationDay     | DWORD      |                                                                                                                         |
| AnimationTime    | DWORD      |                                                                                                                         |
| Appearance_Head  | BYTE       |                                                                                                                         |
| AreaId           | DWORD      | ObjectId of area containing creature                                                                                    |
| ArmorPart_RFoot  | BYTE       |                                                                                                                         |
| BaseAttackBonus  | BYTE       |                                                                                                                         |
| BodyBagId        | DWORD      |                                                                                                                         |
| CombatInfo       | Struct     | StructID 51882                                                                                                          |
| CombatRoundData  | Struct     | StructID 51930                                                                                                          |
| CreatureSize     | INT        | Index into `creaturesize.2da`, and matches up to hardcoded constants in the game.                                       |
| DeadSelectable   | BYTE       | 1 if the creature is dead and selectable. That is, mousing over it causes it to highlight. 0 otherwise.                 |
| DetectMode       | BYTE       | 1 if creature is in detect mode, 0 otherwise                                                                            |
| EffectList       | List       | list of Effects on this creature. See **Common GFF Structs** document, **Section 4**.                                   |
| Experience       | DWORD      | 0 for non-player characters                                                                                             |
| ExpressionList   | List       | StructID 5 ExpressionId INT ExpressionString CExoString                                                                 |
| FamiliarName     | CExoString |                                                                                                                         |
| FamiliarType     | INT        |                                                                                                                         |
| FortSaveThrow    | CHAR       |                                                                                                                         |
| Gold             | DWORD      | Amount of gold being carried by the creature                                                                            |
| IsCommandable    | BYTE       |                                                                                                                         |
| IsDestroyable    | BYTE       |                                                                                                                         |
| IsDM             | BYTE       | 1 if the creature is a DM; 0 otherwise                                                                                  |
| IsRaiseable      | BYTE       | 1 if the creature can be raised; otherwise                                                                              |
| Listening        | BYTE       |                                                                                                                         |
| MasterID         | DWORD      |                                                                                                                         |
| MClassLevUpIn    | BYTE       |                                                                                                                         |
| ObjectId         | DWORD      | Object ID used by game for this object.                                                                                 |
| OverrideBAB      | BYTE       | 0 to use normal BAB calculated based on levels in each class. Otherwise, specifies a BAB that overrides the normal one. |
| PerceptionList   | List       | StructID 0 ObjectId DWORD PerceptionData BYTE 3                                                                         |
| PersonalRepList  | List       | List of PersonalReputation Structs (StructID 0xABED) describing how other creatures feel about this one.                |
| PM_IsPolymorphed | BYTE       |                                                                                                                         |
| PregameCurrent   | SHORT      | 62                                                                                                                      |
| RefSaveThrow     | CHAR       |                                                                                                                         |
| SitObject        | DWORD      | ObjectID of the Placeable Object that the creature is sitting on                                                        |
| SkillPoints      | WORD       | 0                                                                                                                       |
| StealthMode      | BYTE       | 1 if the creature is in stealth mode, 0 otherwise                                                                       |
| VarTable         | List       | List of scripting variables stored on this object. StructID 0. See **Section 3**of the **Common GFF Structs**document.  |
| WillSaveThrow    | CHAR       |                                                                                                                         |

**Table 2.5.2:** Additional Fields in Class Struct (StructID 2)

| **Label**        | **Type** | **Description**                                                                                                                                                                                                                                                                                                                                                                                                                                                                     |
| ---------------- | -------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Domain1 Domain2  | BYTE     | Index into `domains.2da`.                                                                                                                                                                                                                                                                                                                                                                                                                                                           |
| School           | BYTE     | Present only for Wizards. Index into `spellschools.2da`.                                                                                                                                                                                                                                                                                                                                                                                                                            |
| SpellsPerDayList | List     | List of SpellsPerDay Structs (StructID 17767), specifying how many more spells the creature can cast at each spell level. There are always 10 elements in this list. The index of a list element is equal to the spell level that the element corresponds to (eg., element 0 is for cantrips, and element 9 is for Level 9 spells). This list is only present for classes that cast spells per day, such as Bards. This list is not present for spell-slot classes such as Wizards. |

**Table 2.5.2:** Fields in SpellsPerDay Struct (StructID 17767)

| **Label**     | **Type** | **Description**                                                                                                                                                |
| ------------- | -------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| NumSpellsLeft | BYTE     | Let the index of this element in the _SpellsPerDayList_ be the spell level, then this Field's value is equal to the number of spells left at this spell level. |

**Table 2.5.3:** Game Fields in MemorizedSpell Struct (StructID 3)

| **Label**      | **Type** | **Description**                       |
| -------------- | -------- | ------------------------------------- |
| Ready          | INT      | 1 if the spell is readied for casting |
| Spell          | WORD     | Index into `spells.2da`.              |
| SpellMetaMagic | SHORT    | Same meaning as in toolset. See Table |

**Table 2.5.4:** Game Fields in KnownSpell Struct (StructID 3)

| **Label** | **Type** | **Description**          |
| --------- | -------- | ------------------------ |
| Spell     | WORD     | Index into `spells.2da`. |

**Table 2.5.5:** Fields in PersonalReputation Struct (StructID 0xABED)

| **Label** | **Type** | **Description**                                                                                                                                                                                               |
| --------- | -------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Amount    | INT      | Reputation adjustment amount. Describes how the reputation of this creature has been adjusted in the eyes of another creature. For example, hitting another creature would typically set this Amount to -100. |
| Day       | DWORD    | Specifies game time at which this PersonalReputation object was created.                                                                                                                                      |
| Decays    | BYTE     | 1 if the reputation adjustment decays after a set time                                                                                                                                                        |
|           |          | 0 if it does not decay                                                                                                                                                                                        |
| Duration  | INT      | Duration in seconds of reputation adjustment                                                                                                                                                                  |
| ObjectId  | DWORD    | Object ID of the other creature for which the reputation adjustment Amount applies.                                                                                                                           |
| Time      | DWORD    | Specifies game time at which this PersonalReputation object was created.                                                                                                                                      |

### 2.6. Player Fields

Player Structs in a savegame or in a BIC file contain all the Fields in Table 2.1.1 and 2.5.1, plus those in Table 2.6.1 below.

**Table 2.6.1:** Additional Fields in Player Structs (StructID 0xBEAD)

| **Label**  | **Type** | **Description**                                                                                                                                                                                                                            |
| ---------- | -------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| Age        | INT      |                                                                                                                                                                                                                                            |
| Experience | DWORD    |                                                                                                                                                                                                                                            |
| QBList     | List     | List of 36 QuickBar Structs having StructID 0. Describes the player's QuickBar assignments. Elements 0 to 11 are for the normal QuickBar. Elements 12 to 23 are for the Shift-QuickBar. Elements 24 to 35 are for the Control-QuickBar.``` |

**Table 2.6.2:** Creature Fields not found in Player Structs

| **Label**       |
| --------------- |
| ChallengeRating |
| Conversation    |
| Comment         |
| TemplateResRef  |

Player Instance Structs exist in the _Mod_PlayerList_ Lists in **module.ifo**. A Player Instance Struct contains all the Fields in Table 2.1.1, 2.5.1, 2.6.1, plus those in Table 2.6.3
below.

**Table 2.6.3:** Additional Fields in Player Instance Structs (StructID 0xBEAD)

| **Label**        | **Type**      | **Description**                                                                                                                                                                                                                                                        |
| ---------------- | ------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Mod_CommntyName  | CExoString    | Player Name                                                                                                                                                                                                                                                            |
| Mod_FirstName    | CExoLocString | Character's First Name. Same as _FirstName_ Field.                                                                                                                                                                                                                     |
| Mod_IsPrimaryPlr | BYTE          |                                                                                                                                                                                                                                                                        |
| Mod_LastName     | CExoLocString | Character's Last Name. Same as _LastName_ Field.                                                                                                                                                                                                                       |
| Mod_MapAreasData | Binary        |                                                                                                                                                                                                                                                                        |
| Mod_MapDataList  | List          | List of Structs. StructID 0. Each Struct has a single Binary Field called Mod_MapData.                                                                                                                                                                                 |
| Mod_MapNumAreas  | INT           |                                                                                                                                                                                                                                                                        |
| ReputationList   | List          | List of Structs. StructID 47837. Each Struct has a single INT Field called Amount that gives the player's rating from 0 to 100 with each faction in the module. There is one Struct per Faction, in the same order as given in the module Faction file **repute.fac**. |

### 2.6.4. QuickBar

**Table 2.6.4.1:** Fields in QuickBar Empty Structs (StructID 0)

| **Label**    | **Type** | **Description**                                                                                                                                                                                                                                                                                                                                  |
| ------------ | -------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| QBObjectType | BYTE     | 0 if qbar slot is empty. If slot is empty, none of the other Fields are present. 1 = item 2 = spell 3 = skill 4 = feat 5 = script 6 = dialog 7 = attack 8 = emote 9 = castspell itemproperty 10 = mode toggle 38 = possess familiar 39 = associate command 40 = examine 41 = barter 42 = quickchat 43 = cancel polymorph 44 = spell-like ability |

**Table 2.6.4.2:** Fields in QuickBar Item Structs (StructID 0)

| **Label**                 | **Type** | **Description**                |
| ------------------------- | -------- | ------------------------------ |
| QBCastPropIndex           | BYTE     | 0xFF if no cast property       |
| QBCastSubPropIdx          | BYTE     | 0xFF if no subproperty         |
| QBContReposX QBContReposY | BYTE     | 0xFF if not inside a container |
| QBItemInvSlot             | DWORD    | object ID                      |
| QBItemReposX QBItemReposY | BYTE     | location of item in inventory  |
| QBObjectType              | BYTE     | 1 for items                    |

**Table 2.6.4.2:** Fields in QuickBar Spell Structs (StructID 0)

| **Label**     | **Type** | **Description**                                           |
| ------------- | -------- | --------------------------------------------------------- |
| QBDomainLevel | BYTE     | 0 for most spells. Domain level for cleric domain spells. |
| QBINTParam1   | INT      | Index into `spells.2da`.                                  |
| QBMetaType    | BYTE     | MetaMagic flags on a spell, if applicable.                |
| QBMultiClass  | BYTE     | Index into creature's _ClassList_.                        |
| QBObjectType  | BYTE     | 2 for spells                                              |

**Table 2.6.4.3:** Fields in QuickBar Skill Structs (StructID 0)

| **Label**    | **Type** | **Description**          |
| ------------ | -------- | ------------------------ |
| QBINTParam1  | INT      | Index into `skills.2da`. |
| QBObjectType | BYTE     | 3 for skills             |

**Table 2.6.4.4:** Fields in QuickBar Feat Structs (StructID 0)

| **Label**    | **Type** | **Description**        |
| ------------ | -------- | ---------------------- |
| QBINTParam1  | INT      | Index into `feat.2da`. |
| QBObjectType | BYTE     | 4 for feats            |

**Table 2.6.3.5:** Fields in QuickBar Mode Structs (StructID 0)

| **Label**    | **Type** | **Description**                      |
| ------------ | -------- | ------------------------------------ |
| QBINTParam1  | INT      | 0 for detect mode 1 for stealth mode |
| QBObjectType | BYTE     | 10 for modes                         |

---

[← Previous: Introduction](Ch1_Introduction.md) | [Next: Chapter 3 - Calculations and Procedures →](Ch3_Calculations_and_Procedures.md)
