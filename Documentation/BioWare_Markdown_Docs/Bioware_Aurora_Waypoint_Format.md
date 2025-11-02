# BioWare Aurora Engine

## Waypoint Format

## 1. Introduction

A Waypoint is a simple object type used for scripting and showing map notes to the player.

Waypoints are stored in the game and toolset using BioWare's [Generic File Format (GFF)](Bioware_Aurora_GFF_Format.md), and it is assumed that the reader of this document is familiar with GFF.

Waypoint objects can be blueprints or instances. Waypoint blueprints are saved as GFF files having a UTW extension and "UTW " as the FileType string in their GFF header. Waypoint instances are stored as Waypoint Structs within a module's GIT files.

[↑ Back to Top](#bioware-aurora-engine)

## 2. Waypoint Struct

The tables in this section describe the GFF Struct for a Waypoint object. Some Fields are only present on Instances and others only on Blueprints.

For List Fields, the tables indicate the StructID used by the List elements.

### 2.1 Toolset Waypoint Fields

#### 2.1.1 Common Waypoint Fields

The Table below lists the Fields that are present in all Waypoint Structs, regardless of where they are found.

**Table 2.1.1.1: Fields in all Waypoint Structs**

| **Label**      | **Type**      | **Description**                                                                                                                                                                                                                                                           |
| -------------- | ------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Appearance     | BYTE          | Index into `waypoint.2da`. Determines only what the waypoint model looks like in the toolset. Has no effect on game.                                                                                                                                                  |
| Description    | CExoLocString | Localized description of waypoint. Only visible in toolset.                                                                                                                                                                                                            |
| HasMapNote     | BYTE          | 1 if Waypoint has a map note, 0 otherwise. If HasMapNote == 0, then in the toolset, the "Map Note Text" and "Map Note Enabled" controls will still have their proper values as stored in the MapNote and MapNoteEnabled Fields, but they will be greyed out. |
| LinkedTo       | CExoString    | Tag of object that waypoint is linked to. Unused and always blank.                                                                                                                                                                                                     |
| LocalizedName  | CExoLocString | Localized name of waypoint. This name appears in the Waypoint palette.                                                                                                                                                                                                 |
| MapNote        | CExoLocString | Text that appears ingame when user mouses over the Waypoint in the Minimap.                                                                                                                                                                                               |
| MapNoteEnabled | BYTE          | 1 if the Waypoint's Map Note is visible in the Minimap in the game, 0 otherwise.                                                                                                                                                                                      |
| Tag            | CExoString    | Tag of the waypoint. Can be up to 32 characters long.                                                                                                                                                                                                                  |

The Appearance Field is an index into waypoint.2da, which is described by the table below:

**Table 2.1.1.2: waypoint.2da columns**

| **Column** | **Type** | **Description**                                                                                                                  |
| ---------- | -------- | -------------------------------------------------------------------------------------------------------------------------------- |
| LABEL      | String   | Programmer label                                                                                                                 |
| RESREF     | String   | ResRef of MDL file to use as the model for the waypoint in the toolset's area viewer.                                            |
| STRREF     | Integer  | StrRef of localized text description to show to user in the Appearance dropdown in the toolset's Waypoint Properties dialog.   |

#### 2.1.2 Waypoint Blueprint Fields

The Top-Level Struct in a UTW file contains all the Fields in Table 2.1.1.1 above, plus those in Table 2.1.2 below.

**Table 2.1.2: Fields in Waypoint Blueprint Structs**

| **Label**      | **Type**   | **Description**                                                                                                                                                                                                                                                                                          |
| -------------- | ---------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Comment        | CExoString | Module designer comment.                                                                                                                                                                                                                                                                                 |
| PaletteID      | BYTE       | ID of the node that the Waypoint Blueprint appears under in the Waypoint palette.                                                                                                                                                                                       |
| TemplateResRef | CResRef    | The filename of the UTW file itself. It is an error if this is different. Certain applications check the value of this Field instead of the ResRef of the actual file. If you manually rename a UTW file outside of the toolset, then you must also update the TemplateResRef Field inside it. |

#### 2.1.3 Waypoint Instance Fields

A Waypoint Instance Struct in a GIT file contains all the Fields in Table 2.1.1.1, plus those in Table 2.1.3 below.

**Table 2.1.3: Fields in Waypoint Instance Structs (StructID 5)**

| **Label**                           | **Type** | **Description**                                                                                                                                                                                                                                                       |
| ----------------------------------- | -------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| TemplateResRef                      | CResRef  | The ResRef of the blueprint that the instance was created from.                                                                                                                                                               |
| XOrientation    YOrientation        | FLOAT    | x and y components of a unit vector that points in the direction that the waypoint faces. Or in other words, the cosine and sine, respectively, of the waypoint's bearing in the xy plane, measured as an angle counterclockwise from the positive x-axis. |
| XPosition    YPosition    ZPosition | FLOAT    | (x,y,z) coordinates of the Waypoint within the Area that it is located in.                                                                                                                                                    |

### 2.2 Game Waypoint Fields

The information discussed in this section is used only by saved games, and is not required by the toolset. Editing the fields listed in this section can readily lead to corrupted save games.

After a GIT file has been saved by the game, the Waypoint Instance Struct contains not only the Fields in Table 2.1.1.1 and Table 2.1.3, it also contains the Fields in Table 2.2.

**Table 2.2: Fields in Waypoint Instance Structs in SaveGames**

| **Label**  | **Type** | **Description**                                                                                                            |
| ---------- | -------- | -------------------------------------------------------------------------------------------------------------------------- |
| ActionList | List     | List of Actions stored on this object StructID 0. See **Section 6** of the **Common GFF Structs** document.                |
| ObjectId   | DWORD    | Object ID used by game for this object.                                                                                    |
| VarTable   | List     | List of scripting variables stored on this object. StructID 0. See **Section 3** of the **Common GFF Structs** document. |

[↑ Back to Top](#bioware-aurora-engine)

---
BioWare Corp. http://www.bioware.com


