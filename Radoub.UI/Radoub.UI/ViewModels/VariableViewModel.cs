using System;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using Radoub.Formats.Gff;

namespace Radoub.UI.ViewModels;

/// <summary>
/// Shared ViewModel for a single Aurora local variable (GFF VarTable entry).
/// Superset of the four former tool-local VMs (Relique/QM/Fence/Trebuchet) — supports
/// all five engine variable types (Int/Float/String/Object/Location). Single source of
/// truth for variable editing across the toolset (#2293, Reliquary epic #2289).
/// </summary>
/// <remarks>
/// The five-type <see cref="TypeIndex"/> map (0=Int … 4=Location) intentionally replaces
/// the three-type 0/1/2 map the old tool VMs hardcoded. The underlying
/// <see cref="VariableType"/> enum is 1-based (Int=1); do not confuse the 0-based ComboBox
/// index with the enum value.
/// </remarks>
public partial class VariableViewModel : ObservableObject
{
    /// <summary>Maximum NWN variable-name length (Aurora tag limit).</summary>
    public const int MaxNameLength = 32;

    private static readonly Regex NamePattern = new("^[A-Za-z0-9_]+$", RegexOptions.Compiled);

    [ObservableProperty]
    private string _name = string.Empty;

    private VariableType _type = VariableType.Int;

    private int _intValue;
    private float _floatValue;

    [ObservableProperty]
    private string _stringValue = string.Empty;

    [ObservableProperty]
    private uint _objectIdValue = 0x7F000000; // OBJECT_INVALID

    // Location components (flattened for simple per-field editing).
    [ObservableProperty] private uint _locationArea;
    [ObservableProperty] private float _locationPositionX;
    [ObservableProperty] private float _locationPositionY;
    [ObservableProperty] private float _locationPositionZ;
    [ObservableProperty] private float _locationOrientationX;
    [ObservableProperty] private float _locationOrientationY;
    [ObservableProperty] private float _locationOrientationZ;

    /// <summary>Validation error flag, set by the hosting panel/view.</summary>
    [ObservableProperty]
    private bool _hasError;

    /// <summary>Description of the current validation error.</summary>
    [ObservableProperty]
    private string _errorMessage = string.Empty;

    partial void OnNameChanged(string value) => OnPropertyChanged(nameof(HasEmptyName));

    /// <summary>True when the name is blank/whitespace.</summary>
    public bool HasEmptyName => string.IsNullOrWhiteSpace(Name);

    /// <summary>The variable's data type. Changing it fans out the derived display/visibility props.</summary>
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
                OnPropertyChanged(nameof(IsLocationType));
            }
        }
    }

    public int IntValue
    {
        get => _intValue;
        set { if (_intValue != value) { _intValue = value; OnPropertyChanged(); OnPropertyChanged(nameof(ValueDisplay)); } }
    }

    /// <summary>Float value, rounded to 3 decimals (Aurora's typical precision).</summary>
    public decimal FloatValue
    {
        get => (decimal)Math.Round(_floatValue, 3);
        set
        {
            var floatVal = (float)Math.Round((double)value, 3);
            if (_floatValue != floatVal)
            {
                _floatValue = floatVal;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ValueDisplay));
            }
        }
    }

    public string TypeDisplay => Type switch
    {
        VariableType.Int => "Int",
        VariableType.Float => "Float",
        VariableType.String => "String",
        VariableType.Object => "Object",
        VariableType.Location => "Location",
        _ => "Unknown"
    };

    public string ValueDisplay => Type switch
    {
        VariableType.Int => IntValue.ToString(),
        VariableType.Float => _floatValue.ToString("F3"),
        VariableType.String => StringValue,
        VariableType.Object => $"0x{ObjectIdValue:X8}",
        VariableType.Location => $"({LocationPositionX:F2}, {LocationPositionY:F2}, {LocationPositionZ:F2})",
        _ => string.Empty
    };

    public bool IsIntType => Type == VariableType.Int;
    public bool IsFloatType => Type == VariableType.Float;
    public bool IsStringType => Type == VariableType.String;
    public bool IsObjectType => Type == VariableType.Object;
    public bool IsLocationType => Type == VariableType.Location;

    /// <summary>
    /// 0-based ComboBox index for type selection. 0=Int, 1=Float, 2=String, 3=Object, 4=Location.
    /// </summary>
    public int TypeIndex
    {
        get => Type switch
        {
            VariableType.Int => 0,
            VariableType.Float => 1,
            VariableType.String => 2,
            VariableType.Object => 3,
            VariableType.Location => 4,
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
                4 => VariableType.Location,
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
    /// True if the name obeys NWN rules: non-empty, ≤32 chars, and only
    /// <c>[A-Za-z0-9_]</c>. Duplicate detection is the hosting panel's job.
    /// </summary>
    public static bool IsValidName(string? name) =>
        !string.IsNullOrEmpty(name) && name.Length <= MaxNameLength && NamePattern.IsMatch(name);

    /// <summary>Build a VM from a model <see cref="Variable"/> (all 5 types).</summary>
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
                vm._floatValue = variable.GetFloat(); // set field directly to skip decimal round-trip
                break;
            case VariableType.String:
                vm.StringValue = variable.GetString();
                break;
            case VariableType.Object:
                vm.ObjectIdValue = variable.GetObjectId();
                break;
            case VariableType.Location:
                var loc = variable.GetLocation();
                if (loc != null)
                {
                    vm.LocationArea = loc.Area;
                    vm.LocationPositionX = loc.PositionX;
                    vm.LocationPositionY = loc.PositionY;
                    vm.LocationPositionZ = loc.PositionZ;
                    vm.LocationOrientationX = loc.OrientationX;
                    vm.LocationOrientationY = loc.OrientationY;
                    vm.LocationOrientationZ = loc.OrientationZ;
                }
                break;
        }

        return vm;
    }

    /// <summary>Convert back to a model <see cref="Variable"/> (all 5 types).</summary>
    public Variable ToVariable() => Type switch
    {
        VariableType.Int => Variable.CreateInt(Name, IntValue),
        VariableType.Float => Variable.CreateFloat(Name, _floatValue),
        VariableType.String => Variable.CreateString(Name, StringValue),
        VariableType.Object => Variable.CreateObject(Name, ObjectIdValue),
        VariableType.Location => Variable.CreateLocation(Name, new VariableLocation
        {
            Area = LocationArea,
            PositionX = LocationPositionX,
            PositionY = LocationPositionY,
            PositionZ = LocationPositionZ,
            OrientationX = LocationOrientationX,
            OrientationY = LocationOrientationY,
            OrientationZ = LocationOrientationZ
        }),
        _ => Variable.CreateInt(Name, 0)
    };
}
