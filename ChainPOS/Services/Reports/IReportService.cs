using ChainPOS.ViewModels.Reports;

namespace ChainPOS.Services.Reports;

public interface IReportService
{
    Task<ReportsIndexViewModel> GetReportsAsync(
        string areaName,
        ReportsFilterViewModel? filter,
        CancellationToken cancellationToken = default);

    Task<(byte[] Content, string FileName, string ContentType)> ExportReportsAsync(
        string areaName,
        ReportsFilterViewModel? filter,
        CancellationToken cancellationToken = default);
}
