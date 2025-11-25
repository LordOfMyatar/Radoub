# BioWare Aurora Engine - Item Format

## Chapter 1: Introduction

[← Back to Main Document](../Bioware_Aurora_Item_Format.md)

---

### 1.1. Overview

An Item is an object that can be placed in an area, or carried in a container, whether the container be a creature, placeable object, or store.

Items are stored in the game and toolset using BioWare's [Generic File Format (GFF)](../Bioware_Aurora_GFF_Format.md), and it is assumed that the reader of this document is familiar with GFF.

Items can be blueprints or instances. Item blueprints are saved as GFF files having a UTI extension and "UTI " as the FileType string in their header. Item instances are stored as Item Structs within a module's GIT files.

### 1.2. Terminology

**icon:** the 2D image of an item as it appears in a player's inventory. The term "icon" may refer to the entire image, or to the component parts of the image, if the inventory icon is constructed from several other images.

**model:** the 3D model of an item as it appears in the game environment. This may refer to the model as seen when equipped by the player or when lying on the ground. An item's equipped model and ground model may or may not be the same, and not all items have equipped models.

---

[Next: Chapter 2 - Item Struct →](Ch2_Item_Struct.md)
