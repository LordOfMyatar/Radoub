# BioWare Aurora Engine - Creature Format

## Chapter 1: Introduction

[← Back to Main Document](../Bioware_Aurora_Creature_Format.md)

---

A **Creature** is an object that can move around in the game and interact with other objects such as doors, placeable objects, items, encounters, triggers, or other creatures. The behaviour of a creature is controlled by a set of scripts, and it may also be controlled by a Dungeon Master player who possesses it.

A **Player Character** (PC) is a specialized Creature that does not have AI scripts, but is instead controlled directly by a player.

Creatures are stored in the game and toolset using BioWare's [Generic File Format (GFF)](../Bioware_Aurora_GFF_Format.md), and it is assumed that the reader of this document is familiar with GFF.

Creatures can be blueprints or instances. Creature blueprints are saved as GFF files having a UTC extension and "UTC " as the FileType string in their header. Creature instances are stored as Creature Structs within a module's GIT files.

Player Characters can be saved as standalone character files, or as instances in a savegame. Character files are BIC files in a player's localvault directory, dmvault directory, or on a server's servervault directory. A BIC file is a GFF, and has "BIC " as the FileType string in its header. Player characters in a savegame are stored as Structs within a module's IFO file.

---

[Next: Chapter 2 - Creature Struct →](Ch2_Creature_Struct.md)
