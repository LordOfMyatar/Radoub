# BioWare Aurora Engine

## Conversation File Format

## 1. Introduction

A Conversation is a branching set of predefined text strings ("lines") that a player and one or more NPCs can say to each other, along with the conditions that govern which lines are said and which are not, which are available to say and which are not, and the actions that accompany each line as it is said.

Conversations are stored as `.dlg` files, and may be present in global game resources in BIF files, in the override directory, in a module, or in a savegame.
Conversation files use BioWare's Generic File Format (GFF), and it is assumed that the reader of this document is familiar with GFF. The GFF FileType string in the header of conversation files is `DLG`.

This document uses color conventions from the toolset's Conversation Editor when referring to certain data structures. In the Conversation Editor, <span style="color:red">NPC text shows up in red</span> by default, and <span style="color:blue">Player text shows up in blue</span> by default. This document uses the same color schemes for the names of data structures that refer to <span style="color:red">NPC</span> or <span style="color:blue">Player</span> text.

[↑ Back to Top](#bioware-aurora-engine)

## 2. Conversation Structs

### 2.1. Top Level Struct

### Table 2.1: Conversation Top Level Struct

| **Label**       | **Type** | **Description**                                                                                                                                                                                                                                                                                                                                                                                      |
| --------------- | -------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| DelayEntry      | DWORD    | Number of seconds to wait before showing each entry.                                                                                                                                                                                                                                                                                                                                                 |
| DelayReply      | DWORD    | Number of seconds to wait before showing each reply.                                                                                                                                                                                                                                                                                                                                                 |
| EndConverAbort  | CResRef  | ResRef of script to run when the conversation is aborted, such as by combat, hitting the ESC key, or saving a game in the middle of conversation.                                                                                                                                                                                                                                                    |
| EndConversation | CResRef  | ResRef of script to run when the conversation ends normally.                                                                                                                                                                                                                                                                                                                                         |
| EntryList       | List     | List of NPC Dialog Structs. StructID = list index.                                                                                                                                                                                                                                                                                                                                                   |
| NumWords        | DWORD    | Number of words counted in this conversation. Dynamically updated as the user edits the conversation in the toolset's Conversation Editor.<br><br>Informational only. Does not serve a purpose in game.                                                                                                                                                                                                     |
| PreventZoomIn   | BYTE     | 1 if initiating the conversation will cause the game camera to zoom in on the speakers, if the "Enable Dialog Zoom" checkbox is checked under the game's "Control Options".<br><br>If a conversation is spoken as a oneliner popup over an NPC's head, then no zoomin occurs regardless of Game Options or the PreventZoomin flag.<br><br>0 if initiating the conversation will not cause the game camera to move. |
| ReplyList       | List     | List of Player Dialog Structs. StructID = list index.                                                                                                                                                                                                                                                                                                                                                |
| StartingList    | List     | List of NPC Sync Structs at the root level of the conversation. These are the entries that are candidates for being the first thing that the NPC says when the conversation starts. These entries are sorted in the same order as they appear in the Conversation Editor in the toolset, with the first entries in the list being the highest in the treeview. StructID = list index.                                                                                                                                                                                                                                                       |

### 2.2. Dialog Structs

A Dialog Struct defines a line of dialog in a conversation tree, plus any additional data relevant to that line. It may be a line spoken by a player or by an NPC.

Dialog Structs appear in the <span style="color:blue">`ReplyList`</span> and the <span style="color:red">`EntryList`</span> found in the Top-Level Struct of a conversation file.

### 2.2.1. Dialog Struct

The Table below lists the Fields that are present in a Dialog Struct.

#### Table 2.2.1: Fields in Dialog Struct (StructID = list index)

| **Label**  | **Type**      | **Description**                                                                                                                                                                                                                                                                                                               |
| ---------- | ------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Animation  | DWORD         | 0 = default, talk normal<br>28 = taunt<br>29 = greeting<br>30 = listen<br>33 = worship<br>34 = overlay salute<br>35 = bow<br>37 = steal<br>38 = talk normal<br>39 = talk pleading<br>40 = talk forceful<br>41 = talk laugh<br>44 = victory fighter<br>45 = victory mage<br>46 = victory thief<br>48 = look far<br>70 = overlay drink<br>71 = overlay read<br>88 = play no animation |
| AnimLoop   | BYTE          | Obsolete. No longer used. Did not play well with animation system because "looping" is a property that is part of an animation, not an external property that can be applied to an animation.                                                                                                                                 |
| Comment    | CExoString    | Conversation writer's comment. In the Conversation Editor, this comment only appears when editing the original version of the line of dialog. When editing a link (shows up in grey by default), the link comment shows up instead (see Section 2.3).                                                                         |
| Delay      | DWORD         | 0xFFFFFFFF                                                                                                                                                                                                                                                                                                                    |
| Quest      | CExoString    | Tag of Journal Category to update when showing this line of conversation.                                                                                                                                                                                                                                                     |
| QuestEntry | DWORD         | ID of the Journal Entry to show when showing this line of conversation. This Field is present only if `Quest` Field is non-empty.                                                                                                                                                                                             |
| Script     | CResRef       | ResRef of script to run when showing this line.                                                                                                                                                                                                                                                                               |
| Sound      | CResRef       | ResRef of WAV file to play                                                                                                                                                                                                                                                                                                    |
| Text       | CExoLocString | Localized text to display to the user for this line of dialog.                                                                                                                                                                                                                                                                |

### 2.2.2. Player <span style="color:blue">Reply</span> Dialog Struct

A Dialog Struct contained in the Player <span style="color:blue">`ReplyList`</span> contains all the Fields listed in Table 2.2.1, plus those Fields listed in Table 2.2.2.

#### Table 2.2.2: Additional Fields in <span style="color:blue">Reply</span> Struct (StructID = list index)

| **Label**   | **Type** | **Description**                                                                                                          |
| ----------- | -------- | ------------------------------------------------------------------------------------------------------------------------ |
| EntriesList | List     | List of Sync Structs describing the list of possible NPC replies to this line of player dialog. Struct ID = list index. |

#### 2.2.3. NPC <span style="color:red">Entry</span> Dialog Struct

A Dialog Struct contained in the NPC <span style="color:red">`EntryList`</span> contains all the Fields found in a Dialog Struct as detailed in Table 2.2.1, plus those Fields listed in Table 2.2.3.

##### Table 2.2.3: Additional Fields in <span style="color:red">Entry</span> Struct (StructID = list index)

| **Label**   | **Type**   | **Description**                                                                                                         |
| ----------- | ---------- | ----------------------------------------------------------------------------------------------------------------------- |
| RepliesList | List       | List of Sync Structs describing the list of possible Player replies to this line of NPC dialog. Struct ID = list index. |
| Speaker     | CExoString | Tag of the speaker. Blank if the speaker is the conversation owner.                                                     |

### 2.3. Sync Structs

A **Sync Struct** describes a pointer or reference to a Dialog Struct.

A Sync Struct may refer directly to a Dialog Struct, or it may be a "link" to the original line of dialog. In the toolset's Conversation Editor, direct references show up in normal text color (blue for player text, red for NPC text), while links show up in grey text.

In a conversation tree, each line of dialog (ie., node in the tree) has several properties associated with it, as described in **Section 2.2**. However, there are some properties that are not part of the dialog lines themselves, but are instead stored on the Sync Structs that point to those dialog lines.

For all dialog lines, the "Action Taken" script is part of the Sync Struct, not the Dialog Struct.

For linked lines of dialog (ie., the ones that appear by default in grey text in the Conversation Editor), the Comment is also part of the Sync Struct, and not the Dialog Struct.

### 2.3.1. NPC <span style="color:red">StartingList</span> Sync Struct

Sync Structs found in the <span style="color:red">*StartingList*</span> point to a NPC Dialogs in the Top-Level Struct's <span style="color:red">*EntryList*</span>. The <span style="color:red">*StartingList*</span> is the list of all lines of dialog that appear at the root level of the conversation tree.

The Table below lists the Fields that are present in a <span style="color:red">*StartingList*</span> Sync Struct.

#### Table 2.3.1: Fields in <span style="color:red">StartingList</span> Sync Struct (StructID = list index)

| **Label** | **Type** | **Description**                                                                                                                                                                            |
| --------- | -------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| Active    | CResRef  | ResRef of conditional script to run to determine if this line of conversation appears to the player. If the script returns FALSE, then skip to the next Link Struct in the *StartingList*. |
| Index     | DWORD    | Index into Top-Level Struct *EntryList*.                                                                                                                                                   |

### 2.3.2. Player <span style="color:blue">RepliesList</span> Link Struct

Sync Structs in the <span style="color:blue">*RepliesList*</span> of an NPC Entry Dialog Struct point to Player Dialogs in the Top-Level Struct's <span style="color:blue">*ReplyList*</span>.

The Table below lists the Fields that are present in a <span style="color:blue">*RepliesList*</span> Sync Struct.

#### Table 2.3.2: Fields in <span style="color:blue">RepliesList</span> Sync Struct (StructID = list index)

| **Label**   | **Type**   | **Description**                                                                                                                                                                                                                                                                  |
| ----------- | ---------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Active      | CResRef    | ResRef of conditional script to run to determine if this line of conversation appears to the player.                                                                                                                                                                             |
| Index       | DWORD      | Index into Top-Level Struct *ReplyList*.                                                                                                                                                                                                                                         |
| IsChild     | BYTE       | 1 if this is a link, and there is a LinkComment. 0 if this is a direct reference to the dialog, and there is no LinkComment.                                                                                                                                                     |
| LinkComment | CExoString | This Field is present only if this Sync Struct is for a linked line of conversation (appears grey by default in the toolset Conversation Editor).<br><br>If this is a link, then the Conversation Editor will show and edit the LinkComment instead of the Dialog Struct's own Comment. |

### 2.3.3. NPC <span style="color:red">EntriesList</span> Sync Struct

Sync Structs in the <span style="color:red">*EntriesList*</span> of a Player Reply Dialog Struct point to NPC Dialogs in the Top-Level Struct's <span style="color:red">*EntryList*</span>.

The Table below lists the Fields that are present in an <span style="color:red">*EntriesList*</span> Sync Struct.

#### Table 2.3.3: Fields in <span style="color:red">EntriesList</span> Sync Struct (StructID = list index)

| **Label**   | **Type**   | **Description**                                                                                                                                                                                                                                                                  |
| ----------- | ---------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Active      | CResRef    | ResRef of conditional script to run to determine if the NPC speaks this line. If the script returns FALSE, then check the next Link Struct in the current *EntriesList*.                                                                                                         |
| Index       | DWORD      | Index into Top-Level Struct *EntryList*.                                                                                                                                                                                                                                         |
| IsChild     | BYTE       | 1 if this is a link, and there is a LinkComment. 0 if this is a direct reference to the dialog, and there is no LinkComment.                                                                                                                                                     |
| LinkComment | CExoString | This Field is present only if this Sync Struct is for a linked line of conversation (appears grey by default in the toolset Conversation Editor).<br><br>If this is a link, then the Conversation Editor will show and edit the LinkComment instead of the Dialog Struct's own Comment. |

[↑ Back to Top](#bioware-aurora-engine)

---

BioWare Corp. http://www.bioware.com
