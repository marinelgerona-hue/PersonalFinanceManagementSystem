using Microsoft.EntityFrameworkCore;
using PersonalFinanceManagementSystem.Data;
using PersonalFinanceManagementSystem.Models;
using PersonalFinanceManagementSystem.Services;

namespace PersonalFinanceManagementSystem;

/// <summary>
/// Applies pending EF migrations and makes sure a working default administrator
/// account and the standard category catalog exist. Everything is idempotent —
/// it only fills gaps and never overwrites existing data.
/// </summary>
public static class StartupSeeder
{
    /// <summary>Sign in with this account on a fresh database to reach the Admin Console.</summary>
    public const string DefaultAdminEmail = "admin@finapp.com";

    public static void Run(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        try
        {
            db.Database.Migrate();
        }
        catch
        {
            // Applying migrations at startup must never block the app when the
            // database is temporarily unreachable; `dotnet ef database update`
            // can always be run manually.
        }

        EnsureAdmin(db);
        EnsureDefaultCategories(db);
    }

    private static void EnsureAdmin(ApplicationDbContext db)
    {
        var email = DefaultAdminEmail;
        if (db.Users.Any(u => u.Email == email))
            return;

        var salt = SecurityHelper.NewSalt();
        var admin = new User
        {
            FullName = "System Administrator",
            Email = email,
            Role = UserRoles.Admin,
            IsActive = true,
            PasswordSalt = salt,
            PasswordHash = SecurityHelper.Hash("Admin@12345", salt),
            CreatedAt = DateTime.Now
        };
        db.Users.Add(admin);
        db.SaveChanges();
    }

    private static void EnsureDefaultCategories(ApplicationDbContext db)
    {
        if (db.Categories.Any())
            return;

        // The catalog starts as the app's built-in lists, so the Admin Console
        // manages the same categories users were already choosing from.
        foreach (var name in Catalog.IncomeSources)
            db.Categories.Add(new Category { Type = CategoryType.Income, Name = name });
        foreach (var name in Catalog.ExpenseCategories)
            db.Categories.Add(new Category { Type = CategoryType.Expense, Name = name });

        db.SaveChanges();
    }
}
