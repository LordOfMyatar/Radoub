# BioWare Aurora Engine - Item Format

## Chapter 2: Item Struct

[← Back to Main Document](../Bioware_Aurora_Item_Format.md) | [← Previous: Introduction](Ch1_Introduction.md)

---

The tables in this section describe the GFF Struct for an Item. Some Fields are only present on Instances and others only on Blueprints.

For List Fields, the tables indicate the StructID used by the List elements.

### 2.1 Common Item Fields

#### 2.1.1 Item Fields in All Items

The Table below lists the Fields that are present in all Item Structs, regardless of whether they are found in blueprints, instances, toolset data, or game data.

**Table 2.1.1: Fields in all Item Structs**

| **Label**      | **Type**      | **Description**                                                                                                                                             |
| -------------- | ------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------- |
| AddCost        | DWORD         | Additional cost                                                                                                                                             |
| BaseItem       | INT           | Index into **baseitems.2da**.                                                                                                                               |
| Charges        | BYTE          | Number of charges left on this item. Note that there is no property for the original number of charges.                                                     |
| Cost           | DWORD         | Cost of the Item                                                                                                                                            |
| Cursed         | BYTE          | 1 if the item cannot be removed from its container (ie., undroppable, unsellable, unpickpocketable). 0 if the item can be removed from its container.       |
| DescIdentified | CExoLocString | Identified description                                                                                                                                      |
| Description    | CExoLocString | Unidentified description                                                                                                                                    |
| LocName        | CExoLocString | Name of the Item as it appears on the toolset's Item palette, in the Name field of the toolset's Item Properties dialog, and in the game if it has been Identified. |
| Plot           | BYTE          | 1 if this is a Plot Item. Plot Items cannot be sold. 0 if this is a normal Item.                                                                            |
| PropertiesList | List          | List of ItemProperty Structs. StructID 0. **See Section 2.1.3**.                                                                                            |
| StackSize      | WORD          | 1 if item has an unstackable base item type. Otherwise, this is the stack size of the item, always greater than or equal to 1. (eg., 50 arrows)             |
| Stolen         | BYTE          | 1 if stolen<br>0 if not stolen                                                                                                                              |
| Tag            | CExoString    | Tag of the Item. Up to 32 characters.                                                                                                                       |
| TemplateResRef | CResRef       | For blueprints (UTI files), this should be the same as the filename. For instances, this is the ResRef of the blueprint that the instance was created from. |

#### 2.1.2 Additional Item Fields by ModelType

The **BaseItem** of an item specifies a row in **baseitems.2da** that provides information on the item type. One of the columns in baseitems.2da is the **ModelType**, which controls what the item looks like. Refer to the ModelType entry in **Table 4.1.1**.

Depending on the ModelType, an Item Struct will contain the Fields listed in one or more of the tables below, in addition to those in Table 2.1.1.

**Table 2.1.2.1: Fields in Layered (ModelType 1) and Armor (ModelType 3) Item Structs**

| **Label**     | **Type** | **Description**                                                                                    |
| ------------- | -------- | -------------------------------------------------------------------------------------------------- |
| Cloth1Color   | BYTE     | Index into a row of pixels in pal_cloth01.tga. Specifies the colors to use for cloth1 PLT layer.   |
| Cloth2Color   | BYTE     | Index into a row of pixels in pal_cloth01.tga. Specifies the colors to use for cloth2 PLT layer.   |
| Leather1Color | BYTE     | Index into a row of pixels in pal_leath01.tga. Specifies the colors to use for leather1 PLT layer. |
| Leather2Color | BYTE     | Index into a row of pixels in pal_leath01.tga. Specifies the colors to use for leather2 PLT layer. |
| Metal1Color   | BYTE     | Index into a row of pixels in pal_armor01.tga. Specifies the colors to use for metal1 PLT layer.   |
| Metal2Color   | BYTE     | Index into a row of pixels in pal_armor01.tga. Specifies the colors to use for metal2 PLT layer.   |

**Table 2.1.2.2: Fields in Simple (ModelType 0) and Layered (ModelType 1) Item Structs**

| **Label**  | **Type** | **Description** |
| ---------- | -------- | --------------- |
| ModelPart1 | BYTE     | part number     |

**Table 2.1.2.3: Fields in Composite Item (ModelType 2) Structs**

| **Label**  | **Type** | **Description** |
| ---------- | -------- | --------------- |
| ModelPart1 | BYTE     | part number     |
| ModelPart2 | BYTE     | part number     |
| ModelPart3 | BYTE     | part number     |

**Table 2.1.2.4: Fields in Armor (ModelType 3) Item Structs**

| **Label**        | **Type** | **Description**               |
| ---------------- | -------- | ----------------------------- |
| ArmorPart_Belt   | BYTE     | Index into `parts_belt.2da`     |
| ArmorPart_LBicep | BYTE     | Index into `parts_bicep.2da`    |
| ArmorPart_LFArm  | BYTE     | Index into `parts_forearm.2da`  |
| ArmorPart_LFoot  | BYTE     | Index into `parts_foot.2da`     |
| ArmorPart_LHand  | BYTE     | Index into `parts_hand.2da`     |
| ArmorPart_LShin  | BYTE     | Index into `parts_shin.2da`     |
| ArmorPart_LShoul | BYTE     | Index into `parts_shoulder.2da` |
| ArmorPart_LThigh | BYTE     | Index into `parts_legs.2da`     |
| ArmorPart_Neck   | BYTE     | Index into `parts_neck.2da`     |
| ArmorPart_Pelvis | BYTE     | Index into `parts_pelvis.2da`   |
| ArmorPart_RBicep | BYTE     | Index into `parts_bicep.2da`    |
| ArmorPart_RFArm  | BYTE     | Index into `parts_forearm.2da`  |
| ArmorPart_RFoot  | BYTE     | Index into `parts_foot.2da`     |
| ArmorPart_RHand  | BYTE     | Index into `parts_hand.2da`     |
| ArmorPart_Robe   | BYTE     | Index into `parts_robe.2da`     |
| ArmorPart_RShin  | BYTE     | Index into `parts_shin.2da`     |
| ArmorPart_RShoul | BYTE     | Index into `parts_shoulder.2da` |
| ArmorPart_RThigh | BYTE     | Index into `parts_legs.2da`     |
| ArmorPart_Torso  | BYTE     | Index into `parts_torso.2da`    |

#### 2.1.3 ItemProperty Fields

Each ItemProperty element in the **PropertiesList** of an Item contains the Fields given below.

**Table 2.1.3: Fields in ItemProperty Structs (StructID 0)**

| **Label**    | **Type** | **Description**                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                        |
| ------------ | -------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| ChanceAppear | BYTE     | Obsolete. Always 100.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                  |
| CostTable    | BYTE     | Index into **iprp_costtable.2da**. Equal to the value in the **CostTableResRef** column of **itempropdef.2da** at the row specified by the **PropertyName** Field. Must always be specified.                                                                                                                                                                                                                                                                                                                                               |
| CostValue    | WORD     | Index into a cost table. The ResRef of the cost table is the value under the **Name** column of **iprp_costtable.2da**, at the row specified by the **CostTable** Field. Must always be specified.                                                                                                                                                                                                                                                                                                                                         |
| Param1       | BYTE     | Index into **iprp_paramtable.2da**. Specifies a params table.<br><br>This value is -1 if there are no parameters. There are 2 ways to determine if there are no parameters depending on whether the ItemProperty struct has a non-zero Subtype Field or not:<br><br>**If there is a Subtype:** there are no parameters if the **Param1ResRef** column does not exist in the subtype table (see the Subtype Field description in this struct) or if there is a **Param1ResRef** column and the value is **** in the row indexed by the Subtype Field.<br><br>**If there is no Subtype:** there are no parameters if the **itempropdef.2da** has **** as the value under its **Param1ResRef** column at the row indexed by the PropertyName Field. |
| Param1Value  | BYTE     | Index into a params 2da. The ResRef of the params 2da is specified by the **TableResRef** column of **iprp_paramtable.2da** at the row specified by the **Param1** Field. If an ItemProperty does not have a params table, this Field defaults to 0.                                                                                                                                                                                                                                                                                       |
| Param2       | BYTE     | Obsolete. Would be the same as Param1 but using the Param1ResRef column instead.                                                                                                                                                                                                                                                                                                                                                                                                                                                       |
| Param2Value  | BYTE     | Obsolete. Would be the same as Param1.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                 |
| PropertyName | WORD     | Index into **itempropdefs.2da**. Must always be specified.                                                                                                                                                                                                                                                                                                                                                                                                                                                                             |
| Subtype      | WORD     | Index into a item property subtype 2da. The ResRef of the subtype 2da is specified by the **SubTypeResRef** column value in **itempropdefs.2da**, at the row specified by the **PropertyName** Field. Set to 0 if there is no SubTypeResRef.                                                                                                                                                                                                                                                                                               |

### 2.2. Item Blueprint Fields

The Top-Level Struct in a UTI file contains all the Fields in Table 2.1.1 above, plus those in Table 2.2 below.

**Table 2.2: Fields in Item Blueprint Structs**

| **Label**      | **Type**   | **Description**                                                                                                                                                                                                                                                                                  |
| -------------- | ---------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| Comment        | CExoString | Module designer comment.                                                                                                                                                                                                                                                                         |
| PaletteID      | BYTE       | ID of the node that the Item Blueprint appears under in the Item palette.                                                                                                                                                                                                                        |
| TemplateResRef | CResRef    | The filename of the UTI file itself. It is an error if this is different. Certain applications check the value of this Field instead of the ResRef of the actual file. If you manually rename a UTI file outside of the toolset, then you must also update the **TemplateResRef** Field inside it. |

### 2.3. Item Instance Fields

An Item Instance Struct in a GIT file contains all the Fields in Table 2.1.1, plus those in Table 2.3 below.

An Item Instance Struct in an InventoryObject instance does not contain any additional Fields.

**Table 2.3: Fields in Item Instance Structs**

| **Label**                     | **Type** | **Description**                                                                        |
| ----------------------------- | -------- | -------------------------------------------------------------------------------------- |
| TemplateResRef                | CResRef  | For instances, this is the ResRef of the blueprint that the instance was created from. |
| XOrientation<br>YOrientation | FLOAT    | x,y vector pointing in the direction of the item's orientation                         |
| XPosition<br>YPosition<br>ZPosition | FLOAT | (x,y,z) coordinates of the Item within the Area that it is located in. |

### 2.4. Item Game Instance Fields

After a GIT file has been saved by the game, the Item Instance Struct not only contains the Fields in Table 2.1.1 and Table 2.3, it also contains the Fields in Table 2.4.

INVALID_OBJECT_ID is a special constant equal to 0x7f000000 in hex.

**Table 2.4: Fields in Item Instance Structs in SaveGames**

| **Label** | **Type** | **Description**                                                                                                        |
| --------- | -------- | ---------------------------------------------------------------------------------------------------------------------- |
| ObjectId  | DWORD    | Object ID used by game for this object.                                                                                |
| VarTable  | List     | List of scripting variables stored on this object. StructID 0. See **Section 3** of the **[Common GFF Structs](../Bioware_Aurora_CommonGFFStructs.md)** document. |

---

[← Previous: Introduction](Ch1_Introduction.md) | [Next: Chapter 3 - InventoryObject Struct →](Ch3_InventoryObject_Struct.md)
