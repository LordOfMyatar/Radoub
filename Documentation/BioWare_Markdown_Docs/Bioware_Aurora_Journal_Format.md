# BioWare Aurora Engine

## Journal System

## 1. Introduction

A **Journal** is system of keeping track of where a player is in each plot that the player has started, and a way of describing the current step of each plot to the player.

Journal information is stored in the **module.jrl** file in a module or savegame. This file uses BioWare's [Generic File Format (GFF)](Bioware_Aurora_GFF_Format.md), and it is assumed that the reader of this document is familiar with GFF. The GFF FileType string in the header of repute.fac is "JRL ".

[↑ Back to Top](#bioware-aurora-engine)

## 2. Journal System Structs

The tables in this section describe the GFF Structs contained within **module.jrl**.

### 2.1. Top Level Struct

**Table 2.1: Journal Top Level Struct**

| **Label**  | **Type** | **Description**                                         |
| ---------- | -------- | ------------------------------------------------------- |
| Categories | List     | List of JournalCategory Structs (StructID = list index) |

### 2.2. JournalCategory Struct

The Table below lists the Fields that are present in a JournalCategory Struct found in the _Categories_ list.

**Table 2.2: Fields in JournalCategory Struct (StructID = list index)**

| **Label** | **Type**      | **Description**                                                                                                                                            |
| --------- | ------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Comment   | CExoString    | Module builder's comments                                                                                                                                  |
| EntryList | List          | List of JournalEntry Structs (StructID = list index)                                                                                                       |
| Name      | CExoLocString | Localized name of the Journal Category. Appears in the player's Journal in game.                                                                           |
| Picture   | WORD          | Unused. Always 0xFFFF.                                                                                                                                     |
| Priority  | DWORD         | Priority of this Journal Category. 0 = Highest 1 = High 2 = Medium 3 = Low 4 = Lowest                                                                      |
| Tag       | CExoString    | Tag of the JournalCategory, used to refer to this Journal Category via scripting. There should not be more than one Journal Category having the same Tag.  |
| XP        | DWORD         | Experience awarded for completing this Journal Category. To complete the Category, the player must reach a JournalEntry where _End_=1 (see **Table 2.3**). |

### 2.3. JournalEntry Struct

The Table below lists the Fields that are present in a JournalEntry Struct found in the _EntryList_ of a JournalCategory Struct. Each JournalEntry Struct describes a single entry within its category.

**Table 2.3: Fields in JournalEntry Struct (StructID = list index)**

| **Label** | **Type**      | **Description**                                                                                                                                                                                                          |
| --------- | ------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| End       | WORD          | 1 if this Entry serves as an endpoint for its Category. There can be more than one ending entry in a category.                                                                                                           |
| ID        | DWORD         | ID of the Journal Entry. Referred to in scripting in order to get and set the current entry. This ID must be unique for each Entry within the Journal Category, but the IDs do not need to be contiguous or even sorted. |
| Text      | CExoLocString | Localized text for the Journal Entry. Appears in the player's Journal in game, under the appropriate category.                                                                                                           |

[↑ Back to Top](#bioware-aurora-engine)

---
BioWare Corp. http://www.bioware.com
