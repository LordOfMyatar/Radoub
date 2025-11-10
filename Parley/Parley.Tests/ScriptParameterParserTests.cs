using Xunit;
using DialogEditor.Parsers;
using DialogEditor.Models;

namespace Parley.Tests
{
    public class ScriptParameterParserTests
    {
        private readonly ScriptParameterParser _parser = new ScriptParameterParser();

        [Fact]
        public void Parse_WithKeyListAndValueList_ExtractsBoth()
        {
            // Arrange
            var nssContent = @"
/*
----KeyList----
CLASS_TYPE
CLASS_LEVEL
CLASS_TYPE_2

----ValueList----
CLASS_TYPE_BARBARIAN
CLASS_TYPE_BARD
CLASS_TYPE_CLERIC
*/
void main() {}
";

            // Act
            var result = _parser.Parse(nssContent);

            // Assert
            Assert.Equal(3, result.Keys.Count);
            Assert.Contains("CLASS_TYPE", result.Keys);
            Assert.Contains("CLASS_LEVEL", result.Keys);
            Assert.Contains("CLASS_TYPE_2", result.Keys);

            Assert.Equal(3, result.Values.Count);
            Assert.Contains("CLASS_TYPE_BARBARIAN", result.Values);
            Assert.Contains("CLASS_TYPE_BARD", result.Values);
            Assert.Contains("CLASS_TYPE_CLERIC", result.Values);
        }

        [Fact]
        public void Parse_WithCommaSeparated_ParsesCorrectly()
        {
            // Arrange
            var nssContent = @"
/*
----KeyList----
PARAM1, PARAM2, PARAM3
----ValueList----
VALUE1, VALUE2, VALUE3
*/
";

            // Act
            var result = _parser.Parse(nssContent);

            // Assert
            Assert.Equal(3, result.Keys.Count);
            Assert.Contains("PARAM1", result.Keys);
            Assert.Contains("PARAM2", result.Keys);
            Assert.Contains("PARAM3", result.Keys);

            Assert.Equal(3, result.Values.Count);
            Assert.Contains("VALUE1", result.Values);
            Assert.Contains("VALUE2", result.Values);
            Assert.Contains("VALUE3", result.Values);
        }

        [Fact]
        public void Parse_WithMixedSeparators_ParsesCorrectly()
        {
            // Arrange
            var nssContent = @"
/*
----KeyList----
KEY1, KEY2
KEY3
KEY4, KEY5

----ValueList----
VAL1
VAL2, VAL3
*/
";

            // Act
            var result = _parser.Parse(nssContent);

            // Assert
            Assert.Equal(5, result.Keys.Count);
            Assert.Equal(3, result.Values.Count);
        }

        [Fact]
        public void Parse_WithNoDeclarations_ReturnsEmpty()
        {
            // Arrange
            var nssContent = @"
// Regular script with no parameter declarations
void main() {
    // Do something
}
";

            // Act
            var result = _parser.Parse(nssContent);

            // Assert
            Assert.Empty(result.Keys);
            Assert.Empty(result.Values);
            Assert.False(result.HasDeclarations);
        }

        [Fact]
        public void Parse_WithOnlyKeyList_ReturnsKeysOnly()
        {
            // Arrange
            var nssContent = @"
/*
----KeyList----
PARAM1
PARAM2
*/
";

            // Act
            var result = _parser.Parse(nssContent);

            // Assert
            Assert.Equal(2, result.Keys.Count);
            Assert.Empty(result.Values);
            Assert.True(result.HasDeclarations);
        }

        [Fact]
        public void Parse_WithOnlyValueList_ReturnsValuesOnly()
        {
            // Arrange
            var nssContent = @"
/*
----ValueList----
VALUE1
VALUE2
*/
";

            // Act
            var result = _parser.Parse(nssContent);

            // Assert
            Assert.Empty(result.Keys);
            Assert.Equal(2, result.Values.Count);
            Assert.True(result.HasDeclarations);
        }

        [Fact]
        public void Parse_WithEmptyContent_ReturnsEmpty()
        {
            // Act
            var result = _parser.Parse("");

            // Assert
            Assert.Empty(result.Keys);
            Assert.Empty(result.Values);
            Assert.False(result.HasDeclarations);
        }

        [Fact]
        public void Parse_WithNullContent_ReturnsEmpty()
        {
            // Act
            var result = _parser.Parse(null);

            // Assert
            Assert.Empty(result.Keys);
            Assert.Empty(result.Values);
            Assert.False(result.HasDeclarations);
        }

        [Fact]
        public void Parse_TrimsWhitespace_Correctly()
        {
            // Arrange
            var nssContent = @"
/*
----KeyList----
   PARAM1
  PARAM2
----ValueList----
  VALUE1
   VALUE2
*/
";

            // Act
            var result = _parser.Parse(nssContent);

            // Assert
            Assert.Equal(2, result.Keys.Count);
            Assert.Contains("PARAM1", result.Keys);
            Assert.Contains("PARAM2", result.Keys);
            Assert.DoesNotContain("   PARAM1   ", result.Keys);

            Assert.Equal(2, result.Values.Count);
            Assert.Contains("VALUE1", result.Values);
            Assert.Contains("VALUE2", result.Values);
        }

        [Fact]
        public void Parse_CaseInsensitiveSectionHeaders_ParsesCorrectly()
        {
            // Arrange
            var nssContent = @"
/*
----keylist----
PARAM1
----valuelist----
VALUE1
*/
";

            // Act
            var result = _parser.Parse(nssContent);

            // Assert
            Assert.Single(result.Keys);
            Assert.Single(result.Values);
        }

        [Fact]
        public void Parse_WithDuplicates_RemovesDuplicates()
        {
            // Arrange
            var nssContent = @"
/*
----KeyList----
PARAM1
PARAM1
PARAM2
PARAM1
*/
";

            // Act
            var result = _parser.Parse(nssContent);

            // Assert
            Assert.Equal(2, result.Keys.Count);
            Assert.Contains("PARAM1", result.Keys);
            Assert.Contains("PARAM2", result.Keys);
        }

        [Fact]
        public void Parse_WithKeyedValueLists_ExtractsCorrectly()
        {
            // Arrange
            var nssContent = @"
/*
----KeyList----
BASE_ITEM
INVENTORY_SLOT

----ValueList-BASE_ITEM----
BASE_ITEM_SHORTSWORD
BASE_ITEM_LONGSWORD
BASE_ITEM_BATTLEAXE

----ValueList-INVENTORY_SLOT----
INVENTORY_SLOT_RIGHTHAND
INVENTORY_SLOT_LEFTHAND
*/
";

            // Act
            var result = _parser.Parse(nssContent);

            // Assert
            Assert.Equal(2, result.Keys.Count);
            Assert.Contains("BASE_ITEM", result.Keys);
            Assert.Contains("INVENTORY_SLOT", result.Keys);

            Assert.Equal(2, result.ValuesByKey.Count);
            Assert.True(result.ValuesByKey.ContainsKey("BASE_ITEM"));
            Assert.True(result.ValuesByKey.ContainsKey("INVENTORY_SLOT"));

            Assert.Equal(3, result.ValuesByKey["BASE_ITEM"].Count);
            Assert.Contains("BASE_ITEM_SHORTSWORD", result.ValuesByKey["BASE_ITEM"]);
            Assert.Contains("BASE_ITEM_LONGSWORD", result.ValuesByKey["BASE_ITEM"]);
            Assert.Contains("BASE_ITEM_BATTLEAXE", result.ValuesByKey["BASE_ITEM"]);

            Assert.Equal(2, result.ValuesByKey["INVENTORY_SLOT"].Count);
            Assert.Contains("INVENTORY_SLOT_RIGHTHAND", result.ValuesByKey["INVENTORY_SLOT"]);
            Assert.Contains("INVENTORY_SLOT_LEFTHAND", result.ValuesByKey["INVENTORY_SLOT"]);
        }

        [Fact]
        public void Parse_GetValuesForKey_ReturnsCorrectValues()
        {
            // Arrange
            var nssContent = @"
/*
----KeyList----
PARAM1

----ValueList-PARAM1----
VALUE1
VALUE2
VALUE3
*/
";

            // Act
            var result = _parser.Parse(nssContent);
            var values = result.GetValuesForKey("PARAM1");
            var emptyValues = result.GetValuesForKey("NONEXISTENT");

            // Assert
            Assert.Equal(3, values.Count);
            Assert.Contains("VALUE1", values);
            Assert.Contains("VALUE2", values);
            Assert.Contains("VALUE3", values);
            Assert.Empty(emptyValues);
        }
    }
}
