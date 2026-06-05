using ChainPOS.ViewModels.Imports;
using Microsoft.AspNetCore.Http;

namespace ChainPOS.Services.Import;

public interface IBulkImportService
{
    Task<BulkImportResultViewModel> ImportOwnersAsync(
        IFormFile file,
        string? currentUserId,
        CancellationToken cancellationToken = default);

    Task<BulkImportResultViewModel> ImportStaffAsync(
        IFormFile file,
        CancellationToken cancellationToken = default);

    Task<BulkImportResultViewModel> ImportStoresAsync(
        IFormFile file,
        CancellationToken cancellationToken = default);

    Task<BulkImportResultViewModel> ImportCategoriesAsync(
        IFormFile file,
        CancellationToken cancellationToken = default);

    Task<BulkImportResultViewModel> ImportProductsAsync(
        IFormFile file,
        CancellationToken cancellationToken = default);

    Task<BulkImportResultViewModel> ImportStoreProductsAsync(
        IFormFile file,
        CancellationToken cancellationToken = default);

    Task<BulkImportResultViewModel> ImportInventoryAsync(
        string areaName,
        IFormFile file,
        CancellationToken cancellationToken = default);
}
