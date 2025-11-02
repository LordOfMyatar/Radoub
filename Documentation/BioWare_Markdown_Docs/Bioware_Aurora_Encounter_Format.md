# BioWare Aurora Engine

## Encounter Format

- [1. Introduction](#1-introduction)
- [2. Encounter Struct](#2-encounter-struct)
  - [2.1 Common Encounter Fields](#21-common-encounter-fields)
  - [2.2 Encounter Blueprint Fields](#22-encounter-blueprint-fields)
  - [2.3 Encounter Instance Fields](#23-encounter-instance-fields)
  - [2.4 Encounter Game Instance Fields](#24-encounter-game-instance-fields)
- [3. The 2DA Files Referenced by Encounter Fields](#3-the-2da-files-referenced-by-encounter-fields)
  - [3.1 EncDifficulty](#31-encdifficulty)
- [4. Geometry Rules for the Point List](#4-geometry-rules-for-the-point-list)

## 1. Introduction

An Encounter is a set of vertices defining a region that can spawn in a set of creatures when creatures of certain factions enter it.

Encounters are stored in the game and toolset using BioWare's Generic File Format (GFF), and it is assumed that the reader of this document is familiar with GFF.

Encounter objects can be blueprints or instances.
Encounter blueprints are saved as GFF files having a UTE extension and "UTE "
as the FileType string in their header. Encounter instances are stored as Encounter Structs within a module's GIT files.

[↑ Back to Top](#bioware-aurora-engine)

## 2. Encounter Struct

The tables in this section describe the GFF Struct for an Encounter object. Some Fields are only present on Instances and others only on Blueprints.

For List Fields, the tables indicate the StructID used by the List elements.

### 2.1 Common Encounter Fields

The Table below lists the Fields that are present in all Encounter Structs, regardless of where they are found.

### Table 2.1.1: Fields in all Encounter Structs

| **Label**       | **Type**      | **Description**                                                                                                                                                                                                                                                                                                                                                                 |
| --------------- | ------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Active          | BYTE          | 1 if the Encounter is active and can spawn creatures, 0 if inactive.<br><br>To be able to spawn, an inactive encounter must be activated via scripting.                                                                                                                                                                                                                                |
| CreatureList    | List          | List of EncounterCreature Structs. StructID 0.<br><br>This is a list of the creatures that the Encounter can spawn.                                                                                                                                                                                                                                                                    |
| Difficulty      | INT           | Obsolete Field.<br><br>Should always be identical to the VALUE in encdifficulty.2da pointed to by the DifficultyIndex Field.                                                                                                                                                                                                                                                           |
| DifficultyIndex | INT           | Index into `encdifficulty.2da`.                                                                                                                                                                                                                                                                                                                                                   |
| Faction         | DWORD         | ID of the Faction that the Encounter belongs to.<br><br>An Encounter will only spawn creatures if it is entered by creatures that are hostile to its Faction. The Faction ID is the index of the Faction in the module's Faction.fac file.                                                                                                                                             |
| LocalizedName   | CExoLocString | Name of the Encounter as it appears on the toolset's Encounter palette and in the Name field of the toolset's Encounter Properties dialog.<br><br>Does not appear in game.                                                                                                                                                                                                             |
| MaxCreatures    | INT           | Maximum number of creatures that this encounter can spawn at a time. Toolset limits this to 1 to 8.<br><br>Must be greater than or equal to MinCreatures.                                                                                                                                                                                                                              |
| OnEntered       | CResRef       | OnEnter event                                                                                                                                                                                                                                                                                                                                                                   |
| OnExhausted     | CResRef       | OnExhausted event                                                                                                                                                                                                                                                                                                                                                               |
| OnExit          | CResRef       | OnExit event                                                                                                                                                                                                                                                                                                                                                                    |
| OnHeartbeat     | CResRef       | OnHeartbeat event                                                                                                                                                                                                                                                                                                                                                               |
| OnUserDefined   | CResRef       | OnUserDefined event                                                                                                                                                                                                                                                                                                                                                             |
| PlayerOnly      | BYTE          | 0 if any creature can fire this Encounter so long as it is of a hostile faction.<br><br>1 if only player characters can fire the encounter. The player must still be hostile to the Encounter's faction for the Encounter to fire.                                                                                                                                                     |
| RecCreatures    | INT           | Recommended number of creatures. Maps to "Min Creatures" field in toolset, but is not a true minimum, because it is actually possible for the encounter system to spawn fewer than this number of creatures if it cannot find enough creatures to fit the level of the encounter.<br><br>Must be less than or equal to MaxCreatures. Toolset restricts this Field to the range 1 to 8. |
| Reset           | BYTE          | 0 if the Encounter does not respawn.<br><br>1 if the Encounter does respawn.                                                                                                                                                                                                                                                                                                           |
| ResetTime       | INT           | Number of seconds before Encounter respawns.<br><br>Maximum in toolset is 32000 seconds.                                                                                                                                                                                                                                                                                               |
| Respawns        | INT           | Number of times to respawn. Maximum in toolset is 32000.<br><br>-1 if the Encounter can respawn an infinite number of times.                                                                                                                                                                                                                                                           |
| SpawnOption     | INT           | 0 = continuous spawn. The encounter continuously evaluates the hostile creatures inside it and spawns new creatures as the existing creatures die.<br><br>1 = single-shot spawn. The encounter fires once when a hostile creature enters it.                                                                                                                                           |
| Tag             | CExoString    | Tag of the Encounter. Up to 32 characters                                                                                                                                                                                                                                                                                                                                       |
| TemplateResRef  | CResRef       | For blueprints (UTE files), this should be the same as the filename.<br><br>For instances, this is the ResRef of the blueprint that the instance was created from.                                                                                                                                                                                                                     |

### Table 2.1.2: Fields in EncounterCreature Struct

(Struct ID 0)

| **Label**   | **Type** | **Description**                                                                                                                                   |
| ----------- | -------- | ------------------------------------------------------------------------------------------------------------------------------------------------- |
| Appearance  | INT      | Appearance of the creature.<br><br>Should be identical to the Appearance stored in the creature blueprint.                                               |
| CR          | FLOAT    | Challenge Rating of the creature.<br><br>Should be identical to the CR stored in the creature blueprint.                                                 |
| ResRef      | CResRef  | ResRef of the creature blueprint (utc file) to spawn an instance of.                                                                              |
| SingleSpawn | BYTE     | 0 if there are no restrictions on how many copies of this creature can spawn.<br><br>1 if only one of this creature can spawn at a time in an encounter. |

The Appearance and CR Fields are stored on the EncounterCreature for performance, so that the game does not have to access the disk to load the blueprint just to get the CR.

### 2.2 Encounter Blueprint Fields

The Top-Level Struct in a UTE file contains all the Fields in Table 2.1 above, plus those in Table 2.2 below.

### Table 2.2: Fields in Encounter Blueprint Structs

| **Label**      | **Type**   | **Description**                                                                                                                                                                                                                                                                                |
| -------------- | ---------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Comment        | CExoString | Module designer comment.                                                                                                                                                                                                                                                                       |
| PaletteID      | BYTE       | ID of the node that the Encounter Blueprint appears under in the Encounter palette.                                                                                                                                                                                                            |
| TemplateResRef | CResRef    | The filename of the UTE file itself. It is an error if this is different.<br><br>Certain applications check the value of this Field instead of the ResRef of the actual file. If you manually rename a UTE file outside of the toolset, then you must also update the TemplateResRef Field inside it. |

### 2.3 Encounter Instance Fields

An Encounter Instance Struct in a GIT file contains all the Fields in Table 2.1.1 and 2.1.2, plus those in Table 2.3.1 of the [Trigger Format](Bioware_Aurora_Trigger_Format.md) document, plus those in Table 2.3.1 below.

### Table 2.3.1: Fields in Encounter Instance Structs

| **Label**                     | **Type** | **Description**                                                                                                                                                                                                                                                                                                                                                                                                                           |
| ----------------------------- | -------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Geometry                      | List     | List of Point Structs (StructID 1) defining the vertices of the Encounter polygon. See Table 2.3.2. The polygon is drawn by starting at the first Point element and drawing a line to each subsequent Point, then connecting the last one back to the first. See section 4 of the Trigger Format document for additional rules governing the Geometry of an Encounter polygon.                                                            |
| SpawnPointList                | List     | List of EncounterSpawnPoint Structs. Struct ID 0. See Table 2.3.3. The SpawnPointList is only saved out if the encounter has spawnpoints defined for it in the toolset. Spawn points define a set of locations at which the game may spawn in creatures belonging to the Encounter. If an Encounter has no defined spawnpoints, then the game will try to spawn creatures out of visible range of the creatures that fired the Encounter. |
| TemplateResRef                | CResRef  | For instances, this is the ResRef of the blueprint that the instance was created from.                                                                                                                                                                                                                                                                                                                                                    |
| XPosition YPosition ZPosition | FLOAT    | (x,y,z) coordinates of the Encounter within the Area that it is located in. The Points in the Encounter Geometry have their coordinates specified relative to the Encounter's own location.                                                                                                                                                                                                                                               |

### Table 2.3.2: Fields in Point Struct (Struct ID 1)

| **Label** | **Type** | **Description**                                                                                  |
| --------- | -------- | ------------------------------------------------------------------------------------------------ |
| X Y Z     | FLOAT    | (x,y,z) coordinates of the Point, assuming that the origin is at the owner Encounter's position. |

The points in the Encounter's Geometry List use a coordinate system where the origin is the

Encounter's own position. For example, suppose that an Encounter has (XPosition, YPosition, ZPosition) = (10,
20, 30). If the Geometry contains a Point at (PointX, PointY, PointZ) = (0, 0,
0), then the actual coordinates of that Point are (10, 20, 30). Similarly, if there is another Point belonging to the same Encounter has coordinates (PointX, PointY, PointZ) = (1, 2, -10), then the actual coordinates of that Point are
(11, 22, 20).

There is no requirement that any Point in the List be at (0,0,0), nor is there any requirement against it.

### Table 2.3.3: Fields in EncounterSpawnPoint Structs (Struct ID 0)

| **Label**   | **Type** | **Description**                                                                                        |
| ----------- | -------- | ------------------------------------------------------------------------------------------------------ |
| Orientation | FLOAT    | Orientation of the SpawnPoint, expressed as a bearing in radians measured counterclockwise from north. |
| X Y Z       | FLOAT    | (x,y,z) coordinates of the SpawnPoint within the Area that it is located in.                           |

### 2.4 Encounter Game Instance Fields

After a GIT file has been saved by the game, the Encounter Instance Struct contains not only the Fields in Table 2.1 and Table 2.3.1, but also those in Table 2.4.

### Table 2.4: Fields in Encounter Instance Structs in SaveGames

| **Label**       | **Type** | **Description**                                                                                                                    |
| --------------- | -------- | ---------------------------------------------------------------------------------------------------------------------------------- |
| ActionList      | List     | List of Actions stored on this object StructID 0. See **Section 6** of the **[Common GFF Structs](Bioware_Aurora_CommonGFFStructs.md)** document.                          |
| AreaListMaxSize | INT      |                                                                                                                                    |
| AreaPoints      | FLOAT    |                                                                                                                                    |
| CurrentSpawns   | INT      |                                                                                                                                    |
| CustomScriptId  | INT      |                                                                                                                                    |
| Exhausted       | BYTE     |                                                                                                                                    |
| HeartbeatDay    | DWORD    |                                                                                                                                    |
| HeartbeatTime   | DWORD    |                                                                                                                                    |
| LastEntered     | DWORD    |                                                                                                                                    |
| LastLeft        | DWORD    |                                                                                                                                    |
| LastSpawnDay    | DWORD    |                                                                                                                                    |
| LastSpawnTime   | DWORD    |                                                                                                                                    |
| NumberSpawned   | INT      |                                                                                                                                    |
| ObjectId        | DWORD    | Object ID used by game for this object.                                                                                            |
| SpawnPoolActive | FLOAT    |                                                                                                                                    |
| Started         | BYTE     | 1 if any creatures currently exist that belong to the encounter.<br><br>0 if there are no creatures currently belonging to the encounter. |
| VarTable        | List     | List of scripting variables stored on this object. StructID 0. See **Section 3** of the **[Common GFF Structs](Bioware_Aurora_CommonGFFStructs.md)** document.             |

[↑ Back to Top](#bioware-aurora-engine)

## 3. The 2DA Files Referenced by Encounter Fields

### 3.1 EncDifficulty

In an Encounter Struct, the DifficultyIndex Field is an index into encdifficulty.2da.

### Table 3.3.1: encdifficulty.2da columns

| **Column** | **Type** | **Description**                                                                                   |
| ---------- | -------- | ------------------------------------------------------------------------------------------------- |
| LABEL      | String   | Programmer label                                                                                  |
| STRREF     | Integer  | StrRef of text to display for this difficulty level in the toolset's Encounter Properties dialog. |
| VALUE      | Integer  | Value to add to the game's calculated encounter level.                                            |

[↑ Back to Top](#bioware-aurora-engine)

## 4. Geometry Rules for the Point List

In an Encounter instance, the Geometry List contains the points that define the outline of the Encounter.

The toolset must enforce several rules for polygon geometry, as given in Section 4 of the [Trigger Format](Bioware_Aurora_Trigger_Format.md) document.

[↑ Back to Top](#bioware-aurora-engine)

---
BioWare Corp. http://www.bioware.com
