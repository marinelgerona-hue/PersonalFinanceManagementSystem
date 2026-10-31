using PersonalFinanceManagementSystem.Data;
using PersonalFinanceManagementSystem.Models;

namespace PersonalFinanceManagementSystem.Services;

public interface ICategoryService
{
    /// <summary>The category names users can currently pick from.
    /// Falls back to the built-in catalog when the database has nothing yet.</summary>
    List<string> GetActive(CategoryType type);
}

/// <summary>Reads the admin-managed category catalog for the user-side forms.</summary>
public class CategoryService : ICategoryService
{
    private readonly ApplicationDbContext _db;

    public CategoryService(ApplicationDbContext db) => _db = db;

    public List<string> GetActive(CategoryType type)
    {
        try
        {
            var names = _db.Categories
                .Where(c => c.Type == type && c.IsActive)
                .OrderBy(c => c.Name)
                .Select(c => c.Name)
                .ToList();
            if (names.Count > 0) return names;
        }
        catch
        {
            // The database being briefly unreachable must not break form pages.
        }

        return type == CategoryType.Expense
            ? Catalog.ExpenseCategories.ToList()
            : Catalog.IncomeSources.ToList();
    }
}
