using static Radoub.Formats.Gff.GffFieldBuilder;

namespace Radoub.Formats.Gff;

/// <summary>
/// Helper class for reading and writing VarTable (local variables) in GFF files.
/// VarTable is a GFF List containing Variable structs used by many Aurora Engine files
/// (UTC, UTI, UTM, ARE, IFO, etc.) to store script-accessible local variables.
/// Reference: BioWare Aurora CommonGFFStructs - VarTable List, Variable Struct
/// </summary>
public static class VarTableHelper
{
    /// <summary>
    /// The GFF field name for the VarTable list.
    /// </summary>
    public const string VarTableFieldName = "VarTable";

    /// <summary>
    /// StructID for Variable structs in a VarTable.
    /// </summary>
    public const uint VariableStructId = 0;

    /// <summary>
    /// StructID for Location structs within location-type variables.
    /// </summary>
    public const uint LocationStructId = 1;

    #region Reading

    /// <summary>
    /// Read all variables from a GFF struct's VarTable field.
    /// Returns an empty list if no VarTable field exists.
    /// </summary>
    /// <param name="root">The GFF struct containing the VarTable field.</param>
    /// <returns>List of variables read from the VarTable.</returns>
    public static List<Variable> ReadVarTable(GffStruct root)
    {
        var variables = new List<Variable>();

        var varTableField = root.GetField(VarTableFieldName);
        if (varTableField == null || !varTableField.IsList || varTableField.Value is not GffList varList)
            return variables;

        foreach (var varStruct in varList.Elements)
        {
            var variable = ReadVariable(varStruct);
            if (variable != null)
                variables.Add(variable);
        }

        return variables;
    }

    /// <summary>
    /// Read a single variable from a Variable struct.
    /// </summary>
    private static Variable? ReadVariable(GffStruct varStruct)
    {
        var name = varStruct.GetFieldValue<string>("Name", string.Empty);
        if (string.IsNullOrEmpty(name))
            return null;

        var typeValue = varStruct.GetFieldValue<uint>("Type", 0);
        if (typeValue < 1 || typeValue > 5)
            return null;

        var type = (VariableType)typeValue;
        var variable = new Variable
        {
            Name = name,
            Type = type
        };

        // Read value based on type
        variable.Value = type switch
        {
            VariableType.Int => varStruct.GetFieldValue<int>("Value", 0),
            VariableType.Float => varStruct.GetFieldValue<float>("Value", 0.0f),
            VariableType.String => varStruct.GetFieldValue<string>("Value", string.Empty),
            VariableType.Object => varStruct.GetFieldValue<uint>("Value", 0x7F000000),
            VariableType.Location => ReadLocationValue(varStruct),
            _ => null
        };

        return variable;
    }

    /// <summary>
    /// Read a location value from a Variable struct.
    /// </summary>
    private static VariableLocation? ReadLocationValue(GffStruct varStruct)
    {
        var valueField = varStruct.GetField("Value");
        if (valueField == null || !valueField.IsStruct || valueField.Value is not GffStruct locStruct)
            return null;

        return new VariableLocation
        {
            Area = locStruct.GetFieldValue<uint>("Area", 0x7F000000),
            PositionX = locStruct.GetFieldValue<float>("PositionX", 0.0f),
            PositionY = locStruct.GetFieldValue<float>("PositionY", 0.0f),
            PositionZ = locStruct.GetFieldValue<float>("PositionZ", 0.0f),
            OrientationX = locStruct.GetFieldValue<float>("OrientationX", 0.0f),
            OrientationY = locStruct.GetFieldValue<float>("OrientationY", 0.0f),
            OrientationZ = locStruct.GetFieldValue<float>("OrientationZ", 0.0f)
        };
    }

    /// <summary>
    /// Get a variable by name from a VarTable.
    /// </summary>
    /// <param name="root">The GFF struct containing the VarTable field.</param>
    /// <param name="name">The variable name (case-sensitive).</param>
    /// <returns>The variable if found, null otherwise.</returns>
    public static Variable? GetVariable(GffStruct root, string name)
    {
        var variables = ReadVarTable(root);
        return variables.FirstOrDefault(v => v.Name == name);
    }

    /// <summary>
    /// Get an integer variable value.
    /// </summary>
    public static int GetInt(GffStruct root, string name, int defaultValue = 0)
    {
        var variable = GetVariable(root, name);
        return variable?.Type == VariableType.Int ? variable.GetInt() : defaultValue;
    }

    /// <summary>
    /// Get a float variable value.
    /// </summary>
    public static float GetFloat(GffStruct root, string name, float defaultValue = 0.0f)
    {
        var variable = GetVariable(root, name);
        return variable?.Type == VariableType.Float ? variable.GetFloat() : defaultValue;
    }

    /// <summary>
    /// Get a string variable value.
    /// </summary>
    public static string GetString(GffStruct root, string name, string defaultValue = "")
    {
        var variable = GetVariable(root, name);
        return variable?.Type == VariableType.String ? variable.GetString() : defaultValue;
    }

    /// <summary>
    /// Get an object variable value.
    /// </summary>
    public static uint GetObjectId(GffStruct root, string name, uint defaultValue = 0x7F000000)
    {
        var variable = GetVariable(root, name);
        return variable?.Type == VariableType.Object ? variable.GetObjectId() : defaultValue;
    }

    /// <summary>
    /// Get a location variable value.
    /// </summary>
    public static VariableLocation? GetLocation(GffStruct root, string name)
    {
        var variable = GetVariable(root, name);
        return variable?.Type == VariableType.Location ? variable.GetLocation() : null;
    }

    #endregion

    #region Writing

    /// <summary>
    /// Write a VarTable field to a GFF struct.
    /// Replaces any existing VarTable field.
    /// </summary>
    /// <param name="root">The GFF struct to write to.</param>
    /// <param name="variables">The variables to write.</param>
    public static void WriteVarTable(GffStruct root, List<Variable> variables)
    {
        // Remove existing VarTable field if present
        var existingField = root.GetField(VarTableFieldName);
        if (existingField != null)
            root.Fields.Remove(existingField);

        // Don't write empty VarTable
        if (variables.Count == 0)
            return;

        var list = new GffList();
        foreach (var variable in variables)
        {
            var varStruct = BuildVariableStruct(variable);
            list.Elements.Add(varStruct);
        }

        AddListField(root, VarTableFieldName, list);
    }

    /// <summary>
    /// Build a Variable GFF struct from a Variable object.
    /// </summary>
    private static GffStruct BuildVariableStruct(Variable variable)
    {
        var varStruct = new GffStruct { Type = VariableStructId };

        AddCExoStringField(varStruct, "Name", variable.Name);
        AddDwordField(varStruct, "Type", (uint)variable.Type);

        // Add value based on type
        switch (variable.Type)
        {
            case VariableType.Int:
                AddIntField(varStruct, "Value", variable.GetInt());
                break;
            case VariableType.Float:
                AddFloatField(varStruct, "Value", variable.GetFloat());
                break;
            case VariableType.String:
                AddCExoStringField(varStruct, "Value", variable.GetString());
                break;
            case VariableType.Object:
                AddDwordField(varStruct, "Value", variable.GetObjectId());
                break;
            case VariableType.Location:
                AddLocationStruct(varStruct, variable.GetLocation());
                break;
        }

        return varStruct;
    }

    /// <summary>
    /// Add a location struct as the Value field.
    /// </summary>
    private static void AddLocationStruct(GffStruct parent, VariableLocation? location)
    {
        var locStruct = new GffStruct { Type = LocationStructId };

        if (location != null)
        {
            AddDwordField(locStruct, "Area", location.Area);
            AddFloatField(locStruct, "PositionX", location.PositionX);
            AddFloatField(locStruct, "PositionY", location.PositionY);
            AddFloatField(locStruct, "PositionZ", location.PositionZ);
            AddFloatField(locStruct, "OrientationX", location.OrientationX);
            AddFloatField(locStruct, "OrientationY", location.OrientationY);
            AddFloatField(locStruct, "OrientationZ", location.OrientationZ);
        }

        AddStructField(parent, "Value", locStruct);
    }

    /// <summary>
    /// Set or update a variable in a VarTable.
    /// If the variable exists, updates it. Otherwise, adds it.
    /// </summary>
    /// <param name="root">The GFF struct containing the VarTable.</param>
    /// <param name="variable">The variable to set.</param>
    public static void SetVariable(GffStruct root, Variable variable)
    {
        var variables = ReadVarTable(root);
        var existing = variables.FindIndex(v => v.Name == variable.Name);

        if (existing >= 0)
            variables[existing] = variable;
        else
            variables.Add(variable);

        WriteVarTable(root, variables);
    }

    /// <summary>
    /// Set an integer variable.
    /// </summary>
    public static void SetInt(GffStruct root, string name, int value)
    {
        SetVariable(root, Variable.CreateInt(name, value));
    }

    /// <summary>
    /// Set a float variable.
    /// </summary>
    public static void SetFloat(GffStruct root, string name, float value)
    {
        SetVariable(root, Variable.CreateFloat(name, value));
    }

    /// <summary>
    /// Set a string variable.
    /// </summary>
    public static void SetString(GffStruct root, string name, string value)
    {
        SetVariable(root, Variable.CreateString(name, value));
    }

    /// <summary>
    /// Set an object variable.
    /// </summary>
    public static void SetObjectId(GffStruct root, string name, uint objectId)
    {
        SetVariable(root, Variable.CreateObject(name, objectId));
    }

    /// <summary>
    /// Set a location variable.
    /// </summary>
    public static void SetLocation(GffStruct root, string name, VariableLocation location)
    {
        SetVariable(root, Variable.CreateLocation(name, location));
    }

    /// <summary>
    /// Delete a variable from a VarTable by name.
    /// </summary>
    /// <param name="root">The GFF struct containing the VarTable.</param>
    /// <param name="name">The variable name to delete.</param>
    /// <returns>True if the variable was found and deleted.</returns>
    public static bool DeleteVariable(GffStruct root, string name)
    {
        var variables = ReadVarTable(root);
        var removed = variables.RemoveAll(v => v.Name == name);

        if (removed > 0)
        {
            WriteVarTable(root, variables);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Check if a variable exists in a VarTable.
    /// </summary>
    public static bool HasVariable(GffStruct root, string name)
    {
        return GetVariable(root, name) != null;
    }

    /// <summary>
    /// Get all variable names from a VarTable.
    /// </summary>
    public static List<string> GetVariableNames(GffStruct root)
    {
        return ReadVarTable(root).Select(v => v.Name).ToList();
    }

    /// <summary>
    /// Clear all variables from a VarTable.
    /// </summary>
    public static void ClearVariables(GffStruct root)
    {
        var existingField = root.GetField(VarTableFieldName);
        if (existingField != null)
            root.Fields.Remove(existingField);
    }

    #endregion
}
