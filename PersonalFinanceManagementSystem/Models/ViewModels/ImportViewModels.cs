using PersonalFinanceManagementSystem.Helpers;

namespace PersonalFinanceManagementSystem.Models.ViewModels;

/// <summary>What Views/Import/Preview.cshtml needs: the parsed result plus the
/// currency to format amounts with.</summary>
public class ImportPreviewViewModel
{
    public ImportResult Result { get; set; } = new();
    public string Currency { get; set; } = "USD";
}
