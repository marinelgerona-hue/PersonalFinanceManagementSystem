using Microsoft.EntityFrameworkCore;
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
builder.Services.AddScoped<IIncomeService, IncomeService>();


builder.Services.AddScoped<IExpenseService, ExpenseService>();

builder.Services.AddScoped<IBudgetService, BudgetService>();

builder.Services.AddScoped<ISavingsGoalService, SavingsGoalService>();

builder.Services.AddScoped<IReportService, ReportService>();
var app = builder.Build();

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

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();