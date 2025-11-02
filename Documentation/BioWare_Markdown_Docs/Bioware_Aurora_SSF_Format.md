# BioWare Aurora Engine

## Sound Set File (SSF) Format

- [1. Introduction](#1-introduction)
- [2. Overall File Format](#2-overall-file-format)
- [3. Header Format](#3-header-format)
- [4. Entry Table](#4-entry-table)
- [5. Data Table](#5-data-table)
- [6. Entry Special Meanings](#6-entry-special-meanings)
- [7. SoundSet 2da files](#7-soundset-2da-files)

## 1. Introduction

The Sound Set File (SSF) format is used to store Neverwinter Nights **soundset** information.

A soundset is a set of sound files to play and associated strings to display when a creature or player character (PC) performs certain actions or when the creature or PC reacts to certain conditions. For example, when a PC attacks a creature, the PC may shout a battle cry, with the text of the battle cry appearing over the PC's head and in the game's message pane. The soundset tells the game what string to display and what sound file to play.

[↑ Back to Top](#bioware-aurora-engine)

## 2. Overall File Format

A SSF contains 3 logical sections: a header, an Entry Table, and a Data Table.

The figure below shows the overall layout of a SSF.

## Figure 2: SSF File Structure

![SSF File Structure Diagram](images/ssf_file_structure_diagram.gif)

[↑ Back to Top](#bioware-aurora-engine)

## 3. Header Format

The header contains file version information and details on how to locate the soundset information in the file. In the current SSF format, the header is 40 bytes long, but a portion of it is padding, to allow for the addition of new header information in later revisions of the SSF format.

## Table 3: Header Format

| **Value**   | **Size/Type**   | **Description**                               |
| ----------- | --------------- | --------------------------------------------- |
| FileType    | 4 char          | 4-char file type string. "SSF "               |
| FileVersion | 4 char          | 4-char SSF version. "V1.0"                    |
| EntryCount  | 32-bit unsigned | Number of entries in Entry Table              |
| TableOffset | 32-bit unsigned | Byte offset of Entry Table from start of file |
| Padding     | 24 bytes        | NULL padding                                  |

[↑ Back to Top](#bioware-aurora-engine)

## 4. Entry Table

The Entry Table is an array of 32-bit unsigned integers. Each integer entry is a byte offset from the beginning of the file to an item of data in the Data Table.

In the current form of the SSF format, the Entry Table is not strictly necessary, since the objects in the Data Table are of fixed size. However, storing the offsets to each one allows for expansion of the SSF format later to a format in which the data objects may be of variable length.

The figure below shows what the Entry Table looks like. The file header specifies the number of entries present.

### Table 4.1: Entry Table Structure

| **Entry Table Element** |
| ----------------------- |
| Entry 0                 |
| Entry 1                 |
| Entry 2                 |
| . . .                   |
| Entry N - 1             |

N = EntryCount from the header

[↑ Back to Top](#bioware-aurora-engine)

## 5. Data Table

The Data Table stores soundset string and sound file information. It is a sequence of SSF data objects packed end-to-end, with the starting offset of each one specified in the Entry Table. There is one data object per entry in the Entry Table.

The figure below shows the layout of the Data Table.

## Figure 5: Data Table

| Data Table Element |
| ------------------ |
| Data 0             |
| Data 1             |
| Data 2             |
| . . .              |
| Data N - 1         |

N = EntryCount from the header

Each data object in the Data Table has the structure given in the table below.

## Table 5: Data Format

| **Value** | **Size/Type**   | **Description**               |
| --------- | --------------- | ----------------------------- |
| ResRef    | 16 char         | Name of sound file to play    |
| StringRef | 32-bit unsigned | Index to string in dialog.tlk |

The ResRef is the name of a `.wav` sound file to play. This file must be located somewhere in the game resources (BIF files, Override folder, Hak Paks) and can have up to 16 characters in its name, not including the `.wav` file extension. The wave files should be in mono format, since soundset sounds are played as 3D sound sources in the game engine, and stereo waves do not make sense in that context.

The StringRef identifies a string in the user's `dialog.tlk` file that should be displayed when the current sound entry plays in the game. If the StringRef is -1 (ie., 0xFFFFFFFF), then no text appears.

[↑ Back to Top](#bioware-aurora-engine)

## 6. Entry Special Meanings

Soundset entries at specific indices have special meanings that remain constant across all soundset files. For example, the game always interprets the first entry in the Entry Table/Data Table as the "attack" cry.

The table below describes each soundset entry. Note that the table row Entry numbers start from 1, but the actual indices start from 0. That is, the Entry index is 1 less than the Entry number.

Also included in the table are the QuickChat keys for sounds that have QuickChat key bindings. To play a QuickChat in the game, press 'V' followed by the Type key, then the key for the individual sound. For example, "Attack" is VWE.

Some of the QuickChat entries have an asterisk (\*) next to their names. The game will cause these entries to issue orders to any associates (henchmen, summonded creatures, etc) belonging to the player character.

**Table 6: Sound Set Entries**

| **Entry** | **Type**                | **Name**       | **QChat**                   | **Comments**            |
| --------- | ----------------------- | -------------- | --------------------------- | ----------------------- |
| 1         | Combat Shouts (QChat W) | Attack\*       | E                           | Command group to attack |
| 2         | Battlecry 1             | R              |                             |                         |
| 3         | Battlecry 2             | R              |                             |                         |
| 4         | Battlecry 3             | R              |                             |                         |
| 5         | Heal me\*               | D              | Heal speaker                |                         |
| 6         | Help                    | W              |                             |                         |
| 7         | Enemies sighted         | A              |                             |                         |
| 8         | Flee                    | S              |                             |                         |
| 9         | Taunt                   | T              | When using Taunt skill      |                         |
| 10        | Guard me\*              | F              | Guard speaker               |                         |
| 11        | Hold\*                  | X              | Hold this spot; stop moving |                         |
| 12        | Combat Events           | Attack Grunt 1 |                             | Short grunt             |
| 13        | Attack Grunt 2          |                | Moderate effort             |                         |
| 14        | Attack Grunt 3          |                | Grunt of effort             |                         |
| 15        | Pain Grunt 1            |                | Short                       |                         |
| 16        | Pain Grunt 2            |                | Pained                      |                         |
| 17        | Pain Grunt 3            |                | Pained                      |                         |
| 18        | Near death              |                |                             |                         |
| 19        | Death                   |                |                             |                         |
| 20        | Poisoned                |                |                             |                         |
| 21        | Spell failed            |                |                             |                         |
| 22        | Weapon ineffective      |                |                             |                         |
| 23        | Exploration (QChat E)   | Follow me\*    | E                           | Follow speaker          |
| 24        | Look here               | W              |                             |                         |
| 25        | Group party             | D              |                             |                         |
| 26        | Move over               | S              |                             |                         |
| 27        | Tasks (QChat D)         | Pick lock      | W                           |                         |
| 28        | Search                  | E              |                             |                         |
| 29        | Go stealthy             | G              |                             |                         |
| 30        | Can do                  | C              |                             |                         |
| 31        | Cannot do               | X              |                             |                         |
| 32        | Task complete           | A              |                             |                         |
| 33        |                         | Encumbered     |                             |                         |
| 34        | Selected                |                |                             |                         |
| 35        | Social (QChat S)        | Hello          | S                           |                         |
| 36        | Yes                     | D              |                             |                         |
| 37        | No                      | W              |                             |                         |
| 38        | Stop                    | E              |                             |                         |
| 39        | Rest                    | C              |                             |                         |
| 40        | Bored                   | X              |                             |                         |
| 41        | Goodbye                 | A              |                             |                         |
| 42        | Feelings (QChat X)      | Thank you      | X                           |                         |
| 43        | Laugh                   | W              |                             |                         |
| 44        | Cuss                    | C              |                             |                         |
| 45        | Cheer                   | D              |                             |                         |
| 46        | Something to say        | S              |                             |                         |
| 47        | Good idea               | A              |                             |                         |
| 48        | Bad idea                | Z              |                             |                         |
| 49        | Threaten                | E              |                             |                         |

[↑ Back to Top](#bioware-aurora-engine)

## 7. SoundSet 2da files

Soundset references in the game and toolset are controlled by two 2da files.

`Soundset.2da` is a centralized source of information about the SoundSet Files that exist in the game resources. It contains important information about the soundsets that is not stored within the SoundSet Files themselves. The table below describes the columns in `soundset.2da`.

## Table 7.1: soundset.2da columns

| **Column** | **Type** | **Description**                                      |
| ---------- | -------- | ---------------------------------------------------- |
| LABEL      | String   | Text label for convenience of person reading the 2da |
| RESREF     | String   | ResRef (16-char filename) of SSF file                |
| STRREF     | Integer  | Index to string in `dialog.tlk`                      |
| GENDER     | Integer  | 0 = male, 1 = female                                 |
| TYPE       | Integer  | Index into `soundsettype.2da`                        |

To reduce network traffic, the soundset used by a creature is not stored as a SoundSet File's ResRef. Instead, a creature's soundset is stored and transmitted as a single integer index into `soundset.2da`. From this index, the game client can consult its own copy of `soundset.2da` to fetch the ResRef.

The StrRef, Gender, and Type columns in `soundset.2da` are used by the game and toolset for display purposes and for filtering which soundsets appear during player character creation and in the toolset's Soundset Selection dialog. The toolset will not display any soundsets that have a StrRef of 0 or less. The Type is an index into `soundsettype.2da`, which is summarized in the table below.

## Table 7.2: soundsettype.2da columns

| **Column** | **Type** | **Description**                                      |
| ---------- | -------- | ---------------------------------------------------- |
| LABEL      | String   | Text label for convenience of person reading the 2da |
| STRREF     | Integer  | Index to string in `dialog.tlk`                      |

The soundset Type is used for filtering the list of selectable soundsets in the toolset. The name displayed for each type is specified by its StrRef.

As a special case, the game understands Type 0 to refer to player soundsets, and these are the ones that are displayed during player character creation.

[↑ Back to Top](#bioware-aurora-engine)

---

BioWare Corp. http://www.bioware.com
