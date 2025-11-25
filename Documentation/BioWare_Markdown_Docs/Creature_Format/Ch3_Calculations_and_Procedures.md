# BioWare Aurora Engine - Creature Format

## Chapter 3: Calculations and Procedures

[← Back to Main Document](../Bioware_Aurora_Creature_Format.md) | [← Previous: Creature Struct](Ch2_Creature_Struct.md)

---

### 3.1. Challenge Rating

### 3.1.1. Additive CR

The Challenge Rating of a Creature is calculated using many sources. Below is the step-by-step procedure for calculating Challenge Rating.

Add up all the following:

````
HD \* 0.15
(Natural AC bonus) \* 0.1
[ (Inventory Value) / (HD \* 20000
- 100000. ] _ 0.2 _ HD

[ (Total HP) / (Average HP) ] _
0.2 _ HD \* (Walk Rate) / (Standard Walk Rate)

[ (Total of all Ability Scores) /
(HD + 50) ] _ 0.1 _ HD

[ (Total Special Ability Levels) / { (HD \* (HD + 1) )
- (HD _ 5 ) } ] _ 0.15 \* HD

[ (Total Spell Levels) / { (HD *
(HD + 1) ) } ] _ 0.15 _ HD

[ ((Bonus Saves) + (Base Saves))
/ (Base Saves) ] _ 0.15 _ HD

[ (Total Feat CR Values) / (HD *
0.```5 + 7) ] _ 0.1 _ HD

````

then multiply the total sum by the racial challenge rating modifer, the _CRModifier_ value from `racialtypes.2da`, using the creature's race as an index into the 2da. The resulting value is the **Additive CR**.

The following are some explanations of the terms in the above formulae:
HD = Hit Dice = total number of levels the creature has in all of its classes.

Natural AC Bonus
Inventory Value = total value of all items in the Creature's inventory, not counting items equipped in the creature weapon slots or the creature hide slot. See **Section 4.4**of the **Items** document for how to calculate item cost.

Total HP = total hit points, not including bonuses from constitution or feats.

Average HP = average hit points based on average hit point dice rolls for each class. For example, a creature having classes Outsider 7/Fighter 3 (d8 and d10 hit dice, respectively) would have Average HP = [7 * (8 + 1)/2] + [3 * (10+1)/2] = 7*4.5 + 3*5.5.

Walk Rate = creature's walk rate from the _WALKRATE_ column of `creaturespeed.2da`, using the creature's _WalkRate_ GFF Field as the 2da row index.

Standard Walk Rate = The _WALKRATE_ column value for row 0 in `creaturespeed.2da`, which corresponds to player characters' movement rate.

Total of all Ability Scores = total of all ability scores, before any modifications due to race, feats, or equipped items.

Total Special Ability Levels =
total levels in all special abilities in the creature's _SpecAbilityList_ GFF List. For each spell in the special ability list, get its _Innate_ value from `spells.2da`, and add that to the total special ability levels.

Total Spell Levels = total levels of all spells in all the spell lists in all the creature's classes. The level of a spell is taken as its _Innate_
value from `spells.2da`. If the _Innate_ level is 0, then count it as 0.5.

Bonus Saves = total of the creature's _fortbonus_, _refbonus_, and _willbonus_ GFF Fields.

Total Feat CR Values = total CR Values of all feats that the creature has. The CR Value of a feat is its value under the _CRValue_ column in `feat.2da`.

### 3.1.2. Calculated CR

The **Calculated CR** is the final CR value before any manual challenge rating adjustments or roundoffs.
If the Additive CR is less than 1.5, then the Calculated CR is adjusted slightly downward from there.

```
If 1.5 > AdditiveCR > 0.75, then AdditiveCR = AdditiveCR - 0.25
Else if AdditiveCR <= 0.75, then AdditiveCR = AddtiveCR - 0.35
End if CalculatedCR = AdditiveCR
```

### 3.1.3. Final CR

The Final CR derived from the Calculated CR is always a whole number, unless it is less than 1, in which case, it must be one of the following fractional ratings: 1/2, 1/3, 1/4, 1/6, or 1/8. The fractionalcr.2da file gives the cutoff values for each fractional CR. The Final CR also includes any manual challenge rating adjustments specified in the Advanced page of the Creature Properties dialog in the toolset (that is, the value of the creature's CRAdjust GFF Field).

```
RoundedCR = AddtiveCR, rounded off to nearest integer AdditiveCR = AdditiveCR + CRAdjust RoundedCR = RoundedCR + CRAdjust If AddtiveCR > 0.75, then FinalCR = RoundedCR Else Find the first row in fractionacr.2da where the value in the Min column is less than AdditiveCR.
Take the value in the Denominator column and divide it by 1 to get the FinalCR.
End if
```

### 3.2. Ability Scores

The ability scores saved with a Creature GFF Struct are not necessarily the same as those observed for the creature in the game.

A creature's final ability scores in the game are dynamically calculated from their base values. Only the base values themselves are saved in a Creature Struct.

Ability scores are dynamically adjusted based on:

- Racial modifiers, specified by the _StrAdjust_,
  _DexAdjust_, etc., columns in `racialtypes.2da`.

- Feats, such as Great Strength

- Modifiers from Effects, such as from spells or items

### 3.3. Saving Throws

A Creature's saving throws are not stored in its GFF Creature Struct. Instead, saving throws are determined dynamically.

First, the base saves for a creature are determined by adding up the saving throws contributed by each class that the creature has. The saving throw contribution of each class is obtained by looking up the class's saving throw table in the _SavingThrowTable_ column of `classes.2da`. In the 2da specified by that column, the saving throws are found in the row that has the same _Level_ label as the creature's level in the class.

A creature's final saving throws are dynamically calculated from their base values.

Things that may affect saving throws are:

- Constitution, Dexterity, and Wisdom ability bonuses. Ability scores themselves are affected by several things, as detailed in **Section 3.2**.

- Feats, such as Iron Will.

- Modifiers from Effects, such as from spells or items.

- Bonus saving throw values set in the Creature Properties dialog, and saved as the _fortbonus_, _refbonus_, and _willbonus_
  GFF Fields.

### 3.4. Hit Points

There are several different hit point values that are saved with a creature.

### 3.4.1. HitPoints

The _HitPoints_ GFF Field contains the creature's Base Maximum Hit Points, not considering any bonuses. This represents the total number of hit points gained by rolling hit dice at levelup.

For a rules-compliant creature, this should not be less than the creature's character level (ie. total levels in all classes), and should not be higher than the total if every die roll was maximized. The toolset does allow non-rules-compliant creatures though, so smaller and larger numbers are actually possible.

> Example: Suppose we have a level 5 Barbarian that rolled 12, 6, 6, 7, 7 for hit points. Its _HitPoints_ would be 12+6+6+7+7 = 38.

### 3.4.2. MaxHitPoints

Maximum Hit Points, after considering all bonuses and penalties.

> Example: Suppose that the level 5 Barbarian in the example above has a constitution of 16, but has no active feats or effects that raise hit points. It then has a +3 modifier due to constitution. Multiplying that by 5 levels gives +15 HP, for a _MaxHitPoints_ of 38 + 15 = 53.

> Example2: Suppose we have a level 3 elven Commoner that rolled 1, 1, 1 for hit points. The Commoner has Constitution 8, adjusted down to 6 for being an elf, for a resulting -2 ability modifier. This creature's _HitPoints_ are 3, but its _MaxHitPoints_ are also 3, because all creatures are required to have a minimum of 1 hit point per level, even if ability modifiers would normally result in less.

### 3.4.3. CurrentHitPoints

The Creature's current hit points, not counting any bonuses. This value may be higher or lower than the creature's maximum hit points.

The toolset always creates creatures that will by default spawn into the game at exactly full health, with neither damage, nor bonus hit points. Thus, *CurrentHitPoints*is always equal to _HitPoints_ for a creature created in the toolset.

In the game, this value reflects any damage the creature may have taken or any bonus hit points that the creature may have gained. _CurrentHitPoints_
= _HitPoints_ - (total damage taken).

Note that a creature that has been reduced to 0 hit points in the game does not necessarily have 0 _CurrentHitPoints_, because _CurrentHitPoints_ does not consider hit point bonuses.

> Example: Suppose that the level 5
> Barbarian in the examples above has been reduced to 0 hit points ingame. The barbarian has lost 53 hit points from maximum, so the _CurrentHitPoints_ are 38 - 53 = -15.

### 3.5. INI List File Format

The Creature Wizard, Creature Levelup Wizard, and Creature Template system all use a set of INI files having a common format. These INI files are called List Files.

### 3.5.1. INI File format

INI List files conform to the standard Windows INI file format. They are plain text documents containing a number of _Sections_, _Keys_, and _Values_. The start of a section is designated by a single line containing the section name in square brackets, like this:

[Name of Section]
Each section can contain zero or more Key/Value pairs, with one pair per line, and written as follows:

```
Key1=Value1
Key2=Value2
```

It is legal for a key to have no value specified, as in:
Key=

### 3.5.2. List File format

In an INI List file, if a key is specified with no value, or if a key is not present at all, then an attempt to read its value will return a default of 0 or an empty string, depending on whether the key data is an integer or a string.

The following example illustrates the types of Keys that may be present in an INI List file, with explanatory comments set off by green-colored text preceded by semi-colons. Note that a real INI List file does not include any comments. Any string values that correspond to ResRefs are not casesensitive.

```
[HALFLING_WIZARD_VAMPIRE_EXAMPLE]
; Bonus ability scores. Add these to the creature's current
; ability scores.
STR=0
INT=0
WIS=0
DEX=0
CON=0
CHA=0
PORTRAIT= ; Default portrait base resref. Can be blank.
GENDER=M ; Default gender; see GENDER column in `gender.2da`.
ALIGNGOODEVIL=50 ; default evil-good alignment. 0-100
ALIGNLAWCHAOS=50 ; default chaos-law alignment. 0-100
; Additional feats.
FEATCOUNT=5 ; Number of additional feats in this section
; There should be as many FEATLABEL keys as specified by
; the FEATCOUNT. Each FEATLABEL value should be the exact text
; of the label of a feat in `feat.2da`.
FEATLABEL1=skillaffinitymovesi FEATLABEL2=skillaffinitylisten FEATLABEL3=lucky FEATLABEL4=fearless FEATLABEL5=good BASEARMORCLASS=0 ; bonus to natural AC
; Saving throw bonuses. These are in addition to the creature's
; base saves, and are added to the totals saved to the
; fortbonus, refbonus, and willbonus GFF Fields SAVEFORT=0
SAVEREF=0
SAVEWILL=0
; Additional Spells. Obsolete.
; No longer used as of game version 1.60, toolset 1.3.0.0, vts 025
; Use the packages system instead.
SPELLCOUNT=4
SPELLLABEL1=Daze SPELLLEVEL1=0
SPELLLABEL2=Ray_of_Frost SPELLLEVEL2=0
SPELLLABEL3=Ray_of_Frost SPELLLEVEL3=1
SPELLLABEL4=Magic_Missile SPELLLEVEL4=1
; Additional Skills. Obsolete.
; No longer used as of game version 1.60, toolset 1.3.0.0, vts 025
; Use the packages system instead.
SKILLCOUNT=3
SKILLLABEL1=Concentration SKILLRANK1=4
SKILLLABEL2=Lore SKILLRANK2=4
SKILLLABEL3=Spellcraft SKILLRANK3=4
; Default Equipped Item ResRefs HEAD=
CHEST=NW_CLOTH005
BOOTS=
ARMS=
RHAND=NW_WSWDG001
LHAND=
CLOAK=
LRING=
RRING=
NECK=
BELT=
ARROWS=
BULLETS=
BOLTS=
; Unequipped items to add to creature inventory UNEQUIPPEDCOUNT=2
UNEQUIPPED1=NW_IT_TORCH001
UNEQUIPPED2=NW_IT_MPOTION001
; Default Scripts ResRefs ONHEARTBEAT=NW_C2_Default1
ONNOTICE=NW_C2_Default2
ONENDCOMBATROUND=NW_C2_Default3
ONSPELLCASTAT=NW_C2_DefaultB ONMELEEATTACKED=NW_C2_Default5
ONDAMAGED=NW_C2_Default6
ONINVENTORYDISTURBED=NW_C2_Default8
ONDIALOGUE=NW_C2_Default4
ONSPAWN=NW_C2_Default9
ONRESTED=NW_C2_DefaultA ONDEATH=NW_C2_Default7
ONUSERDEFINED=NW_C2_DefaultD ONBLOCKED=NW_C2_DefaultE
; Additional special abilities.
; SPECIALABILITYCOUNT is the number of additional
; special abilities in this section of the INI file SPECIALABILITYCOUNT=2
; There should be as many SPECIALABILITYLABEL,
; SPECIALABILITYCASTERLEVEL, and SPECIALABILITYUNLIMITEDUSE
; keys as specified by the SPECIALABILITYCOUNT.
; Each SPECIALABILITYLABEL value should be the exact text of the
; label of a spell in `spells.2da`.
SPECIALABILITYLABEL1=Gaze_Dominate
; SPECIALABILITYCASTERLEVEL is the caster level for the ability SPECIALABILITYCASTERLEVEL1=1
; SPECIALABILITYUNLIMITEDUSE specifies if this ability can be
; used unlimited times per day.
; If its value is zero, then the entry counts as a single use
; of the ability per day. Additional uses per day require
; duplicate entries.
SPECIALABILITYUNLIMITEDUSE1=0
SPECIALABILITYLABEL2=Aura_Fear SPECIALABILITYCASTERLEVEL2=10
SPECIALABILITYUNLIMITEDUSE2=0
; Creature Template properties SUBRACESTRREF=5644 ; StrRef of string to add to SubRace Field SUBRACE= ; literal text of string to add to SubRace NEWRACE=UNDEAD ; Label of new Race in `racialtypes.2da`.
NEWHD=12 ; New HD to apply to all class levels
; Creature item blueprint resrefs CQUALITIES=NW_CREITEMVAM ; creature hide item resref
; Creature weapon item resrefs for weapons slots 1, 2, 3
CLAW1=
CLAW2=
CLAW3=
; Special creature weapon modifier item resrefs.
; For more details, see Section 3.8. Applying Creature Templates CWSLASH= ; slash weapon modifier CWPIERCE= ; pierce weapon modifier CWSLASHPIERCE=NW_CREWPVBT ; slash+pierce weapon modifier CWBLUDGEON= ; slam weapon modifier CWALL= ; all weapons modifier
```

### 3.6. Creature Wizard Race Initialization

The first step to creating a creature in the Creature Wizard is to pick its race from among those defined in `racialtypes.2da`. Several starting characteristics are defined in racialtypes.2da (Appearance, default portrait from appearance, racial feats, default class), but some others are obtained from a Race INI file.

The ResRef of the race INI file is **race\_**, where is the value from the _Label_ column in racialtypes.2da, truncated to 11 characters if it is longer than that, to ensure that the final ResRef is 16 characters or less. The race INI file contains one section, having the same name as the ResRef itself, but in all-caps and without the "RACE\_" prefix. (eg., [HUMANOID_MO])

A Race INI file is a list file
(See **Section 3.5. INI List File Format**).
It contains a single section, named after the portion of its ResRef.

The following example displays some of the keys that can be present in a race section. Not all of them need to be present.

```
[HALFLING]
STR=0
INT=0
WIS=0
DEX=0
CON=0
CHA=0
PORTRAIT=
GENDER=M PHENOTYPE=
ALIGNGOODEVIL=50
ALIGNLAWCHAOS=50
```

In addition to the above, a race section can also include feats, equipped inventory, and unequipped inventory.
See **Section 3.5.2. List File format**
for the exact keynames of these properties.

Although ability modifiers are included in the race list file, please note that these are NOT the standard racial ability score modifiers. The standard racial modifiers are in `racialtypes.2da`, so any modifiers in the race list file are in addition to those in racialtypes.2da.

### 3.7. Auto-levelup

Creatures can be automatically leveled up by the toolset's Creature Wizard or Creature Levelup Wizard, or by the game's scripting function LevelUpHenchman().

When a creature is auto-leveled, the toolset or game requires a _Package_
to determine what ability, skill, feat, and spell choices to make. Creature levelup packages include the same Packages that are available to players at character creation. Autoleveling up a non-player creature has results that are similar to what would happen if a player clicked the Recommended button at every opportunity during character creation and levelup.

There are some minor differences between how the game handles autolevelup and how the toolset does it. Most of these differences stem from the toolset ignoring certain restrictions.

### 3.7.1. Check Class Requirements

Check if the creature meets the class prerequisites, by check the following columns in `classes.2da`:

_MaxLevel_, _AlignRestrict_, _AlignRstrctType_, _InvertRestrict_, _PreReqTable_.
See `Table 5.3.1: classes.2da`. Note that the toolset does not actually prevent taking a class with an incompatible alignment. Instead, it shifts the creature's alignment by the minimum amount required to make it conform to the requirements of the class level being added.

The _PreReqTable_
points to a separate 2da that contains additional prerequisites for taking prestige classes. The requirements are detailed in **Table 5.3.6: Prestige Class** Prerequisites Table: cls*pres*\*.2da.

### 3.7.2. Determine levelup package

In the game, using LevelUpHenchman() specifies the Class and Package to use for levelup.

In the Creature Wizard and Creature Levelup Wizard, the procedure is different:

Use the creature's _StartingPackage_ GFF Field value as an index into `packages.2da` and check the _ClassID_ for the package. If the package _ClassID_ matches the class being leveled up in, then use the _StartingPackage_
as the levelup package.

If the _StartingPackage_ _ClassID_
does not match the class being leveled up in, then find the first package in `packages.2da` that has a _ClassID_ that matches the levelup class.

If the creature is being leveled up for the first time because it is being created in the Creature Wizard, then its _StartingPackage_ defaults to the first package in `packages.2da` that has a _ClassID_ that matches the creature's first class.

### 3.7.3. Apply Class List File

If adding level 1 in a class, and the class is the creature's first class, then read the default scripts from the Primary section of the C*lass INI File*.

The ResRef of the class INI file is **class\_**, where is the value from the _Label_ column in classes.2da, truncated to 10 characters if it is longer than that, to ensure that the final ResRef is 16 characters or less.

A Class INI file is a list file
(See **Section 3.5. INI List File Format**).
It contains 1 section for every supported class level, except for level 1, which has 2 sections. The sections for levels 2 and up are named after the class's truncated from its ResRef, with a space followed by the level number. Level 1 has two sections, a primary section and a secondary section, with " Primary" and " Secondary" appended to the section names.

Examples of class INI file sections are:

```
[WIZARD 1 Primary]

[WIZARD 1 Secondary]
[WIZARD 2]
[WIZARD 3]
```

Each section describes things to be applied when gaining the appropriate level in the class. Level 1 is special in that gaining level 1 has different effects depending on if it is the creature's very first level in any class, or if the creature multiclassed. The Primary section applies for a creature's first class only. The Secondary section applies when multiclassing.

#### As of game version 1.60 and toolset version 1.3.0.0, vts025, the only keys used from a class list file are the default scripts from the primary section:

```
ONHEARTBEAT=

ONNOTICE=
ONENDCOMBATROUND=
ONSPELLCASTAT=
ONMELEEATTACKED=
ONDAMAGED=
ONINVENTORYDISTURBED=
ONDIALOGUE=
ONSPAWN=
ONRESTED=
ONDEATH=
ONUSERDEFINED=
ONBLOCKED=
For reference, however, older versions of the toolset also loaded the following keys

STR=
INT=
WIS=
DEX=
CON=
CHA=
BASEARMORCLASS=
SAVEFORT=
SAVEREF=
SAVEWILL=
```

plus the keys for special abilities, spells, feats, skills, equipped inventory, and unequipped inventory.

Loading of these keys, however, has been superceded by usage of the Package system. The list file method to gain skills and spells did not take into account differing numbers of skill points or spell slots due to ability scores. The packages system does.

### 3.7.4. Add or Initialize

Ability Scores
If adding level 1 in the creature's very first class, its ability scores are set to the default values specified in `classes.2da` in the _Str_, _Dex_,
_Con_, _Wis_, _Int_, and _Cha_ columns.

If adding any other level in a class, if the creature's new total level in all classes is divisible by 4, then it gains a bonus ability point. If the class being raised is a class for which the creature's
_StartingPackage_
applies, then ability score that raises is the one specified by the _Attribute_ column in `packages.2da`. Otherwise, the ability that raises is the one specified by the _PrimaryAbil_ column in `classes.2da` for the class being leveled up in.

### 3.7.5. Add Skills

#### Calculate Skill Points
Determine the number of skill points available by reading the _SkillPointBase_
from `classes.2da`. Add the creature's intelligence ability bonus to the number of skill points. The intelligence modifier is: [(creature's _Intelligence_)

- (its racial intelligence modifier from `racialtypes.2da`)]
  /2 - 5 rounded down the nearest integer. The total skill points cannot be less
  than 1, however.

If the creature has the Quick to Master feat (hardcoded feat number 258), then it gets an additional skill point.

Multiply the final number of skill points by 4 if adding level 1 of the creature's first class.

#### Class Skills Table
Determine the _Class Skills Table_ (**cls*skill*\*.2da**) for the class being leveled up in by getting the _SkillsTable_ from `classes.2da`. There are two columns in the Class Skills Table: _SkillIndex_, an index into `skills.2da`; and _ClassSkill_, which contains a 1 if the skill is a class skill, or 0 if not. If a class cannot take a skill at all (eg., Clerics can't take Perform), then that skill does not appear at all in the Class Skills table.

#### Package Skill Preferences

Determine the _Package Skills Preference Table_ (**packsk\*.2da**) for the levelup package by getting the _SkillPref2DA_ from `packages.2da`.

Iterate through the Package Skill Preferences 2da from top to bottom. If the _SkillIndex_ at the current row in the Package SkillPref table corresponds to a class skill as defined by the Class Skills Table, then add 1
rank to the creature's ranks in that skill. A class skill may not have a rank that is higher than the creature's total level in all classes + 3.

Continue looping through the SkillPref table, adding 1 to each class skill until all skill points have been spent, or all class skills are maximized at CharacterLevel + 3. If the end of the SkillPref table is reached, repeat from the top.

If there are skill points left over after every class skill has been maximized, then continue looping through the SkillPref table, but adding cross-class skills instead, at a cost of 2 skill points each. A cross class skill cannot exceed total character level divided by 2, rounded down. Continue adding cross class skills until the creature has 0 or 1 skill points remaining or until every cross-class skill has been maximized, then stop.

### 3.7.6. Add Feats

#### Class Feats Table
In `classes.2da`, the _FeatsTable_ column specifies the _Class Feats Table_ (**cls*feat*\*.2da**) for each class. The Class Feats Table describes what feats are available to a class, whether the class automatically gains a feat at a certain level, and whether the feat is a bonus feat that can only be taken as a bonus feat, or can also be taken as a normal feat. See **Table 5.3.3: Class Feats** Table:
cls*feat*\*.2da for more details.

#### Package Feat Preferences
To determine which feats the creature takes on levelup, use the _Package Feat Preference Table_
(**packft\*.2da**) specified in the _FeatPref2DA_ column of `packages.2da`. The Feat Preference table lists the preferred feats for the creature's levelup package in order of most preferred to least preferred. The exact usage of this table is outlined in more detail further below.

#### Calculate number of normal feats

At character level 1, and at every character level divisible by 3, a creature gains a feat.

If the creature has the Quick to Master feat, then at character level 1, it gets 2 normal feats instead of just 1.

#### Calculate number of bonus feats

Certain classes gain bonus feats at specific class levels. These levels are specified in the _Class Bonus Feats Table_ (**cls*bfeat*\*.2da**), which is specified in the _BonusFeatsTable_ column of `classes.2da`. The _Bonus_ column in **cls*bfeat*\*.2da**
is 0 if there are no bonus feats at a particular level, or 1 if there is 1
bonus feat.

#### Feat prerequisites

To take a feat, the creature must meet the prerequisites for it as defined by the following columns from `feat.2da`: PreReqEpic, MINATTACKBONUS, MINSTR, MINDEX, MININT, MINWIS, MINSPELLLVL, PREREQFEAT1, PREREQFEAT2, OrReqFeat, REQSKILL, MinLevel, MinLevelClass, MaxLevel. See `Table 5.4.1: feat.2da` for details as to what each of these columns mean.

A creature cannot take a feat more than once unless the _GAINMULTIPLE_ column value in `feat.2da` is 1.

#### Successor feats

Some feats are successors to other feats. If a feat has a value specified in the _SUCCESSOR_ column in `feat.2da`, then if the creature gains the successor feat, the original feat is removed. For example, Sneak Attack 3 is the successor for Sneak Attack 2. If a creature gains Sneak Attack 3, it loses Sneak Attack 2.

#### Assign class feats

Iterate through the Class Feats Table and find all feats that have 3 as their _List_ column value, and _GrantedOnLevel_ equal to the level of the class being leveled up in. Add these feats to the creature's feat list. For example, when adding level 5 of a class, add all feats that have
_List_=3 and _GrantedOnLevel_=5.

#### Cleric Domain feats

Clerics gain bonus feats at class level 1 for their chosen domains. If the creature is gaining Cleric level 1, then for each of the cleric's domains specified in the _Domain1_ and _Domain2_ columns in `packages.2da`, get the _GrantedFeat_ column value in `domains.2da`, and add the specified feat.

#### Assign bonus feats

If a feat in the Class Feats Table has a _List_ column value of 1 (bonus or normal) or 2 (bonus only), then it can be taken as a bonus feat. Find all the feats in the Class Feats Table that can be gained as bonus feats.

To determine which bonus feats the creature takes on levelup, use the _Package Feat Preference Table_ (**packft\*.2da**)
specified in the _FeatPref2DA_ column of `packages.2da`. Scan through the Feat Preference Table from top to bottom, using the _FeatIndex_ column values as indices into `feat.2da`. If the creature meets the prerequisites for a feat, and the feat can be taken as a bonus feat, then add the feat to the creature. Continue doing this until the creature has taken as many bonus feats as it is allowed to for the current level, starting over from the top of the list if the bottom has been reached. Stop if a full pass through the list has been done without adding any feats.

#### Assign normal feats

If a feat in the Class Feats Table has a _List_ column value of 0 (normal choosable) or 1 (bonus or normal), then it can be taken as a normal feat. Use the same procedure as for bonus feats to add normal feats to a creature.

### 3.7.7. Add Spells

#### Class SpellGain Table

To determine if a class is a spellcasting class, get the Class Spell Gain Table (**cls*spgn*\*.2da**) from `classes.2da`
by reading from the _SpellGainTable_ column. If the class has \***\* in that column, then skip the rest of this section. The SpellGain table specifies the base number of spell slots or spells per day that the class has per day at each spell level. See **Table 5.3.7: Class Spell\*_ Gain Table:
cls*spgn*_.2da for details.

A creature cannot cast spells of a given spell level unless its spellcasting ability score bonus is at least equal to the spell level itself. The spellcasting ability score is listed under the _PrimaryAbil_ column in `classes.2da`.

The number of bonus spell slots or spells per day that a creature has for a given spell level is:

```
(Bonus Spells) = (Ability Bonus) -
(Spell Level) Treat a negative result as 0.
```

#### Class Spells Known
Some spellcasting classes have a limited number of spells that they can know at each spell level. For these classes, the _Class Spells Known Table_ (**cls*spkn*\*.2da**) is specified under the _SpellKnownTable_ column of `classes.2da`. If the _SpellKnownTable_ value is \*\*\*\*, then the class does not have a limit on number of spells known.

#### Package Spell Preferences
To determine which spells the creature learns or prepares on levelup, use the _Package Spell Preference Table_ (**packsp\*.2da**) specified in the _SpellPref2DA_ column of `packages.2da`.

#### Adding Spells
If the levelup class is one that prepares spells in advance, then spells are added to its MemorizedLists. If the levelup class is one that does not prepare spells, then spells are added to its KnownLists.

To add new spells, do the following steps for each spell level that has a column in the SpellGain/SpellKnown table:

1. Determine how many spells the creature has at this spell level. If the creature has a SpellsKnown table, then use the value from the appropriate column of the cls_spkn 2da. Otherwise, use the appropriate column in the cls_spgn 2da. If the creature uses the SpellGain table, then the creature may also have bonus spell slots at this level. Add those to the total number of spellslots for the current spell level. If the final total number of spell slots or spells known is 0, skip to the next spell level.

2. Determine if the creature gains any new spells at this spell level. To do this, subtract the number obtained in Step 1 from the number of spells the creature currently has in its _KnownList_ (if the creature has a SpellsKnown table) or _MemorizedList_ (if the creature has no SpellsKnown table) for this spell level. The resulting difference is the number of new spells that the creature gains. If the result is 0, skip to the next spell level.

3. If the levelup class is Cleric, then add domain spells, if any. Check the appropriate \_Level\_\_ column in `domains.2da` for extra domain spells at the current spell level, and add those spells to the creature's MemorizedList. For each spell added, subtract from the number of new spells calculated in Step 2

4. Iterate through the Package Spell Preference Table from top to bottom, starting over from the top after reaching the bottom, perform the following:

5. Read the _SpellIndex_ column value for the current row in the SpellPreference 2da. Use it as an index into `spells.2da`.
6. Look up the spell in `spells.2da`, using the _Bard_, _Cleric_, _Druid_, _Paladin_, _Ranger_, or _Wiz_Sorc_ column as appropriate to determine the spell level. If none of these columns matches the levelup class, use the _Innate_ column instead. If the spell level does not match the spell level for which we are currently adding spells, then skip to the next SpellPreference row.

7. If the spell is not already in the creature's MemorizedList for the current spell level, then add it, flagging it as Readied, and with no Metamagic. Subtract 1 from the number of new spells calculated in Step 2. If the value becomes 0, stop adding spells.

8. If a complete pass of the SpellPreference 2da has been done with no spells added, but there are still new spells to add, continue to Step 5.

9. Repeat step 4, but add a spell even if it is already in the MemorizedList. Continue looping through the spell preference list until a full pass is made where no spells are added, then stop adding spells, even if there are still new spells to add.

### 3.7.8. Add Package Equipment

If adding level 1 of a class, open the Package Equipment Table (**packeq\*.2da**) specified in the _Equip2DA_ column of `packages.2da`. Otherwise, skip this step.

The _Label_ columns contain ResRefs of Item Blueprints (UTI files). Add all the Items in the Package Equipment table to the creature's inventory.

If an Item is equippable, then equip it to the appropriate inventory slot, unless there is already an Item there.

### 3.7.9. Add Hit Points

Add hit points from the levelup.Get the _HitDie_ column value from `classes.2da` to determine the size of the hit point die roll.

If the class is the creature's first class, the _PlayerClass_ column value in `classes.2da` is a 1, and the level is 1, then increase the creature's _CurrentHitPoints_, *HitPoints,*and _MaxHitPoints_ Fields by the full hit die roll for that class. Otherwise, add the average die roll for the class (DieSize/2 + 0.5), keeping fractional hit point values until the Creature Wizard or Creature Levelup Wizard has finished adding all class levels. After all levels have been added, the total hit point value is rounded to the nearest integer.

### 3.8. Applying Creature Templates

A Template is a set of properties that modify an existing creature. Examples of Templates are Vampire, Half-Dragon, and Lich. Templates are applied using the List File system (see **Section 3.5. INI List File Format**). The list of available Templates is provided in `crtemplates.2da`.

### 3.8.1. Apply List File

The _NAME_ column of `crtemplates.2da`
lists the Template labels. The filename of a Template List file is **tmlt\_.ini**, where is the _NAME_ value.

Template List files contain a single section having the same name as the label.

When applying a Template to a creature, the following List File keys are used: ALIGNGOODEVIL,

ALIGNLAWCHAOS, SUBRACESTRREF, SUBRACE, NEWRACE, NEWHD, HitDie, BONUSAC, and the keys for ability scores, saving throws, special abilities, skills, and feats.

The Creature Item Listfile keys are also used. They are describedin more detail in **Section 3.8.4. Creature Weapon Changes** and **Section 3.8.5. Creature Hide Changes**.

### 3.8.2. Hit Die Changes

If a NEWHD key is specified, then the creature's Hit Die for all class levels changes to the NEWHD value, _if_ the NEWHD value is larger. The creature gains additional Hit Points assuming average die rolls for the new Hit Die size.

> Example: Suppose that a Wizard 4/Cleric 5/Barbarian 6 acquires a Template that has NEWHD=8. The creature's old class hit die sizes are d4, d8, and d12.
> The creature would gain 4 hit points for Wizard level 1 because the first class gets maximum hit die rolls, and 8 - 4 = 4. The creature would gain (8-4)/2 = 1 additional Hit Point for Wizard levels 2 to 4. The creature would gain no additional Hit Points for its Cleric and Barbarian levels because those classes already have Hit Dice that are greater than or equal to the Template Hit Die.

### 3.8.3. Race/Subrace Changes

If a NEWRACE is specified, then the creature's _Race_ Field changes to the row number in `racialtypes.2da`
that has the same _Label_ as that specified by the NEWRACE value.

If a SUBRACESTRREF is specified, then fetch the string for that StrRef from dialog.tlk and include it in the creature's S*ubRace* Field.

If a SUBRACE is specified, then add the SUBRACE value directly to the creature's _SubRace_. Ignore SUBRACE if SUBRACESTRREF is already specified.

When adding a Subrace string, set the creature's _SubRace_ Field to the new subrace string if the _SubRace_ Field was originally empty. If there was already text in the _SubRace_ Field, then check if the new subrace string is already part of the _SubRace_ Field. If not, then prepend the new subrace to the beginning of the existing _SubRace_ string with a "; " separator between the new subrace and the old text.

### 3.8.4. Creature Weapon Changes

The creature item keys are resolved using a very specific order and method.

#### Specific Damage-type Weapon Modifiers
The CWSLASH, CWPIERCE, CWSLASHPIERCE, and CWBLUDGEON keys are Creature Weapon modifiers, and are applied first, and in the order listed in this sentence.

Each of these keys refers to an Item Blueprint (UTI file).

For each key, do the following for each of the creature's 3 Creature Weapon slots:

1. Check if there is an item in the current slot. If not, skip to the next slot
2. Check if the current slot's item has the same damage type as that for the current weapon modifier. If not, skip to the next slot

3. Load the weapon modifier's item blueprint and add its properties to those of the current slot's item.

#### Additional Normal Creature Weapons
After that, the normal creature weapons CLAW1, CLAW2, and CLAW3 are added to the creature's CreatureWeapon item slots if those slots are not already occupied. Note that the 1, 2, and 3 do not specify exact slot numbers. For example, if a creature already has an item in its Claw1 and Claw2
slot, then the CLAW1 item will go into the Claw3 slot, and CLAW2 and CLAW3 will not be added at all.

#### All-weapon modifier
After handling the creature weapon modifiers and the normal creature weapons, the CWALL key is applied. It is a Creature Weapon modifier that is applied to all creature weapons regardless of their damage type.

If the creature is a Creature Blueprint, then if any weapons were modified by the CWSLASH, CWPIERCE, CWSLASHPIERCE, CWBLUDGEON, or CWALL modifiers, the new creature weapons are saved as new Item Blueprints with user-specified ResRefs.

### 3.8.5. Creature Hide Changes

The CQUALITIES key specifies an Item Blueprint for the creature's Hide item. If the creature does not already have a hide item, the item having the ResRef specified by the CQUALITIES key value is loaded and added to the creature's Hide slot.

If the creature already has a hide item, then the CQUALITIES item's properties are added to those of the creature's existing hide item. If the creature is a Creature Blueprint, then the resulting item is saved as a new Item Blueprint with a user-specified ResRef.

---

[← Previous: Creature Struct](Ch2_Creature_Struct.md) | [Next: Chapter 5 - Creature-Related 2DA Files →](Ch5_Creature_Related_2DA_Files.md)
