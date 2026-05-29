using System.ComponentModel.DataAnnotations;
using ChainPOS.Constants;

namespace ChainPOS.ViewModels.Admin.SubscriptionPlans;

public sealed class SubscriptionPlanFormViewModel
{
    public Guid? Id { get; set; }

    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [Range(0, double.MaxValue, ErrorMessage = "Price must be greater than or equal to 0.")]
    public decimal Price { get; set; }

    [Required]
    public string BillingCycle { get; set; } = BillingCycles.Monthly;

    [Range(0, int.MaxValue, ErrorMessage = "Max stores must be greater than or equal to 0.")]
    public int? MaxStores { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "Max staff must be greater than or equal to 0.")]
    public int? MaxStaff { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "Max products must be greater than or equal to 0.")]
    public int? MaxProducts { get; set; }

    public bool IsActive { get; set; } = true;
}
