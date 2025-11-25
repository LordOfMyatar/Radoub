# BioWare Aurora Engine - Item Format

## Chapter 4: Calculations and Procedures

[← Back to Main Document](../Bioware_Aurora_Item_Format.md) | [← Previous: InventoryObject Struct](Ch3_InventoryObject_Struct.md)

---

### 4.1. Icon and Model Part Availability

The icons and model resources for items have ResRefs that end in a 3-digit, zero-padded numerical string.

>Examples: helm_001.mdl, pmh0_bicepl006.mdl, waxbt_b_011.mdl, iwaxbt_t_011.tga.

This section describes how to determine the ResRefs (aka filenames) of the model and icon resources for an item, depending on its **ModelType** as specified in **baseitems.2da**. In the descriptions below, the following tokens are used:

* = the *ItemClass*from baseitems.2da.
* = part number

If an item model cannot be found, then the MDL file specified by the **DefaultModel** column in baseitems.2da will be used instead.

If an item icon cannot be found, then the icon specified by the **DefaultIcon** column in baseitems.2da will be used instead.

The icon for an item must have dimensions equal to 32 pixels multiplied by the InvSlotWidth and InvSlotHeight values from **baseitems.2da**.

### 4.1.1. Simple
```
Model = <ItemClass>_<number>.mdl
  (eg., it_torch_001.mdl)
Icon = i<ItemClass>_<number>.tga
  (eg., iit_torch_001.tga)
```
Simple item types typically use one model for all variants, with only the icon being different.

The available icons in the toolset consist of all existing TGA icons from the **MinRange** to the **MaxRange** as specified in baseitems.2da, using the Icon ResRef naming convention given above.

### 4.1.2. Layered

```
Model = <ItemClass>_<number>.mdl
  (eg., helm_001.mdl)
Icon = i<ItemClass>_<number>.plt
  (eg., ihelm_001.plt)
```

The available parts are determined by scanning for all existing PLT icons from the **MinRange** to the **MaxRange** as specified in baseitems.2da, using the Icon ResRef naming convention given above. If an icon exists, assume that the MDL also exists and make that part number selectable in the toolset. (Exception: for helmets: check for the MDL instead of the PLT)

### 4.1.3. Composite

```
Model = <ItemClass>_<position>_<number>.mdl
  (eg., waxbt_b_011.mdl, waxbt_m_011.mdl, waxbt_t_011.mdl)
Icon = i<Model ResRef>.tga
  (eg., iwaxbt_b_011.tga, iwaxbt_m_011.tga, iwaxbt_t_011.tga)
where
<position> = b (bottom), m (middle), or t (top)
```

The is one of the following letters: b (bottom), m (middle), or t (top), usually corresponding to the pommel, hilt, or blade of a weapon.

For weapons, the 3-digit field is broken up into two parts. The first 2 digits map to a physical shape variant, and the last digit is a color variant. For example, parts 011, 021, and 031 all have the same color, but different shapes. Parts 011, 012, and 013 all have the same shape, but different colors.

To determine what colors are available for each of the b, m, and t portions of a composite item, do the following steps. Scan for icons, incrementing the from the baseitems.2da **MinRange** to **MaxRange**, and find first icon file that exists. Check the color code in the icon's resref (the last digit). This is the minimum available color. Increment the from there until another icon is found that has the same color. Recall what color the icon was before that. That color is the maximum available color.

To determine what shapes are available, scan for TGA icons from = (_MinRange_ + minimum color) to = _MaxRange_, incrementing by 10 each time. If an icon exists, then assume that all the MDL file exists as well, and also assume that the shape variant exists in all the other colors as well. For example, suppose that the minimum color found earlier was 2, the maximum color was 4, **MinRange** = 10, and **MaxRange** = 100. The icon scan would check for 012, 022, 032, ... 092. If 082 exists, for example, then assume that 083 and 084 also exist

When drawing the Icon for a composite item, the 3 portions are painted one after the other in the order:
bottom, middle, top. The order is important because the icons overlap.

### 4.1.4. Armor

```
Model = p<gender><race><phenotype>_<bodypart><number>.mdl
  (eg., pmh0_chest001.mdl)
Icon = ip<gender>_<bodypart><number>.plt
  (eg., ipm_chest001.plt)
where
<gender> = m or f
<bodypart> = belt, bicepl, bicepr, chest, footl, footr, forel, forer, handl, handr, legl, legr, neck,
pelvis, shinl, shinr, shol, shor--as listed in capart.2da
```

The complete icon for an Armor item consists of several individual bodypart icons drawn onto a surface in the following order: pelvis, chest, belt, right shoulder, left shoulder, robe. The order is important because the icons may overlap. There are no icons for the other body parts.

For each , the available values are determined by checking the appropriate **parts\_.2da** file, as given in **Table 4.1.4** below.

**Table 4.1.4: Armor body part and parts 2da matchups**

| **Body Part** | **2DA**        |
| ------------- | -------------- |
| belt          | parts_belt     |
| bicepl bicepr | parts_bicep    |
| chest         | parts_chest    |
| footl footr   | parts_foot     |
| forel forer   | parts_forearm  |
| handl handr   | parts_hand     |
| legl legr     | parts_legs     |
| neck          | parts_neck     |
| pelvis        | parts_pelvis   |
| robe          | parts_robe     |
| shinl shinr   | parts_shin     |
| shol shor     | parts_shoulder |

If a row in the **parts\_.2da** file has an
**ACBONUS** column value that is not \*\*\*\*, then the equal to that row index is available as a selectable bodypart. Note that in some cases, part 000 is available even though there are no 000 MDL or PLT files for armor. If part 000 is selected, then that armor part is empty. (For example, no shoulder or belt, or no robe.)

In the toolset, the available parts are sorted in order of increasing **ACBONUS** as listed in the parts 2da. If several parts have identical **ACBONUS**, then they are sorted in order of increasing row number. Note that for all armor parts except the chest, the **ACBONUS** 2da value is used only for sorting part numbers, and do not actually affect the AC of the armor.

Robe parts are different from other armor parts in that they may hide other parts. A robe-hidden part does not appear in the armor icon, and its 3D model is not rendered. The **parts_robe.2da** file contains additional columns beyond those normally present in a parts 2da. There exists one column for every other part, each one titled **HIDE**, where is one of the values in the Body Part column of **Table 4.1.4**. For example, HIDEFOREL, HIDELEGR and HIDECHEST. Each HIDE column contains a 1 if the specified part is hidden by the robe, or 0 if not.

### 4.2. Property Availability

To determine which Item Properties are available for a given base item type, get the number in the PropColumn column from **baseitems.2da** (**Table 5.1**), then find the column in **itemprops.2da** (**Table 5.3**) that starts with that number in its name. If a row in itemprops.2da has 1 as the value under that column, then the Item Property for the corresponding row in **itempropdefs.2da** (**Table 5.2**) is available for the baseitem. If the value in itemprops.2da is \*\*\*\*, then the corresponding Item Property in itempropdefs.2da is not available.

For example, baseitem 12 (amulet) has 16 as its **PropColumn** value. Thus, the available Item Properties are determined by checking under the column "16_Misc" in itemprops.2da. This means that AC Bonus vs. Alignment is available for amulets but Enhancement Bonus is not.

### 4.3. Property Description Construction

The format used by the toolset for an item property description is:
```
PropertyName : Subtype [CostValue] [Param1: Param1Value]
eg., Damage Bonus vs. Racial Type: Dragon [1 Damage] [Type: Acid]
eg., On Hit: Daze [DC = 14] [Duration: 50% / 2 rounds]
eg., Light [Dim (5m)] [Color: Blue]
```
The game uses similar formatting, but without the square brackets.

This section describes how to get the text for the various components of an Item Property description.

### 4.3.1. PropertyName

Look up the StrRef stored in column 0 (should be Name) of **itempropdef.2da** (**Table 5.2**), at the row indexed by the ItemProperty Struct's _PropertyName_ Field (see **Table 2.1.3**). This StrRef points to the name of the Item Property (eg., "Damage Bonus vs. Racial Type", "On Hit", "Light")

### 4.3.2. Subtype

In **itempropdef.2da**, look up the **SubTypeResRef** column value at the row indexed by the ItemProperty Struct's _PropertyName_ Field. If it is \***\*, then there are no subtypes for this Item Property, so there is no subtype portion of the Item Property description. Otherwise, the string under this column is the ResRef of the **subtype table\*\* 2da.

If there is a subtype table, load it and use the ItemProperty Struct's _Subtype_ Field as an index into the 2da. Get the StrRef from the _name_ column. This StrRef points to the name of the Subtype (eg., "Dragon", "Daze").

### 4.3.3. CostTable Value

In **itempropdef.2da**, look up the _CostTableResRef_ number at the row indexed by the ItemProperty Struct's _PropertyName_ Field. This should be the same as the ItemProperty Struct's _CostTable_ Field. Use the CostTableResRef value as an index into **iprp_costtable.2da** and get the string under the **Name** column. This is the ResRef of the cost table 2da to use.

Load the cost table 2da. Using the ItemProperty Struct's _CostValue_ Field as an index into the cost table, get the _Name_ column StrRef. This StrRef points to the name of the Cost value (eg., "1 Damage", "DC = 14", "Dim (5m)")

### 4.3.4. Param

Get the ResRef of the Param Table.

If the ItemProperty has a subtype table (see **Section 4.3.2**), then look for a **Param1ResRef** column in the subtype table. This column contains the _param table index_, and should be identical the _Param1_ Field of the ItemProperty Struct.

If the ItemProperty does not have a subtype table, or the subtype table does not have a _Param1ResRef_ column, then look under the Param1ResRef column in **itempropdef.2da**, and use that as the param table index. In this case as well, the index should equal the _Param1_ Field of the ItemProperty Struct.

Use the param table index as an index into **iprp_paramtable.2da**. Look under the **Name** column for a StrRef, and look under the **TableResRef** column for a string. The _Name_ StrRef points to the text for the name of the parameter (eg., "Type", "Duration", "Color"). The _TableResRef_ string is the ResRef of the **param table** 2da.

Use the ItemProperty Struct's _Param1Value_ as an index into the param table found above, and get the StrRef under the "Name" column. This StrRef points to the name of the param value (eg., "Acid", "50% / 2 Rounds", "Red")

### 4.4. Cost Calculation

The cost of an item is determined by the following formula:

```
ItemCost = [BaseCost + 1000*(Multiplier^2 - NegMultiplier^2) + SpellCosts]*MaxStack*BaseMult + AdditionalCost
```
where:
```
BaseMult = **ItemMultiplier** column value from **baseitems.2da**.
AdditionalCost = *AddCost*Field from the Item Struct.
and the other terms are as defined in the following subsections:
```
### 4.4.1. Base Cost

BaseCost = **BaseCost** column value from **baseitems.2da** for all items except armor.

For armor, BaseCost is the cost in **armor.2da** for the armor's base AC. The armor's base AC is the **ACBONUS** from **parts_chest.2da**, using the part number of the chest as the index into parts_chest.2da.

### 4.4.2. Multipliers

Multiplier is the sum of the costs of all the Item Properties whose costs are positive. That is: NegMultiplier is the sum of the costs of all Item Properties whose costs are negative.

If an Item Property has a _PropertyName_ of 15 (Cast Spell), then omit it from the Multiplier/NegMultiplier totals. It will be handled when calculating the SpellCosts instead.

To calculate the cost of a single Item Property, use the following formula:

```
ItemPropertyCost = PropertyCost +
SubtypeCost + CostValue
```
Add the ItemProperty's cost to the Multiplier total if it is positive. Add it to the NegMultiplier total if it is negative.

Note that Item Property Params do not affect Item Property cost.

The PropertyCost, SubtypeCost, and CostValue terms are obtained as described below.

#### PropertyCost

In **itempropdef.2da**, get the floating point value in the **Cost** column, at the row indexed by the _PropertyName_ Field of the ItemProperty Struct. If the **Cost** column value is \*\*\*\*, treat it as 0. This floating point value is the PropertyCost.

#### SubtypeCost

If the PropertyCost obtained above from **itempropdef.2da** was 0, then get the ResRef in the _SubTypeResRef_ column of itempropdef.2da, at the row indexed by the 2*PropertyName* Field of the ItemProperty Struct. This is the resref of the subtype table 2da.

In the subtype 2da, get the floating point value in the _Cost_ column at the row indexed by the _Subtype_ Field of the ItemProperty Struct. This floating point value is the SubtypeCost.

Only get the SubtypeCost if the PropertyCost was 0. If the PropertyCost was greater than 0, then the SubtypeCost is automatically 0 instead.

#### CostValue

In **iprp_costtable.2da**, get the string in the _Name_ column at the row indexed by the _CostTable_ Field in the ItemProperty Struct. This is the ResRef of the cost table 2da.

In the cost table, get the floating point value in the **Cost** column in the row indexed by the _CostValue_ Field in the ItemProperty Struct. This floating point value is the CostValue.

### 4.4.3. Cast Spell Costs

To calculate the cost of a single Cast Spell Item Property, use the following formula:
```
CastSpellCost = (PropertyCost + CostValue)* SubtypeCost
```
The PropertyCost, SubtypeCost, and CostValue terms are obtained in the same way as for nonCastSpell Item Properties.

After calculating all the CastSpellCost values for all the Cast Spell Item Properties, modify them as follows:

- Most expensive: multiply by 100%
- Second most expensive: multiply by 75%
- All others: multiply by 50%

After adjusting the CastSpellCosts, add them up to obtain the total SpellCosts value. Use the total SpellCosts to calculate the total ItemCost using the formula given at the very beginning of Section 4.4.

### 4.5. Required Lore and Level

The character level required to use an item is equal to 1 plus the row index in **itemvalue.2da** that contains the smallest number in the MAXSINGLEITEMVALUE column that is still greater than or equal to the cost of the item.

The Lore skill check required to identify an item is equal to the row index in **skillvsitemcost.2da**
that contains the smallest number in the DeviceCostMax column that is still greater than or equal to the cost of them item.

---

[← Previous: InventoryObject Struct](Ch3_InventoryObject_Struct.md) | [Next: Chapter 5 - Item-related 2DA Files →](Ch5_Item_Related_2DA_Files.md)
