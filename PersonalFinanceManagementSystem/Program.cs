using Microsoft.EntityFrameworkCore;
using PersonalFinanceManagementSystem;
using PersonalFinanceManagementSystem.Data;
using PersonalFinanceManagementSystem.Services;

var builder = WebApplication.CreateBuilder(args);

var connectionString =
    builder.Configuration.GetConnectionString("DefaultConnection");

// MySQL Database
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySQL(connectionString!));

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));

// Session keeps the signed-in user id.
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(12);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});
builder.Services.AddSingleton<InMemoryDataStore>();

// Database-backed services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IIncomeService, IncomeService>();


builder.Services.AddScoped<IExpenseService, ExpenseService>();

builder.Services.AddScoped<IBudgetService, BudgetService>();

builder.Services.AddScoped<ISavingsGoalService, SavingsGoalService>();

builder.Services.AddScoped<IReportService, ReportService>();

builder.Services.AddScoped<IRecurringTransactionService, RecurringTransactionService>();
builder.Services.AddHostedService<RecurringTransactionBackgroundService>();

// Admin console
builder.Services.AddScoped<IActivityLogger, ActivityLogger>();
builder.Services.AddScoped<IAdminService, AdminService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();

var app = builder.Build();

// Apply migrations and seed the default admin + category catalog (idempotent).
StartupSeeder.Run(app.Services);

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseSession();

app.UseAuthorization();

app.MapStaticAssets();

// The separate Admin side lives in the "Admin" area and matches first, so
// its own controllers (AdminDashboard/Users/…) never collide with user routes.
app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();