using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Radoub.Formats.Gff;

namespace MerchantEditor.ViewModels;

/// <summary>
/// ViewModel for a local variable in the store.
/// </summary>
public class VariableViewModel : INotifyPropertyChanged
{
    private string _name = string.Empty;
    private VariableType _type = VariableType.Int;
    private int _intValue;
    private float _floatValue;
    private string _stringValue = string.Empty;
    private uint _objectValue;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Name
    {
        get => _name;
        set { if (_name != value) { _name = value; OnPropertyChanged(); } }
    }

    public VariableType Type
    {
        get => _type;
        set
        {
            if (_type != value)
            {
                _type = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TypeDisplay));
                OnPropertyChanged(nameof(ValueDisplay));
                OnPropertyChanged(nameof(IsIntType));
                OnPropertyChanged(nameof(IsFloatType));
                OnPropertyChanged(nameof(IsStringType));
                OnPropertyChanged(nameof(IsObjectType));
            }
        }
    }

    public int IntValue
    {
        get => _intValue;
        set { if (_intValue != value) { _intValue = value; OnPropertyChanged(); OnPropertyChanged(nameof(ValueDisplay)); } }
    }

    public float FloatValue
    {
        get => _floatValue;
        set { if (Math.Abs(_floatValue - value) > 0.0001f) { _floatValue = value; OnPropertyChanged(); OnPropertyChanged(nameof(ValueDisplay)); } }
    }

    public string StringValue
    {
        get => _stringValue;
        set { if (_stringValue != value) { _stringValue = value; OnPropertyChanged(); OnPropertyChanged(nameof(ValueDisplay)); } }
    }

    public uint ObjectValue
    {
        get => _objectValue;
        set { if (_objectValue != value) { _objectValue = value; OnPropertyChanged(); OnPropertyChanged(nameof(ValueDisplay)); OnPropertyChanged(nameof(ObjectValueHex)); } }
    }

    /// <summary>
    /// Object value as hex string for TextBox binding with validation.
    /// </summary>
    public string ObjectValueHex
    {
        get => $"0x{ObjectValue:X8}";
        set
        {
            if (TryParseHex(value, out var parsed))
            {
                ObjectValue = parsed;
            }
        }
    }

    private static bool TryParseHex(string input, out uint result)
    {
        result = 0;
        if (string.IsNullOrWhiteSpace(input))
            return false;

        // Remove 0x or 0X prefix if present
        var hex = input.Trim();
        if (hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            hex = hex.Substring(2);

        return uint.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out result);
    }

    /// <summary>
    /// Display string for the variable type.
    /// </summary>
    public string TypeDisplay => Type switch
    {
        VariableType.Int => "Int",
        VariableType.Float => "Float",
        VariableType.String => "String",
        VariableType.Object => "Object",
        VariableType.Location => "Location",
        _ => "Unknown"
    };

    /// <summary>
    /// Display string for the variable value.
    /// </summary>
    public string ValueDisplay => Type switch
    {
        VariableType.Int => IntValue.ToString(),
        VariableType.Float => FloatValue.ToString("F3"),
        VariableType.String => StringValue,
        VariableType.Object => $"0x{ObjectValue:X8}",
        VariableType.Location => "(Location)",
        _ => string.Empty
    };

    // Type visibility helpers for UI
    public bool IsIntType => Type == VariableType.Int;
    public bool IsFloatType => Type == VariableType.Float;
    public bool IsStringType => Type == VariableType.String;
    public bool IsObjectType => Type == VariableType.Object;

    /// <summary>
    /// ComboBox index for type selection.
    /// Maps: 0=Int, 1=Float, 2=String, 3=Object
    /// </summary>
    public int TypeIndex
    {
        get => Type switch
        {
            VariableType.Int => 0,
            VariableType.Float => 1,
            VariableType.String => 2,
            VariableType.Object => 3,
            _ => 0
        };
        set
        {
            var newType = value switch
            {
                0 => VariableType.Int,
                1 => VariableType.Float,
                2 => VariableType.String,
                3 => VariableType.Object,
                _ => VariableType.Int
            };
            if (Type != newType)
            {
                // Reset the value when type changes
                Type = newType;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Create a ViewModel from a Variable model.
    /// </summary>
    public static VariableViewModel FromVariable(Variable variable)
    {
        var vm = new VariableViewModel
        {
            Name = variable.Name,
            Type = variable.Type
        };

        switch (variable.Type)
        {
            case VariableType.Int:
                vm.IntValue = variable.GetInt();
                break;
            case VariableType.Float:
                vm.FloatValue = variable.GetFloat();
                break;
            case VariableType.String:
                vm.StringValue = variable.GetString();
                break;
            case VariableType.Object:
                vm.ObjectValue = variable.GetObjectId();
                break;
            // Location type not editable in simple UI
        }

        return vm;
    }

    /// <summary>
    /// Convert this ViewModel back to a Variable model.
    /// </summary>
    public Variable ToVariable()
    {
        return Type switch
        {
            VariableType.Int => Variable.CreateInt(Name, IntValue),
            VariableType.Float => Variable.CreateFloat(Name, FloatValue),
            VariableType.String => Variable.CreateString(Name, StringValue),
            VariableType.Object => Variable.CreateObject(Name, ObjectValue),
            _ => Variable.CreateInt(Name, 0) // Fallback
        };
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
