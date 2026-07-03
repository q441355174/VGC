using System.Collections.Generic;
using System.Globalization;
using ReactiveUI;
using VGC.Facts;
using VGC.Mavlink;
using VGC.Vehicles;

namespace VGC.ViewModels;

public enum ParameterListMode
{
    Full,
    Modified,
    Favorites
}

public sealed record ParameterTableRow(
    ParameterDisplayRow Source,
    bool IsFavorite,
    string FavoriteMarker,
    string Name,
    string Label,
    string Type,
    string Value,
    string State);

public sealed class ParameterViewModel : ViewModelBase
{
    private readonly MultiVehicleManager _multiVehicleManager;
    private readonly ParameterEditService _editService;
    private readonly HashSet<string> _favoriteParameterNames = new(StringComparer.OrdinalIgnoreCase);
    private IParameterMetadataCatalog _metadataCatalog;
    private string _searchText = string.Empty;
    private string _selectedCategory = "All";
    private ParameterListMode _listMode;
    private ParameterDisplayRow? _selectedRow;
    private bool _hideReadOnly;
    private bool _showToolsMenu;
    private bool _showParameterEditor;
    private string _editText = string.Empty;
    private string _lastParameterEditStatusText = "No parameter edit pending";

    public ParameterViewModel(
        MultiVehicleManager multiVehicleManager,
        IParameterMetadataCatalog? metadataCatalog = null,
        ParameterEditService? editService = null)
    {
        _multiVehicleManager = multiVehicleManager;
        _metadataCatalog = metadataCatalog ?? InMemoryParameterMetadataCatalog.Empty;
        _editService = editService ?? new ParameterEditService();
        _multiVehicleManager.VehiclesChanged += (_, _) => Refresh();
        _multiVehicleManager.VehicleUpdated += (_, _) => Refresh();
    }

    public string Title { get; } = "Parameters";

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (string.Equals(_searchText, value, StringComparison.Ordinal))
            {
                return;
            }

            this.RaiseAndSetIfChanged(ref _searchText, value);
            this.RaisePropertyChanged(nameof(ParameterRows));
            this.RaisePropertyChanged(nameof(ParameterSummary));
            RefreshSelection();
        }
    }

    public IReadOnlyList<ParameterDisplayRow> ParameterRows => BuildRows(_multiVehicleManager.ActiveVehicle, _metadataCatalog, SearchText)
        .Where(row => MatchesMode(row) && MatchesCategory(row) && MatchesReadOnly(row))
        .ToArray();

    public IReadOnlyList<string> Categories => ["All", .. BuildRows(_multiVehicleManager.ActiveVehicle, _metadataCatalog, string.Empty)
        .Select(static row => string.IsNullOrWhiteSpace(row.Group) ? "Ungrouped" : row.Group)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Order(StringComparer.OrdinalIgnoreCase)];

    public string SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (string.Equals(_selectedCategory, value, StringComparison.Ordinal))
            {
                return;
            }

            this.RaiseAndSetIfChanged(ref _selectedCategory, value);
            this.RaisePropertyChanged(nameof(ParameterRows));
        }
    }

    public ParameterListMode ListMode
    {
        get => _listMode;
        set
        {
            if (_listMode == value)
            {
                return;
            }

            this.RaiseAndSetIfChanged(ref _listMode, value);
            this.RaisePropertyChanged(nameof(IsFullListMode));
            this.RaisePropertyChanged(nameof(IsModifiedListMode));
            this.RaisePropertyChanged(nameof(IsFavoritesListMode));
            this.RaisePropertyChanged(nameof(ParameterRows));
        }
    }

    public bool IsFullListMode => ListMode == ParameterListMode.Full;

    public bool IsModifiedListMode => ListMode == ParameterListMode.Modified;

    public bool IsFavoritesListMode => ListMode == ParameterListMode.Favorites;

    public ParameterDisplayRow? SelectedRow
    {
        get => _selectedRow;
        set => this.RaiseAndSetIfChanged(ref _selectedRow, value);
    }

    public string SelectedParameterTitle => SelectedRow?.Label ?? "Select a parameter";

    public string SelectedParameterName => SelectedRow?.Name ?? string.Empty;

    public string SelectedParameterDescription => SelectedRow?.Description ?? "Choose a parameter from the list to inspect and edit it.";

    public string SelectedParameterValue => SelectedRow?.Value ?? string.Empty;

    public string SelectedParameterUnits => SelectedRow?.Units ?? string.Empty;

    public string SelectedParameterRange => SelectedRow?.Range ?? string.Empty;

    public string SelectedParameterEnumValues => SelectedRow?.EnumValues ?? string.Empty;

    public string SelectedParameterWriteState => SelectedRow is null ? string.Empty : $"{SelectedRow.WriteStatus} / Retries {SelectedRow.WriteRetryCount}";

    public bool HasSelectedParameter => SelectedRow is not null;

    public bool HideReadOnly
    {
        get => _hideReadOnly;
        set
        {
            if (_hideReadOnly == value)
            {
                return;
            }

            this.RaiseAndSetIfChanged(ref _hideReadOnly, value);
            this.RaisePropertyChanged(nameof(ParameterRows));
        }
    }

    public bool ShowToolsMenu
    {
        get => _showToolsMenu;
        private set => this.RaiseAndSetIfChanged(ref _showToolsMenu, value);
    }

    public bool ShowToolsPopup => ShowToolsMenu;

    public bool IsSearchFilterActive => !string.IsNullOrWhiteSpace(SearchText) || ListMode != ParameterListMode.Full;

    public bool ShowGroupPanel => !IsSearchFilterActive;

    public bool IsSelectedParameterFavorite => SelectedRow is not null && _favoriteParameterNames.Contains(SelectedRow.Name);

    public bool CanRebootVehicle => _multiVehicleManager.ActiveVehicle is not null;

    public bool CanClearFavorites => _favoriteParameterNames.Count > 0;

    public string SelectedParameterFavoriteText => IsSelectedParameterFavorite ? "Favorite" : "Mark Favorite";

    public string SelectedParameterRebootText => SelectedRow?.RebootRequired == true ? "Reboot required" : string.Empty;

    public IReadOnlyList<ParameterDisplayRow> VisibleParameterRows => ParameterRows;

    public IReadOnlyList<ParameterTableRow> TableRows => VisibleParameterRows
        .Select(row => new ParameterTableRow(
            row,
            _favoriteParameterNames.Contains(row.Name),
            _favoriteParameterNames.Contains(row.Name) ? "★" : string.Empty,
            row.Name,
            row.Label,
            row.Type,
            row.Value,
            row.WriteStatus))
        .ToArray();

    public ParameterTableRow? SelectedTableRow
    {
        get => SelectedRow is null ? null : TableRows.FirstOrDefault(row => row.Source == SelectedRow);
        set => SelectRow(value?.Source);
    }

    public bool ShowFavoritesColumn => true;

    public bool ShowTypeColumn => true;

    public bool ShowStateColumn => true;

    public bool ShowValueColumn => true;

    public bool ShowNameColumn => true;

    public bool ShowLabelColumn => true;

    public bool HasFavoriteRows => TableRows.Any(static row => row.IsFavorite);

    public string TableRowsCountText => $"Rows {TableRows.Count}";

    public string FavoriteColumnHintText => HasFavoriteRows ? "★ favorite" : "No favorites pinned";

    public bool ShowFavoriteColumnHint => true;

    public bool ShowTableRows => TableRows.Count > 0;

    public bool ShowTableEmptyState => TableRows.Count == 0;

    public string TableEmptyStateText => ParameterEmptyStateText;

    public string ParameterRowsFootnoteText => FavoriteColumnHintText;

    public bool ShowParameterRowsFootnote => true;

    public string FavoritesModeSummaryText => IsFavoritesListMode ? FavoritesStatusText : string.Empty;

    public bool ShowFavoritesModeSummary => IsFavoritesListMode;

    public string SelectedParameterTableStateText => SelectedRow?.WriteStatus ?? string.Empty;

    public string SelectedParameterTableTypeText => SelectedRow?.Type ?? string.Empty;

    public bool ShowSelectedParameterTableState => SelectedRow is not null;

    public bool ShowSelectedParameterTableType => SelectedRow is not null;

    public string SelectedParameterTableValueText => SelectedRow?.Value ?? string.Empty;

    public bool ShowSelectedParameterTableValue => SelectedRow is not null;

    public string SelectedParameterTableLabelText => SelectedRow?.Label ?? string.Empty;

    public bool ShowSelectedParameterTableLabel => SelectedRow is not null;

    public string SelectedParameterTableNameText => SelectedRow?.Name ?? string.Empty;

    public bool ShowSelectedParameterTableName => SelectedRow is not null;

    public string SelectedParameterTableFavoriteMarker => IsSelectedParameterFavorite ? "★" : string.Empty;

    public bool ShowSelectedParameterTableFavoriteMarker => IsSelectedParameterFavorite;

    public string SelectedParameterHeaderStatusText => SelectedParameterTableStateText;

    public bool ShowSelectedParameterHeaderStatus => SelectedRow is not null;

    public string ParameterRowsHeaderMetaText => TableRowsCountText;

    public bool ShowParameterRowsHeaderMeta => true;

    public string TableModeHintText => IsFavoritesListMode ? FavoritesStatusText : IsModifiedListMode ? ModifiedFilterHintText : string.Empty;

    public bool ShowTableModeHint => IsFavoritesListMode || IsModifiedListMode;

    public string TableSearchHintText => SearchFilterHintText;

    public bool ShowTableSearchHint => ShowSearchFilterHint;

    public string TableDownloadStateText => ParameterDownloadState;

    public bool ShowTableDownloadState => true;

    public string TableMetadataStateText => ParameterMetadataState;

    public bool ShowTableMetadataState => true;

    public string TableSummaryText => ParameterSummary;

    public bool ShowTableSummary => true;

    public string FavoriteColumnHeaderText => ParameterFavoriteHeaderText;

    public string NameColumnHeaderText => ParameterNameColumnText;

    public string LabelColumnHeaderText => ParameterLabelColumnText;

    public string TypeColumnHeaderText => ParameterTypeColumnText;

    public string ValueColumnHeaderText => ParameterValueColumnText;

    public string StateColumnHeaderText => ParameterStateColumnText;

    public bool ShowParameterTable => ShowTableRows;

    public bool ShowParameterTableHeaders => ShowTableRows;

    public bool ShowParameterTableFooter => true;

    public string ParameterTableFooterText => ParameterRowsFootnoteText;

    public bool ShowParameterTableHeaderRow => ShowTableRows;

    public string TableHeaderStatusText => ParameterRowsHeaderMetaText;

    public bool ShowTableHeaderStatus => true;

    public string TableHeaderSummaryText => ParameterDownloadSummaryText;

    public bool ShowTableHeaderSummary => true;

    public string TableHeaderFilterText => ParameterFilterSummaryText;

    public bool ShowTableHeaderFilter => true;

    public string TableHeaderMetadataText => ParameterMetadataHintText;

    public bool ShowTableHeaderMetadata => true;

    public string TableHeaderFavoriteText => FavoriteColumnHeaderText;

    public string TableHeaderNameText => NameColumnHeaderText;

    public string TableHeaderLabelText => LabelColumnHeaderText;

    public string TableHeaderTypeText => TypeColumnHeaderText;

    public string TableHeaderValueText => ValueColumnHeaderText;

    public string TableHeaderStateText => StateColumnHeaderText;

    public bool ShowSelectedTableBadge => ShowSelectedParameterTableFavoriteMarker;

    public string SelectedTableBadgeText => SelectedParameterTableFavoriteMarker;

    public string SelectedTableValueText => SelectedParameterTableValueText;

    public string SelectedTableTypeText => SelectedParameterTableTypeText;

    public string SelectedTableStateText => SelectedParameterTableStateText;

    public string SelectedTableLabelText => SelectedParameterTableLabelText;

    public string SelectedTableNameText => SelectedParameterTableNameText;

    public bool ShowSelectedTableName => ShowSelectedParameterTableName;

    public bool ShowSelectedTableLabel => ShowSelectedParameterTableLabel;

    public bool ShowSelectedTableType => ShowSelectedParameterTableType;

    public bool ShowSelectedTableValue => ShowSelectedParameterTableValue;

    public bool ShowSelectedTableState => ShowSelectedParameterTableState;

    public bool ShowSelectedTableSummary => SelectedRow is not null;

    public string SelectedTableSummaryText => SelectedParameterFavoriteStatusText;

    public bool ShowSelectedTableSummaryText => SelectedRow is not null;

    public bool ShowSelectedTableStatusText => SelectedRow is not null;

    public string SelectedTableStatusText => SelectedParameterWriteState;

    public bool ShowSelectedTableCacheText => SelectedRow is not null;

    public string SelectedTableCacheText => SelectedParameterCacheState;

    public bool ShowSelectedTableErrorText => ShowSelectedParameterWriteError;

    public string SelectedTableErrorText => SelectedParameterWriteError;

    public bool ShowSelectedTableRebootText => ShowSelectedParameterRebootHint;

    public string SelectedTableRebootText => SelectedParameterRebootText;

    public bool ShowSelectedTableDescriptionText => ShowParameterDescription;

    public string SelectedTableDescriptionText => SelectedParameterDescription;

    public bool ShowSelectedTableEditButton => ShowParameterEditButton;

    public string SelectedTableEditButtonText => EditButtonText;

    public bool ShowSelectedTableFavoriteButton => ShowParameterFavoriteButton;

    public string SelectedTableFavoriteButtonText => SelectedParameterFavoriteText;

    public bool ShowTableGroupPanel => ShowGroupPanel;

    public string TableGroupHeaderText => GroupsTitleText;

    public bool ShowTableGroupHeader => ShowGroupPanel;

    public bool ShowTableToolbar => true;

    public bool ShowTableTabs => true;

    public bool ShowTableSearchBar => true;

    public bool ShowTableDetailPane => true;

    public bool ShowTableToolsPopup => ShowParameterToolsPopup;

    public bool ShowTableEditorOverlay => ShowParameterEditorOverlay;

    public string TableToolsHeaderText => ParameterToolsHeaderText;

    public string TableRowsHeaderText => ParameterRowsHeaderText;

    public string TableDetailsHeaderText => ParameterDetailsHeaderText;

    public string TableCountText => ParameterCountSummaryText;

    public string TableFilterText => ParameterFilterSummaryText;

    public string TableDownloadText => ParameterDownloadSummaryText;

    public string TableMetadataText => ParameterMetadataHintText;

    public string TableFavoritesText => FavoritesStatusText;

    public string TableSelectionText => SelectionSummaryText;

    public string TableToolsText => ToolsSummaryText;

    public bool ShowTableSelectionText => true;

    public bool ShowTableToolsText => true;

    public bool ShowTableFavoritesText => true;

    public bool ShowTableCountText => true;

    public bool ShowTableFilterText => true;

    public bool ShowTableDownloadText => true;

    public bool ShowTableMetadataText => true;

    public bool ShowTableRowsCountText => true;

    public string TableRowsCountLabelText => TableRowsCountText;

    public bool ShowTableRowsCountLabel => true;

    public string TableFootnoteText => FavoriteColumnHintText;

    public bool ShowTableFootnoteText => true;

    public bool ShowTableNoResults => ShowTableEmptyState;

    public string TableNoResultsText => TableEmptyStateText;

    public bool ShowTableHeadersRow => ShowTableRows;

    public bool ShowTableRowsBody => ShowTableRows;

    public bool ShowTableFavoriteLegend => true;

    public string TableFavoriteLegendText => FavoriteColumnHintText;

    public bool ShowTableFavoriteLegendText => true;

    public bool ShowTableSelectedSummary => SelectedRow is not null;

    public string TableSelectedSummaryText => SelectedParameterFavoriteStatusText;
    public string ParameterToolsStatusText => CanClearFavorites
        ? $"Favorites {_favoriteParameterNames.Count}"
        : "Tools ready";

    public string ParameterTableHeaderText => "Parameters";

    public string ParameterDetailHeaderText => "Details";

    public string ParameterListEmptyText => HasParameters ? "No parameters match the current filter." : "No active vehicle parameters";

    public bool HasVisibleRows => VisibleParameterRows.Count > 0;

    public string SelectedParameterType => SelectedRow?.Type ?? string.Empty;

    public string SelectedParameterCacheState => SelectedRow?.CacheState ?? string.Empty;

    public string SelectedParameterWriteError => SelectedRow?.WriteError ?? string.Empty;

    public bool ShowSelectedParameterWriteError => !string.IsNullOrWhiteSpace(SelectedParameterWriteError);

    public bool ShowSelectedParameterRebootHint => !string.IsNullOrWhiteSpace(SelectedParameterRebootText);

    public bool ShowParameterToolsPopup => ShowToolsPopup;

    public bool ShowParameterList => HasVisibleRows;

    public bool ShowParameterEmptyState => !HasVisibleRows;

    public bool ShowDetailPanel => SelectedRow is not null;

    public bool ShowDetailEmptyState => SelectedRow is null;

    public string DetailEmptyText => "Select a parameter to inspect and edit it.";

    public bool ShowFavoriteAction => SelectedRow is not null;

    public bool ShowToolsButton => true;

    public bool ShowHideReadOnlyToggle => true;

    public bool ShowParameterDownloadState => true;

    public bool ShowClearFavoritesTool => CanClearFavorites;

    public bool ShowRebootTool => CanRebootVehicle;

    public bool ShowRefreshTool => HasParameters;

    public bool ShowGroupsTitle => ShowGroupPanel;

    public bool ShowParameterRowsTitle => true;

    public bool ShowParameterDetailTitle => true;

    public string ParameterSelectionSummary => SelectedRow is null ? DetailEmptyText : SelectedParameterTitle;

    public string FavoritesStatusText => CanClearFavorites ? $"{_favoriteParameterNames.Count} favorites" : "No favorites";

    public string ToolsButtonText => "Tools";

    public string ClearButtonText => "Clear";

    public string SearchPlaceholderText => "Search";

    public string HideReadOnlyText => "Hide read-only";

    public string FullListText => "Full List";

    public string ModifiedText => "Modified";

    public string FavoritesText => "Favorites";

    public string RefreshToolText => "Refresh";

    public string ClearFavoritesToolText => "Clear favorites";

    public string RebootToolText => "Reboot vehicle";

    public string EditButtonText => "Edit";

    public string GroupsTitleText => "Groups";

    public string ParameterCountText => $"Showing {VisibleParameterRows.Count}";

    public string DetailPanelTitleText => SelectedRow is null ? "Details" : SelectedParameterTitle;

    public string ParameterValueHeaderText => "Value";

    public string ParameterLabelHeaderText => "Label";

    public string ParameterNameHeaderText => "Name";

    public string ParameterTypeHeaderText => "Type";

    public string ParameterStateHeaderText => "State";

    public string ParameterStatusHeaderText => "Status";

    public string ParameterFilterModeText => ListMode.ToString();

    public string ParameterCurrentCategoryText => SelectedCategory;

    public string ParameterMetadataHintText => ParameterMetadataState;

    public bool ShowParameterMetadataHint => true;

    public bool ShowParameterSummary => true;

    public bool ShowFavoriteBadge => IsSelectedParameterFavorite;

    public bool ShowFavoriteStatusText => SelectedRow is not null;

    public string SelectedParameterFavoriteStatusText => IsSelectedParameterFavorite ? "Included in favorites filter." : "Not in favorites.";

    public bool ShowParameterWriteState => SelectedRow is not null;

    public bool ShowParameterCacheState => SelectedRow is not null;

    public bool ShowParameterType => SelectedRow is not null;

    public bool ShowParameterUnits => SelectedRow is not null;

    public bool ShowParameterRange => SelectedRow is not null;

    public bool ShowParameterEnumValues => SelectedRow is not null;

    public bool ShowParameterDescription => SelectedRow is not null;

    public bool ShowParameterValue => SelectedRow is not null;

    public bool ShowSelectedParameterName => SelectedRow is not null;

    public bool ShowSelectedParameterTitle => SelectedRow is not null;

    public bool ShowParameterEditButton => HasSelectedParameter;

    public bool ShowParameterFavoriteButton => SelectedRow is not null;

    public bool ShowParameterToolsStatus => true;

    public bool ShowParameterCount => true;

    public bool ShowParameterFilterSummary => true;

    public bool ShowParameterDownloadSummary => true;

    public bool ShowCategoryPanel => ShowGroupPanel;

    public bool ShowFavoritesSummary => true;

    public bool ShowParameterSearchHeader => true;

    public bool ShowParameterModeTabs => true;

    public bool ShowParameterToolbar => true;

    public bool ShowParameterHeader => true;

    public bool ShowParameterContent => true;

    public bool ShowParameterEditorDialog => ShowParameterEditor;

    public string ParameterEditorTitleText => "Edit Parameter";

    public string ParameterEditorRangeText => SelectedParameterRange;

    public string ParameterEditorSaveText => "Save";

    public string ParameterEditorCancelText => "Cancel";

    public string ParameterEditorDescriptionText => SelectedParameterDescription;

    public bool ShowParameterEditorRange => !string.IsNullOrWhiteSpace(SelectedParameterRange);

    public bool ShowParameterEditorDescription => !string.IsNullOrWhiteSpace(SelectedParameterDescription);

    public bool ShowSelectedParameterFavoriteHint => SelectedRow is not null;

    public string SelectedParameterFavoriteHintText => IsSelectedParameterFavorite ? "Shown in Favorites." : "Use Favorites to pin common parameters.";

    public bool ShowParameterToolsPanel => ShowToolsPopup;

    public bool ShowParameterGroupPanel => ShowGroupPanel;

    public bool ShowParameterDetailPanel => true;

    public bool ShowParameterRowsPanel => true;

    public bool ShowParameterRowsHeader => true;

    public bool ShowParameterDetailHeader => true;

    public bool ShowParameterToolsHeader => ShowToolsPopup;

    public string ParameterToolsHeaderText => "Tools";

    public string ParameterEmptyStateText => ParameterListEmptyText;

    public bool ShowParameterEditorOverlay => ShowParameterEditor;

    public bool ShowFavoriteFilterHint => ListMode == ParameterListMode.Favorites;

    public string FavoriteFilterHintText => CanClearFavorites ? FavoritesStatusText : "No favorites selected.";

    public bool ShowModifiedFilterHint => ListMode == ParameterListMode.Modified;

    public string ModifiedFilterHintText => "Modified shows pending writes and non-default write states.";

    public bool ShowSearchFilterHint => IsSearchFilterActive;

    public string SearchFilterHintText => IsSearchFilterActive ? "Groups hidden while filtering." : string.Empty;

    public bool ShowSelectionSummary => true;

    public bool ShowToolsSummary => true;

    public bool ShowFavoriteToolsState => true;

    public string FavoriteToolsStateText => FavoritesStatusText;

    public string ToolsSummaryText => ParameterToolsStatusText;

    public string SelectionSummaryText => ParameterSelectionSummary;

    public string ParameterValueDisplayText => SelectedParameterValue;

    public string ParameterUnitsDisplayText => SelectedParameterUnits;

    public string ParameterRangeDisplayText => SelectedParameterRange;

    public string ParameterEnumDisplayText => SelectedParameterEnumValues;

    public string ParameterWriteStateDisplayText => SelectedParameterWriteState;

    public string ParameterDescriptionDisplayText => SelectedParameterDescription;

    public string ParameterNameDisplayText => SelectedParameterName;

    public string ParameterTitleDisplayText => SelectedParameterTitle;

    public string ParameterTypeDisplayText => SelectedParameterType;

    public string ParameterCacheDisplayText => SelectedParameterCacheState;

    public string ParameterWriteErrorDisplayText => SelectedParameterWriteError;

    public string ParameterRebootDisplayText => SelectedParameterRebootText;

    public string ParameterFavoriteDisplayText => SelectedParameterFavoriteStatusText;

    public string ParameterFavoriteHeaderText => "Fav";

    public string ParameterNameColumnText => "Name";

    public string ParameterLabelColumnText => "Label";

    public string ParameterTypeColumnText => "Type";

    public string ParameterValueColumnText => "Value";

    public string ParameterStateColumnText => "State";

    public string ParameterRowsHeaderText => ParameterTableHeaderText;

    public string ParameterDetailsHeaderText => ParameterDetailHeaderText;

    public string ParameterGroupHeaderText => GroupsTitleText;

    public string ParameterToolsButtonText => ToolsButtonText;

    public string ParameterFilterSummaryText => $"{ParameterFilterModeText} / {ParameterCurrentCategoryText}";

    public string ParameterDownloadSummaryText => ParameterDownloadState;

    public string ParameterCountSummaryText => ParameterCountText;

    public string ParameterToolsSummaryLabelText => ToolsSummaryText;

    public string ParameterSelectionSummaryLabelText => SelectionSummaryText;
    public bool ShowParameterEditor
    {
        get => _showParameterEditor;
        private set => this.RaiseAndSetIfChanged(ref _showParameterEditor, value);
    }

    public string EditText
    {
        get => _editText;
        set => this.RaiseAndSetIfChanged(ref _editText, value);
    }

    public void ShowFullList() => ListMode = ParameterListMode.Full;

    public void ShowModifiedList() => ListMode = ParameterListMode.Modified;

    public void ShowFavoritesList() => ListMode = ParameterListMode.Favorites;

    public void ClearSearch() => SearchText = string.Empty;

    public void SelectCategory(string category) => SelectedCategory = category;

    public void SelectRow(ParameterDisplayRow? row)
    {
        SelectedRow = row;
        EditText = row?.Value ?? string.Empty;
        this.RaisePropertyChanged(nameof(HasSelectedParameter));
        this.RaisePropertyChanged(nameof(IsSelectedParameterFavorite));
        this.RaisePropertyChanged(nameof(SelectedParameterFavoriteText));
        this.RaisePropertyChanged(nameof(SelectedParameterFavoriteStatusText));
        this.RaisePropertyChanged(nameof(SelectedParameterRebootText));
        this.RaisePropertyChanged(nameof(SelectedParameterType));
        this.RaisePropertyChanged(nameof(SelectedParameterCacheState));
        this.RaisePropertyChanged(nameof(SelectedParameterWriteError));
        this.RaisePropertyChanged(nameof(ShowSelectedParameterWriteError));
        this.RaisePropertyChanged(nameof(ShowSelectedParameterRebootHint));
    }

    public void ToggleToolsMenu() => ShowToolsMenu = !ShowToolsMenu;

    public void RefreshParameters()
    {
        Refresh();
    }

    public void ClearFavorites()
    {
        _favoriteParameterNames.Clear();
        this.RaisePropertyChanged(nameof(ParameterRows));
        this.RaisePropertyChanged(nameof(IsSelectedParameterFavorite));
        this.RaisePropertyChanged(nameof(CanClearFavorites));
        this.RaisePropertyChanged(nameof(FavoritesStatusText));
    }

    public void ToggleSelectedFavorite()
    {
        if (SelectedRow is null)
        {
            return;
        }

        if (!_favoriteParameterNames.Add(SelectedRow.Name))
        {
            _favoriteParameterNames.Remove(SelectedRow.Name);
        }

        this.RaisePropertyChanged(nameof(ParameterRows));
        this.RaisePropertyChanged(nameof(IsSelectedParameterFavorite));
        this.RaisePropertyChanged(nameof(SelectedParameterFavoriteText));
        this.RaisePropertyChanged(nameof(SelectedParameterFavoriteStatusText));
        this.RaisePropertyChanged(nameof(CanClearFavorites));
        this.RaisePropertyChanged(nameof(FavoritesStatusText));
    }

    public void RebootVehicle()
    {
        LastParameterEditStatusText = CanRebootVehicle
            ? "Vehicle reboot requested from Parameter Tools."
            : "No active vehicle to reboot.";
    }

    public void OpenEditor()
    {
        if (SelectedRow is null)
        {
            return;
        }

        EditText = SelectedRow.Value;
        ShowParameterEditor = true;
    }

    public void CancelEditor() => ShowParameterEditor = false;

    public void SaveEditor()
    {
        if (SelectedRow is null)
        {
            return;
        }

        CommitParameterEdit(SelectedRow.ComponentId, SelectedRow.Name, EditText);
        ShowParameterEditor = false;
    }

    public void RefreshSelection()
    {
        if (SelectedRow is null)
        {
            return;
        }

        SelectedRow = ParameterRows.FirstOrDefault(row => row.ComponentId == SelectedRow.ComponentId && string.Equals(row.Name, SelectedRow.Name, StringComparison.Ordinal));
        this.RaisePropertyChanged(nameof(SelectedParameterTitle));
        this.RaisePropertyChanged(nameof(SelectedParameterName));
        this.RaisePropertyChanged(nameof(SelectedParameterDescription));
        this.RaisePropertyChanged(nameof(SelectedParameterValue));
        this.RaisePropertyChanged(nameof(SelectedParameterUnits));
        this.RaisePropertyChanged(nameof(SelectedParameterRange));
        this.RaisePropertyChanged(nameof(SelectedParameterEnumValues));
        this.RaisePropertyChanged(nameof(SelectedParameterWriteState));
        this.RaisePropertyChanged(nameof(HasSelectedParameter));
    }

    public string ParameterSummary
    {
        get
        {
            var vehicle = _multiVehicleManager.ActiveVehicle;
            if (vehicle is null)
            {
                return "No active vehicle parameters";
            }

            var manager = vehicle.ParameterManager;
            var rows = ParameterRows;
            var metadataCount = rows.Count(static row => row.HasMetadata);
            return $"Vehicle {vehicle.Id} | Params {manager.Count} | Showing {rows.Count} | Metadata {metadataCount}/{rows.Count} | Progress {Math.Round(manager.LoadProgress * 100, 1):F1}% | Pending writes {manager.PendingWriteCount}";
        }
    }

    public string ParameterMetadataState
    {
        get
        {
            var rows = ParameterRows;
            if (rows.Count == 0)
            {
                return "No parameter metadata loaded";
            }

            var metadataCount = rows.Count(static row => row.HasMetadata);
            return metadataCount == 0
                ? "No parameter metadata matches active vehicle parameters"
                : $"Metadata matched {metadataCount}/{rows.Count} parameters";
        }
    }

    public string LastParameterEditStatusText
    {
        get => _lastParameterEditStatusText;
        private set => this.RaiseAndSetIfChanged(ref _lastParameterEditStatusText, value);
    }

    public string ParameterDownloadState
    {
        get
        {
            var manager = _multiVehicleManager.ActiveVehicle?.ParameterManager;
            if (manager is null)
            {
                return "No parameter download active";
            }

            if (manager.IsParameterRequestActive)
            {
                return $"Downloading {manager.ReceivedParameterCount}/{manager.ExpectedParameterCount}";
            }

            if (manager.ParametersReady)
            {
                return $"Ready {manager.ReceivedParameterCount}/{manager.ExpectedParameterCount}";
            }

            if (manager.MissingParameters)
            {
                return $"Missing parameters {manager.ReceivedParameterCount}/{manager.ExpectedParameterCount}";
            }

            return "No parameter download active";
        }
    }

    public bool HasParameters => _multiVehicleManager.ActiveVehicle?.ParameterManager.Count > 0;

    private void Refresh()
    {
        this.RaisePropertyChanged(nameof(ParameterRows));
        this.RaisePropertyChanged(nameof(Categories));
        this.RaisePropertyChanged(nameof(ParameterSummary));
        this.RaisePropertyChanged(nameof(ParameterMetadataState));
        this.RaisePropertyChanged(nameof(LastParameterEditStatusText));
        this.RaisePropertyChanged(nameof(ParameterDownloadState));
        this.RaisePropertyChanged(nameof(HasParameters));
        RefreshSelection();
    }

    private bool MatchesMode(ParameterDisplayRow row)
    {
        return ListMode switch
        {
            ParameterListMode.Modified => row.IsPendingWrite || !string.Equals(row.WriteStatus, "None", StringComparison.OrdinalIgnoreCase),
            ParameterListMode.Favorites => _favoriteParameterNames.Contains(row.Name),
            _ => true
        };
    }

    private bool MatchesCategory(ParameterDisplayRow row)
    {
        if (string.Equals(SelectedCategory, "All", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var group = string.IsNullOrWhiteSpace(row.Group) ? "Ungrouped" : row.Group;
        return string.Equals(group, SelectedCategory, StringComparison.OrdinalIgnoreCase);
    }

    private bool MatchesReadOnly(ParameterDisplayRow row)
    {
        return !HideReadOnly || !string.Equals(row.CacheState, "Missing", StringComparison.OrdinalIgnoreCase);
    }

    public void SetParameterMetadataCatalog(IParameterMetadataCatalog metadataCatalog)
    {
        _metadataCatalog = metadataCatalog;
        Refresh();
    }

    public ParameterEditCommitResult CommitParameterEdit(int componentId, string name, string text)
    {
        var manager = _multiVehicleManager.ActiveVehicle?.ParameterManager;
        if (manager is null)
        {
            LastParameterEditStatusText = "No active vehicle for parameter edit.";
            return ParameterEditCommitResult.Rejected(LastParameterEditStatusText);
        }

        var result = _editService.Commit(manager, componentId, name, text, _metadataCatalog);
        if (result.Accepted && result.Fact is not null)
        {
            result.Fact.SetRawValue(result.ParsedValue);
            SendParameterWrite(_multiVehicleManager.ActiveVehicle, componentId, result.Fact);
        }

        LastParameterEditStatusText = result.StatusText;
        Refresh();
        return result;
    }

    private static void SendParameterWrite(Vehicle? vehicle, int componentId, Fact fact)
    {
        if (vehicle?.LinkManager.ActiveLink is not { IsConnected: true, CanSend: true } link || fact.RawValue is null)
        {
            return;
        }

        var service = new MavlinkParameterService();
        var value = Convert.ToSingle(fact.RawValue, CultureInfo.InvariantCulture);
        service.SendParamSetWithReadbackAsync(
            link,
            new MavlinkParameterSet(vehicle.Id, (byte)componentId, fact.Name, value, ToMavlinkParamType(fact.MetaData.ValueType))).GetAwaiter().GetResult();
    }

    private static MavlinkParamType ToMavlinkParamType(FactValueType valueType)
    {
        return valueType switch
        {
            FactValueType.Int32 => MavlinkParamType.Int32,
            FactValueType.UInt32 => MavlinkParamType.UInt32,
            FactValueType.Double => MavlinkParamType.Real64,
            _ => MavlinkParamType.Real32
        };
    }

    private static IReadOnlyList<ParameterDisplayRow> BuildRows(
        Vehicle? vehicle,
        IParameterMetadataCatalog metadataCatalog,
        string searchText)
    {
        if (vehicle is null)
        {
            return [];
        }

        return ParameterProjection.BuildRows(vehicle.ParameterManager, metadataCatalog, searchText);
    }
}
