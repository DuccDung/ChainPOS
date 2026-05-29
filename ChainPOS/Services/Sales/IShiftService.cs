using ChainPOS.ViewModels.Sales;

namespace ChainPOS.Services.Sales;

public interface IShiftService
{
    Task<ShiftIndexViewModel> GetShiftsAsync(
        string areaName,
        Guid? storeId,
        string? status,
        CancellationToken cancellationToken = default);

    Task<ShiftOpenViewModel> GetOpenFormAsync(
        string areaName,
        ShiftOpenViewModel? model = null,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string? Error, Guid? ShiftId)> OpenShiftAsync(
        ShiftOpenViewModel model,
        CancellationToken cancellationToken = default);

    Task<ShiftCloseViewModel?> GetCloseFormAsync(
        string areaName,
        Guid id,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string? Error)> CloseShiftAsync(
        Guid id,
        ShiftCloseViewModel model,
        CancellationToken cancellationToken = default);
}
