using ChainPOS.ViewModels.Sales;

namespace ChainPOS.Services.Sales;

public interface IPaymentService
{
    Task<PaymentIndexViewModel> GetPaymentsAsync(
        string areaName,
        Guid? storeId,
        string? search,
        string? method,
        string? status,
        DateOnly? date,
        CancellationToken cancellationToken = default);
}
