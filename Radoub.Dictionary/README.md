# Radoub.Dictionary

D&D/NWN spell-checking library for Radoub tools.

## Features

- **Hybrid Spell-Checking**: Combines Hunspell (real language) + custom dictionaries (game terms)
- **Bundled English**: en_US dictionary included (~550KB)
- **Custom Dictionaries**: JSON format for D&D/NWN terminology
- **Term Extraction**: Extract proper nouns from .2da and dialog files

## Usage

```csharp
// Create dictionary manager for custom terms
var manager = new DictionaryManager();
manager.AddWord("Neverwinter");
manager.AddWord("Aribeth");

// Create spell checker with Hunspell
using var checker = new SpellChecker(manager);
await checker.LoadBundledDictionaryAsync("en_US");

// Check spelling
checker.IsCorrect("adventure");    // true (Hunspell)
checker.IsCorrect("Neverwinter");  // true (custom)
checker.IsCorrect("spyes");        // false

// Get suggestions
checker.GetSuggestions("spyes");   // ["spies", "spews", ...]
```

## Adding Additional Languages

The library bundles en_US. To add other languages:

### 1. Download Dictionary Files

Get `.dic` and `.aff` files from [LibreOffice dictionaries](https://cgit.freedesktop.org/libreoffice/dictionaries/):

| Language | Download |
|----------|----------|
| Spanish (ES) | [es/](https://cgit.freedesktop.org/libreoffice/dictionaries/tree/es) |
| French | [fr_FR/](https://cgit.freedesktop.org/libreoffice/dictionaries/tree/fr_FR) |
| German | [de/](https://cgit.freedesktop.org/libreoffice/dictionaries/tree/de) |
| Portuguese (BR) | [pt_BR/](https://cgit.freedesktop.org/libreoffice/dictionaries/tree/pt_BR) |

### 2. Place in Dictionaries Folder

```
Windows:  %APPDATA%\Radoub\Dictionaries\es_ES\
Linux:    ~/.config/Radoub/Dictionaries/es_ES/
macOS:    ~/Library/Application Support/Radoub/Dictionaries/es_ES/
```

Each folder should contain:
- `{lang_code}.dic` (e.g., `es_ES.dic`)
- `{lang_code}.aff` (e.g., `es_ES.aff`)

### 3. Load in Code

```csharp
// Load from file paths
await checker.LoadHunspellDictionaryAsync(
    "/path/to/es_ES.dic",
    "/path/to/es_ES.aff"
);
```

## Custom Dictionary Format

```json
{
  "version": "1.0",
  "source": "NWN Official Campaign",
  "description": "Proper nouns from Neverwinter Nights",
  "words": [
    "Aribeth",
    "Neverwinter",
    "Luskan",
    "Waterdeep"
  ],
  "ignoredWords": [
    "lol",
    "brb"
  ]
}
```

## Dependencies

- [WeCantSpell.Hunspell](https://github.com/aarondandy/WeCantSpell.Hunspell) v7.0.1 (MIT)
- [LibreOffice en_US dictionary](https://cgit.freedesktop.org/libreoffice/dictionaries/) (BSD/Public Domain)

## License

See repository LICENSE.
