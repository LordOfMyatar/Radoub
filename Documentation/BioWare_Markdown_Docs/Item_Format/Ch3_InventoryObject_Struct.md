# BioWare Aurora Engine - Item Format

## Chapter 3: InventoryObject Struct

[← Back to Main Document](../Bioware_Aurora_Item_Format.md) | [← Previous: Item Struct](Ch2_Item_Struct.md)

---

An InventoryObject is a Struct that may be present in the inventory list of a Container object. Creatures, Items, and Placeable Objects can all be Containers.

A Container Item contains a Field called **ItemList**, of GFF type List, containing InventoryObject Structs. Items in the toolset never have an ItemList, but they can in a savegame (inside the GIT and/or IFO files) or character file (BIC).
The game saves the StructIDs as being equal to the index of the Struct element within the ItemList.

### 3.1 Common InventoryObject Fields

The Table below lists the Fields that are present in all InventoryObject Structs, regardless of where they are found.

**Table 3.1: Fields in all InventoryObject Structs**

| **Label**  | **Type** | **Description**                      |
| ---------- | -------- | ------------------------------------ |
| Repos_PosX | WORD     | x-position of item in inventory grid |
| Repos_PosY | WORD     | y-position of item in inventory grid |

### 3.2. InventoryObject Blueprint Fields

An InventoryObject Blueprint Struct contains all the Fields in Table 3.1, plus those in Table 3.2 below.

**Table 3.2: Fields in InventoryObject Blueprint Structs**

| **Label**    | **Type** | **Description**                   |
| ------------ | -------- | --------------------------------- |
| InventoryRes | CResRef  | ResRef of UTI Item Blueprint file |

### 3.3. InventoryObject Instance Fields

An Item Instance Struct in a GIT file contains all the Fields in Table 3.1, plus those in Item Instances, as given in Tables 2.1.1 and 2.3.

### 3.4. InventoryObject Game Instance Fields

An Item Instance Struct in a GIT file contains all the Fields in Table 3.1, plus those in Item Game Instances, as given in Tables 2.1.1, 2.3, and 2.4.

The XOrientation is always 1.0, and the YOrientation and 3 Position Fields are always 0.0.

Character files use the same format as savegames for InventoryObjects, except that they do not include the Fields in Table 2.4.

---

[← Previous: Item Struct](Ch2_Item_Struct.md) | [Next: Chapter 4 - Calculations and Procedures →](Ch4_Calculations_and_Procedures.md)
