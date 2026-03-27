using Radoub.Formats.Common;
using Radoub.Formats.Search;
using Radoub.UI.Services.Search;
using RadoubLauncher.ViewModels;

namespace Trebuchet.Tests;

public class MarlinspikePanelViewModelTests
{
    [Fact]
    public void Constructor_DefaultState_AllFileTypesChecked()
    {
        var vm = new MarlinspikePanelViewModel();

        Assert.True(vm.IncludeDlg);
        Assert.True(vm.IncludeUtc);
        Assert.True(vm.IncludeBic);
        Assert.True(vm.IncludeUti);
        Assert.True(vm.IncludeUtm);
        Assert.True(vm.IncludeJrl);
        Assert.True(vm.IncludeUtp);
        Assert.True(vm.IncludeUtd);
        Assert.True(vm.IncludeUte);
        Assert.True(vm.IncludeUtt);
        Assert.True(vm.IncludeUtw);
        Assert.True(vm.IncludeUts);
        Assert.True(vm.IncludeGit);
        Assert.True(vm.IncludeAre);
        Assert.True(vm.IncludeIfo);
        Assert.True(vm.IncludeFac);
        Assert.True(vm.IncludeItp);
    }

    [Fact]
    public void Constructor_DefaultState_SearchOptionsDefaults()
    {
        var vm = new MarlinspikePanelViewModel();

        Assert.Equal("", vm.SearchPattern);
        Assert.Equal("", vm.ReplaceText);
        Assert.False(vm.IsCaseSensitive);
        Assert.False(vm.IsRegex);
        Assert.False(vm.IsWholeWord);
        Assert.Equal("All Fields", vm.SelectedCategory);
        Assert.False(vm.IsSearching);
        Assert.Equal("Ready", vm.StatusText);
    }

    [Fact]
    public void Constructor_DefaultState_ButtonStates()
    {
        var vm = new MarlinspikePanelViewModel();

        Assert.False(vm.CanSearch);  // Empty pattern = can't search
        Assert.False(vm.CanReplace);
        Assert.False(vm.IsSearching);
    }

    [Fact]
    public void CanSearch_EmptyPattern_ReturnsFalse()
    {
        var vm = new MarlinspikePanelViewModel();
        vm.SearchPattern = "";

        Assert.False(vm.CanSearch);
    }

    [Fact]
    public void CanSearch_WithPattern_ReturnsTrue()
    {
        var vm = new MarlinspikePanelViewModel();
        vm.SearchPattern = "poisoned apple";

        Assert.True(vm.CanSearch);
    }

    [Fact]
    public void CanSearch_WhileSearching_ReturnsFalse()
    {
        var vm = new MarlinspikePanelViewModel();
        vm.SearchPattern = "poisoned apple";
        vm.SetSearching(true);

        Assert.False(vm.CanSearch);
    }

    [Fact]
    public void CanReplace_NoResults_ReturnsFalse()
    {
        var vm = new MarlinspikePanelViewModel();
        vm.SearchPattern = "test";
        vm.ReplaceText = "replacement";

        Assert.False(vm.CanReplace);
    }

    [Fact]
    public void CanReplace_WithResultsAndReplaceText_ReturnsTrue()
    {
        var vm = new MarlinspikePanelViewModel();
        vm.SearchPattern = "test";
        vm.ReplaceText = "replacement";
        vm.SetResults(CreateTestResults());

        Assert.True(vm.CanReplace);
    }

    [Fact]
    public void BuildSearchCriteria_PlainText_CorrectCriteria()
    {
        var vm = new MarlinspikePanelViewModel();
        vm.SearchPattern = "poisoned apple";
        vm.IsCaseSensitive = true;
        vm.IsWholeWord = true;

        var criteria = vm.BuildSearchCriteria();

        Assert.Equal("poisoned apple", criteria.Pattern);
        Assert.True(criteria.CaseSensitive);
        Assert.True(criteria.WholeWord);
        Assert.False(criteria.IsRegex);
        Assert.Null(criteria.CategoryFilter);
    }

    [Fact]
    public void BuildSearchCriteria_Regex_CorrectCriteria()
    {
        var vm = new MarlinspikePanelViewModel();
        vm.SearchPattern = "poison.*apple";
        vm.IsRegex = true;

        var criteria = vm.BuildSearchCriteria();

        Assert.Equal("poison.*apple", criteria.Pattern);
        Assert.True(criteria.IsRegex);
    }

    [Fact]
    public void BuildSearchCriteria_CategoryFilter_AppliedCorrectly()
    {
        var vm = new MarlinspikePanelViewModel();
        vm.SearchPattern = "test";
        vm.SelectedCategory = "Content";

        var criteria = vm.BuildSearchCriteria();

        Assert.NotNull(criteria.CategoryFilter);
        Assert.Single(criteria.CategoryFilter!);
        Assert.Equal(SearchFieldCategory.Content, criteria.CategoryFilter![0]);
    }

    [Fact]
    public void BuildSearchCriteria_AllFieldsCategory_NoCategoryFilter()
    {
        var vm = new MarlinspikePanelViewModel();
        vm.SearchPattern = "test";
        vm.SelectedCategory = "All Fields";

        var criteria = vm.BuildSearchCriteria();

        Assert.Null(criteria.CategoryFilter);
    }

    [Fact]
    public void BuildSearchCriteria_FileTypeFilter_UncheckedTypesExcluded()
    {
        var vm = new MarlinspikePanelViewModel();
        vm.SearchPattern = "test";
        vm.IncludeDlg = false;
        vm.IncludeUtc = false;

        var criteria = vm.BuildSearchCriteria();

        Assert.NotNull(criteria.FileTypeFilter);
        Assert.DoesNotContain(ResourceTypes.Dlg, criteria.FileTypeFilter!);
        Assert.DoesNotContain(ResourceTypes.Utc, criteria.FileTypeFilter!);
        Assert.Contains(ResourceTypes.Uti, criteria.FileTypeFilter!);
    }

    [Fact]
    public void BuildSearchCriteria_AllTypesChecked_NullFileTypeFilter()
    {
        var vm = new MarlinspikePanelViewModel();
        vm.SearchPattern = "test";

        var criteria = vm.BuildSearchCriteria();

        Assert.Null(criteria.FileTypeFilter);
    }

    [Fact]
    public void SelectAllFileTypes_SetsAllTrue()
    {
        var vm = new MarlinspikePanelViewModel();
        vm.IncludeDlg = false;
        vm.IncludeUtc = false;
        vm.IncludeUti = false;

        vm.SelectAllFileTypes();

        Assert.True(vm.IncludeDlg);
        Assert.True(vm.IncludeUtc);
        Assert.True(vm.IncludeUti);
        Assert.True(vm.IncludeUtm);
    }

    [Fact]
    public void DeselectAllFileTypes_SetsAllFalse()
    {
        var vm = new MarlinspikePanelViewModel();

        vm.DeselectAllFileTypes();

        Assert.False(vm.IncludeDlg);
        Assert.False(vm.IncludeUtc);
        Assert.False(vm.IncludeUti);
        Assert.False(vm.IncludeUtm);
        Assert.False(vm.IncludeJrl);
    }

    [Fact]
    public void DeselectAllFileTypes_CanSearch_ReturnsFalse()
    {
        var vm = new MarlinspikePanelViewModel();
        vm.SearchPattern = "test";

        vm.DeselectAllFileTypes();

        Assert.False(vm.HasAnyFileTypeSelected);
        Assert.False(vm.CanSearch);
    }

    [Fact]
    public void HasAnyFileTypeSelected_OneChecked_ReturnsTrue()
    {
        var vm = new MarlinspikePanelViewModel();
        vm.DeselectAllFileTypes();
        vm.IncludeDlg = true;

        Assert.True(vm.HasAnyFileTypeSelected);
    }

    [Fact]
    public void SetResults_UpdatesResultGroups()
    {
        var vm = new MarlinspikePanelViewModel();
        var results = CreateTestResults();

        vm.SetResults(results);

        Assert.NotEmpty(vm.ResultGroups);
        Assert.True(vm.HasResults);
    }

    [Fact]
    public void SetResults_UpdatesStatusText()
    {
        var vm = new MarlinspikePanelViewModel();
        var results = CreateTestResults();

        vm.SetResults(results);

        Assert.Contains("match", vm.StatusText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ClearResults_ClearsEverything()
    {
        var vm = new MarlinspikePanelViewModel();
        vm.SetResults(CreateTestResults());

        vm.ClearResults();

        Assert.Empty(vm.ResultGroups);
        Assert.False(vm.HasResults);
        Assert.False(vm.CanReplace);
    }

    [Fact]
    public void Categories_ContainsAllExpected()
    {
        var vm = new MarlinspikePanelViewModel();

        Assert.Contains("All Fields", vm.Categories);
        Assert.Contains("Content", vm.Categories);
        Assert.Contains("Identity", vm.Categories);
        Assert.Contains("Script", vm.Categories);
        Assert.Contains("Metadata", vm.Categories);
        Assert.Contains("Variable", vm.Categories);
    }

    [Fact]
    public void SetSearching_UpdatesStateCorrectly()
    {
        var vm = new MarlinspikePanelViewModel();
        vm.SearchPattern = "test";

        vm.SetSearching(true);
        Assert.True(vm.IsSearching);
        Assert.False(vm.CanSearch);

        vm.SetSearching(false);
        Assert.False(vm.IsSearching);
        Assert.True(vm.CanSearch);
    }

    private static ModuleSearchResults CreateTestResults()
    {
        var match = new SearchMatch
        {
            Field = new FieldDefinition
            {
                Name = "Text",
                GffPath = "Text",
                FieldType = SearchFieldType.LocString,
                Category = SearchFieldCategory.Content
            },
            MatchedText = "poisoned apple",
            FullFieldValue = "The poisoned apple was on the table",
            MatchOffset = 4,
            MatchLength = 14
        };

        var fileResult = new FileSearchResult
        {
            FilePath = "/test/merchant_01.dlg",
            ResourceType = ResourceTypes.Dlg,
            ToolId = "parley",
            Matches = new List<SearchMatch> { match }
        };

        return new ModuleSearchResults
        {
            Files = new List<FileSearchResult> { fileResult },
            TotalFilesScanned = 10,
            Duration = TimeSpan.FromSeconds(1.2)
        };
    }
}
