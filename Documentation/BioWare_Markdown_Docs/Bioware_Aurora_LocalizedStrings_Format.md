# BioWare Aurora Engine

## Localized Strings

The game and toolset use a localized string format called a **CExoLocString**, or just **LocString**.

A CExoLocString is a string type used to store text that may
appear to the user and has a number of features designed to allow users to see
text in their own language.

This document assumes that the
reader is already familiar with the game's Talk Table format, as described in
the Talk Table document.

[↑ Back to Top](#bioware-aurora-engine)

## 1. StringRef

A CExoLocString always contains a StrRef, which is an 32-bit unsigned integer index into the **talk table**. The talk table is a table containing all the user-visible text used by the game and official campaigns. Different language versions of the game use talk tables containing text that has been translated for the user's language. Please refer to the Talk Table document for information.

The StrRef stored in a CExoLocString may be 0xFFFFFFFF, or 4294967295, to indicate that it is invalid and does not point to any text in the talk table.

The maximum value for a valid StrRef is 0x00FFFFFF, or 16777215.

[↑ Back to Top](#bioware-aurora-engine)

## 2. Embedded Strings with Language IDs

If the StrRef is invalid (ie., 0xFFFFFFFF), then the talk table is not used, and instead, the localized text must be embedded within the CExoLocString. A CExoLocString may contain zero or more embedded strings, each paired with a **language ID** that identifies what language the string should be displayed for. The talk table file itself stores the user's language ID.

The following is a list of languages and their IDs:

**Table 2: Language IDs**

| **Language**        | **ID** |
| ------------------- | ------ |
| English             | 0      |
| French              | 1      |
| German              | 2      |
| Italian             | 3      |
| Spanish             | 4      |
| Polish              | 5      |
| Korean              | 128    |
| Chinese Traditional | 129    |
| Chinese Simplified  | 130    |
| Japanese            | 131    |

[↑ Back to Top](#bioware-aurora-engine)

## 3. Gender

In addition to specifying a string by Language ID, substrings in a LocString have a gender associated with them. 0 = neutral or masculine; 1 = feminine. In some languages, the text that should appear would vary depending on the gender of the player character, and this flag allows the application to choose an appropriate string.

[↑ Back to Top](#bioware-aurora-engine)

## 4. LanguageID and Gender combination

Internally, LocStrings store LanguageID and Gender as single combined ID that is equal to double the LanguageID, plus 0 for male strings and 1 for female strings. This is the same format in which LocStrings are saved out (refer to the **Generic File Format document, section 4.6**).

[↑ Back to Top](#bioware-aurora-engine)

## 5. Procedure to Fetch LocString Text

When fetching the text for a locstring, an application should check two things: the text itself, and whether the text was valid.

Note that it is possible for a string to be deliberately blank, which is why it is important to also return an error code to specify if a string really was found or not. It is up to the calling application to decide how to handle a no-string error. It may present an error message to the user, or it may silently use a blank string.

1. Get the user's Language ID from the talk table and determine the gender to display (eg., gender of the player character who is speaking in a conversation).

2. Try to find an embedded string in the LocString that matches the user's language ID and the current gender. Use that string if found, and indicate success.

3. If there is no embedded string that matches the above criteria, get the StrRef of the LocString and try to fetch the text of that StrRef from the talk table, using the current gender. If the talk table contains the desired text, return that text and indicate success.

4. If there is no text in the talk table for the specified StrRef, then check if the calling application wishes to search for an alternative in another language (searching is on by default; off only in special cases).

5. If searching is off, indicate failure and return a blank string.

6. If searching is on, then scan the LocString for embedded strings in languages other than the user's own LanguageID. The order to scan is: English, French, German, Italian, Spanish. At this time, no other languages are used as fall-backs. Return the first string found and a indicate success, or indicate failure and return a blank string if none was found.

[↑ Back to Top](#bioware-aurora-engine)

---
BioWare Corp. http://www.bioware.com
