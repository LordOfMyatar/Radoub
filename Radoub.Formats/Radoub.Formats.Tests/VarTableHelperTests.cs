using Radoub.Formats.Gff;
using Xunit;

namespace Radoub.Formats.Tests;

public class VarTableHelperTests
{
    #region Reading Tests

    [Fact]
    public void ReadVarTable_NoVarTable_ReturnsEmptyList()
    {
        var root = new GffStruct { Type = 0xFFFFFFFF };

        var variables = VarTableHelper.ReadVarTable(root);

        Assert.Empty(variables);
    }

    [Fact]
    public void ReadVarTable_EmptyVarTable_ReturnsEmptyList()
    {
        var root = CreateStructWithEmptyVarTable();

        var variables = VarTableHelper.ReadVarTable(root);

        Assert.Empty(variables);
    }

    [Fact]
    public void ReadVarTable_IntVariable_ParsesCorrectly()
    {
        var root = CreateStructWithIntVariable("TestInt", 42);

        var variables = VarTableHelper.ReadVarTable(root);

        Assert.Single(variables);
        Assert.Equal("TestInt", variables[0].Name);
        Assert.Equal(VariableType.Int, variables[0].Type);
        Assert.Equal(42, variables[0].GetInt());
    }

    [Fact]
    public void ReadVarTable_FloatVariable_ParsesCorrectly()
    {
        var root = CreateStructWithFloatVariable("TestFloat", 3.14f);

        var variables = VarTableHelper.ReadVarTable(root);

        Assert.Single(variables);
        Assert.Equal("TestFloat", variables[0].Name);
        Assert.Equal(VariableType.Float, variables[0].Type);
        Assert.Equal(3.14f, variables[0].GetFloat(), 0.001f);
    }

    [Fact]
    public void ReadVarTable_StringVariable_ParsesCorrectly()
    {
        var root = CreateStructWithStringVariable("TestString", "Hello World");

        var variables = VarTableHelper.ReadVarTable(root);

        Assert.Single(variables);
        Assert.Equal("TestString", variables[0].Name);
        Assert.Equal(VariableType.String, variables[0].Type);
        Assert.Equal("Hello World", variables[0].GetString());
    }

    [Fact]
    public void ReadVarTable_ObjectVariable_ParsesCorrectly()
    {
        var root = CreateStructWithObjectVariable("TestObject", 0x12345678);

        var variables = VarTableHelper.ReadVarTable(root);

        Assert.Single(variables);
        Assert.Equal("TestObject", variables[0].Name);
        Assert.Equal(VariableType.Object, variables[0].Type);
        Assert.Equal(0x12345678u, variables[0].GetObjectId());
    }

    [Fact]
    public void ReadVarTable_LocationVariable_ParsesCorrectly()
    {
        var root = CreateStructWithLocationVariable("TestLocation", 1.0f, 2.0f, 3.0f, 0.0f, 1.0f, 0.0f, 0x7F000001);

        var variables = VarTableHelper.ReadVarTable(root);

        Assert.Single(variables);
        Assert.Equal("TestLocation", variables[0].Name);
        Assert.Equal(VariableType.Location, variables[0].Type);
        var loc = variables[0].GetLocation();
        Assert.NotNull(loc);
        Assert.Equal(1.0f, loc.PositionX);
        Assert.Equal(2.0f, loc.PositionY);
        Assert.Equal(3.0f, loc.PositionZ);
        Assert.Equal(0.0f, loc.OrientationX);
        Assert.Equal(1.0f, loc.OrientationY);
        Assert.Equal(0.0f, loc.OrientationZ);
        Assert.Equal(0x7F000001u, loc.Area);
    }

    [Fact]
    public void ReadVarTable_MultipleVariables_ParsesAll()
    {
        var root = CreateStructWithMultipleVariables();

        var variables = VarTableHelper.ReadVarTable(root);

        Assert.Equal(3, variables.Count);
        Assert.Contains(variables, v => v.Name == "IntVar" && v.Type == VariableType.Int);
        Assert.Contains(variables, v => v.Name == "FloatVar" && v.Type == VariableType.Float);
        Assert.Contains(variables, v => v.Name == "StringVar" && v.Type == VariableType.String);
    }

    [Fact]
    public void GetVariable_ExistingVariable_ReturnsVariable()
    {
        var root = CreateStructWithIntVariable("MyVar", 100);

        var variable = VarTableHelper.GetVariable(root, "MyVar");

        Assert.NotNull(variable);
        Assert.Equal("MyVar", variable.Name);
        Assert.Equal(100, variable.GetInt());
    }

    [Fact]
    public void GetVariable_NonExistentVariable_ReturnsNull()
    {
        var root = CreateStructWithIntVariable("MyVar", 100);

        var variable = VarTableHelper.GetVariable(root, "OtherVar");

        Assert.Null(variable);
    }

    [Fact]
    public void GetInt_ExistingIntVariable_ReturnsValue()
    {
        var root = CreateStructWithIntVariable("MyInt", 42);

        var value = VarTableHelper.GetInt(root, "MyInt");

        Assert.Equal(42, value);
    }

    [Fact]
    public void GetInt_NonExistentVariable_ReturnsDefault()
    {
        var root = new GffStruct { Type = 0xFFFFFFFF };

        var value = VarTableHelper.GetInt(root, "MissingVar", -1);

        Assert.Equal(-1, value);
    }

    [Fact]
    public void GetFloat_ExistingFloatVariable_ReturnsValue()
    {
        var root = CreateStructWithFloatVariable("MyFloat", 2.5f);

        var value = VarTableHelper.GetFloat(root, "MyFloat");

        Assert.Equal(2.5f, value, 0.001f);
    }

    [Fact]
    public void GetString_ExistingStringVariable_ReturnsValue()
    {
        var root = CreateStructWithStringVariable("MyString", "Test");

        var value = VarTableHelper.GetString(root, "MyString");

        Assert.Equal("Test", value);
    }

    #endregion

    #region Writing Tests

    [Fact]
    public void WriteVarTable_EmptyList_DoesNotAddField()
    {
        var root = new GffStruct { Type = 0xFFFFFFFF };

        VarTableHelper.WriteVarTable(root, new List<Variable>());

        Assert.Null(root.GetField(VarTableHelper.VarTableFieldName));
    }

    [Fact]
    public void WriteVarTable_SingleIntVariable_WritesCorrectly()
    {
        var root = new GffStruct { Type = 0xFFFFFFFF };
        var variables = new List<Variable> { Variable.CreateInt("TestInt", 123) };

        VarTableHelper.WriteVarTable(root, variables);
        var readBack = VarTableHelper.ReadVarTable(root);

        Assert.Single(readBack);
        Assert.Equal("TestInt", readBack[0].Name);
        Assert.Equal(VariableType.Int, readBack[0].Type);
        Assert.Equal(123, readBack[0].GetInt());
    }

    [Fact]
    public void WriteVarTable_MultipleVariables_WritesAll()
    {
        var root = new GffStruct { Type = 0xFFFFFFFF };
        var variables = new List<Variable>
        {
            Variable.CreateInt("IntVar", 42),
            Variable.CreateFloat("FloatVar", 1.5f),
            Variable.CreateString("StringVar", "Hello")
        };

        VarTableHelper.WriteVarTable(root, variables);
        var readBack = VarTableHelper.ReadVarTable(root);

        Assert.Equal(3, readBack.Count);
    }

    [Fact]
    public void WriteVarTable_ReplacesExisting()
    {
        var root = CreateStructWithIntVariable("OldVar", 1);

        VarTableHelper.WriteVarTable(root, new List<Variable> { Variable.CreateInt("NewVar", 2) });
        var readBack = VarTableHelper.ReadVarTable(root);

        Assert.Single(readBack);
        Assert.Equal("NewVar", readBack[0].Name);
    }

    [Fact]
    public void SetVariable_NewVariable_AddsToVarTable()
    {
        var root = new GffStruct { Type = 0xFFFFFFFF };

        VarTableHelper.SetVariable(root, Variable.CreateInt("NewVar", 50));
        var readBack = VarTableHelper.ReadVarTable(root);

        Assert.Single(readBack);
        Assert.Equal("NewVar", readBack[0].Name);
        Assert.Equal(50, readBack[0].GetInt());
    }

    [Fact]
    public void SetVariable_ExistingVariable_UpdatesValue()
    {
        var root = CreateStructWithIntVariable("MyVar", 10);

        VarTableHelper.SetVariable(root, Variable.CreateInt("MyVar", 20));
        var readBack = VarTableHelper.ReadVarTable(root);

        Assert.Single(readBack);
        Assert.Equal(20, readBack[0].GetInt());
    }

    [Fact]
    public void SetInt_NewVariable_AddsIntVariable()
    {
        var root = new GffStruct { Type = 0xFFFFFFFF };

        VarTableHelper.SetInt(root, "Counter", 100);

        Assert.Equal(100, VarTableHelper.GetInt(root, "Counter"));
    }

    [Fact]
    public void SetFloat_NewVariable_AddsFloatVariable()
    {
        var root = new GffStruct { Type = 0xFFFFFFFF };

        VarTableHelper.SetFloat(root, "Speed", 5.5f);

        Assert.Equal(5.5f, VarTableHelper.GetFloat(root, "Speed"), 0.001f);
    }

    [Fact]
    public void SetString_NewVariable_AddsStringVariable()
    {
        var root = new GffStruct { Type = 0xFFFFFFFF };

        VarTableHelper.SetString(root, "Name", "TestName");

        Assert.Equal("TestName", VarTableHelper.GetString(root, "Name"));
    }

    [Fact]
    public void SetObjectId_NewVariable_AddsObjectVariable()
    {
        var root = new GffStruct { Type = 0xFFFFFFFF };

        VarTableHelper.SetObjectId(root, "Target", 0xABCDEF00);

        Assert.Equal(0xABCDEF00u, VarTableHelper.GetObjectId(root, "Target"));
    }

    [Fact]
    public void SetLocation_NewVariable_AddsLocationVariable()
    {
        var root = new GffStruct { Type = 0xFFFFFFFF };
        var location = new VariableLocation
        {
            PositionX = 10.0f,
            PositionY = 20.0f,
            PositionZ = 0.0f,
            OrientationX = 0.0f,
            OrientationY = 1.0f,
            OrientationZ = 0.0f,
            Area = 0x7F000001
        };

        VarTableHelper.SetLocation(root, "SpawnPoint", location);
        var readBack = VarTableHelper.GetLocation(root, "SpawnPoint");

        Assert.NotNull(readBack);
        Assert.Equal(10.0f, readBack.PositionX);
        Assert.Equal(20.0f, readBack.PositionY);
    }

    #endregion

    #region Delete Tests

    [Fact]
    public void DeleteVariable_ExistingVariable_ReturnsTrue()
    {
        var root = CreateStructWithIntVariable("ToDelete", 1);

        var result = VarTableHelper.DeleteVariable(root, "ToDelete");

        Assert.True(result);
        Assert.Empty(VarTableHelper.ReadVarTable(root));
    }

    [Fact]
    public void DeleteVariable_NonExistentVariable_ReturnsFalse()
    {
        var root = CreateStructWithIntVariable("KeepMe", 1);

        var result = VarTableHelper.DeleteVariable(root, "NotHere");

        Assert.False(result);
        Assert.Single(VarTableHelper.ReadVarTable(root));
    }

    [Fact]
    public void DeleteVariable_OneOfMany_KeepsOthers()
    {
        var root = CreateStructWithMultipleVariables();

        VarTableHelper.DeleteVariable(root, "FloatVar");
        var remaining = VarTableHelper.ReadVarTable(root);

        Assert.Equal(2, remaining.Count);
        Assert.DoesNotContain(remaining, v => v.Name == "FloatVar");
        Assert.Contains(remaining, v => v.Name == "IntVar");
        Assert.Contains(remaining, v => v.Name == "StringVar");
    }

    [Fact]
    public void ClearVariables_RemovesAllVariables()
    {
        var root = CreateStructWithMultipleVariables();

        VarTableHelper.ClearVariables(root);

        Assert.Empty(VarTableHelper.ReadVarTable(root));
        Assert.Null(root.GetField(VarTableHelper.VarTableFieldName));
    }

    #endregion

    #region Utility Tests

    [Fact]
    public void HasVariable_ExistingVariable_ReturnsTrue()
    {
        var root = CreateStructWithIntVariable("Exists", 1);

        Assert.True(VarTableHelper.HasVariable(root, "Exists"));
    }

    [Fact]
    public void HasVariable_NonExistentVariable_ReturnsFalse()
    {
        var root = new GffStruct { Type = 0xFFFFFFFF };

        Assert.False(VarTableHelper.HasVariable(root, "Missing"));
    }

    [Fact]
    public void GetVariableNames_ReturnsAllNames()
    {
        var root = CreateStructWithMultipleVariables();

        var names = VarTableHelper.GetVariableNames(root);

        Assert.Equal(3, names.Count);
        Assert.Contains("IntVar", names);
        Assert.Contains("FloatVar", names);
        Assert.Contains("StringVar", names);
    }

    [Fact]
    public void GetVariableNames_EmptyVarTable_ReturnsEmptyList()
    {
        var root = new GffStruct { Type = 0xFFFFFFFF };

        var names = VarTableHelper.GetVariableNames(root);

        Assert.Empty(names);
    }

    #endregion

    #region Round-Trip Tests

    [Fact]
    public void RoundTrip_IntVariable_PreservesValue()
    {
        var root = new GffStruct { Type = 0xFFFFFFFF };
        VarTableHelper.SetInt(root, "Counter", int.MaxValue);

        // Simulate round-trip by building GFF and reading back
        var gff = new GffFile
        {
            FileType = "UTC ",
            FileVersion = "V3.2",
            RootStruct = root
        };
        var buffer = GffWriter.Write(gff);
        var gff2 = GffReader.Read(buffer);

        Assert.Equal(int.MaxValue, VarTableHelper.GetInt(gff2.RootStruct, "Counter"));
    }

    [Fact]
    public void RoundTrip_FloatVariable_PreservesValue()
    {
        var root = new GffStruct { Type = 0xFFFFFFFF };
        VarTableHelper.SetFloat(root, "Speed", -123.456f);

        var gff = new GffFile
        {
            FileType = "UTC ",
            FileVersion = "V3.2",
            RootStruct = root
        };
        var buffer = GffWriter.Write(gff);
        var gff2 = GffReader.Read(buffer);

        Assert.Equal(-123.456f, VarTableHelper.GetFloat(gff2.RootStruct, "Speed"), 0.001f);
    }

    [Fact]
    public void RoundTrip_StringVariable_PreservesValue()
    {
        var root = new GffStruct { Type = 0xFFFFFFFF };
        VarTableHelper.SetString(root, "Message", "Hello, NWN! Special chars: äöü");

        var gff = new GffFile
        {
            FileType = "UTC ",
            FileVersion = "V3.2",
            RootStruct = root
        };
        var buffer = GffWriter.Write(gff);
        var gff2 = GffReader.Read(buffer);

        Assert.Equal("Hello, NWN! Special chars: äöü", VarTableHelper.GetString(gff2.RootStruct, "Message"));
    }

    [Fact]
    public void RoundTrip_LocationVariable_PreservesAllFields()
    {
        var root = new GffStruct { Type = 0xFFFFFFFF };
        var location = new VariableLocation
        {
            PositionX = 100.5f,
            PositionY = 200.25f,
            PositionZ = -10.0f,
            OrientationX = 0.707f,
            OrientationY = 0.707f,
            OrientationZ = 0.0f,
            Area = 0x7F000002
        };
        VarTableHelper.SetLocation(root, "Waypoint", location);

        var gff = new GffFile
        {
            FileType = "UTC ",
            FileVersion = "V3.2",
            RootStruct = root
        };
        var buffer = GffWriter.Write(gff);
        var gff2 = GffReader.Read(buffer);

        var readBack = VarTableHelper.GetLocation(gff2.RootStruct, "Waypoint");
        Assert.NotNull(readBack);
        Assert.Equal(100.5f, readBack.PositionX, 0.001f);
        Assert.Equal(200.25f, readBack.PositionY, 0.001f);
        Assert.Equal(-10.0f, readBack.PositionZ, 0.001f);
        Assert.Equal(0.707f, readBack.OrientationX, 0.001f);
        Assert.Equal(0.707f, readBack.OrientationY, 0.001f);
        Assert.Equal(0.0f, readBack.OrientationZ, 0.001f);
        Assert.Equal(0x7F000002u, readBack.Area);
    }

    [Fact]
    public void RoundTrip_MultipleVariables_PreservesAll()
    {
        var root = new GffStruct { Type = 0xFFFFFFFF };
        VarTableHelper.SetInt(root, "Level", 15);
        VarTableHelper.SetFloat(root, "XPMultiplier", 1.5f);
        VarTableHelper.SetString(root, "Faction", "Neutral");
        VarTableHelper.SetObjectId(root, "Leader", 0x12340001);

        var gff = new GffFile
        {
            FileType = "UTC ",
            FileVersion = "V3.2",
            RootStruct = root
        };
        var buffer = GffWriter.Write(gff);
        var gff2 = GffReader.Read(buffer);

        Assert.Equal(15, VarTableHelper.GetInt(gff2.RootStruct, "Level"));
        Assert.Equal(1.5f, VarTableHelper.GetFloat(gff2.RootStruct, "XPMultiplier"), 0.001f);
        Assert.Equal("Neutral", VarTableHelper.GetString(gff2.RootStruct, "Faction"));
        Assert.Equal(0x12340001u, VarTableHelper.GetObjectId(gff2.RootStruct, "Leader"));
    }

    #endregion

    #region Variable Model Tests

    [Fact]
    public void Variable_CreateInt_SetsCorrectProperties()
    {
        var v = Variable.CreateInt("Test", 42);

        Assert.Equal("Test", v.Name);
        Assert.Equal(VariableType.Int, v.Type);
        Assert.Equal(42, v.GetInt());
    }

    [Fact]
    public void Variable_CreateFloat_SetsCorrectProperties()
    {
        var v = Variable.CreateFloat("Test", 3.14f);

        Assert.Equal("Test", v.Name);
        Assert.Equal(VariableType.Float, v.Type);
        Assert.Equal(3.14f, v.GetFloat(), 0.001f);
    }

    [Fact]
    public void Variable_CreateString_SetsCorrectProperties()
    {
        var v = Variable.CreateString("Test", "Value");

        Assert.Equal("Test", v.Name);
        Assert.Equal(VariableType.String, v.Type);
        Assert.Equal("Value", v.GetString());
    }

    [Fact]
    public void Variable_CreateObject_SetsCorrectProperties()
    {
        var v = Variable.CreateObject("Test", 0xDEADBEEF);

        Assert.Equal("Test", v.Name);
        Assert.Equal(VariableType.Object, v.Type);
        Assert.Equal(0xDEADBEEFu, v.GetObjectId());
    }

    [Fact]
    public void Variable_CreateLocation_SetsCorrectProperties()
    {
        var loc = new VariableLocation { PositionX = 1.0f };
        var v = Variable.CreateLocation("Test", loc);

        Assert.Equal("Test", v.Name);
        Assert.Equal(VariableType.Location, v.Type);
        Assert.NotNull(v.GetLocation());
        Assert.Equal(1.0f, v.GetLocation()!.PositionX);
    }

    [Fact]
    public void Variable_GetInt_WrongType_ReturnsDefault()
    {
        var v = Variable.CreateString("Test", "NotAnInt");

        Assert.Equal(0, v.GetInt());
    }

    [Fact]
    public void Variable_GetFloat_WrongType_ReturnsDefault()
    {
        var v = Variable.CreateInt("Test", 42);

        Assert.Equal(0.0f, v.GetFloat());
    }

    [Fact]
    public void Variable_GetString_WrongType_ReturnsEmpty()
    {
        var v = Variable.CreateInt("Test", 42);

        Assert.Equal(string.Empty, v.GetString());
    }

    [Fact]
    public void Variable_GetObjectId_WrongType_ReturnsInvalid()
    {
        var v = Variable.CreateInt("Test", 42);

        Assert.Equal(0x7F000000u, v.GetObjectId()); // OBJECT_INVALID
    }

    [Fact]
    public void Variable_GetLocation_WrongType_ReturnsNull()
    {
        var v = Variable.CreateInt("Test", 42);

        Assert.Null(v.GetLocation());
    }

    #endregion

    #region Test Helpers

    private static GffStruct CreateStructWithEmptyVarTable()
    {
        var root = new GffStruct { Type = 0xFFFFFFFF };
        var varTable = new GffList { Count = 0 };
        root.Fields.Add(new GffField
        {
            Type = GffField.List,
            Label = VarTableHelper.VarTableFieldName,
            Value = varTable
        });
        return root;
    }

    private static GffStruct CreateStructWithIntVariable(string name, int value)
    {
        var root = new GffStruct { Type = 0xFFFFFFFF };
        var varTable = new GffList();

        var varStruct = new GffStruct { Type = 0 };
        varStruct.Fields.Add(new GffField { Type = GffField.CExoString, Label = "Name", Value = name });
        varStruct.Fields.Add(new GffField { Type = GffField.DWORD, Label = "Type", Value = (uint)VariableType.Int });
        varStruct.Fields.Add(new GffField { Type = GffField.INT, Label = "Value", Value = value });
        varTable.Elements.Add(varStruct);
        varTable.Count = 1;

        root.Fields.Add(new GffField
        {
            Type = GffField.List,
            Label = VarTableHelper.VarTableFieldName,
            Value = varTable
        });
        return root;
    }

    private static GffStruct CreateStructWithFloatVariable(string name, float value)
    {
        var root = new GffStruct { Type = 0xFFFFFFFF };
        var varTable = new GffList();

        var varStruct = new GffStruct { Type = 0 };
        varStruct.Fields.Add(new GffField { Type = GffField.CExoString, Label = "Name", Value = name });
        varStruct.Fields.Add(new GffField { Type = GffField.DWORD, Label = "Type", Value = (uint)VariableType.Float });
        varStruct.Fields.Add(new GffField { Type = GffField.FLOAT, Label = "Value", Value = value });
        varTable.Elements.Add(varStruct);
        varTable.Count = 1;

        root.Fields.Add(new GffField
        {
            Type = GffField.List,
            Label = VarTableHelper.VarTableFieldName,
            Value = varTable
        });
        return root;
    }

    private static GffStruct CreateStructWithStringVariable(string name, string value)
    {
        var root = new GffStruct { Type = 0xFFFFFFFF };
        var varTable = new GffList();

        var varStruct = new GffStruct { Type = 0 };
        varStruct.Fields.Add(new GffField { Type = GffField.CExoString, Label = "Name", Value = name });
        varStruct.Fields.Add(new GffField { Type = GffField.DWORD, Label = "Type", Value = (uint)VariableType.String });
        varStruct.Fields.Add(new GffField { Type = GffField.CExoString, Label = "Value", Value = value });
        varTable.Elements.Add(varStruct);
        varTable.Count = 1;

        root.Fields.Add(new GffField
        {
            Type = GffField.List,
            Label = VarTableHelper.VarTableFieldName,
            Value = varTable
        });
        return root;
    }

    private static GffStruct CreateStructWithObjectVariable(string name, uint objectId)
    {
        var root = new GffStruct { Type = 0xFFFFFFFF };
        var varTable = new GffList();

        var varStruct = new GffStruct { Type = 0 };
        varStruct.Fields.Add(new GffField { Type = GffField.CExoString, Label = "Name", Value = name });
        varStruct.Fields.Add(new GffField { Type = GffField.DWORD, Label = "Type", Value = (uint)VariableType.Object });
        varStruct.Fields.Add(new GffField { Type = GffField.DWORD, Label = "Value", Value = objectId });
        varTable.Elements.Add(varStruct);
        varTable.Count = 1;

        root.Fields.Add(new GffField
        {
            Type = GffField.List,
            Label = VarTableHelper.VarTableFieldName,
            Value = varTable
        });
        return root;
    }

    private static GffStruct CreateStructWithLocationVariable(string name,
        float posX, float posY, float posZ,
        float orientX, float orientY, float orientZ,
        uint area)
    {
        var root = new GffStruct { Type = 0xFFFFFFFF };
        var varTable = new GffList();

        var locStruct = new GffStruct { Type = 1 };
        locStruct.Fields.Add(new GffField { Type = GffField.DWORD, Label = "Area", Value = area });
        locStruct.Fields.Add(new GffField { Type = GffField.FLOAT, Label = "PositionX", Value = posX });
        locStruct.Fields.Add(new GffField { Type = GffField.FLOAT, Label = "PositionY", Value = posY });
        locStruct.Fields.Add(new GffField { Type = GffField.FLOAT, Label = "PositionZ", Value = posZ });
        locStruct.Fields.Add(new GffField { Type = GffField.FLOAT, Label = "OrientationX", Value = orientX });
        locStruct.Fields.Add(new GffField { Type = GffField.FLOAT, Label = "OrientationY", Value = orientY });
        locStruct.Fields.Add(new GffField { Type = GffField.FLOAT, Label = "OrientationZ", Value = orientZ });

        var varStruct = new GffStruct { Type = 0 };
        varStruct.Fields.Add(new GffField { Type = GffField.CExoString, Label = "Name", Value = name });
        varStruct.Fields.Add(new GffField { Type = GffField.DWORD, Label = "Type", Value = (uint)VariableType.Location });
        varStruct.Fields.Add(new GffField { Type = GffField.Struct, Label = "Value", Value = locStruct });
        varTable.Elements.Add(varStruct);
        varTable.Count = 1;

        root.Fields.Add(new GffField
        {
            Type = GffField.List,
            Label = VarTableHelper.VarTableFieldName,
            Value = varTable
        });
        return root;
    }

    private static GffStruct CreateStructWithMultipleVariables()
    {
        var root = new GffStruct { Type = 0xFFFFFFFF };
        var varTable = new GffList();

        // Int variable
        var intVar = new GffStruct { Type = 0 };
        intVar.Fields.Add(new GffField { Type = GffField.CExoString, Label = "Name", Value = "IntVar" });
        intVar.Fields.Add(new GffField { Type = GffField.DWORD, Label = "Type", Value = (uint)VariableType.Int });
        intVar.Fields.Add(new GffField { Type = GffField.INT, Label = "Value", Value = 42 });
        varTable.Elements.Add(intVar);

        // Float variable
        var floatVar = new GffStruct { Type = 0 };
        floatVar.Fields.Add(new GffField { Type = GffField.CExoString, Label = "Name", Value = "FloatVar" });
        floatVar.Fields.Add(new GffField { Type = GffField.DWORD, Label = "Type", Value = (uint)VariableType.Float });
        floatVar.Fields.Add(new GffField { Type = GffField.FLOAT, Label = "Value", Value = 3.14f });
        varTable.Elements.Add(floatVar);

        // String variable
        var stringVar = new GffStruct { Type = 0 };
        stringVar.Fields.Add(new GffField { Type = GffField.CExoString, Label = "Name", Value = "StringVar" });
        stringVar.Fields.Add(new GffField { Type = GffField.DWORD, Label = "Type", Value = (uint)VariableType.String });
        stringVar.Fields.Add(new GffField { Type = GffField.CExoString, Label = "Value", Value = "Hello" });
        varTable.Elements.Add(stringVar);

        varTable.Count = 3;

        root.Fields.Add(new GffField
        {
            Type = GffField.List,
            Label = VarTableHelper.VarTableFieldName,
            Value = varTable
        });
        return root;
    }

    #endregion
}
