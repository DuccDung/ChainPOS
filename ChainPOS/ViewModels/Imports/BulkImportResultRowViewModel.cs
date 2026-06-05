namespace ChainPOS.ViewModels.Imports;

public sealed class BulkImportResultRowViewModel
{
    public int RowNumber { get; set; }

    public bool Succeeded { get; set; }

    public string Message { get; set; } = string.Empty;
}
