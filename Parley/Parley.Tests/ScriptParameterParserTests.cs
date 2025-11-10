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
    }
}
