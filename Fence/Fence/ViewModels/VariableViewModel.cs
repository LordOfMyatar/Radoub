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
            }
        }
    }

    public int IntValue
    {
        get => _intValue;
        set { if (_intValue != value) { _intValue = value; OnPropertyChanged(); OnPropertyChanged(nameof(ValueDisplay)); } }
    }

    public decimal FloatValue
    {
        get => (decimal)_floatValue;
        set
        {
            var floatVal = (float)value;
            if (_floatValue != floatVal)
            {
                _floatValue = floatVal;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ValueDisplay));
            }
        }
    }

    public string StringValue
    {
        get => _stringValue;
        set { if (_stringValue != value) { _stringValue = value; OnPropertyChanged(); OnPropertyChanged(nameof(ValueDisplay)); } }
    }

    /// <summary>
    /// Display string for the variable type.
    /// </summary>
    public string TypeDisplay => Type switch
    {
        VariableType.Int => "Int",
        VariableType.Float => "Float",
        VariableType.String => "String",
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
        _ => string.Empty
    };

    // Type visibility helpers for UI
    public bool IsIntType => Type == VariableType.Int;
    public bool IsFloatType => Type == VariableType.Float;
    public bool IsStringType => Type == VariableType.String;

    /// <summary>
    /// ComboBox index for type selection.
    /// Maps: 0=Int, 1=Float, 2=String
    /// </summary>
    public int TypeIndex
    {
        get => Type switch
        {
            VariableType.Int => 0,
            VariableType.Float => 1,
            VariableType.String => 2,
            _ => 0
        };
        set
        {
            var newType = value switch
            {
                0 => VariableType.Int,
                1 => VariableType.Float,
                2 => VariableType.String,
                _ => VariableType.Int
            };
            if (Type != newType)
            {
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
                vm._floatValue = variable.GetFloat(); // Set internal field directly to avoid decimal conversion
                break;
            case VariableType.String:
                vm.StringValue = variable.GetString();
                break;
            // Object and Location types not supported in UI
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
            VariableType.Float => Variable.CreateFloat(Name, _floatValue),
            VariableType.String => Variable.CreateString(Name, StringValue),
            _ => Variable.CreateInt(Name, 0) // Fallback for unsupported types
        };
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
