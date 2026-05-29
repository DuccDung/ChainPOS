using ChainPOS.ViewModels.Owner.Categories;

namespace ChainPOS.Services.Owner;

public interface IOwnerCategoryService
{
    Task<CategoryIndexViewModel> GetCategoriesAsync(string? search, string? status, CancellationToken cancellationToken = default);

    Task<CategoryDetailsViewModel?> GetCategoryDetailsAsync(Guid id, CancellationToken cancellationToken = default);

    Task<CategoryFormViewModel?> GetCategoryFormAsync(Guid id, CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string? Error, Guid? CategoryId)> CreateCategoryAsync(CategoryFormViewModel model, CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string? Error)> UpdateCategoryAsync(Guid id, CategoryFormViewModel model, CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string? Error)> ToggleCategoryAsync(Guid id, bool isActive, CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string? Error)> DeleteCategoryAsync(Guid id, CancellationToken cancellationToken = default);
}
