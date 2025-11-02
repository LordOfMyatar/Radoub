# BioWare Aurora Engine

## Common Game GFF Structures

- [1. Introduction](#1-introduction)
- [2. Location Struct](#2-location-struct)
- [3. VarTable List, Variable Struct](#3-vartable-list-variable-struct)
- [4. EffectsList List, Effect Struct](#4-effectslist-list-effect-struct)
- [5. EventQueue List, Event Struct](#5-eventqueue-list-event-struct)
- [6. ActionList List, Action Struct](#6-actionlist-list-action-struct)
- [7. Script Situation Struct and Substructs](#7-script-situation-struct-and-substructs)

## 1. Introduction

This document describes Structs and Lists that are frequently seen in files saved in the BioWare Generic File Format (GFF). This document assumes that the reader is already familiar with the [Generic File Format](Bioware_Aurora_GFF_Format.md) document.

When describing a Struct in this document, the StructID is provided for completeness, although in some cases, the StructID may vary depending on the List or Struct that contains the Struct being described. Also, in most cases, the game and toolset do not actually check the StructID.

This document is intended to supplement the documentation for various GFF files (eg., IFO, ARE, etc.). Consequently, it may not fully disclose the details of any given Struct. In those instances where a Struct is not completely described, it is strongly recommended that an application should write it out to disk exactly as it was read in originally, with no modifications. Modifying a Struct without a good understanding of it can lead to corrupted modules and corrupted saved games.

[↑ Back to Top](#bioware-aurora-engine)

## 2. Location Struct

A Location is a Struct that describes a location in a module.

**Table 2: Fields in a Location Struct (StructID 1)**

| **Label**                              | **Type** | **Description**                                                        |
| -------------------------------------- | -------- | ---------------------------------------------------------------------- |
| Area                                   | DWORD    | ObjectId of the area containing the location                           |
| OrientationX OrientationY OrientationZ | FLOAT    | (x,y,z) components of the direction vector in which the location faces |
| PositionX PositionY PositionZ          | FLOAT    | (x,y,z) coordinates of the location                                    |

[↑ Back to Top](#bioware-aurora-engine)

## 3. VarTable List, Variable Struct

A **VarTable** is a GFF List containing **Variable** GFF Structs. It is a list of scripting variables and their values.

**Table 3.1: Fields in a Variable Struct (StructID 0)**

| **Label** | **Type**        | **Description**                                                                                                                                                 |
| --------- | --------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Name      | CExoString      | The name of the variable as set by the SetGlobalInt(), SetGlobalString(), etc. scripting functions, and retrieved by the corresponding GetGlobal\*() functions. |
| Type      | DWORD           | Variable's data type                                                                                                                                            |
| Value     | Depends on Type | The value of the Variable                                                                                                                                       |

The actual data type of a Variable's 'Value' Field depends on the value of it's 'Type' Field. The table below lists the type IDs and their associated data types.

**Table 3.2: Variable Types**

| **TypeID** | **GFF Type**                            | **NWScript Type** |
| ---------- | --------------------------------------- | ----------------- |
| 1          | INT                                     | int               |
| 2          | FLOAT                                   | float             |
| 3          | CExoString                              | string            |
| 4          | DWORD                                   | object            |
| 5          | Location Struct. See **Section 2**. | location          |

[↑ Back to Top](#bioware-aurora-engine)

## 4. EffectsList List, Effect Struct

An **EffectsList** is a GFF List containing **Effect** GFF Structs. It is a list of effects on an object.

**Table 4: Fields in an Effect Struct (variable StructID)**

| **Label**   | **Type**   | **Description**                                       |
| ----------- | ---------- | ----------------------------------------------------- |
| CreatorId   | DWORD      | ObjectID of effect's creator                          |
| Duration    | FLOAT      | Duration of the effect                                |
| ExpireDay   | DWORD      | Day the effect expires                                |
| ExpireTime  | DWORD      | Time the effect expires                               |
| FloatList   | List       | StructID 4. Struct given on next line:                |
| Value       | FLOAT      | List of float parameters for the effect               |
| IntList     | List       | StructID 3. Struct given on next line:                |
| Value       | INT        | List of int parameters for the effect                 |
| IsExposed   | INT        | Bool – is the effect exposed to scripting?              |
| IsIconShown | INT        | Bool – does it show the icon?                           |
| NumIntegers | INT        | -                                                     |
| ObjectList  | List       | StructID 6. Struct given on next line:                |
| Value       | DWORD      | List of ObjectID parameters for the effect            |
| SkipOnLoad  | BYTE       | Bool – should this effect be added on load? Or skipped? |
| SpellId     | DWORD      | -                                                     |
| StringList  | List       | StructID 5. Struct given on next line:                |
| Value       | CExoString | String parameters for the effect                      |
| SubType     | WORD       | The effect sub-type                                   |
| Type        | WORD       | The type of the effect.                               |

[↑ Back to Top](#bioware-aurora-engine)

## 5. EventQueue List, Event Struct

An **EventQueue** is a GFF List containing **Event** GFF Structs. The Fields in an Event are given in the table below:

**Table 5.1: Fields in an Event Struct (StructID 0xABCD)**

| **Label** | **Type**           | **Description**                    |
| --------- | ------------------ | ---------------------------------- |
| CallerId  | DWORD              | Object Id of the actor object      |
| Day       | DWORD              | Game day the event should fire     |
| EventData | Depends on EventId | Struct that depends on the EventId |
| EventId   | DWORD              | ID of the Event type               |
| ObjectId  | DWORD              | Object ID the event is acting on   |
| Time      | DWORD              | Game time the event should fire    |

The EventData Field is a GFF Struct that depends on the value of the EventId Field. The table below lists some EventId values and what Structs are associated with them. These Structs are saved using the StructID specified in the table, rather than whatever StructIDs they may normally use. Some EventIds do not save an EventData Struct at all.

**Table 5.2: EventId values**

| **EventId** | **EventData StructID** | **Event Description**     | **Struct**                                               |
| ----------- | ---------------------- | ------------------------- | -------------------------------------------------------- |
| 1           | 0x7777                 | TIMED_EVENT               | ScriptSituation. See **Section 7**.                  |
| 2           | -                      | ENTERED_TRIGGER           | none                                                     |
| 3           | -                      | LEFT_TRIGGER              | none                                                     |
| 4           | 0x9999                 | REMOVE_FROM_AREA          | Struct consists of a single BYTE Field of Label "Value"  |
| 5           | 0x1111                 | APPLY_EFFECT              | Effect Struct. See **Section 4**.                    |
| 6           | -                      | CLOSE_OBJECT              | none                                                     |
| 7           | -                      | OPEN_OBJECT               | none                                                     |
| 8           | 0x6666                 | SPELL_IMPACT              | SpellScriptData Struct                                   |
| 9           | 0x3333                 | PLAY_ANIMATION            | Struct consists of a single INT Field of Label "Value"   |
| 10          | 0x4444                 | SIGNAL_EVENT              | ScriptEvent. See **Section 7**.                      |
| 11          | -                      | DESTROY_OBJECT            | none                                                     |
| 12          | -                      | UNLOCK_OBJECT             | none                                                     |
| 13          | -                      | LOCK_OBJECT               | none                                                     |
| 14          | 0x1111                 | REMOVE_EFFECT             | Effect Struct. See **Section 4**.                     |
| 15          | 0x2222                 | ON_MELEE_ATTACKED         | CombatAttackData Struct.                                 |
| 16          | -                      | DECREMENT_STACKSIZE       | none                                                     |
| 17          | 0x5555                 | SPAWN_BODY_BAG            | BodyBagInfo Struct                                       |
| 18          | 0x8888                 | FORCED_ACTION             | ForcedAction Struct                                      |
| 19          | 0x6666                 | ITEM_ON_HIT_SPELL_IMPACT  | SpellScriptData Struct                                   |
| 20          | 0xAAAA                 | BROADCAST_AOO             | Struct consists of a single DWORD Field of Label "Value" |
| 21          | 0x2222                 | BROADCAST_SAFE_PROJECTILE | CombatAttackData Struct.                                 |
| 22          | 0xCCCC                 | FEEDBACK_MESSAGE          | ClientMessageData Struct                                 |
| 23          | -                      | ABILITY_EFFECT_APPLIED    | none                                                     |
| 24          | 0xDDDD                 | SUMMON_CREATURE           | ScriptEvent. See **Section 7**.                      |
| 25          | -                      | ACQUIRE_ITEM              | none                                                     |

[↑ Back to Top](#bioware-aurora-engine)

## 6. ActionList List, Action Struct

Game object instances may have actions queued up at the time that the game is saved. Any such instances will contain a number of Action objects in their ActionList Field. The table below describes an Action Struct.

**Table 6.1: Fields in an Action Struct (StructID 0)**

| **Label**     | **Type** | **Description**                                                                                   |
| ------------- | -------- | ------------------------------------------------------------------------------------------------- |
| ActionId      | DWORD    | -                                                                                                 |
| GroupActionId | WORD     | -                                                                                                 |
| NumParams     | WORD     | Number of elements in the Parameter List                                                          |
| Paramaters    | List     | List of Parameter Structs (StructID 1). See Table 6.2<br><br>This List is not present if NumParams == 0. |

_Note that the spelling of the "Param**a**ters" Field is not a typographical error_. It really is spelled that way.<br><br>By the time someone noticed that the spelling of "param**e**ters" was incorrect, there was too much existing data to justify fixing the spelling.

The table below describes a Struct in the Parameter List.

**Table 6.2: Fields in a Parameter Struct (StructID 1)**

| **Label** | **Type**        | **Description**                 |
| --------- | --------------- | ------------------------------- |
| Type      | DWORD           | The Parameter Value's data type |
| Value     | Depends on Type | The value of the Parameter      |

In a Parameter Struct, the actual datatype of the Value Field varies depending on the value of the Type Field. The table below specifies the Value datatypes associated with each Parameter Type.

**Table 6.3: Parameter Types**

| **Parameter Type** | **GFF Type of the Value Field** | **Description**                                                                                               |
| ------------------ | ------------------------------- | ------------------------------------------------------------------------------------------------------------- |
| 1                  | INT                             | Integer                                                                                                       |
| 2                  | FLOAT                           | Floating point value                                                                                          |
| 3                  | DWORD                           | Object ID                                                                                                     |
| 4                  | CExoString                      | String                                                                                                        |
| 5                  | Struct                          | Script Situation. StructID 2. Corresponds to a ScriptSituation in the virtual machine. See **Section 7**. |

[↑ Back to Top](#bioware-aurora-engine)

## 7. Script Situation Struct and Substructs

A Script Situation is a very complicated structure that is used by the scripting virtual machine. Details of this structure are provided in here, but it is highly recommended that if an application reads this structure, then it should write it back out exactly as it was read in originally.

**Table 7.1: Fields in a Script Situation Struct (variable StructID)**

| **Label**      | **Type** | **Description**                                 |
| -------------- | -------- | ----------------------------------------------- |
| CodeSize       | INT      |                                                 |
| Code           | VOID     |                                                 |
| InstructionPtr | INT      |                                                 |
| SecondaryPtr   | INT      |                                                 |
| Name           | String   |                                                 |
| StackSize      | INT      |                                                 |
| Stack          | Struct   | Stack Structure. StructID 0. See **Table 7.2**. |

**Table 7.2: Fields in a Stack Struct (StructID 0)**

| **Label**    | **Type** | **Description**                                                                                                                                                           |
| ------------ | -------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| BasePointer  | INT      |                                                                                                                                                                           |
| StackPointer | INT      |                                                                                                                                                                           |
| TotalSize    | INT      |                                                                                                                                                                           |
| Stack        | List     | Has a number of elements equal to the value of the StackPointer Field.<br><br>StructID of each list element is equal to the index of the Struct in the List. See **Table 7.3a**. |

**Table 7.3a: Fields in each Struct in the Stack List (variable StructID)**

| **Label** | **Type** | **Description**                                                  |
| --------- | -------- | ---------------------------------------------------------------- |
| Type      | CHAR     | Specifies the Field Type of the Value Field. **See Table 7.3b**. |
| Value     | Variable | Depends on Type Field. See **Table 7.3b**.                       |

**Table 7.3b: Stack Element Value Types**

| **Stack Element Type** | **GFF Type of the Value Field** | **Description**                                                                                                        |
| ---------------------- | ------------------------------- | ---------------------------------------------------------------------------------------------------------------------- |
| 3                      | INT                             | Integer                                                                                                                |
| 4                      | FLOAT                           | Floating point value                                                                                                   |
| 5                      | CExoString                      | String                                                                                                                 |
| 6                      | DWORD                           | Object ID                                                                                                              |
| 10 to 19               | Struct                          | Game engine structure.<br><br>Subtract 10 from the Type to get the StructID and look up the structure type in **Table 7.3c.** |

**Table 7.3c: Game Engine Structure IDs**

| **Struct ID/Stack Element Type** | **Description**                                                     |
| -------------------------------- | ------------------------------------------------------------------- |
| 0                                | Effect. See **Section 4**.                                      |
| 1                                | ScriptEvent. See **Table 7.4**                                  |
| 2                                | ScriptLocation. See **Table 7.5**                               |
| 3                                | ScriptTalent. See **Table 7.6**                                 |
| 4                                | ItemProperty. Same save function as effects. See **Section 4**. |
| 5 to 8                           | Unused; Reserved                                                    |

**Table 7.4: ScriptEvent Fields (StructID 1)**

| **Label**  | **Type** | **Description**                                                                                                                     |
| ---------- | -------- | ----------------------------------------------------------------------------------------------------------------------------------- |
| EventType  | WORD     |                                                                                                                                     |
| IntList    | List     | List of Structs having StructID 105. Each Struct contains a single Field having the Label "Parameter" and the Field is an INT       |
| FloatList  | List     | List of Structs having StructID 105. Each Struct contains a single Field having the Label "Parameter" and the Field is a FLOAT      |
| StringList | List     | List of Structs having StructID 105. Each Struct contains a single Field having the Label "Parameter" and the Field is a CExoString |
| ObjectList | List     | List of Structs having StructID 105. Each Struct contains a single Field having the Label "Parameter" and the Field is a DWORD.     |

**Table 7.5: ScriptLocation Fields (StructID 2)**

| **Label**                                | **Type** | **Description**    |
| ---------------------------------------- | -------- | ------------------ |
| Area                                     | DWORD    | ObjectID of area   |
| OrientationX, OrientationY, OrientationZ | FLOAT    | orientation vector |
| PositionX, PositionY, PositionZ          | FLOAT    | position vector    |

**Table 7.6: ScriptTalent Fields (StructID 3)**

| **Label**         | **Type** | **Description** |
| ----------------- | -------- | --------------- |
| ID                | INT      |                 |
| Type              | INT      |                 |
| MultiClass        | BYTE     |                 |
| Item              | DWORD    | Object ID       |
| ItemPropertyIndex | INT      |                 |
| CasterLevel       | BYTE     |                 |
| MetaType          | BYTE     |                 |

[↑ Back to Top](#bioware-aurora-engine)

---

BioWare Corp. http://www.bioware.com
