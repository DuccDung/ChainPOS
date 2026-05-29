using ChainPOS.ViewModels.Inventory;

namespace ChainPOS.Services.Inventory;

public interface IInventoryService
{
    Task<InventoryIndexViewModel> GetInventoryAsync(
        string areaName,
        Guid? storeId,
        string? search,
        string? stockStatus,
        CancellationToken cancellationToken = default);

    Task<InventoryMovementViewModel> GetImportFormAsync(
        string areaName,
        InventoryMovementViewModel? model = null,
        CancellationToken cancellationToken = default);

    Task<InventoryMovementViewModel> GetExportFormAsync(
        string areaName,
        InventoryMovementViewModel? model = null,
        CancellationToken cancellationToken = default);

    Task<InventoryAdjustViewModel> GetAdjustFormAsync(
        string areaName,
        InventoryAdjustViewModel? model = null,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string? Error)> ImportStockAsync(
        InventoryMovementViewModel model,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string? Error)> ExportStockAsync(
        InventoryMovementViewModel model,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string? Error)> AdjustStockAsync(
        InventoryAdjustViewModel model,
        CancellationToken cancellationToken = default);
}
