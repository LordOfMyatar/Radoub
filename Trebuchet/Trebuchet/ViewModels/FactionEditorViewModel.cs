using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Radoub.Formats.Fac;
using Radoub.Formats.Logging;
using Radoub.Formats.Settings;

namespace RadoubLauncher.ViewModels;

/// <summary>
/// ViewModel for a single faction in the list.
/// </summary>
public partial class FactionViewModel : ObservableObject
{
    private readonly FactionEditorViewModel _parent;

    public FactionViewModel(int index, Faction faction, FactionEditorViewModel parent)
    {
        _parent = parent;
        Index = index;
        _name = faction.FactionName;
        _isGlobal = faction.FactionGlobal != 0;
        _parentFactionId = faction.FactionParentID;
    }

    public int Index { get; }
    public bool IsDefault => Index < 5;

    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private bool _isGlobal;

    [ObservableProperty]
    private uint _parentFactionId;

    public string ParentName
    {
        get
        {
            if (ParentFactionId == 0xFFFFFFFF) return "(None)";
            var parent = _parent.Factions.FirstOrDefault(f => f.Index == (int)ParentFactionId);
            return parent?.Name ?? $"#{ParentFactionId}";
        }
    }

    partial void OnNameChanged(string value) => _parent.MarkDirty();
    partial void OnIsGlobalChanged(bool value) => _parent.MarkDirty();

    partial void OnParentFactionIdChanged(uint value)
    {
        OnPropertyChanged(nameof(ParentName));
        _parent.MarkDirty();
    }

    public override string ToString() => Name;
}

/// <summary>
/// Represents a single cell in the reputation matrix.
/// Row (FactionID2) perceives Column (FactionID1).
/// </summary>
public partial class MatrixCellViewModel : ObservableObject
{
    private readonly FactionEditorViewModel _parent;

    public MatrixCellViewModel(int row, int col, uint value, FactionEditorViewModel parent)
    {
        _parent = parent;
        Row = row;
        Col = col;
        _reputationValue = value;
        IsDiagonal = row == col;
    }

    public int Row { get; }
    public int Col { get; }
    public bool IsDiagonal { get; }
    public bool IsEditable => !IsDiagonal;

    private uint _reputationValue;

    public uint ReputationValue
    {
        get => _reputationValue;
        set
        {
            if (value > 100) value = 100;
            if (SetProperty(ref _reputationValue, value))
            {
                OnPropertyChanged(nameof(ReputationText));
                _parent.OnCellValueChanged(Row, Col, value);
            }
        }
    }

    public string ReputationText
    {
        get => _reputationValue.ToString();
        set
        {
            if (uint.TryParse(value, out uint parsed))
                ReputationValue = parsed;
        }
    }

    /// <summary>
    /// Sets the initial value without triggering dirty tracking.
    /// </summary>
    internal void SetInitialValue(uint value)
    {
        if (value > 100) value = 100;
        _reputationValue = value;
        OnPropertyChanged(nameof(ReputationValue));
        OnPropertyChanged(nameof(ReputationText));
    }
}

/// <summary>
/// ViewModel for the Faction Editor window.
/// Manages faction definitions and the reputation matrix.
/// </summary>
public partial class FactionEditorViewModel : ObservableObject
{
    private Window? _parentWindow;
    private FacFile? _facFile;
    private string? _facFilePath;
    private string? _workingDirectoryPath;
    private bool _isReadOnly;

    public ObservableCollection<FactionViewModel> Factions { get; } = new();

    [ObservableProperty]
    private FactionViewModel? _selectedFaction;

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isModuleLoaded;

    [ObservableProperty]
    private bool _hasUnsavedChanges;

    [ObservableProperty]
    private bool _isModuleReadOnly;

    // Matrix data: [row][col] = cell
    private MatrixCellViewModel[,]? _matrix;

    public int FactionCount => Factions.Count;

    public MatrixCellViewModel? GetCell(int row, int col)
    {
        if (_matrix == null || row < 0 || col < 0 || row >= FactionCount || col >= FactionCount)
            return null;
        return _matrix[row, col];
    }

    /// <summary>
    /// Event raised when the matrix needs to be rebuilt in the view.
    /// </summary>
    public event EventHandler? MatrixChanged;

    public void SetParentWindow(Window window) => _parentWindow = window;

    public async Task LoadFacFileAsync()
    {
        IsLoading = true;
        StatusText = "Loading factions...";

        try
        {
            var modulePath = RadoubSettings.Instance.CurrentModulePath;
            if (string.IsNullOrEmpty(modulePath))
            {
                IsModuleLoaded = false;
                StatusText = "No module loaded";
                return;
            }

            _facFilePath = ResolveFacPath(modulePath);

            if (_facFilePath != null)
            {
                await Task.Run(() => _facFile = FacReader.Read(_facFilePath));
                UnifiedLogger.LogApplication(LogLevel.INFO,
                    $"Loaded faction file: {UnifiedLogger.SanitizePath(_facFilePath)}");
            }
            else
            {
                _facFile = FacReader.CreateDefault();
                // Populate default reputation matrix from repute.2da defaults
                PopulateDefaultReputations(_facFile);
                _isReadOnly = false;
                UnifiedLogger.LogApplication(LogLevel.INFO,
                    "No repute.fac found - using defaults");
            }

            BuildViewModels();
            BuildMatrix();
            IsModuleLoaded = true;
            IsModuleReadOnly = _isReadOnly;
            StatusText = _facFilePath != null
                ? $"Loaded: {Path.GetFileName(_facFilePath)} ({Factions.Count} factions)"
                : $"Default factions ({Factions.Count} factions) - will create repute.fac on save";
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to load factions: {ex.Message}");
            StatusText = $"Error: {ex.Message}";
            IsModuleLoaded = false;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private string? ResolveFacPath(string modulePath)
    {
        // Check if it's a .mod file
        if (File.Exists(modulePath) && modulePath.EndsWith(".mod", StringComparison.OrdinalIgnoreCase))
        {
            var moduleName = Path.GetFileNameWithoutExtension(modulePath);
            var moduleDir = Path.GetDirectoryName(modulePath);
            var workingDir = FindWorkingDirectory(moduleDir, moduleName);

            if (workingDir != null)
            {
                _workingDirectoryPath = workingDir;
                var facPath = FindFileCaseInsensitive(workingDir, "repute.fac");
                if (facPath != null) return facPath;
            }
            else
            {
                _isReadOnly = true;
                // TODO: could read from packed .mod via ERF
            }
        }
        else if (Directory.Exists(modulePath))
        {
            _workingDirectoryPath = modulePath;
            var facPath = FindFileCaseInsensitive(modulePath, "repute.fac");
            if (facPath != null) return facPath;
        }

        return null;
    }

    /// <summary>
    /// Find a file by name using case-insensitive matching.
    /// Required on Linux where filesystems are case-sensitive but
    /// Aurora Engine resources may have mixed case. (#1384)
    /// </summary>
    private static string? FindFileCaseInsensitive(string directory, string fileName)
    {
        var exactPath = Path.Combine(directory, fileName);
        if (File.Exists(exactPath)) return exactPath;

        // Case-insensitive fallback for Linux
        try
        {
            var match = Directory.GetFiles(directory, fileName,
                new EnumerationOptions { MatchCasing = MatchCasing.CaseInsensitive })
                .FirstOrDefault();
            return match;
        }
        catch
        {
            return null;
        }
    }

    private static string? FindWorkingDirectory(string? moduleDir, string moduleName)
    {
        if (string.IsNullOrEmpty(moduleDir)) return null;

        var candidates = new[]
        {
            Path.Combine(moduleDir, moduleName),
            Path.Combine(moduleDir, "temp0"),
            Path.Combine(moduleDir, "temp1")
        };

        foreach (var candidate in candidates)
        {
            if (Directory.Exists(candidate))
                return candidate;
        }
        return null;
    }

    private void PopulateDefaultReputations(FacFile fac)
    {
        // Standard NWN default reputation matrix (from repute.2da)
        // Format: [perceived][perceiver] = rep value
        // Indices: 0=PC, 1=Hostile, 2=Commoner, 3=Merchant, 4=Defender
        var defaults = new uint[,]
        {
            //        PC  Hos Com Mer Def
            /* PC  */ { 0,  0,  0,  0,  0 }, // PC perceiving (unused but stored)
            /* Hos */ { 0,100,  0,  0,  0 }, // Hostile perceiving
            /* Com */ {50,  0,100, 50,100 }, // Commoner perceiving
            /* Mer */ {50,  0, 50,100,100 }, // Merchant perceiving
            /* Def */ {50,  0,100,100,100 }, // Defender perceiving
        };

        for (int perceiver = 1; perceiver < 5; perceiver++)
        {
            for (int perceived = 0; perceived < 5; perceived++)
            {
                if (perceiver == perceived) continue;
                fac.RepList.Add(new Reputation
                {
                    FactionID1 = (uint)perceived,
                    FactionID2 = (uint)perceiver,
                    FactionRep = defaults[perceiver, perceived]
                });
            }
        }
    }

    private void BuildViewModels()
    {
        Factions.Clear();
        if (_facFile == null) return;

        for (int i = 0; i < _facFile.FactionList.Count; i++)
        {
            Factions.Add(new FactionViewModel(i, _facFile.FactionList[i], this));
        }

        OnPropertyChanged(nameof(FactionCount));
        if (Factions.Count > 0)
            SelectedFaction = Factions[0];
    }

    private void BuildMatrix()
    {
        if (_facFile == null) return;

        int n = _facFile.FactionList.Count;
        _matrix = new MatrixCellViewModel[n, n];

        // Initialize all cells with default values
        for (int row = 0; row < n; row++)
        {
            for (int col = 0; col < n; col++)
            {
                _matrix[row, col] = new MatrixCellViewModel(row, col, row == col ? 100u : 50u, this);
            }
        }

        // Populate from RepList (use SetInitialValue to avoid dirty tracking)
        foreach (var rep in _facFile.RepList)
        {
            int perceived = (int)rep.FactionID1;
            int perceiver = (int)rep.FactionID2;
            if (perceived < n && perceiver < n)
            {
                _matrix[perceiver, perceived].SetInitialValue(rep.FactionRep);
            }
        }

        MatrixChanged?.Invoke(this, EventArgs.Empty);
    }

    internal void OnCellValueChanged(int row, int col, uint value)
    {
        if (_facFile == null) return;

        // Update the underlying FacFile RepList
        var existing = _facFile.RepList.FirstOrDefault(r =>
            r.FactionID1 == (uint)col && r.FactionID2 == (uint)row);

        if (existing != null)
        {
            existing.FactionRep = value;
        }
        else
        {
            _facFile.RepList.Add(new Reputation
            {
                FactionID1 = (uint)col,
                FactionID2 = (uint)row,
                FactionRep = value
            });
        }

        MarkDirty();
    }

    internal void MarkDirty()
    {
        HasUnsavedChanges = true;
    }

    [RelayCommand]
    private void Save()
    {
        if (_facFile == null || _isReadOnly) return;

        try
        {
            // Sync ViewModel changes back to FacFile
            SyncViewModelsToFacFile();

            // Determine save path
            var savePath = _facFilePath;
            if (savePath == null && _workingDirectoryPath != null)
            {
                savePath = Path.Combine(_workingDirectoryPath, "repute.fac");
            }

            if (savePath == null)
            {
                StatusText = "Cannot save - no working directory";
                return;
            }

            FacWriter.Write(_facFile, savePath);
            _facFilePath = savePath;
            HasUnsavedChanges = false;
            StatusText = $"Saved: {Path.GetFileName(savePath)}";
            UnifiedLogger.LogApplication(LogLevel.INFO,
                $"Saved faction file: {UnifiedLogger.SanitizePath(savePath)}");
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to save factions: {ex.Message}");
            StatusText = $"Save failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private void DiscardChanges()
    {
        if (_facFile == null) return;

        // Reload from file or defaults
        _ = LoadFacFileAsync();
    }

    public void AddFaction(string name, bool isGlobal, uint parentId)
    {
        if (_facFile == null) return;

        var faction = new Faction
        {
            FactionName = name,
            FactionGlobal = (ushort)(isGlobal ? 1 : 0),
            FactionParentID = parentId
        };

        _facFile.FactionList.Add(faction);

        int newIndex = _facFile.FactionList.Count - 1;

        // Initialize reputations: new faction starts neutral (50) with all existing factions
        for (int i = 0; i < _facFile.FactionList.Count; i++)
        {
            if (i == newIndex) continue;

            // Other factions see new faction as neutral
            _facFile.RepList.Add(new Reputation
            {
                FactionID1 = (uint)newIndex,
                FactionID2 = (uint)i,
                FactionRep = 50
            });

            // New faction sees others as neutral (except PC perception, still add it)
            _facFile.RepList.Add(new Reputation
            {
                FactionID1 = (uint)i,
                FactionID2 = (uint)newIndex,
                FactionRep = 50
            });
        }

        BuildViewModels();
        BuildMatrix();
        MarkDirty();
        SelectedFaction = Factions.LastOrDefault();
        StatusText = $"Added faction: {name}";
    }

    public bool CanRemoveFaction(FactionViewModel? faction)
    {
        return faction != null && !faction.IsDefault && !_isReadOnly;
    }

    public void RemoveFaction(FactionViewModel faction)
    {
        if (_facFile == null || faction.IsDefault) return;

        int removeIndex = faction.Index;

        // Remove the faction
        _facFile.FactionList.RemoveAt(removeIndex);

        // Remove all reputation entries referencing this faction
        _facFile.RepList.RemoveAll(r =>
            r.FactionID1 == (uint)removeIndex || r.FactionID2 == (uint)removeIndex);

        // Reindex remaining reputation entries
        foreach (var rep in _facFile.RepList)
        {
            if (rep.FactionID1 > (uint)removeIndex) rep.FactionID1--;
            if (rep.FactionID2 > (uint)removeIndex) rep.FactionID2--;
        }

        // Reindex parent IDs
        foreach (var f in _facFile.FactionList)
        {
            if (f.FactionParentID == (uint)removeIndex)
                f.FactionParentID = 0xFFFFFFFF;
            else if (f.FactionParentID != 0xFFFFFFFF && f.FactionParentID > (uint)removeIndex)
                f.FactionParentID--;
        }

        BuildViewModels();
        BuildMatrix();
        MarkDirty();
        StatusText = $"Removed faction: {faction.Name}";
    }

    private void SyncViewModelsToFacFile()
    {
        if (_facFile == null) return;

        for (int i = 0; i < Factions.Count && i < _facFile.FactionList.Count; i++)
        {
            var vm = Factions[i];
            var f = _facFile.FactionList[i];
            f.FactionName = vm.Name;
            f.FactionGlobal = (ushort)(vm.IsGlobal ? 1 : 0);
            f.FactionParentID = vm.ParentFactionId;
        }
    }
}
