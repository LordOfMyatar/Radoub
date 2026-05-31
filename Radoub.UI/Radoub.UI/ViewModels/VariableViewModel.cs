using System;
using System.Globalization;
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
///
/// Int/Float/String/Object values are edited through the raw <see cref="ValueText"/> string
/// so bad input (e.g. letters in an int) is NEVER silently discarded — the typed text is
/// kept and flagged via <see cref="HasError"/>/<see cref="ErrorMessage"/>, letting the user
/// correct it or switch the type to String. Location keeps its seven numeric fields.
/// </remarks>
public partial class VariableViewModel : ObservableObject
{
    /// <summary>Maximum NWN variable-name length (Aurora tag limit).</summary>
    public const int MaxNameLength = 32;

    private static readonly Regex NamePattern = new("^[A-Za-z0-9_]+$", RegexOptions.Compiled);

    // Float must be written with an explicit decimal point and >=1 digit on each side (e.g. 5.0, -2.5).
    private static readonly Regex FloatPattern = new(@"^-?\d+\.\d+$", RegexOptions.Compiled);

    /// <summary>Invariant float format that always emits a decimal point + trailing digit (5 -> "5.0").</summary>
    private const string FloatFormat = "0.0###";

    [ObservableProperty]
    private string _name = string.Empty;

    private VariableType _type = VariableType.Int;

    /// <summary>
    /// Raw value text as typed by the user. Source of truth for Int/Float/String/Object.
    /// Never auto-reverted — invalid numeric text is kept and flagged (see remarks).
    /// </summary>
    [ObservableProperty]
    private string _valueText = "0";

    // Location components (flattened for simple per-field editing).
    [ObservableProperty] private uint _locationArea;
    [ObservableProperty] private float _locationPositionX;
    [ObservableProperty] private float _locationPositionY;
    [ObservableProperty] private float _locationPositionZ;
    [ObservableProperty] private float _locationOrientationX;
    [ObservableProperty] private float _locationOrientationY;
    [ObservableProperty] private float _locationOrientationZ;

    /// <summary>Validation error flag (name OR value). Maintained by <see cref="Validate"/>.</summary>
    [ObservableProperty]
    private bool _hasError;

    /// <summary>Description of the current validation error.</summary>
    [ObservableProperty]
    private string _errorMessage = string.Empty;

    partial void OnNameChanged(string value) => OnPropertyChanged(nameof(HasEmptyName));

    partial void OnValueTextChanged(string value)
    {
        OnPropertyChanged(nameof(ValueDisplay));
        OnPropertyChanged(nameof(IntValue));
        OnPropertyChanged(nameof(FloatValue));
        OnPropertyChanged(nameof(ObjectIdValue));
        OnPropertyChanged(nameof(StringValue));
    }

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

    /// <summary>Integer view over <see cref="ValueText"/>. Returns 0 if the text is not a valid int.</summary>
    public int IntValue
    {
        get => int.TryParse(ValueText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) ? i : 0;
        set => ValueText = value.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Float view over <see cref="ValueText"/>, rounded to 3 decimals (Aurora's typical precision).
    /// Returns 0 if the text is not a valid float.
    /// </summary>
    public decimal FloatValue
    {
        get => float.TryParse(ValueText, NumberStyles.Float, CultureInfo.InvariantCulture, out var f)
            ? (decimal)Math.Round(f, 3)
            : 0m;
        set => ValueText = ((float)Math.Round((double)value, 3)).ToString(FloatFormat, CultureInfo.InvariantCulture);
    }

    /// <summary>Object-id view over <see cref="ValueText"/>. Returns OBJECT_INVALID if not a valid uint.</summary>
    public uint ObjectIdValue
    {
        get => uint.TryParse(ValueText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var u) ? u : 0x7F000000;
        set => ValueText = value.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>String view over <see cref="ValueText"/> (the text IS the value for string vars).</summary>
    public string StringValue
    {
        get => ValueText;
        set => ValueText = value ?? string.Empty;
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
        VariableType.Int => ValueText,
        VariableType.Float => ValueText,
        VariableType.String => ValueText,
        VariableType.Object => ValueText,
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

    /// <summary>
    /// True if <see cref="ValueText"/> parses for the current <see cref="Type"/>.
    /// String accepts anything; Location is edited via numeric fields and is always valid here.
    /// </summary>
    public bool IsValueValid() => Type switch
    {
        VariableType.Int => int.TryParse(ValueText, NumberStyles.Integer, CultureInfo.InvariantCulture, out _),
        VariableType.Float => FloatPattern.IsMatch(ValueText)
            && float.TryParse(ValueText, NumberStyles.Float, CultureInfo.InvariantCulture, out _),
        VariableType.Object => uint.TryParse(ValueText, NumberStyles.Integer, CultureInfo.InvariantCulture, out _),
        _ => true
    };

    /// <summary>
    /// Recompute <see cref="HasError"/>/<see cref="ErrorMessage"/> for this single variable's
    /// name format and value parse. Duplicate-name detection is layered on by the panel.
    /// Returns the value-only error message (null if the value parses), so the panel can
    /// keep name/duplicate errors it owns.
    /// </summary>
    public string? ValidateValue()
    {
        if (!IsValueValid())
        {
            var expected = Type switch
            {
                VariableType.Int => "a whole number",
                VariableType.Float => "a decimal number (needs a point and a digit after, e.g. 5.0)",
                VariableType.Object => "a non-negative object id",
                _ => "valid"
            };
            return $"\"{ValueText}\" is not {expected} — fix it or switch the type to String.";
        }
        return null;
    }

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
                vm.ValueText = variable.GetInt().ToString(CultureInfo.InvariantCulture);
                break;
            case VariableType.Float:
                vm.ValueText = variable.GetFloat().ToString(FloatFormat, CultureInfo.InvariantCulture);
                break;
            case VariableType.String:
                vm.ValueText = variable.GetString();
                break;
            case VariableType.Object:
                vm.ValueText = variable.GetObjectId().ToString(CultureInfo.InvariantCulture);
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

    /// <summary>
    /// Convert back to a model <see cref="Variable"/> (all 5 types). Unparseable numeric
    /// text falls back to a safe default (the variable is flagged via <see cref="ValidateValue"/>
    /// so the host blocks save before this is reached).
    /// </summary>
    public Variable ToVariable() => Type switch
    {
        VariableType.Int => Variable.CreateInt(Name, IntValue),
        VariableType.Float => Variable.CreateFloat(Name, (float)FloatValue),
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
