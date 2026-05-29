using ChainPOS.ViewModels.Owner.Products;

namespace ChainPOS.Services.Owner;

public interface IOwnerProductService
{
    Task<ProductIndexViewModel> GetProductsAsync(
        string? search,
        Guid? categoryId,
        string? status,
        CancellationToken cancellationToken = default);

    Task<ProductFormViewModel> GetCreateFormAsync(
        ProductFormViewModel? model = null,
        CancellationToken cancellationToken = default);

    Task<ProductFormViewModel?> GetProductFormAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ProductDetailsViewModel?> GetProductDetailsAsync(Guid id, CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string? Error, Guid? ProductId)> CreateProductAsync(
        ProductFormViewModel model,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string? Error)> UpdateProductAsync(
        Guid id,
        ProductFormViewModel model,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string? Error)> ToggleProductAsync(
        Guid id,
        bool isActive,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string? Error)> DeleteProductAsync(Guid id, CancellationToken cancellationToken = default);
}
