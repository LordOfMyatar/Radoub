# BioWare Aurora Engine - Item Format

## Chapter 5: Item-related 2DA Files

[← Back to Main Document](../Bioware_Aurora_Item_Format.md) | [← Previous: Calculations and Procedures](Ch4_Calculations_and_Procedures.md)

---

### 5.1. Base Items

The baseitems 2da defines all the item types that exist. Many characteristics of an item are determined by its base item type and cannot be set by the addition of ItemProperties to its PropertiesList. These characteristics are defined in baseitems.2da.

**Table 5.1: baseitems.2da columns**

| **Column** | **Type** | **Description** |
| ---------- | -------- | --------------- |
| Name | Integer | StrRef for the name of the base item type |
| Label | String | Programmer label |
| InvSlotWidth | Integer | Height of item's inventory icon, measured in number of inventory grid squares. |
| InvSlotHeight | Integer | Width of item's inventory icon, measured in number of inventory grid squares. |
| EquipableSlots | Integer | Set of bit flags specifying where the item can be equipped:<br>HEAD 0x1<br>CHEST 0x2<br>BOOTS 0x4<br>ARMS 0x8<br>RIGHTHAND 0x10<br>LEFTHAND 0x20<br>CLOAK 0x40<br>LEFTRING 0x80<br>RIGHTRING 0x100<br>NECK 0x200<br>BELT 0x400<br>ARROWS 0x800<br>BULLETS 0x1000<br>BOLTS 0x2000 |
| CanRotateIcon | Integer | 1 if inventory icon for this item may be rotated 90 degrees clockwise, such as when placed on a player's quickbar. 0 if the icon may not be rotated. |
| ModelType | Integer | Defines how the item's model and icon are constructed. See **Section 4.1**.<br><br>Value: Description: Has Color Layers: # of parts<br>0: simple: no: 1<br>1: layered: yes: 1<br>2: composite: no: 3<br>3: armor: yes: 18 |
| ItemClass | String | Base ResRef for item icon and model parts. See **Section 4.1**. |
| GenderSpecific | Integer | Gender-specific model usage:<br>0 if icons and models are identical for all genders of players<br>1 if the icons and models differ |
| Part1EnvMap Part2EnvMap Part3EnvMap | Integer | Determines if part 1, 2, or 3 of the item's model should have environment mapping applied to it. 1 to use environment mapping. 0 to not use environment mapping. |
| DefaultModel | String | ResRef of the default model to use for the item. |
| Container | Integer | Container capability:<br>0 if this item is not a container<br>1 if this item is a container and can contain other items |
| WeaponWield | Integer | Weapon Wield style:<br>**** - item cannot be wielded, or it is a melee weapon that is held in one or two hands depending on the size of the creature wielding it<br>1 - nonweapon<br>4 - pole<br>5 - bow<br>6 - crossbow<br>7 - shield<br>8 - two-bladed<br>9 - creature weapon<br>10 - sling<br>11 - thrown |
| WeaponType | Integer | Weapon damage type:<br>0 - none<br>1 - piercing<br>2 - bludgeoning<br>3 - slashing<br>4 - piercing and slashing |
| WeaponSize | Integer | Weapon size:<br>1 - tiny<br>2 - small<br>3 - medium<br>4 - large<br>5 - huge |
| RangedWeapon | Integer | **** if not a ranged weapon, otherwise, an integer. |
| PrefAttackDist | Float | **** if not a weapon, otherwise, the preferred attacking distance when using this weapon. The distance is selected so that attack animations look their best in the most commonly anticipated situations. |
| MinRange | Integer | Minimum part number to scan for in toolset. Lowest possible value is 0. |
| MaxRange | Integer | Maximum part number to scan for in toolset. Highest possible value is 999. |
| NumDice | Integer | Number of dice to roll to determine weapon damage. **** for non-weapons. |
| DieToRoll | Integer | Size of dice to roll to determine weapon damage. **** for non-weapons. |
| CritThread | Integer | Critical threat range. **** for non-weapons. |
| CritHitMult | Integer | Critical hit multiplier. **** for non-weapons. |
| Category | Integer | Item category:<br>0 - none<br>1 - melee<br>2 - ranged<br>3 - shield<br>4 - armor<br>5 - helmet<br>6 - ammo<br>7 - thrown<br>8 - staves<br>9 - potion<br>10 - scroll<br>11 - thieves' tools<br>12 - misc<br>13 - wands<br>14 - rods<br>15 - traps<br>16 - misc unequippable<br>17 - container<br>19 - healers |
| BaseCost | Integer | base cost to use in item cost calculation |
| Stacking | Integer | Maximum stack size of item |
| ItemMultiplier | Integer | Used in Cost calculation. See **Section 4.4**. |
| Description | Integer | StrRef of basic description when examining an item of this type, if the item is unidentified, or if there is no description for the item in its Description CExoLocString Field. |
| InvSoundType | Integer | Specifies sound to make when moving the item in inventory during the game:<br>0 - Armor<br>1 - Shield<br>2 - Wood Melee Weapon<br>3 - Metal Melee Weapon<br>4 - Ranged Weapon<br>5 - Ammo<br>6 - Potion<br>7 - Paper<br>8 - Treasure<br>9 - Generic<br><br>The resref is contructed in the following way:<br>Non-Armor XX_YYYYY<br>XX = PU or DR for pickup/equipping and drop/unequpping.<br>YYYYY is hardcoded to correspond to one of the above inventory sound types. |
| MaxProps | Integer | Maximum number of Cast Spell item properties allowed. |
| MinProps | Integer | Minimum number of Cast Spell item properties that must exist on item. |
| PropColumn | String | Column of **itemprops.2da** that defines what item properties are available for this baseitem. There is a one-to-one correspondence between rows in itemprops.2da and **itempropdefs.2da**. If a baseitem can have a certain property, its row in itemprops.2da is 1. If not, then the value is ****. |
| StorePanel | Integer | Store Panel that items of this type appear in:<br>0 - armor<br>1 - weapons<br>2 - potions<br>3 - scrolls<br>4 - miscellaneous |
| ReqFeat0 ReqFeat1 ReqFeat2 ReqFeat3 ReqFeat4 | Integer | List of feats required to use the item. Can specify up to 5 required feats.<br>**** indicates no requirement |
| AC_Enchant | Integer | The type of AC bonus the item applies:<br>0 - dodge<br>1 - natural<br>2 - armor<br>3 - shield<br>4 - deflection |
| BaseAC | Integer | Base AC added when item is equipped. Note that sheields use this column, but armor does not. |
| ArmorCheckPen | Integer | Armor check penalty |
| BaseItemStatRef | Integer | StrRef describing the statistics of this item |
| ChargesStarting | Integer | Initial number of charges on an item of this type |
| RotateOnGround | Integer | Ground rotation behavior:<br>0 - no rotation<br>1 - rotate 90 degrees around positive y-axis<br>2 - rotate 90 degrees around positive x-axis |
| TenthLBS | Integer | Weight in tenths of pounds |
| WeaponMatType | Integer | Weapon material type. Index into **weaponsounds.2da**. Determines the sound emitted when weapon strikes something. |
| AmmunitionType | Integer | Ammunition type required:<br>**** if no ammunition<br>0 - none<br>1 - arrow<br>2 - bolt<br>3 - bullet<br>4 - dart<br>5 - shuriken<br>6 - throwing ax |
| QBBehaviour | Integer | Determines the behaviour when this property appears on the player's quick bar:<br>0 - default<br>1 - select spell, targets normally<br>2 - select spell, always targets self |
| ArcaneSpellFailure | Integer | Arcane spell failure chance when equipped. **** if does not affect arcane spell casting. |
| %AnimSlashL %AnimSlashR %AnimSlashS | Integer | % chance to use the left-slash, right-slash, or stab animation when using this weapon. **** if the item is not a melee weapon.<br><br>Left-Slash and Stab should add up to 100, and Right-Slash and Stab should add up to 100.<br><br>The Left and Right slash percentages are the chances of doing that move if the wielder is in the already proper stance. For example, a creature in the right-ready combat animation can only left-slash or stab, and after a left-slash, it enters the left-ready stance. |
| StorePanelSort                               | Integer | 0 to 99. Lower-numbered items appear first in the store panel display in the game. Higher-numbered items appear last.                                                                                                                                                               |
| ILRStackSize                                 | Integer | Sometimes used instead of StackSize when calculating item cost                                                                                                                                                                                                                      |

### 5.2. Item Property Definitions

The table itempropdef.2da defines the available item properties that can be added to an Item's _PropertiesList_.

**Table 5.2: itempropdef.2da columns**

| **Column**      | **Type** | **Description**                                                                                                                                       |
| --------------- | -------- | ----------------------------------------------------------------------------------------------------------------------------------------------------- |
| Name            | Integer  | StrRef of name of the item property. eg., "Enhancement Bonus"                                                                                         |
| Label           | String   | Programmer label                                                                                                                                      |
| SubTypeResRef   | String   | ResRef of SubType 2da                                                                                                                                 |
| Cost            | Float    | Used in Cost calculation. See **Section 4.4**.                                                                                                        |
| CostTableResRef | Integer  | Index into **iprp_costtable.2da**                                                                                                                     |
| Param1ResRef    | Integer  | \***\* for properties that have no parameters, or whose parameters are defined in the subtype table. Otherwise, index into **iprp_paramtable.2da\*\*. |
| GameStrRef      | Integer  | StrRef of name of the item property, formatted to form a partial string. eg., "Enhancement Bonus:"                                                    |
| Description     | Integer  | StrRef of description of the item property                                                                                                            |

### 5.3. Item Property Availability

The table itemprops.2da defines the available properties that are available for different baseitems, according the PropColumn for the baseitem.

**Table 5.3: itemprops.2da columns**

| **Column** | **Type** | **Description** |
| ---------- | -------- | --------------- |
| numbered columns, column name is &lt;number&gt;_&lt;string&gt; | Integer | The &lt;number&gt; in the column name is a number that can appear under the PropColumn column in **baseitems.2da**. The &lt;string&gt; in the column name is unimportant and present only for the convenience of the programmer editing the 2da.<br><br>Each row in itemprops.2da has a one-to-one correspondence with a row in itempropdef.2da.<br><br>The value under these columns is 1 if the property on the row is available for the specified property column<br><br>The value is **** if the property is not available<br><br>See **Section 4.2**. |
| StringRef | Integer | StrRef of the property name. Should be the same as the matching StrRef under the "Name" column of itempropdef.2da. |
| Label | String | Programmer label.<br><br>Depending on the row within itemprops.2da, there may be multiple additional columns after the Label column, with no heading. This is a violation of the 2da formatting convention where all rows have the same number of columns and all columns have a header.<br><br>However, this violation of the rules does not matter, because in this case, the Label column is known to always be the last column, with no meaningful columns after it.<br><br>This is the only 2da where, if you wish to add columns, you should insert them, not append them to the end. The insertion should be immediately before the Label column. |

### 5.4. Item Property Subtype Tables

All subtype tables contain the columns specified in Table 5.4.1.

**Table 5.4: subtype table 2da columns**

| **Column** | **Type** | **Description** |
| ---------- | -------- | --------------- |
| Name | Integer | StrRef of the name of the SubType. |
| Label | String | Programmer label |
| Cost | Float | (Required if itempropdef.2da has **** for the Cost) |
| Param1ResRef | Integer | Index into iprp_paramtable.2da. Specifies the param table for this item property's subtype. The ResRef of the param table is listed in the TableResRef column of iprp_paramtable.2da.<br><br>If the value in this column is ****, check the Param1ResRef column in itempropdefs.2da, using the same row index into itempropdefs.2da that brought you to this subtype table in the first place. |

Some subtype tables contain additional columns beyond those specified in Table 5.4.1. Those tables are detailed below.

**Table 5.4.1: iprp_ammotype.2da additional columns**

| **Additional Column** | **Type** | **Description** |
| --------------------- | -------- | --------------- |
| AmmoType | Integer | Index into baseitems.2da |

**Table 5.4.2: iprp_feats.2da additional columns**

| **Additional Column** | **Type** | **Description** |
| --------------------- | -------- | --------------- |
| FeatIndex | Integer | Index into feat.2da |

**Table 5.4.3: iprp_spells.2da additional columns**

| **Additional Column** | **Type** | **Description**                                                                            |
| --------------------- | -------- | ------------------------------------------------------------------------------------------ |
| CasterLvl             | Integer  | Cast the spell as if the caster had the specified level.                                   |
| InnateLvl             | Float    | Spell Level. Cantrips are 0.5. All other spells have InnateLvl equal to their Spell Level. |
| SpellIndex            | Integer  | Index into `spells.2da`                                                                      |
| PotionUse             | Integer  | Can be applied to potions                                                                  |
| WandUse               | Integer  | Can be applied to wands                                                                    |
| GeneralUse            | Integer  | Can be applied to items that are not potions or wands                                      |
| Icon                  | String   | ResRef of TGA icon to use ingame                                                           |

**Table 5.4.4: spellshl.2da columns**

| **Additional Column** | **Type** | **Description** |
| --------------------- | -------- | --------------- |
| Letter                | String   | Unused.         |

### 5.5. Item Cost Tables

The table iprp_paramtable.2da defines the available cost tables.

**Table 5.5: iprp_costtable.2da columns**

| **Column** | **Type** | **Description**                                  |
| ---------- | -------- | ------------------------------------------------ |
| Name       | String   | ResRef of CostTable 2da                          |
| Label      | String   | Programmer label                                 |
| ClientLoad | Integer  | 0 if loaded by client 1 if loaded by game client |

### Cost Tables

All cost tables contain the columns specified in Table 5.5.1.

**Table 5.5.1: cost table 2da columns**

| **Column** | **Type** | **Description**                                                                                                                                                                                                                                                                                |
| ---------- | -------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Cost       | Float    | Used in Item Cost calculation. See **Section 4.4**.                                                                                                                                                                                                                                            |
| Name       | Integer  | StrRef of the name of the cost table entry. If the Name is \***\*, then the cost table value for this row is not available for assignment to an ItemProperty. That is, an ItemProperty may not have its CostValue Field set to the index of a row that contains \*\*** for the Name. eg., "+1" |
| Label      | String   | Programmer label                                                                                                                                                                                                                                                                               |

Some cost tables contain additional columns beyond those specified in Table 5.5.1. Those tables are detailed below.

**Table 5.5.2: iprp_ammocost.2da additional columns**

| **Additional Column** | **Type** | **Description**                                                                                                                                |
| --------------------- | -------- | ---------------------------------------------------------------------------------------------------------------------------------------------- |
| Arrow                 | String   | ResRef of Item Blueprint (UTI file) to use to create instances of the ammunition. \*\*\*\* if there is no arrow blueprint for this ammo type.  |
| Bolt                  | String   | ResRef of Item Blueprint (UTI file) to use to create instances of the ammunition. \*\*\*\* if there is no bolt blueprint for this ammo type.   |
| Bullet                | String   | ResRef of Item Blueprint (UTI file) to use to create instances of the ammunition. \*\*\*\* if there is no bullet blueprint for this ammo type. |

**Table 5.5.3: iprp_bonuscost.2da additional columns**

| **Additional Column** | **Type** | **Description**                                        |
| --------------------- | -------- | ------------------------------------------------------ |
| Value                 | Integer  | Amount of bonus to ability score, etc. as appropriate. |

**Table 5.5.4: iprp_chargecost.2da additional columns**

| **Additional Column** | **Type** | **Description**                                                         |
| --------------------- | -------- | ----------------------------------------------------------------------- |
| PotionCost            | Integer  | Use this column instead of the Cost column if the baseitem is a potion. |
| WandCost              | Integer  | Use this column instead of the Cost column if the baseitem is a wand.   |

**Table 5.5.5: damagecost.2da additional columns**

| **Additional Column** | **Type** | **Description**                                                                                                |
| --------------------- | -------- | -------------------------------------------------------------------------------------------------------------- |
| NumDice               | Integer  | Number of dice to throw                                                                                        |
| Die                   | Integer  | Size of each die to throw                                                                                      |
| Rank                  | Integer  | Strength of this damage bonus relative to the others in the 2da. Starts from 1 for the weakest, and counts up. |
| GameString            | Integer  | StrRef of string to display in the game for the damage amount. eg. "1d4"                                       |

**Table 5.5.6: damvulcost.2da additional columns**

| **Additional Column** | **Type** | **Description** |
| --------------------- | -------- | --------------- |
| Value | Integer | Percent amount of damage vulnerability |

**Table 5.5.7: immuncost.2da additional columns**

| **Additional Column** | **Type** | **Description** |
| --------------------- | -------- | --------------- |
| Value | Integer | Percent amount of immunity |

**Table 5.5.8: meleecost.2da additional columns**

| **Additional Column** | **Type** | **Description** |
| --------------------- | -------- | --------------- |
| Value | Integer | Amount of bonus to damage, AC, attack bonus, enhancement bonus, saving throw, etc. as appropriate |

**Table 5.5.9: monstcost.2da additional columns**

| **Additional Column** | **Type** | **Description**           |
| --------------------- | -------- | ------------------------- |
| NumDice               | Integer  | Number of dice to throw   |
| Die                   | Integer  | Size of each die to throw |

**Table 5.5.10: neg5cost and neg10cost.2da additional columns**

| **Additional Column** | **Type** | **Description** |
| --------------------- | -------- | --------------- |
| Value | Integer | Amount of penalty to ability score, AC, skill, etc. as appropriate. |

**Table 5.5.11: onhitcost.2da additional columns**

| **Additional Column** | **Type** | **Description** |
| --------------------- | -------- | --------------- |
| Value | Integer | DC of the onhit effect |

**Table 5.5.12: redcost.2da additional columns**

| **Additional Column** | **Type** | **Description** |
| --------------------- | -------- | --------------- |
| Value | Float | Amount by which to reduce weight of the item's contents. Mulitply weight by this value and subtract the result from the normal weight. |

**Table 5.5.13: resistcost.2da additional columns**

| **Additional Column** | **Type** | **Description** |
| --------------------- | -------- | --------------- |
| Amount | Integer | Number of hit points of damage resistance |

**Table 5.5.14: soakcost.2da additional columns**

| **Additional Column** | **Type** | **Description** |
| --------------------- | -------- | --------------- |
| Amount | Integer | In damage reduction, the number of hit points by which to reduce the damage taken from an attack that does not exceed the required attack bonus. |

**Table 5.5.15: spellcost.2da additional columns**

| **Additional Column** | **Type** | **Description** |
| --------------------- | -------- | --------------- |
| SpellIndex | Integer | Index into spells.2da |

**Table 5.5.16: srcost.2da additional columns**

| **Additional Column** | **Type** | **Description** |
| --------------------- | -------- | --------------- |
| Value | Integer | Amount of spell resistance |

**Table 5.5.17: weightcost.2da additional columns**

| **Additional Column** | **Type** | **Description** |
| --------------------- | -------- | --------------- |
| Value | Float | Amount by which to muliply the item's weight to obtain its new, modified weight. |

### 5.6. Item Param Tables

The table iprp_paramtable.2da defines the available param tables.

**Table 5.6: iprp_paramtable.2da columns**

| **Column**  | **Type** | **Description**      |
| ----------- | -------- | -------------------- |
| Name        | Integer  | StrRef of Param name |
| Label [sic] | String   | Programmer label     |
| TableResRef | String   | ResRef of Param 2da  |

### ItemProperty Param tables

All param tables contain the columns specified in Table 5.6.1.

**Table 5.6.1: param table 2da columns**

| **Column** | **Type** | **Description**                  |
| ---------- | -------- | -------------------------------- |
| Name       | Integer  | StrRef of the name of the param. |
| Label      | String   | Programmer label                 |

Some param tables contain additional columns beyond those specified in Table 5.6.1. Those tables are detailed below.

**Table 5.6.2: onhitdur.2da additional columns**

| **Additional Column** | **Type** | **Description**                                                    |
| --------------------- | -------- | ------------------------------------------------------------------ |
| EffectChance          | Integer  | Percent chance to cause the onhit effect when landing a hit.       |
| DurationRounds        | Integer  | Number of rounds of duration of the effect if saving throw failed. |

**Table 5.6.3: weightinc.2da additional columns**

| **Additional Column** | **Type** | **Description**              |
| --------------------- | -------- | ---------------------------- |
| Value                 | Float    | Additional weight in pounds. |

### 5.7. Miscellaneous Item Tables

#### 5.7.1 Item Valuation

**Table 5.7.1.1: itemvalue.2da columns**

| **Column**         | **Type** | **Description**                                                                                                                         |
| ------------------ | -------- | --------------------------------------------------------------------------------------------------------------------------------------- |
| LABEL              | String   | Programmer label. A String referring to the character level that the row corresponds to. Row 0 is level 1, row 1 is level 2, and so on. |
| DESIREDTREASURE    | Integer  | Always 0                                                                                                                                |
| MAXSINGLEITEMVALUE | Integer  | Cost of the most expensive item that a character of level (row+1) can use.                                                              |
| TOTALVALUEFILTER   | Integer  | Unused.                                                                                                                                 |

**Table 5.7.1.2: skillvsitemcost.2da columns**

| **Column**     | **Type** | **Description**                                                                                                                                                                                                                                           |
| -------------- | -------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| DeviceCostMax  | Integer  | Cost of the most expensive item that can be identified with a Lore skill check equal to the row index.                                                                                                                                                    |
| SkillReq_Class | Integer  | Required number of ranks in Use Magic Device skill to be able to use an item that has a cost equal to or less than the DeviceCostMax, if the item type is not normally useable any of the character's classes, but is not restricted by race or alignment |
| SkillReq_Race  | Integer  | Required number of ranks in Use Magic Device skill to be able to use an item that has a cost equal to or less than the DeviceCostMax, if the item type is not normally useable by the character's race.                                                   |
| SkillReq_Align | Integer  | Required number of ranks in Use Magic Device skill to be able to use an item that has a cost equal to or less than the DeviceCostMax, if the item type is not normally useable by the character's alignment.                                              |

#### 5.7.2 Armor Statistics

**Table 5.7.2.1: capart.2da columns**

| **Column** | **Type** | **Description**                                         |
| ---------- | -------- | ------------------------------------------------------- |
| NAME       | String   | Always 0.                                               |
| MDLNAME    | String   | \<bodypart\> portion of an armor part ResRef. See **Section 4.1.4**. |
| NODENAME   | String   | Node on the base model to append this part model to.    |

**Table 5.7.2.2: armor.2da columns**

| **Column**      | **Type** | **Description**                                                                           |
| --------------- | -------- | ----------------------------------------------------------------------------------------- |
| ACBONUS         | Integer  | Base AC bonus of the item                                                                 |
| DEXBONUS        | Integer  | Max Dexterity bonus when wearing armor of this AC                                         |
| ACCHECK         | Integer  | Armor Skill Check penalty                                                                 |
| ARCANEFAILURE%  | Integer  | Percent chance of Arcane Spell Failure                                                    |
| WEIGHT          | Integer  | Weight of armor having this AC                                                            |
| COST            | Integer  | BaseCost for an armor item that has this AC                                               |
| DESCRIPTIONS    | Integer  | StrRef of a qualitative description of this armor type, that does not include statistics. |
| BASEITEMSTATREF | Integer  | StrRef of description that includes statistics on the armor type.                         |

#### 5.7.3 Weapon Combat Sounds

The **weaponsounds.2da** file specifies what sounds play when weapons of specific types hit targets of various material types. Each row refers to a weapon type. In baseitems.2da, the _WeaponMatType_
column value is an index into weaponsounds.2da. The non-label columns in weaponsounds.2da specify the ResRefs of wave files to play when a weapon hits the material type named in the column.

Material type is determined by the appearance of the object being hit:

For creatures, the _SOUNDAPPTYPE_ in **appearance.2da** indexes into **appearancesndset.2da**.
If the row in appearancesndset.2da has **** values, then the material type is determined by the armor worn by the creature. The base AC of the creature's armor is an index into **defaultacsounds.2da**, where the _ArmorType_ string value serves as a column name in **weaponsounds.2da** by randomly appending a "0" or a "1" to the end of it.

For placeables, the _SoundAppType_ in **placeables.2da** indexes into **placeableobjsnds.2da**, in which the _ArmorType_ string value serves as a column name in weaponsounds.2da by randomly appending a
"0" or a "1" to the end of it.

**Table 5.7.3.1: weaponsounds.2da columns**

| **Column**          | **Type** | **Description**                                                                                                                                                                                                                 |
| ------------------- | -------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Label               | String   | Programmer label                                                                                                                                                                                                                |
| Leather0 Leather1   | String   | -                                                                                                                                                                                                                               |
| Chain0 Chain1       | String   | -                                                                                                                                                                                                                               |
| Plate0 Plate1       | String   | -                                                                                                                                                                                                                               |
| Stone0 Stone1       | String   | In addition to those cases where the target really does use stone as its material type for onhit sounds: If the target has Stoneskin, Shadowskin, or Petrification, then use this sound instead of what would normally be used. |
| Wood0 Wood1         | String   | In addition to those cases where the target really does use wood as its material type for onhit sounds: Played when target has Barkskin.                                                                                        |
| Chitin0 Chitin1     | String   | -                                                                                                                                                                                                                               |
| Scale0 Scale1       | String   | -                                                                                                                                                                                                                               |
| Ethereal0 Ethereal1 | String   | -                                                                                                                                                                                                                               |
| Miss0 Miss1         | String   | -                                                                                                                                                                                                                               |
| Parry0              | String   | Played on a parry or on a miss that caused the parry animation to play.                                                                                                                                                         |
| Critical0           | String   | Played on a critical hit.                                                                                                                                                                                                       |

**Table 5.7.3.2: appearancesndset.2da columns**

| **Column**                                                                     | **Type** | **Description**                                                                                                                                                                                                                                                                                             |
| ------------------------------------------------------------------------------ | -------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Label                                                                          | String   | Programmer label                                                                                                                                                                                                                                                                                            |
| ArmorType | String | Specifies set of columns in **weaponsounds.2da** to use when creature is hit.<br><br>**** means to use the ArmorType from **armourtypes.2da** for the armor that the creature is wearing. |
| WeapTypeL<br>WeapTypeR<br>WeapTypeS<br>WeapTypeClsLW<br>WeapTypeClsH<br>WeapTypeRch<br>MissIndex | Integer | Row in **weaponsounds.2da** to use when creature attacks and hits. In order, these columns correspond to the following attacks: left swing, right swing, stab, close low, close high, far reach, and miss<br><br>**** means to use the WeaponMatType of the creature's equipped weapon from **baseitems.2da**. |
| Looping                                                                        | String   | ResRef of looping WAV to emanate from the creature                                                                                                                                                                                                                                                          |
| FallFwd                                                                        | String   | ResRef of WAV to play when creature falls forward.                                                                                                                                                                                                                                                          |
| FallBck                                                                        | String   | ResRef of WAV to play when creature falls backward.                                                                                                                                                                                                                                                         |

**Table 5.7.3.3: defaultacsounds.2da columns**

| **Column** | **Type** | **Description**                                                                      |
| ---------- | -------- | ------------------------------------------------------------------------------------ |
| ArmorIndex | Integer  | Index into **parts_chest.2da** that matches up to the armor the creature is wearing. |
| ArmorType  | String   | Armor type. Use as column name in **weaponsounds.2da**.                              |

---

[← Previous: Calculations and Procedures](Ch4_Calculations_and_Procedures.md) | [Back to Main Document →](../Bioware_Aurora_Item_Format.md)

---

BioWare Corp. http://www.bioware.com
