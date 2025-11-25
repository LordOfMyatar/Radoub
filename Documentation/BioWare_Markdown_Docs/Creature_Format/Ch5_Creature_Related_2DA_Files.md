# BioWare Aurora Engine - Creature Format

## Chapter 5: Creature-related 2DA Files

[← Back to Main Document](../Bioware_Aurora_Creature_Format.md) | [← Previous: Calculations and Procedures](Ch3_Calculations_and_Procedures.md)

---

### 5.1. Appearance

The appearance 2da defines all the Creature appearances that exist. Many characteristics of a Creature are determined by its Appearance. These characteristics are defined in appearance.2da.

**Table 5.1.1:** appearance.2da
| **Column** | **Type** | **Description** |
| ---------- | -------- | ---------------- |
| LABEL | String | programmer label |
| STRING*REF | Integer | StrRef of the name of the appearance type, as it appears in the Appearance dropdown in the toolset
| NAME | String | programmer label |
| RACE | String | If \_MODELTYPE* is not "P", then this is the ResRef of the MDL file to use for the creature model. If _MODELTYPE_ is "P", then this is the player model letter used in constructing the complete creature model. For example, if _RACE_ is "D", then chest part 3 for a normalphenotype male creature is pmd0*chest003. |
| ENVMAP | String | "default": use the default environment map for the current area's tileset, as specified in the .SET file's *[General] EnvMap* property. \*\*\*\*: use no environment map on the creature model Interpret any other value as the ResRef of the TGA file to use as the environment map for the creature. |
| BLOODCOLOR | String | R = red G = green W = white Y = yellow N = none |
| MODELTYPE | String | P = player: creature model is composed of multiple body parts each with their own MDL and textured with PLTs. Model changes when armor is worn, and colors are selectable. S = simple: creature model is a single MDL and textured with a single TGA or DDS texture file. Colors are not selectable. Model does not change when wearing armor. Weapons do not appear when equipped. F = same as simple, but weapon items do appear when equipped in right or left hand inventory slots. L = large: same as F, but only right-hand weapon appears. |
| WEAPONSCALE | Float | Size scaling factor to apply to weapon models equipped by creatures having this appearance. Only meaningful if \_MODELTYPE* is not S. |
| WING*TAIL_SCALE | Float | Size scaling factor to apply to wings or tails attached to the creature model. |
| HELMET_SCALE_M | Float | Size scaling to apply to helms equipped by male creatures. Only meaningful if \_MODELTYPE*=P |
| HELMET*SCALE_F | Float | Size scaling to apply to helms equipped by female creatures. Only meaningful if \_MODELTYPE*=P |
| MOVERATE | String | Default walking/running speed for creatures having this appearance. Specifies a row in `creaturespeed.2da` that has this value in its _2DAName_ column. |
| WALKDIST | Float | Distance in metres travelled by creature from the beginning of its walk animation to the end of its walk animation |
| RUNDIST | Float | Distance in metres travelled by creature from the beginning of its run animation to the end of its run animation |
| PERSPACE | Float | Personal space used to determine if the creature will fit | | | through an openning
| CREPERSPACE | Float | Personal space used for combat. Usually larger than _PERSPACE_. |
| HEIGHT | Float | Height of the creature. Used for pathfinding under obstacles and zoomin camera height. |
| HITDIST | Float | When this creature is attacking another creature, subtract the _HITDIST_ from the actual distance between attacker and target before comparing the distance to the _PREFATCKDIST_. |
| PREFATCKDIST | Float | Preferred distance from which to attack a target. Creature will use short-range, normal-range, or long-range versions of its melee animations depending on distance of the target. |
| TARGETHEIGHT | String | Target height when hitting creatures having this appearance H = normal height, used by most appearances L = low, used by short appearances, such as badgers |
| ABORTONPARRY | Integer | 1 if attack animation aborts when the attacked creature plays the parry animation |
| RACIALTYPE | Integer | Index into `racialtypes.2da`. Default racialtype of creatures having this appearance. |
| HASLEGS | Integer | 1 if the appearance has legs, 0 otherwise. The Feat "Called Shot: Leg" only works if the creature has legs. |
| HASARMS | Integer | 1 if the appearance has arms, 0 otherwise. The Feat "Called Shot: Arm" only works if the creature has arms. |
| PORTRAIT | String | Base ResRef of the default portrait for creatures having this appearance. Example: if PORTRAIT is po_badger, then use the portraits po_badger_h.tga, po_badger_l.tga, po_badger_m.tga, etc. This value should not exceed 14 characters in length. |
| SIZECATEGORY | Integer | Index into `creaturesize.2da`, references hard-coded list of creature size definitions in game engine. |
| PERCEPTIONDIST | Integer | Default perception range in metres for creatures having this appearance |
| FOOTSTEPTYPE | Integer | -1 if makes no sound when walking or running Otherwise, index into `footstepsounds.2da`. |
| SOUNDAPPTYPE | Integer | Index into `appearancesndset.2da`. See **Table 5.7.3.2** in the **Items** GFF document. |
| HEADTRACK | Integer | 1 if the creature's head tracks nearby creatures, speakers in a conversation, or objects being moused over by the player. 0 otherwise. |
| HEAD_ARC_H | Float | Maximum angle in degrees that the creature's head will turn to the side when tracking something. |
| HEAD_ARC_V | Float | Maximum angle in degrees that the creature's head will tilt up or down when tracking something. |
| HEAD_NAME | String | Name of the head node to rotate in the creature's model in order to make its head track an object. |
| BODY_BAG | Integer | Index into `bodybag.2da`, specifying the default bodybag to leave behind when the creature dies and its corpse fades. |
| TARGETABLE | Integer | 1 if the creature can be targetted, such as by mousing over it. |
| | | 0 if the creature cannot be targetted |

**Table 5.1.2:** tailmodel.2da
| **Column** | **Type** | **Description** |
| ---------- | -------- | ------------------------------------------ |
| LABEL | String | programmer label |
| MODEL | String | ResRef of the MDL file to use for the tail |

**Table 5.1.3:** wingmodel.2da
| **Column** | **Type** | **Description** |
| ---------- | -------- | ------------------------------------------- |
| LABEL | String | programmer label |
| MODEL | String | ResRef of the MDL file to use for the wings |

**Table 5.1.4:** creaturespeed.2da
| **Column** | **Type** | **Description** |
| ---------- | -------- | -------------------------------------------------------------------------------------------------------------------------------------------------- |
| Label | String | programmer label |
| Name | Integer | StrRef to display when selecting speed in the toolset. If StrRef is \*\*\*\*, as it is for row 0 (playerspeed), it is unselectable in the toolset. |
| 2DAName | String | String value used in `Appearance.2da` under the _MOVERATE_ column to specify the default creature speed for a given appearance. |
| WALKRATE | Float | Walking speed of the creature in m/s |
| RUNRATE | Float | Running speed of the creature in m/s |

**Table 5.1.5:** phenotype.2da
| **Column** | **Type** | **Description** |
| ---------- | -------- | ---------------- |
| Label | String | programmer label |
| Name | Integer | StrRef |

**Table 5.1.6:** creaturesize.2da
| **Column** | **Type** | **Description** |
| ----------- | -------- | ----------------------------------------------------------------------------------------------------- |
| LABEL | String | Programmer label describing the size category of the current row. |
| ACATTACKMOD | Integer | Attack modifier when a creature of the specified size is attacking a medium-sized creature. Not used. |
| STRREF | Integer | StrRef of the name of the size category. Not used. |

**Table 5.1.7:** footstepsounds.2da
| **Column** | **Type** | **Description** |
| -------------------------- | -------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Label | String | programmer label |
| Dirt0 Dirt1 Dirt2 | String | ResRef of WAV to play when stepping on a suface of the specified type. There are 3 sound variations for each surface material, played at random at each footstep. |
| Grass0 Grass1 Grass2 | | |
| Stone0 Stone1 Stone2 | | |
| Wood0 Wood1 Wood2 | | |
| Water0 Water1 Water2 | | |
| Carpet0 Carpet1 Carpet2 | | |
| Metal0 Metal1 Metal2 | | |
| Puddles0 Puddles1 Puddles2 | | |
| Leaves0 Leaves1 Leaves2 | | |
| Sand0 Sand1 Sand2 | | |
| Snow0 Snow1 Snow2 | | |

### 5.2. Races

**Table 5.2:** racialtypes.2da
| **Column** | **Type** | **Description** |
| ----------------------------------------------------------- | -------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Label | String | programmer label |
| Abbrev | String | Obsolete. Unused. |
| Name | Integer | StrRef of the race name, capitalized. eg, Dwarf |
| ConverName | Integer | StrRef of the race name as an adjective. eg., Dwarven |
| ConverNameLower | Integer | Lower-case version of ConverName. eg. dwarven |
| NamePlural | Integer | StrRef of the race name in plural and capitalized. eg, Dwarves |
| Description | Integer | StrRef of a description of the race |
| Appearance | Integer | Index into `appearance.2da`. Default appearance for a creature of this race. |
| StrAdjust DexAdjust IntAdjust ChaAdjust WisAdjust ConAdjust | Integer | Racial ability modifier. Applied dynamically by the game. For example, if StrAdjust is 2, and the creature has a Strength of 12 ingame when unbuffed and naked, then its Strength is stored as 10. |
| Endurance | Integer | Obsolete. Unused. |
| Favored | Integer | Index into `classes.2da`. Favored class for this race. If \*\*\*\*, then favored class is creature's highest class. |
| FeatsTable | String | ResRef of racial feats table |
| Biography | Integer | StrRef of default biography for player characters of this race |
| PlayerRace | Integer | 1 if players can choose this race at character creation, 0 if not. |
| Constant | String | Identifier to use when referring to this race in a script. Must match constant defined for it in nwscript.nss. |
| Age | Integer | Default Age of a player character of this race. |
| ToolsetDefaultClass | Integer | Index into `classes.2da`. Default class for this race when creating a creature in the Creature Wizard. |
| CRModifier | Float | Modifier used in CR calculation for creatures of this race. |

### 5.3. Classes

**Table 5.3.1:** classes.2da
| **Column** | **Type** | **Description** |
| ------------------------- | -------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Label | String | programmer label |
| Name | Integer | StrRef of the class name. eg. Barbarian |
| Plural | Integer | StrRef of the plural class name. eg. Barbarians |
| Lower | Integer | lowercase cass name. eg. barbarian |
| Description | Integer | StrRef of description of the class |
| Icon | String | ResRef of TGA icon used in game GUIs to represent class |
| HitDie | Integer | Size of die to roll for hit points on leveling up in this class |
| AttackBonusTable | String | ResRef of class base attack bonus table (cls*atk*\*) |
| FeatsTable | String | ResRef of class feats table, listing feats available (cls*feat*\*) |
| SavingThrowTable | String | ResRef of class saves table, listing saving throws by level (cls*savethr*\*) |
| SkillsTable | String | ResRef of class skills table (cls*skills*\*) |
| BonusFeatsTable | String | ResRef of class bonus feats table, listing levels at which class gains bonus feats |
| SkillPointBase | Integer | Base number of skill points available on levelup in class |
| SpellGainTable | String | ResRef of class spellgain table (cls*spgn*\*) \*\*\*\* if class does not cast spells |
| SpellKnownTable | String | ResRef of class spellknown table (cls*spkn*\*) \*\*\*\* if class is not restricted to a certain number of known spells at each spell level, or if class does not cast spells |
| PlayerClass | Integer | 1 if players can choose this class, 0 if not |
| SpellCaster | Integer | 1 if the class is a spellcaster, 0 if not |
| Str Dex Con Wis Int Cha | Integer | Default starting ability scores for creatures created in Creature Wizard if this class is the creature's first class. |
| PrimaryAbil | String | Primary ability score. Autolevelup will always pick this ability to raise. For spellcaster classes, this is also the casting ability. |
| AlignRestrict | Integer | Bit field: neutral = 0x01 lawful = 0x02 chaotic = 0x04 good = 0x08 evil = 0x10 |
| AlignRestrictType | Integer | Bit field: 0x01 = law-chaos axis 0x02 = good-evil axis |
| InvertRestrict | Integer | 0 if alignment restrictions applied normally 1 if alignment restrictions applied inversely |
| Constant | String | Identifier used to refer to this class in a script. Must match constant defined for it in nwscript.nss. |
| EffCRLvl01 ... EffCRLvl20 | Integer | Effective level of character for purposes of encounter challenge rating calculations used by original NWN Official Campaign. Ignored if not playing original Official Campaign. |
| PreReqTable | String | ResRef of prestige class prerequisites table. (cls*pres*\*) |
| | | \*\*\*\* if class can be taken at character level 1. |
| MaxLevel | Integer | Maximum level allowed in this class. Usually applies to prestige classes. 0 if no maximum exists. |
| XPPenalty | Integer | 1 if this class counts toward multiclassing XP penalties |
| ArcSpellLvlMod | Integer | 0 if this class does not affect arcane spellcasting level. If greater than zero, then let this value be X. For every X levels in this class, the character is treated as being one level higher in the character's first arcane spellcasting class, for purposes of calculating spell slots and spells per day. |
| DivSpellLvlMod | Integer | Same as ArcSpellLvlMod, but for divine spellcasting classes. |
| EpicLevel | Integer | -1 if class becomes epic after the default level, ie. 20. Otherwise, this value is greater than 0 and specifies the level beyond which the character is considered epic in this class. |
| Package | Integer | Default package for this class. Index into `packages.2da`. |

**Table 5.3.2: Class Base Attack Bonus Table: cls*atk*#.2da**
| **Column** | **Type** | **Description** |
| ---------- | -------- | ---------------------------------------------------------- |
| BAB | Integer | Base attack bonus gained from having level = (2da row) + 1 |

**Table 5.3.2: Class Saving Throw Table: cls*savethr*\*.2da**
| **Column** | **Type** | **Description** |
| ---------- | -------- | --------------- |
| Level | Integer | class level |
| FortSave | Integer | Fortitude save |
| RefSave | Integer | Reflex save |
| WillSave | Integer | Will save |

**Table 5.3.3: Class Feats Table: cls*feat*\*.2da**
| **Column** | **Type** | **Description** |
| -------------- | -------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| FeatLabel | String | programmer label |
| FeatIndex | Integer | index into `feat.2da` |
| List | Integer | -1: feat granted at creation, at level 1 0: feat is choosable 1: feat can be chosen as a bonus feat or normal feat 2: feat can only be chosen as a class bonus feat 3: feat is granted at the level specified by _GrantedOnLevel_. |
| GrantedOnLevel | Integer | class level at which feat is awarded -1 if feat is not automatically granted at any level, and must be chosen. |
| OnMenu | Integer | 1 if appears on radial menu 0 if does not appear on radial menu |

**Table 5.3.4: Class Bonus Feats Table: cls*bfeat*\*.2da**
| **Column** | **Type** | **Description** |
| ---------- | -------- | --------------------------------------------------------------------------------------------------------------------------------- |
| Bonus | Integer | Determines whether a character gets a bonus feat at level = (2da row) + 1 in this class. 1 - bonus feat awarded 0 - no bonus feat |

**Table 5.3.5: Class Skills Table: cls*skill*\*.2da**
| **Column** | **Type** | **Description** |
| ---------- | -------- | ---------------------------------------- |
| SkillLabel | String | programmer label |
| SkillIndex | Integer | Index into `skills.2da`. |
| ClassSkill | Integer | 1 if class skill, 0 if cross-class skill |

**Table 5.3.6: Prestige Class Prerequisites Table: cls*pres*\*.2da**
| **Column** | **Type** | **Description** |
| ---------- | -------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| LABEL | String | programmer label |
| ReqType | String | Several possible values, each one dictating how the _ReqParam_ columns are interpreted: FEAT: required feat. _ReqParam1_ indexes into `feats.2da`. FEATOR: must have at least one of the FEATOR requirements in order to take this prestige class. _ReqParam1_ indexes into `feats.2da`. SKILL: _ReqParam1_ indexes into `skills.2da`. _ReqParam2_ specifies the required number of ranks in the specified skill. RACE: must be of one of the specified races. _ReqParam1_ indexes into `racialtypes.2da`. BAB: base attack bonus must be greater than or equal to _ReqParam1_. VAR: the scripting variable named in _ReqParam1_ column must exist on the creature and be set to the value in _ReqParam2_. Ignored by toolset. ARCSPELL: _ReqParam1_ must be 1. Specifies that the character must be able to cast arcane spells. Ignored by toolset. |
| ReqParam1 | varies | See _ReqType_ for how to interpret this column. |
| ReqParam2 | varies | See _ReqType_ for how to interpret this column. \*\*\*\* if not required. |

The spellgain 2das define the number of spells per day a caster can cast, or the number of spell slots in which a caster can prepare spells each day. All caster classes each have a spellgain 2da specified for them under the _SpellGainTable_
column in classes.2da.

**Table
5.3.7: Class Spell Gain Table: cls*spgn*\*.2da**
| **Column** | **Type** | **Description** |
| --------------------------------------- | -------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| Level | Integer | Class level label. Should be equal to row index + 1 |
| NumSpellLevels | Integer | Number of spell levels available at this class level |
| SpellLevel0 SpellLevel1 ... SpellLevel9 | Integer | For classes that must prepare their spells in advance, such as wizards and clerics, this is the number of base number of spell slots available at the spell level named in the column name. For classes that do not prepare their spells in advance, such |
| | | as sorcerers and bards, this is the base number of spells per day at the spell level named in the column name. Note that not all class spellgain 2das will have spell levels up to 9. Value in the column is \*\*\*\* if there are no spells per day at this level. The actual number of spell slots or spells per day available to a creature is modified by the creature's ability score in the relevant casting stat. For sorcerer or bard-type casters, a 0 value in one of these columns means that there are no spells per day at this level unless the character qualifies for it by virtue of having a sufficiently high spellcasting ability score bonus. |

The spellknown 2das define the number of spells that a caster knows at each spell level. Caster classes that do not prepare their spells in advance, such as sorcerers, each have a spellknown 2da specified for them under the _SpellKnownTable_
column in classes.2da.

**Table
5.3.8: Class Spells Known Table: cls*spkn*\*.2da**
| **Column** | **Type** | **Description** |
| --------------------------------------- | -------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| Level | Integer | Class level label. Should be equal to row index + 1 |
| SpellLevel0 SpellLevel1 ... SpellLevel9 | Integer | Number of spells known at the spell level named in the column name. Note that not all class spellknown 2das will have spell levels up to 9. \*\*\*\* if there are no spells known at this level. |

**Table 5.3.9:** hen_companion.2da and hen_familiar.2da
| **Column** | **Type** | **Description** |
| ----------- | -------- | ---------------------------------- |
| NAME | String | programmer label |
| BASERESREF | String | ResRef of UTC creature blueprint |
| STRREF | Integer | StrRef of name of associate |
| DESCRIPTION | Integer | StrRef of description of associate |

### 5.4. Feats

**Table 5.4.1:** feat.2da
| **Column** | **Type** | **Description** |
| ----------------------------------------- | -------- | ----------------------------------------------------------------------------------------------------------------- |
| LABEL | String | programmer label |
| FEAT | Integer | StrRef of the feat name |
| DESCRIPTION | Integer | StrRef of the feat description |
| ICON | String | ResRef of the TGA icon |
| MINATTACKBONUS | Integer | Minimum base attack bonus required to take this feat \*\*\*\* if no min attack bonus requirement |
| MINSTR MINDEX MININT MINWIS MINCON MINCHA | Integer | Minimum ability score to take this feat. \*\*\*\* if no minimum required ability score |
| MINSPELLLVL | Integer | Minimum spell level that the creature must be able to cast in order to take this feat. For example, Empower Spell requires that the creature can cast level 2 spells. \*\*\*\* if there is no spell level requirement
| PREREQFEAT1 PREREQFEAT2 | Integer | Index into `feat.2da` specifying feats that the creature must have in order to take this one. |
| GAINMULTIPLE | | 0 if feat cannot be gained multiple times 1 if feat can be gained more than once. Not supported at this time. Always 0. |
| EFFECTSSTACK | Integer | 1 if effects from the feat stack with other effects of the same type 0 if effects do not stack |
| ALLCLASSESCANUSE | Integer | 1 if all classes can use this feat, 0 if not |
| CATEGORY | Integer | Index into `categories.2da`. Not used. |
| MAXCR | Integer | not used |
| SPELLID | Integer | Index into `spells.2da` specifying a spell that implements this feat |
| SUCCESSOR | Integer | Index into `feat.2da` specifying a feat that succeeds this one. Example: if this feat is Elemental Shape, then the successor is Elemental Shape 2. |
| CRValue | Float | Challenge Rating weighting for this feat when calculating creature challenge rating. See **Section 3.1. Challenge Rating**. |
| USESPERDAY | Integer | Number times feat can be used per day. -1 if uses per day depends on certain hardcoded conditions such as number of levels in a class (Example, stunning fist). \*\*\*\* if feat can be used unlimited times per day or if feat is passive. |
| MASTERFEAT | Integer | Index into `masterfeats.2da`, specifying the "master feat" that this feat belongs to. Example: the "Improved Critical: Club" feat belongs the "Improved Critical" master feat. |
| TARGETSELF | Integer | 1 if the feat targets oneself, \*\*\*\* if the feat does not. |
| OrReqFeat0 OrReqFeat1 OrReqFeat2 OrReqFeat3 OrReqFeat4 | Integer | If any of the OrReqFeats are non-\*\*\*\*, then the creature must have at least one of the OrReqFeats in order to take the current feat. |
| REQSKILL | Integer | Index into `skills.2da` specifying a required skill. \*\*\*\* if no skill required |
| ReqSkillMinRanks | Integer | Number of ranks required in the required skill |
| REQSKILL2 | Integer | Index into `skills.2da` specifying a second required skill. \*\*\*\* if no skill required |
| ReqSkillMinRanks2 | Integer | Number of ranks required in the second required skill |
| Constant | String | Identifier of scripting constant used to refer to this feat. Feat index and Constant name must match constant definition in nwscript.nss |
| TOOLSCATEGORIES | Integer | Specifies one of a set of harcoded feat categories used by toolset in Creature Properties dialog to allow filtering feat lists by feat category 1 = combat feat 2 = active combat feat 3 = defensive feat 4 = magical feat |
| | | 5 = other feat |
| HostileFeat | Integer | 1 if using feat on another creature is considered a hostile act, \*\*\*\* if not. |
| MinLevel | Integer | Minimum level in _MinLevelClass_ required to take this feat \*\*\*\* if no min level |
| MinLevelClass | Integer | Index into `classes.2da` specifying class in which creature must have _MinLevel_ levels. |
| MaxLevel | Integer | Maximum character level to be able to take this feat. Example: the Luck of Heroes feat can only be taken at level 1. |
| MinFortSave | Integer | Minimum fortitude save to be able to take this feat. \*\*\*\* if there is no fortitude save requirement. |
| PreReqEpic | Integer | 1 if feat can only be taken by epic characters. 0 if feat can be taken by non-epic characters. |

**Table 5.4.2:** masterfeats.2da
| **Column** | **Type** | **Description** |
| ----------- | -------- | ------------------------------------------ |
| LABEL | String | programmer label |
| STRREF | Integer | StrRef of the master feat name |
| DESCRIPTION | Integer | StrRef of the master feat |
| ICON | String | ResRef of the TGA icon for the master feat |

**Table 5.4.3:** categories.2da
| **Column** | **Type** | **Description** |
| ---------- | -------- | ---------------- |
| Category | String | programmer label |

**Table 5.4.4: race*feat*\*.2da**

| **Column** | **Type** | **Description**        |
| ---------- | -------- | ---------------------- |
| FeatLabel  | String   | programmer label       |
| FeatIndex  | Integer  | Index into `feat.2da`. |

### 5.5. Skills

**Table 5.5:** skills.2da
| **Column** | **Type** | **Description** |
| ----------------- | -------- | --------------------------------------------------------------------------------------------------------- |
| Label | String | programmer label |
| Name | Integer | StrRef of skill name |
| Description | Integer | StrRef of skill description |
| Icon | String | ResRef of skill TGA icon |
| Untrained | Integer | 1 if skill can be used without training. 0 if must have at least 1 rank in skill to use it. |
| KeyAbility | String | Ability used to modify skill check. Possible values: STR, CON, DEX, INT, WIS, CHA |
| ArmorCheckPenalty | Integer | 1 if skill is affected by armor check penalty, 0 if not. |
| AllClassesCanUse | Integer | 1 if all classes can use this skill, 0 if not all classes can use this skill |
| Category | none | Unused. Always \*\*\*\*. |
| MaxCR | Integer | Maximum Talent CR for this skill in the game's Talent system. \*\*\*\* if no max |
| Constant | String | Identifier used to represent this skill in scripting. Must match constant defined for it in nwscript.nss. |
| HostileSkill | Integer | 1 if using this skill on another creature is considered a |
| | | hostile act, 0 if not. |

### 5.6. Spells

**Table 5.6.1:** spells.2da
| **Column** | **Type** | **Description** |
| ----------------------------------------- | -------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Label | String | programmer label |
| Name | Integer | StrRef of spell name |
| IconResRef | String | ResRef of TGA icon for this spell |
| School | String | Spell School. Possible values are as listed in the _Letter_ column in `spellschools.2da`: A = abjuration C = conjuration D = divination E = enchantment I = illusion N = necromancy T = transmutation V = evocation |
| Range | String | Range of spell P = personal T = touch S = short M = medium L = long |
| VS | String | Verbal/Somatic v = spell is verbal only s = spell is somatic only vs = spell is verbal and somatic |
| Metamagic | Integer | Bit field specifying what metamagic feats are useable with this spell. 0x00: none 0x01: empower 0x02: extend 0x04: maximize 0x08: quicken 0x10: silent 0x20: still |
| TargetType | Integer | Bit field specifying what things this spell can target. 0x01: self 0x02: creature 0x04: ground 0x08: item 0x10: door 0x20: placeable 0x40: trigger |
| ImpactScript | String | ResRef of script to run when spell hits its target. |
| Bard Cleric Druid Paladin Ranger Wiz*Sorc | Integer | Spell level of this spell for the specified class |
| Innate | Integer | Spell level of this spell when used as a spell-like ability |
| ConjTime | Integer | Number of milliseconds to do the conjure animation
| ConjAnim | String | Conjuration animation to use head: conjure is done with raised arms, head looking up hand: conjure is done with hands in front, head looking at hands |
| ConjHeadVisual ConjHandVisual ConjGrndVisual | String | ResRef of visual effect MDL to apply to the caster's head, hand or ground node during the conjure animation \*\*\*\* if no visual |
| ConjSoundMale | String | ResRef of WAV to play during the conjure if caster is male |
| ConjSoundFemale | String | ResRef of WAV to play during the conjure if caster is female |
| CastAnim | String | Animation to use for the cast. area: arms spread out to sides touch: one hand held out to touch target self: hands drawn in toward chest out: both hands aimed forward, arms outstretched up: both hands pointed up |
| CastTime | Integer | Number of milliseconds to hold the cast animation |
| CastHeadVisual CastHandVisual CastGrndVisual | String | ResRef of visual effect MDL to apply to the caster's model's headconjure, handconjure, or root node during the cast animation \*\*\*\* if no visual. |
| CastSound | String | ResRef of WAV to play on cast. \*\*\*\* if no sound. |
| Proj | Integer | 1 if spell has a projectile, 0 if not |
| ProjModel | String | If \_Proj*=1, then this is the ResRef of the MDL to use for the spell projectile. Otherwise, \*\*\*\* |
| ProjType | String | If _Proj_=1, then this is the projectile type. homing accelerating linked ballistic spiral bounce \*\*\*\* if no projectile |
| ProjSpawnPoint | String | Node to spawn the projectile at. hand: spawn at hand node monster0 monster1 monster2 monster3 monster4: spawn at specified special monster node \*\*\*\*: no projectile to spawn |
| ProjSound | String | ResRef of WAV to play from moving projectile |
| ProjOrientation | String | Orientation of projectile model path: along the path of travel \*\*\*\* no path |
| ImmunityType | String | Name of immunity type that works against this spell. Acid Cold Death Disease Divine |
| | | Electricity Fear Fire Mind*Affecting Negative Poison Positive Sonic Not actually used by the game. |
| ItemImmunity | Integer | Not used by game. |
| SubRadSpell1 ... SubRadSpell5 | Integer | Index into `spells.2da` specifying spells cast off of a subradial when casting this spell. SubRadSpell1 is the spell used when this spell is dragged directly from spellbook to quickbar. |
| Category | Integer | Index into `categories.2da` specifying the category of the spell as used by the talent system. An example usage of the \_Category* is when a creature AI asks itself, "do I have any healing spells"? |
| Master | Integer | Index into `spells.2da` specifying the spell for which this spell is a subradial option. Reverse of the _SubRadSpell_ columns. |
| UserType | Integer | 1 = spell 2 = special ability 3 = feat 4 = item |
| SpellDesc | Integer | StrRef of spell description |
| UseConcentration | Integer | 1 if should use Concentration checks when casting this spell |
| SpontaneouslyCast | Integer | 1 if spell can be cast spontaneously, sacrificing another spell of equal level, 0 if not. |
| AltMessage | Integer | StrRef of an alternate message to display instead of " casts ". Example 1: " uses breath weapon." Example 2: " is surrounded by an aura." \*\*\*\* if there is no alternate message for this spell |
| HostileSetting | Integer | 1 if this spell is hostile 0 if this spell is harmless |
| FeatID | Integer | Index into `feat.2da` pointing to the feat that this spell implements. The feat in turn has a _SPELLID_ that points at this spell. |
| Counter1 Counter2 | Integer | Index into `spells.2da` specifying spells that can be used as counterspells to this spell |
| HasProjectile | Integer | Should be same as _Proj_. |

**Table 5.6.2:** spellschools.2da
| **Column** | **Type** | **Description** |
| ----------- | -------- | ------------------------------------------------------------------------------------------- |
| Label | String | programmer label |
| Letter | String | single letter used to identify the spell school, used in column _School_ in `spells.2da`. |
| StringRef | Integer | StrRef of the spell school name |
| Opposition | Integer | Opposition school, index into `spellschools.2da`. \*\*\*\* if no opposition school. |
| Description | Integer | StrRef of spell school description |

**Table 5.6.3:** domains.2da
| **Column** | **Type** | **Description** |
| ------------------- | -------- | ------------------------------------------------------------------------------------------------------------------ |
| Label | String | programmer label |
| Name | Integer | StrRef of name of domain |
| Description | Integer | StrRef of description |
| Icon | String | ResRef of TGA icon |
| Level*1 ... Level_9 | Integer | Index into `spells.2da` specifying extra known spell granted at this level. \*\*\*\* if nothing granted at level |
| GrantedFeat | Integer | Index into `feat.2da` specifying an additional feat granted by this domain. |
| CastableFeat | Integer | 1 if the \_GrantedFeat* is castable 0 if not |

**Table 5.6.4:** categories.2da
| **Column** | **Type** | **Description** |
| ---------- | -------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Label | String | programmer label describing the category. This 2da is for designer reference. The game has a list of hardcoded category constants that this 2da must conform to. |

### 5.7. Packages

The packages.2da defines the packages that can be chosen by players to automatically recommend feats, skills, spells, and ability increases. It also defines the packages used by the game and toolset to automatically level up non-player creatures.

**Table 5.7.1:** packages.2da
| **Column** | **Type** | **Description** |
| ------------------------------------------------ | -------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Label | String | programmer label |
| Name | Integer | StrRef of the package name |
| Description | Integer | Description of the package |
| ClassID | Integer | Index into `classes.2da` specifying what class this package is for. |
| Attribute | String | Primary ability for this package, the one that raises during levelup. Allowed values: STR, DEX, CON, INT, WIS, CHA |
| Gold | Integer | Starting gold for a player character created with this package. |
| School | Integer | Index into `spellschools.2da`, or \*\*\*\* if no package has no specialist spell school. |
| Domain1 Domain2 | Integer | Index into `domains.2da`, or \*\*\*\* if package has no domains. |
| Associate | Integer | Specifies default starting associate. If ClassID references wizard or sorcerer in classes.2da, then this is an Index into `hen_familiar.2da`. If ClassID references druid in classes.2da, then this is an index into `hen_companion.2da`. The game uses a hard-coded index into classes.2da to determine if the class is wizard, sorcerer, or druid. |
| SpellPref2DA | String | ResRef of spell preference 2da |
| FeatPref2DA | String | ResRef of feat preference 2da |
| SkillPref2DA | String | ResRef of skill preference 2da |
| Equip2DA | String | ResRef of starting equipment 2da |
| SoundSet | Integer | Index into `soundset.2da` for default soundset. Unused, |
| | | always 0. |
| **Table 5.7.2: Package Equipment: packeq\*.2da** | | |
| **Column** | **Type** | **Description** |
| Label | String | ResRef of UTI item blueprint specifying item to include in inventory of creature |

**Table 5.7.3: Package Feat Preference Table: packft\*.2da**
| **Column** | **Type** | **Description** |
| ---------- | -------- | ------------------------ |
| FeatIndex | Integer | Index into `feat.2da`. |
| Label | String | programmer label |

**Table 5.7.4: Package Skill Preference Table: packsk\*.2da**
| **Column** | **Type** | **Description** |
| ---------- | -------- | -------------------------- |
| SkillIndex | Integer | Index into `skills.2da`. |
| Label | String | programmer label |

**Table 5.7.5: Package Spell Preference Table: packsp\*.2da**
| **Column** | **Type** | **Description** |
| ---------- | -------- | -------------------------- |
| SpellIndex | Integer | Index into `spells.2da`. |
| Label | String | programmer label |

### 5.8. Challenge Rating

**Table 5.8:** fractionalcr.2da
| **Column** | **Type** | **Description** |
| ------------- | -------- | ----------------------------------------------------------------------------------------------------------------------------------------- |
| Label | String | programmer label |
| DisplayStrRef | Integer | StrRef of the fractional CR |
| Denominator | Integer | Denominator of the fractional CR, assuming a numerator of 1. |
| Min | Float | Minimum calculated CR required to have the final CR rounded off to the fractional value implied by the value in the _Denominator_ column. |

### 5.9. Other

**Table 5.9.1:** portraits.2da

| **Column**    | **Type** | **Description**                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                       |
| ------------- | -------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| BaseResRef    | String   | "Base" ResRef of TGA texture file to use as the portrait. The actual ResRef used depends on the portrait size to display. To get the actual ResRef, prepend "po\_" to the BaseResRef, and append one of the following letters: h = huge (256x512 pixels), size used in character creation portrait selection l = large (128x256), appears in Character Record sheet in game. m = medium (64x128), appears in centre of radial menu, in conversation window, examine window, and as player portrait in upper right corner. s = small (32x64), appears as party member portraits along right-hand side of game GUI. t = tiny (16x32) appears in chat window, and in text bubbles if text bubble mode is set to "Full" in Game Options FeedBack Options. |
| Sex           | Integer  | Index into `gender.2da`                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                               |
| Race          | Integer  | Index into `racialtypes.2da`, or \*\*\*\* for door and plaeable object portraits.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                     |
| InanimateType | Integer  | Index into `placeabletypes.2da`, or \*\*\*\* for creature portraits                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                   |
| Plot          | Integer  | 0 for normal portraits. 1 if portrait is for a plot character. Shows up when the "Plot Characters" radio button is selected in the toolset's Select Portrait dialog. Plot portraits do not show up for selection in the game during character creation.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                               |
| LowGore       | String   | Alternate version of BaseResRef to use if the game violence settings are low.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                         |

**Table 5.9.2:** gender.2da
| **Column** | **Type** | **Description** |
| ---------- | -------- | ------------------------------------------------------------------------------------------------------------------------------ |
| NAME | Integer | StrRef of the gender. |
| GENDER | String | single capital letter abbreviation |
| GRAPHIC | String | Not used |
| CONSTANT | String | Identifier to use in scripting to refer to the gender. Used in toolset Script Wizard to autogenerate source code for a script. |

**Table 5.9.3:** ranges.2da
| **Column** | **Type** | **Description** |
| -------------- | -------- | ------------------------------------------------------------------------------------------------------ |
| Label | String | label |
| PrimaryRange | Float | Max spot range for creatures, or Range value for spells and weapons. |
| SecondaryRange | Float | Max listen range for creatures, or \*\*\*\* for spell and weapon ranges. |
| Name | Integer | StrRef of the range name if this is a creature perception range. \*\*\*\* for spell and weapon ranges. |

---

[← Previous: Calculations and Procedures](Ch3_Calculations_and_Procedures.md) | [Back to Main Document →](../Bioware_Aurora_Creature_Format.md)

---

BioWare Corp. http://www.bioware.com
