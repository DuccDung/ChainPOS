namespace ChainPOS.ViewModels.Imports;

public sealed class BulkImportResultViewModel
{
    public string Title { get; set; } = "Bulk Import";

    public string AreaName { get; set; } = string.Empty;

    public string BackController { get; set; } = string.Empty;

    public string BackAction { get; set; } = "Index";

    public int TotalRows => Rows.Count;

    public int SuccessRows => Rows.Count(x => x.Succeeded);

    public int FailedRows => Rows.Count(x => !x.Succeeded);

    public List<BulkImportResultRowViewModel> Rows { get; set; } = new();
}
