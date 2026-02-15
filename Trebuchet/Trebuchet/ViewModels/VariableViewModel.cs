using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Radoub.Formats.Gff;

namespace RadoubLauncher.ViewModels;

/// <summary>
/// ViewModel for a single variable in the VarTable.
/// </summary>
public partial class VariableViewModel : ObservableObject, System.ComponentModel.INotifyDataErrorInfo
{
    private readonly Dictionary<string, List<string>> _errors = new();
    private Func<string, VariableViewModel, bool>? _isNameUnique;

    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private VariableType _type;

    [ObservableProperty]
    private string _valueString;

    /// <summary>
    /// Validation error message for the Value field (shown in UI).
    /// </summary>
    [ObservableProperty]
    private string? _valueError;

    /// <summary>
    /// Validation error message for the Name field (shown in UI).
    /// </summary>
    [ObservableProperty]
    private string? _nameError;

    public static ObservableCollection<VariableType> VariableTypes { get; } = new()
    {
        VariableType.Int,
        VariableType.Float,
        VariableType.String
    };

    public VariableViewModel(Variable variable)
    {
        _name = variable.Name;
        _type = variable.Type;
        _valueString = variable.Type switch
        {
            VariableType.Int => variable.GetInt().ToString(),
            VariableType.Float => variable.GetFloat().ToString("F2"),
            VariableType.String => variable.GetString(),
            _ => string.Empty
        };
    }

    /// <summary>
    /// Set the uniqueness check function. Called by parent ViewModel.
    /// </summary>
    public void SetUniquenessCheck(Func<string, VariableViewModel, bool> isNameUnique)
    {
        _isNameUnique = isNameUnique;
    }

    partial void OnNameChanged(string value)
    {
        ValidateName();
    }

    partial void OnTypeChanged(VariableType value)
    {
        // Re-validate when type changes
        ValidateValue();
    }

    partial void OnValueStringChanged(string value)
    {
        ValidateValue();
    }

    private void ValidateName()
    {
        ClearErrors(nameof(Name));
        NameError = null;

        if (string.IsNullOrWhiteSpace(Name))
        {
            AddError(nameof(Name), "Name is required");
            NameError = "Name is required";
            return;
        }

        if (_isNameUnique != null && !_isNameUnique(Name, this))
        {
            AddError(nameof(Name), "Name must be unique");
            NameError = "Name must be unique";
        }
    }

    private void ValidateValue()
    {
        ClearErrors(nameof(ValueString));
        ValueError = null;

        switch (Type)
        {
            case VariableType.Int:
                if (!string.IsNullOrEmpty(ValueString) && !int.TryParse(ValueString, out _))
                {
                    AddError(nameof(ValueString), "Must be a valid integer");
                    ValueError = "Must be a valid integer";
                }
                break;

            case VariableType.Float:
                if (!string.IsNullOrEmpty(ValueString) && !float.TryParse(ValueString, out _))
                {
                    AddError(nameof(ValueString), "Must be a valid number");
                    ValueError = "Must be a valid number";
                }
                break;
        }
    }

    public Variable ToVariable()
    {
        return Type switch
        {
            VariableType.Int => Variable.CreateInt(Name ?? "", int.TryParse(ValueString, out var i) ? i : 0),
            VariableType.Float => Variable.CreateFloat(Name ?? "", float.TryParse(ValueString, out var f) ? f : 0f),
            VariableType.String => Variable.CreateString(Name ?? "", ValueString ?? ""),
            _ => Variable.CreateInt(Name ?? "", 0)
        };
    }

    // INotifyDataErrorInfo implementation
    public bool HasErrors => _errors.Count > 0;

    public event EventHandler<System.ComponentModel.DataErrorsChangedEventArgs>? ErrorsChanged;

    public System.Collections.IEnumerable GetErrors(string? propertyName)
    {
        if (string.IsNullOrEmpty(propertyName))
            return _errors.SelectMany(e => e.Value);

        return _errors.TryGetValue(propertyName, out var errors) ? errors : Enumerable.Empty<string>();
    }

    private void AddError(string propertyName, string error)
    {
        if (!_errors.ContainsKey(propertyName))
            _errors[propertyName] = new List<string>();

        if (!_errors[propertyName].Contains(error))
        {
            _errors[propertyName].Add(error);
            ErrorsChanged?.Invoke(this, new System.ComponentModel.DataErrorsChangedEventArgs(propertyName));
            OnPropertyChanged(nameof(HasErrors));
        }
    }

    private void ClearErrors(string propertyName)
    {
        if (_errors.Remove(propertyName))
        {
            ErrorsChanged?.Invoke(this, new System.ComponentModel.DataErrorsChangedEventArgs(propertyName));
            OnPropertyChanged(nameof(HasErrors));
        }
    }
}
