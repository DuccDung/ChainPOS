namespace ChainPOS.ViewModels.Imports;

public class BulkImportUploadViewModel
{
    public string Title { get; set; } = string.Empty;

    public string AreaName { get; set; } = string.Empty;

    public string ControllerName { get; set; } = string.Empty;

    public string ActionName { get; set; } = string.Empty;

    public string TemplateAction { get; set; } = string.Empty;

    public string BackAction { get; set; } = "Index";
}
