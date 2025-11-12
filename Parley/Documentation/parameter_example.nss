/*
This is an example starting conditional script demonstrating Parley's parameter management system.
It checks if a PC has the specific base item like a short sword or a container.
The Parley conversation editor allows you to browse for the key list and value lists saving you time and reducing typos.


----KeyList----
BASE_ITEM
INVENTORY_SLOT
MUST_BE_EQUIPPED
MUST_HAVE_QUANTITY

----ValueList-BASE_ITEM----
BASE_ITEM_SHORTSWORD
BASE_ITEM_LONGSWORD
BASE_ITEM_BATTLEAXE
BASE_ITEM_BASTARDSWORD
BASE_ITEM_LIGHTFLAIL
BASE_ITEM_WARHAMMER
BASE_ITEM_HEAVYCROSSBOW
BASE_ITEM_LIGHTCROSSBOW
BASE_ITEM_LONGBOW
BASE_ITEM_LIGHTMACE
BASE_ITEM_HALBERD
BASE_ITEM_SHORTBOW
BASE_ITEM_TWOBLADEDSWORD
BASE_ITEM_GREATSWORD
BASE_ITEM_SMALLSHIELD
BASE_ITEM_TORCH
BASE_ITEM_ARMOR
BASE_ITEM_HELMET
BASE_ITEM_GREATAXE
BASE_ITEM_AMULET
BASE_ITEM_ARROW
BASE_ITEM_BELT
BASE_ITEM_DAGGER
BASE_ITEM_MISCSMALL
BASE_ITEM_BOLT
BASE_ITEM_BOOTS
BASE_ITEM_BULLET
BASE_ITEM_CLUB
BASE_ITEM_MISCMEDIUM
BASE_ITEM_DART
BASE_ITEM_DIREMACE
BASE_ITEM_DOUBLEAXE
BASE_ITEM_MISCLARGE
BASE_ITEM_HEAVYFLAIL
BASE_ITEM_GLOVES
BASE_ITEM_LIGHTHAMMER
BASE_ITEM_HANDAXE
BASE_ITEM_HEALERSKIT
BASE_ITEM_KAMA
BASE_ITEM_KATANA
BASE_ITEM_KUKRI
BASE_ITEM_MISCTALL
BASE_ITEM_MAGICROD
BASE_ITEM_MAGICSTAFF
BASE_ITEM_MAGICWAND
BASE_ITEM_MORNINGSTAR
BASE_ITEM_POTIONS
BASE_ITEM_QUARTERSTAFF
BASE_ITEM_RAPIER
BASE_ITEM_RING
BASE_ITEM_SCIMITAR
BASE_ITEM_SCROLL
BASE_ITEM_SCYTHE
BASE_ITEM_LARGESHIELD
BASE_ITEM_TOWERSHIELD
BASE_ITEM_SHORTSPEAR
BASE_ITEM_SHURIKEN
BASE_ITEM_SICKLE
BASE_ITEM_SLING
BASE_ITEM_THIEVESTOOLS
BASE_ITEM_THROWINGAXE
BASE_ITEM_TRAPKIT
BASE_ITEM_KEY
BASE_ITEM_LARGEBOX
BASE_ITEM_MISCWIDE
BASE_ITEM_CSLASHWEAPON
BASE_ITEM_CPIERCWEAPON
BASE_ITEM_CBLUDGWEAPON
BASE_ITEM_CSLSHPRCWEAP
BASE_ITEM_CREATUREITEM
BASE_ITEM_BOOK
BASE_ITEM_SPELLSCROLL
BASE_ITEM_GOLD
BASE_ITEM_GEM
BASE_ITEM_BRACER
BASE_ITEM_MISCTHIN
BASE_ITEM_CLOAK
BASE_ITEM_GRENADE
BASE_ITEM_TRIDENT
BASE_ITEM_BLANK_POTION
BASE_ITEM_BLANK_SCROLL
BASE_ITEM_BLANK_WAND
BASE_ITEM_ENCHANTED_POTION
BASE_ITEM_ENCHANTED_SCROLL
BASE_ITEM_ENCHANTED_WAND
BASE_ITEM_DWARVENWARAXE
BASE_ITEM_CRAFTMATERIALMED
BASE_ITEM_CRAFTMATERIALSML
BASE_ITEM_WHIP

----ValueList-INVENTORY_SLOT----
INVENTORY_SLOT_ARMS
INVENTORY_SLOT_ARROWS
INVENTORY_SLOT_BELT
INVENTORY_SLOT_BOLTS
INVENTORY_SLOT_BOOTS
INVENTORY_SLOT_BULLETS
INVENTORY_SLOT_CARMOUR
INVENTORY_SLOT_CHEST
INVENTORY_SLOT_CLOAK
INVENTORY_SLOT_CWEAPON_B
INVENTORY_SLOT_CWEAPON_L
INVENTORY_SLOT_CWEAPON_R
INVENTORY_SLOT_HEAD
INVENTORY_SLOT_LEFTHAND
INVENTORY_SLOT_LEFTRING
INVENTORY_SLOT_NECK
INVENTORY_SLOT_RIGHTHAND
INVENTORY_SLOT_RIGHTRING

----ValueList-MUST_BE_EQUIPPED----
0
1

----ValueList-MUST_HAVE_QUANTITY----
0
1
2
3
4
5
10
20
50
100

----

 */

// ========== DEBUG MODE TOGGLE ==========
// Set to TRUE to see debug messages, FALSE to disable
const int DEBUG_MODE = FALSE;
// ========================================

int StringToBaseItemConstant(string sBaseItem);
int StringToInventorySlotConstant(string sSlotItem);
int GetEquippedItemOfType(object oPC, int iBaseItemType);
int GetItemTypeInInventory(object oPC, int iBaseItem);
int GetItemCountByBaseItem(object oPC, int iBaseItemType);
int CountEquippedItemsOfType(object oPC, int iBaseItemType);

int StartingConditional()
{
	object oPC				 = GetPCSpeaker();
	string sBaseItem		 = GetScriptParam("BASE_ITEM");			  // BASE_ITEM_*
	string sSlotItem		 = GetScriptParam("INVENTORY_SLOT");	  // Must be equipped in this slot (must be off hand, main hand, etc)
	string sMustBeEquipped	 = GetScriptParam("MUST_BE_EQUIPPED");	  // "1" must be equipped, "0" must NOT be equipped, "" = don't care
	string sMustHaveQuantity = GetScriptParam("MUST_HAVE_QUANTITY");  // minimum quantity needed, "" = don't care
	int iBaseItem			 = StringToBaseItemConstant(sBaseItem);
	int iMustBeEquipped		 = StringToInt(sMustBeEquipped);
	int iMustHaveQuantity	 = StringToInt(sMustHaveQuantity);
	int iSlotRequired		 = StringToInventorySlotConstant(sSlotItem);

	// DEBUG: Log all parameters received
	if (DEBUG_MODE)
	{
		SendMessageToPC(oPC, "[SC_BASE_ITEM] --- SCRIPT CALLED ---");
		SendMessageToPC(oPC, "[SC_BASE_ITEM] BASE_ITEM='" + sBaseItem + "' (int=" + IntToString(iBaseItem) + ")");
		SendMessageToPC(oPC, "[SC_BASE_ITEM] INVENTORY_SLOT='" + sSlotItem + "' (int=" + IntToString(iSlotRequired) + ")");
		SendMessageToPC(oPC, "[SC_BASE_ITEM] MUST_BE_EQUIPPED='" + sMustBeEquipped + "' (int=" + IntToString(iMustBeEquipped) + ")");
		SendMessageToPC(oPC, "[SC_BASE_ITEM] MUST_HAVE_QUANTITY='" + sMustHaveQuantity + "' (int=" + IntToString(iMustHaveQuantity) + ")");
	}

	// Get current state
	int isEquipped	  = GetEquippedItemOfType(oPC, iBaseItem);
	int isInInventory = GetItemTypeInInventory(oPC, iBaseItem);
	int iTotalCount	  = GetItemCountByBaseItem(oPC, iBaseItem);
	int hasItemAtAll  = (isEquipped || isInInventory);

	// DEBUG: Log current PC state
	if (DEBUG_MODE)
	{
		SendMessageToPC(oPC, "[SC_BASE_ITEM] PC State: isEquipped=" + IntToString(isEquipped) + " isInInventory=" + IntToString(isInInventory) + " iTotalCount=" + IntToString(iTotalCount));
	}

	// Special case: BASE_ITEM_SHORTSWORD has value 0, so check if parameter was actually provided
	if (iBaseItem == 0 && sBaseItem == "")
	{
		if (DEBUG_MODE) SendMessageToPC(oPC, "[SC_BASE_ITEM] RESULT: FALSE - BASE_ITEM parameter not provided");
		return FALSE;  // if iBaseItem parameter is missing, fail.
	}

	// Check all conditions - empty string ("") means no requirement for that parameter
	// Handle combinations of requirements properly

	// Get equipped and inventory counts separately for more precise checks
	int iEquippedCount = CountEquippedItemsOfType(oPC, iBaseItem);
	int iInventoryOnlyCount = iTotalCount - iEquippedCount;

	// Handle combined equipment + quantity requirements
	if (sMustBeEquipped != "" && sMustHaveQuantity != "")
	{
		if (DEBUG_MODE) SendMessageToPC(oPC, "[SC_BASE_ITEM] COMBINED CHECK: Equipment + Quantity");
		if (iMustBeEquipped == 1 && iMustHaveQuantity > 0)
		{
			// Must have at least X equipped
			if (DEBUG_MODE) SendMessageToPC(oPC, "[SC_BASE_ITEM] Check: Need " + IntToString(iMustHaveQuantity) + " equipped, have " + IntToString(iEquippedCount));
			if (iEquippedCount < iMustHaveQuantity)
			{
				if (DEBUG_MODE) SendMessageToPC(oPC, "[SC_BASE_ITEM] RESULT: FALSE - Not enough equipped");
				return FALSE;
			}
		}
		else if (iMustBeEquipped == 0 && iMustHaveQuantity > 0)
		{
			// Must have at least X in inventory (not equipped)
			if (DEBUG_MODE) SendMessageToPC(oPC, "[SC_BASE_ITEM] Check: Need " + IntToString(iMustHaveQuantity) + " in inventory (not equipped), have " + IntToString(iInventoryOnlyCount));
			if (iInventoryOnlyCount < iMustHaveQuantity)
			{
				if (DEBUG_MODE) SendMessageToPC(oPC, "[SC_BASE_ITEM] RESULT: FALSE - Not enough in inventory");
				return FALSE;
			}
			// Also ensure none are equipped
			if (iEquippedCount > 0)
			{
				if (DEBUG_MODE) SendMessageToPC(oPC, "[SC_BASE_ITEM] RESULT: FALSE - Some are equipped (must not be)");
				return FALSE;
			}
		}
	}
	// Handle individual requirements when not combined
	else
	{
		if (DEBUG_MODE) SendMessageToPC(oPC, "[SC_BASE_ITEM] INDIVIDUAL CHECKS:");
		// 1. Check equipment requirement if specified (without quantity)
		if (sMustBeEquipped != "")
		{
			if (iMustBeEquipped == 1)
			{
				// Must be equipped (at least 1)
				if (DEBUG_MODE) SendMessageToPC(oPC, "[SC_BASE_ITEM] Check: Must be equipped, isEquipped=" + IntToString(isEquipped));
				if (!isEquipped)
				{
					if (DEBUG_MODE) SendMessageToPC(oPC, "[SC_BASE_ITEM] RESULT: FALSE - Not equipped");
					return FALSE;
				}
			}
			else if (iMustBeEquipped == 0)
			{
				// Must NOT be equipped (only in inventory)
				if (DEBUG_MODE) SendMessageToPC(oPC, "[SC_BASE_ITEM] Check: Must NOT be equipped, isEquipped=" + IntToString(isEquipped) + " isInInventory=" + IntToString(isInInventory));
				if (isEquipped)
				{
					if (DEBUG_MODE) SendMessageToPC(oPC, "[SC_BASE_ITEM] RESULT: FALSE - Is equipped (must not be)");
					return FALSE;
				}
				// Also must have it in inventory if it can't be equipped
				if (!isInInventory)
				{
					if (DEBUG_MODE) SendMessageToPC(oPC, "[SC_BASE_ITEM] RESULT: FALSE - Not in inventory");
					return FALSE;
				}
			}
		}

		// 3. Check quantity requirement if specified (without equipment requirement)
		if (sMustHaveQuantity != "")
		{
			if (iMustHaveQuantity > 0)
			{
				if (DEBUG_MODE) SendMessageToPC(oPC, "[SC_BASE_ITEM] Check: Need " + IntToString(iMustHaveQuantity) + " total, have " + IntToString(iTotalCount));
				if (iTotalCount < iMustHaveQuantity)
				{
					if (DEBUG_MODE) SendMessageToPC(oPC, "[SC_BASE_ITEM] RESULT: FALSE - Not enough total items");
					return FALSE; // Not enough items of this type total
				}
			}
		}
	}

	// 2. Check specific slot requirement if specified
	if (sSlotItem != "")
	{
		if (DEBUG_MODE) SendMessageToPC(oPC, "[SC_BASE_ITEM] SLOT CHECK: " + sSlotItem + " (slot=" + IntToString(iSlotRequired) + ")");
		if (iSlotRequired != 256) // 256 = invalid slot
		{
			object oItemInSlot = GetItemInSlot(iSlotRequired, oPC);
			int isValid		   = GetIsObjectValid(oItemInSlot);
			int itemType	   = GetBaseItemType(oItemInSlot);
			if (DEBUG_MODE) SendMessageToPC(oPC, "[SC_BASE_ITEM] Item in slot: valid=" + IntToString(isValid) + " type=" + IntToString(itemType) + " expected=" + IntToString(iBaseItem));
			if (!GetIsObjectValid(oItemInSlot) || GetBaseItemType(oItemInSlot) != iBaseItem)
			{
				if (DEBUG_MODE) SendMessageToPC(oPC, "[SC_BASE_ITEM] RESULT: FALSE - Wrong item in slot or slot empty");
				return FALSE; // Required item not in specified slot
			}
			// If quantity is also specified with slot requirement, check the stack size in that slot
			if (sMustHaveQuantity != "" && iMustHaveQuantity > 0)
			{
				int iSlotStackSize = GetItemStackSize(oItemInSlot);
				if (DEBUG_MODE) SendMessageToPC(oPC, "[SC_BASE_ITEM] Slot stack check: need " + IntToString(iMustHaveQuantity) + " have " + IntToString(iSlotStackSize));
				if (iSlotStackSize < iMustHaveQuantity)
				{
					if (DEBUG_MODE) SendMessageToPC(oPC, "[SC_BASE_ITEM] RESULT: FALSE - Not enough in slot stack");
					return FALSE; // Not enough in the specific slot
				}
			}
		}
	}

	// 4. If no specific requirements were set, just check if they have the item at all
	if (sMustBeEquipped == "" && sMustHaveQuantity == "" && sSlotItem == "")
	{
		if (DEBUG_MODE) SendMessageToPC(oPC, "[SC_BASE_ITEM] DEFAULT CHECK: No specific requirements, hasItemAtAll=" + IntToString(hasItemAtAll));
		if (!hasItemAtAll)
		{
			if (DEBUG_MODE) SendMessageToPC(oPC, "[SC_BASE_ITEM] RESULT: FALSE - Don't have item at all");
			return FALSE; // They don't have the item and no specific requirements
		}
	}

	// If we got here, all specified conditions are met
	if (DEBUG_MODE) SendMessageToPC(oPC, "[SC_BASE_ITEM] RESULT: TRUE - All conditions met");
	return TRUE;
}

////////////////////////////////////////////////////////////////////////
////////////////////// ** Helper Functions ** //////////////////////////
////////////////////////////////////////////////////////////////////////

int GetItemTypeInInventory(object oPC, int iBaseItem)
{
	object oItem = GetFirstItemInInventory(oPC);
	while (GetIsObjectValid(oItem))
	{
		if (GetBaseItemType(oItem) == iBaseItem)
		{
			// isInInventory = TRUE;  // Do I want a count of items?
			return TRUE;  // Found the item type in inventory
		}
		oItem = GetNextItemInInventory(oPC);
	}
	return FALSE;
}

int CountEquippedItemsOfType(object oPC, int iBaseItemType)
{
	int iSlot;
	object oItem;
	int iCount = 0;

	// Check all possible equipment slots (0 through 17)
	for (iSlot = 0; iSlot < NUM_INVENTORY_SLOTS; iSlot++)
	{
		oItem = GetItemInSlot(iSlot, oPC);
		if (GetIsObjectValid(oItem))
		{
			if (GetBaseItemType(oItem) == iBaseItemType)
			{
				iCount++;
			}
		}
	}

	return iCount;
}

int GetEquippedItemOfType(object oPC, int iBaseItemType)
{
	int iSlot;
	object oItem;

	// Check all possible equipment slots (0 through 17)
	for (iSlot = 0; iSlot < NUM_INVENTORY_SLOTS; iSlot++)
	{
		oItem = GetItemInSlot(iSlot, oPC);
		if (GetIsObjectValid(oItem))
		{
			if (GetBaseItemType(oItem) == iBaseItemType)
			{
				return TRUE;
			}
		}
	}
	return FALSE;
}

int StringToInventorySlotConstant(string sSlotItem)
{
	// NUM_INVENTORY_SLOTS
	sSlotItem = GetStringUpperCase(sSlotItem);

	if (sSlotItem == "INVENTORY_SLOT_ARMS") return INVENTORY_SLOT_ARMS;
	if (sSlotItem == "INVENTORY_SLOT_ARROWS") return INVENTORY_SLOT_ARROWS;
	if (sSlotItem == "INVENTORY_SLOT_BELT") return INVENTORY_SLOT_BELT;
	if (sSlotItem == "INVENTORY_SLOT_BOLTS") return INVENTORY_SLOT_BOLTS;
	if (sSlotItem == "INVENTORY_SLOT_BOOTS") return INVENTORY_SLOT_BOOTS;
	if (sSlotItem == "INVENTORY_SLOT_BULLETS") return INVENTORY_SLOT_BULLETS;
	if (sSlotItem == "INVENTORY_SLOT_CARMOUR") return INVENTORY_SLOT_CARMOUR;
	if (sSlotItem == "INVENTORY_SLOT_CHEST") return INVENTORY_SLOT_CHEST;
	if (sSlotItem == "INVENTORY_SLOT_CLOAK") return INVENTORY_SLOT_CLOAK;
	if (sSlotItem == "INVENTORY_SLOT_CWEAPON_B") return INVENTORY_SLOT_CWEAPON_B;
	if (sSlotItem == "INVENTORY_SLOT_CWEAPON_L") return INVENTORY_SLOT_CWEAPON_L;
	if (sSlotItem == "INVENTORY_SLOT_CWEAPON_R") return INVENTORY_SLOT_CWEAPON_R;
	if (sSlotItem == "INVENTORY_SLOT_HEAD") return INVENTORY_SLOT_HEAD;
	if (sSlotItem == "INVENTORY_SLOT_LEFTHAND") return INVENTORY_SLOT_LEFTHAND;
	if (sSlotItem == "INVENTORY_SLOT_LEFTRING") return INVENTORY_SLOT_LEFTRING;
	if (sSlotItem == "INVENTORY_SLOT_NECK") return INVENTORY_SLOT_NECK;
	if (sSlotItem == "INVENTORY_SLOT_RIGHTHAND") return INVENTORY_SLOT_RIGHTHAND;
	if (sSlotItem == "INVENTORY_SLOT_RIGHTRING") return INVENTORY_SLOT_RIGHTRING;

	return 256;	 // Invalid slot
}  //

int StringToBaseItemConstant(string sBaseItem)
{
	// Convert to uppercase for case-insensitive matching.
	// I really want a better way to do this.
	sBaseItem = GetStringUpperCase(sBaseItem);

	if (sBaseItem == "BASE_ITEM_SHORTSWORD") return BASE_ITEM_SHORTSWORD;
	if (sBaseItem == "BASE_ITEM_LONGSWORD") return BASE_ITEM_LONGSWORD;
	if (sBaseItem == "BASE_ITEM_BATTLEAXE") return BASE_ITEM_BATTLEAXE;
	if (sBaseItem == "BASE_ITEM_BASTARDSWORD") return BASE_ITEM_BASTARDSWORD;
	if (sBaseItem == "BASE_ITEM_LIGHTFLAIL") return BASE_ITEM_LIGHTFLAIL;
	if (sBaseItem == "BASE_ITEM_WARHAMMER") return BASE_ITEM_WARHAMMER;
	if (sBaseItem == "BASE_ITEM_HEAVYCROSSBOW") return BASE_ITEM_HEAVYCROSSBOW;
	if (sBaseItem == "BASE_ITEM_LIGHTCROSSBOW") return BASE_ITEM_LIGHTCROSSBOW;
	if (sBaseItem == "BASE_ITEM_LONGBOW") return BASE_ITEM_LONGBOW;
	if (sBaseItem == "BASE_ITEM_LIGHTMACE") return BASE_ITEM_LIGHTMACE;
	if (sBaseItem == "BASE_ITEM_HALBERD") return BASE_ITEM_HALBERD;
	if (sBaseItem == "BASE_ITEM_SHORTBOW") return BASE_ITEM_SHORTBOW;
	if (sBaseItem == "BASE_ITEM_TWOBLADEDSWORD") return BASE_ITEM_TWOBLADEDSWORD;
	if (sBaseItem == "BASE_ITEM_GREATSWORD") return BASE_ITEM_GREATSWORD;
	if (sBaseItem == "BASE_ITEM_SMALLSHIELD") return BASE_ITEM_SMALLSHIELD;
	if (sBaseItem == "BASE_ITEM_TORCH") return BASE_ITEM_TORCH;
	if (sBaseItem == "BASE_ITEM_ARMOR") return BASE_ITEM_ARMOR;
	if (sBaseItem == "BASE_ITEM_HELMET") return BASE_ITEM_HELMET;
	if (sBaseItem == "BASE_ITEM_GREATAXE") return BASE_ITEM_GREATAXE;
	if (sBaseItem == "BASE_ITEM_AMULET") return BASE_ITEM_AMULET;
	if (sBaseItem == "BASE_ITEM_ARROW") return BASE_ITEM_ARROW;
	if (sBaseItem == "BASE_ITEM_BELT") return BASE_ITEM_BELT;
	if (sBaseItem == "BASE_ITEM_DAGGER") return BASE_ITEM_DAGGER;
	if (sBaseItem == "BASE_ITEM_MISCSMALL") return BASE_ITEM_MISCSMALL;
	if (sBaseItem == "BASE_ITEM_BOLT") return BASE_ITEM_BOLT;
	if (sBaseItem == "BASE_ITEM_BOOTS") return BASE_ITEM_BOOTS;
	if (sBaseItem == "BASE_ITEM_BULLET") return BASE_ITEM_BULLET;
	if (sBaseItem == "BASE_ITEM_CLUB") return BASE_ITEM_CLUB;
	if (sBaseItem == "BASE_ITEM_MISCMEDIUM") return BASE_ITEM_MISCMEDIUM;
	if (sBaseItem == "BASE_ITEM_DART") return BASE_ITEM_DART;
	if (sBaseItem == "BASE_ITEM_DIREMACE") return BASE_ITEM_DIREMACE;
	if (sBaseItem == "BASE_ITEM_DOUBLEAXE") return BASE_ITEM_DOUBLEAXE;
	if (sBaseItem == "BASE_ITEM_MISCLARGE") return BASE_ITEM_MISCLARGE;
	if (sBaseItem == "BASE_ITEM_HEAVYFLAIL") return BASE_ITEM_HEAVYFLAIL;
	if (sBaseItem == "BASE_ITEM_GLOVES") return BASE_ITEM_GLOVES;
	if (sBaseItem == "BASE_ITEM_LIGHTHAMMER") return BASE_ITEM_LIGHTHAMMER;
	if (sBaseItem == "BASE_ITEM_HANDAXE") return BASE_ITEM_HANDAXE;
	if (sBaseItem == "BASE_ITEM_HEALERSKIT") return BASE_ITEM_HEALERSKIT;
	if (sBaseItem == "BASE_ITEM_KAMA") return BASE_ITEM_KAMA;
	if (sBaseItem == "BASE_ITEM_KATANA") return BASE_ITEM_KATANA;
	if (sBaseItem == "BASE_ITEM_KUKRI") return BASE_ITEM_KUKRI;
	if (sBaseItem == "BASE_ITEM_MISCTALL") return BASE_ITEM_MISCTALL;
	if (sBaseItem == "BASE_ITEM_MAGICROD") return BASE_ITEM_MAGICROD;
	if (sBaseItem == "BASE_ITEM_MAGICSTAFF") return BASE_ITEM_MAGICSTAFF;
	if (sBaseItem == "BASE_ITEM_MAGICWAND") return BASE_ITEM_MAGICWAND;
	if (sBaseItem == "BASE_ITEM_MORNINGSTAR") return BASE_ITEM_MORNINGSTAR;
	if (sBaseItem == "BASE_ITEM_POTIONS") return BASE_ITEM_POTIONS;
	if (sBaseItem == "BASE_ITEM_QUARTERSTAFF") return BASE_ITEM_QUARTERSTAFF;
	if (sBaseItem == "BASE_ITEM_RAPIER") return BASE_ITEM_RAPIER;
	if (sBaseItem == "BASE_ITEM_RING") return BASE_ITEM_RING;
	if (sBaseItem == "BASE_ITEM_SCIMITAR") return BASE_ITEM_SCIMITAR;
	if (sBaseItem == "BASE_ITEM_SCROLL") return BASE_ITEM_SCROLL;
	if (sBaseItem == "BASE_ITEM_SCYTHE") return BASE_ITEM_SCYTHE;
	if (sBaseItem == "BASE_ITEM_LARGESHIELD") return BASE_ITEM_LARGESHIELD;
	if (sBaseItem == "BASE_ITEM_TOWERSHIELD") return BASE_ITEM_TOWERSHIELD;
	if (sBaseItem == "BASE_ITEM_SHORTSPEAR") return BASE_ITEM_SHORTSPEAR;
	if (sBaseItem == "BASE_ITEM_SHURIKEN") return BASE_ITEM_SHURIKEN;
	if (sBaseItem == "BASE_ITEM_SICKLE") return BASE_ITEM_SICKLE;
	if (sBaseItem == "BASE_ITEM_SLING") return BASE_ITEM_SLING;
	if (sBaseItem == "BASE_ITEM_THIEVESTOOLS") return BASE_ITEM_THIEVESTOOLS;
	if (sBaseItem == "BASE_ITEM_THROWINGAXE") return BASE_ITEM_THROWINGAXE;
	if (sBaseItem == "BASE_ITEM_TRAPKIT") return BASE_ITEM_TRAPKIT;
	if (sBaseItem == "BASE_ITEM_KEY") return BASE_ITEM_KEY;
	if (sBaseItem == "BASE_ITEM_LARGEBOX") return BASE_ITEM_LARGEBOX;
	if (sBaseItem == "BASE_ITEM_MISCWIDE") return BASE_ITEM_MISCWIDE;
	if (sBaseItem == "BASE_ITEM_CSLASHWEAPON") return BASE_ITEM_CSLASHWEAPON;
	if (sBaseItem == "BASE_ITEM_CPIERCWEAPON") return BASE_ITEM_CPIERCWEAPON;
	if (sBaseItem == "BASE_ITEM_CBLUDGWEAPON") return BASE_ITEM_CBLUDGWEAPON;
	if (sBaseItem == "BASE_ITEM_CSLSHPRCWEAP") return BASE_ITEM_CSLSHPRCWEAP;
	if (sBaseItem == "BASE_ITEM_CREATUREITEM") return BASE_ITEM_CREATUREITEM;
	if (sBaseItem == "BASE_ITEM_BOOK") return BASE_ITEM_BOOK;
	if (sBaseItem == "BASE_ITEM_SPELLSCROLL") return BASE_ITEM_SPELLSCROLL;
	if (sBaseItem == "BASE_ITEM_GOLD") return BASE_ITEM_GOLD;
	if (sBaseItem == "BASE_ITEM_GEM") return BASE_ITEM_GEM;
	if (sBaseItem == "BASE_ITEM_BRACER") return BASE_ITEM_BRACER;
	if (sBaseItem == "BASE_ITEM_MISCTHIN") return BASE_ITEM_MISCTHIN;
	if (sBaseItem == "BASE_ITEM_CLOAK") return BASE_ITEM_CLOAK;
	if (sBaseItem == "BASE_ITEM_GRENADE") return BASE_ITEM_GRENADE;
	if (sBaseItem == "BASE_ITEM_TRIDENT") return BASE_ITEM_TRIDENT;
	if (sBaseItem == "BASE_ITEM_BLANK_POTION") return BASE_ITEM_BLANK_POTION;
	if (sBaseItem == "BASE_ITEM_BLANK_SCROLL") return BASE_ITEM_BLANK_SCROLL;
	if (sBaseItem == "BASE_ITEM_BLANK_WAND") return BASE_ITEM_BLANK_WAND;
	if (sBaseItem == "BASE_ITEM_ENCHANTED_POTION") return BASE_ITEM_ENCHANTED_POTION;
	if (sBaseItem == "BASE_ITEM_ENCHANTED_SCROLL") return BASE_ITEM_ENCHANTED_SCROLL;
	if (sBaseItem == "BASE_ITEM_ENCHANTED_WAND") return BASE_ITEM_ENCHANTED_WAND;
	if (sBaseItem == "BASE_ITEM_DWARVENWARAXE") return BASE_ITEM_DWARVENWARAXE;
	if (sBaseItem == "BASE_ITEM_CRAFTMATERIALMED") return BASE_ITEM_CRAFTMATERIALMED;
	if (sBaseItem == "BASE_ITEM_CRAFTMATERIALSML") return BASE_ITEM_CRAFTMATERIALSML;
	if (sBaseItem == "BASE_ITEM_WHIP") return BASE_ITEM_WHIP;
	if (sBaseItem == "BASE_ITEM_INVALID") return BASE_ITEM_INVALID;
	return 256;	 // Invalid base item
}

/*
 * GetItemCountByBaseItem
 * Count how many items of a specific base item type a PC is carrying (both inventory and equipped)
 * Example: GetItemCountByBaseItem(oPC, BASE_ITEM_BASTARDSWORD) returns how many bastard swords the PC has
 * Parameters:
 *   oPC - The player character to check
 *   iBaseItemType - The base item type constant (BASE_ITEM_*) to look for
 * Returns: Integer count of matching items (includes stack sizes for stackable items)
 */
int GetItemCountByBaseItem(object oPC, int iBaseItemType)
{
	int iCount = 0;
	object oItem;

	// Count items in regular inventory
	oItem = GetFirstItemInInventory(oPC);
	while (GetIsObjectValid(oItem))
	{
		if (GetBaseItemType(oItem) == iBaseItemType)
		{
			// For stackable items like arrows, bolts, bullets, darts, shuriken, etc.
			int iStackSize = GetItemStackSize(oItem);
			if (iStackSize > 1)
			{
				iCount += iStackSize;
			}
			else
			{
				iCount++;
			}
		}
		oItem = GetNextItemInInventory(oPC);
	}

	// Also check equipped items
	int iSlot;
	for (iSlot = 0; iSlot < NUM_INVENTORY_SLOTS; iSlot++)
	{
		oItem = GetItemInSlot(iSlot, oPC);
		if (GetIsObjectValid(oItem) && GetBaseItemType(oItem) == iBaseItemType)
		{
			// Most equipped items aren't stacked, but just in case
			int iStackSize = GetItemStackSize(oItem);
			if (iStackSize > 1)
			{
				iCount += iStackSize;
			}
			else
			{
				iCount++;
			}
		}
	}

	return iCount;
}
