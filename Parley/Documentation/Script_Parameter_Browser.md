# Script Parameter Browser

## Overview

The Script Parameter Browser helps you quickly add script parameters to dialog nodes by browsing available keys and values defined in your NWScript files. This reduces typos and saves time compared to manually typing parameter names.

## How It Works

When you assign a script to a dialog node (either a condition or action script), Parley automatically scans the script file for parameter declarations in special comment blocks. These declarations define:

- **Keys**: Parameter names (like `BASE_ITEM`, `INVENTORY_SLOT`)
- **Values**: Valid values for each key (like `BASE_ITEM_SHORTSWORD`, `INVENTORY_SLOT_RIGHTHAND`)

## Using the Parameter Browser

### Adding Parameters to a Dialog Node

1. **Assign a script** to your dialog node using the script browser
2. **Click the "Browse Parameters" button** (appears next to the parameter list)
3. **Select a parameter key** from the left list (e.g., `BASE_ITEM`)
4. **Select a value** from the right list (e.g., `BASE_ITEM_SHORTSWORD`)
5. **Click "Add Parameter"** or press Enter

The parameter will be added to your dialog node automatically.

### Browser Features

**Two-Column Layout**:
- Left column: Available parameter keys
- Right column: Values for the selected key

**Keyboard Shortcuts**:
- **Enter**: Add selected parameter and close
- **Escape**: Cancel without adding
- **Double-click key**: Add key without value
- **Double-click value**: Add key + value

**Button Actions**:
- **Copy Key**: Copy selected key to clipboard
- **Copy Value**: Copy selected value to clipboard
- **Add Parameter**: Add key/value pair to dialog node
- **Close**: Cancel without adding

## Creating Script Parameter Declarations

To make your scripts work with the Parameter Browser, add special comment blocks at the top of your `.nss` files:

### Format

```nwscript
/*
----KeyList----
PARAMETER_NAME_1
PARAMETER_NAME_2

----ValueList-PARAMETER_NAME_1----
VALUE_1
VALUE_2
VALUE_3

----ValueList-PARAMETER_NAME_2----
OTHER_VALUE_1
OTHER_VALUE_2

----
*/
```

### Example: Static Values

See `Documentation/parameter_example.nss` for a complete working example. Here's a simplified version:

```nwscript
/*
----KeyList----
BASE_ITEM
MUST_BE_EQUIPPED

----ValueList-BASE_ITEM----
BASE_ITEM_SHORTSWORD
BASE_ITEM_LONGSWORD
BASE_ITEM_DAGGER

----ValueList-MUST_BE_EQUIPPED----
0
1

----
*/

int StartingConditional()
{
    string sBaseItem = GetScriptParam("BASE_ITEM");
    string sEquipped = GetScriptParam("MUST_BE_EQUIPPED");
    // ... rest of script logic
}
```

### Example: Dynamic Journal Values

This example loads quest tags dynamically from your module's journal file:

```nwscript
/*
----KeyList----
sQuest
iEntryID

----ValueList-sQuest----FROM_JOURNAL_TAGS----

----ValueList-iEntryID----FROM_JOURNAL_ENTRIES(sQuest)----

----
*/

int StartingConditional()
{
    object oPC = GetPCSpeaker();
    string sQuest = GetScriptParam("sQuest");
    int iQuestState = GetCampaignInt(CAMPAIGN, sQuest, oPC);
    int iEntryID = StringToInt(GetScriptParam("iEntryID"));

    return (iQuestState == iEntryID);
}
```

When you browse parameters:
- `sQuest` will show all quest tags from module.jrl (e.g., "QUEST_001", "QUEST_MERCHANT")
- `iEntryID` will show only entry IDs for the selected quest (e.g., if `sQuest="LISTA"`, shows "1", "2", "3", "4")

### Format Rules

**KeyList Section**:
- Lists all parameter names your script accepts
- One parameter name per line
- Use uppercase with underscores (e.g., `BASE_ITEM`)

**ValueList Sections**:
- One section per key: `----ValueList-KEYNAME----`
- Lists valid values for that specific key
- One value per line
- Must match the key name exactly

**Dynamic Value Sources**:
- Use special markers to load values from runtime data
- `FROM_JOURNAL_TAGS` - Loads all quest tags from module.jrl
- `FROM_JOURNAL_ENTRIES(keyname)` - Loads all unique entry IDs across all quests
- If journal is empty or not loaded, shows "No journal entries found"

**Closing**:
- End with `----` on its own line
- Comment block must use `/* */` style

## Caching

Parley caches script parameter declarations for the duration of your editing session:

- First time you browse parameters for a script, Parley parses the file
- Subsequent uses load from cache instantly
- Cache clears when you close Parley

## Tips

**Reduce Typos**: Use the browser instead of manually typing parameter names and values

**Script Templates**: Create template scripts with common parameter sets (items, slots, quantities) and reuse them

**Legacy Scripts**: Scripts without parameter declarations still work - you can type parameters manually

**Copy/Paste**: Use "Copy Key" and "Copy Value" buttons to paste into other tools or documentation

## Troubleshooting

**"No parameters available" message**:
- Check that your script file has parameter declaration comments
- Verify the format matches the examples above
- Ensure the script file is in a configured search path

**Values not showing for a key**:
- Check that the `----ValueList-KEYNAME----` section exists
- Verify the key name in the ValueList matches the KeyList exactly
- Make sure each value is on its own line

**Parameters not working in-game**:
- Verify your script actually uses `GetScriptParam()` to read the parameters
- Check that parameter names match between declarations and `GetScriptParam()` calls
- Test with debug mode enabled (see `parameter_example.nss` for DEBUG_MODE pattern)

## See Also

- `Documentation/parameter_example.nss` - Complete working example with debug mode
- Script Browser documentation
- NWScript `GetScriptParam()` function reference
