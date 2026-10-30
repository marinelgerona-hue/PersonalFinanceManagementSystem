---
name: pfms-dev
description: Run, build, seed and export-test the Personal Finance Management System (.NET 10 MVC + MySQL). Use for "run this project", "start/stop the app", "seed demo data", "add a migration", "export xlsx", or any question about this repo's codebase layout and known gotchas.
metadata:
  version: 1.0
---

# PFMS — run, build, data, codebase map

Repo root is the directory containing `PersonalFinanceManagementSystem.slnx`.
Project folder (same name, nested one level down): `PersonalFinanceManagementSystem/` — `cd` here for all `dotnet` commands.
Solution file: `PersonalFinanceManagementSystem.slnx` at repo root.

## Running it

.NET SDK is installed locally at `~/.dotnet`, not system-wide — every shell needs:
```bash
export PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet"
```

MySQL: the skill's Docker note is stale on this machine — Docker Desktop isn't installed. The `PersonalFinanceManagementDb` database lives on the **native MySQL Server 8.0** (service `MySQL80`), host port **3306**, user `root`, password `12345`. Client: `"/c/Program Files/MySQL/MySQL Server 8.0/bin/mysql.exe" -h 127.0.0.1 -P 3306 -uroot -p12345 PersonalFinanceManagementDb`. If connection is refused, start the service: `powershell -Command "Start-Service MySQL80"`.

Build + run (from the inner project folder):
```bash
dotnet build
ConnectionStrings__DefaultConnection="Server=127.0.0.1;Port=3307;Database=PersonalFinanceManagementDb;User=root;Password=12345;" \
  dotnet run --no-build --launch-profile http
```
Run this in the background and poll `curl -s -o /dev/null -w '%{http_code}' 127.0.0.1:5003/Account/Login` until it returns 200.

**Port 5003 gotcha**: an unrelated pre-existing Node.js process ("MASTERCLEAN API v4") also listens on `*:5003` on this machine. It is not part of this project — never kill it. Always browse/curl `127.0.0.1:5003` explicitly (not bare `localhost`) to reliably hit the .NET app, not the Node one. To stop only this app: `pkill -f "PersonalFinanceManagementSystem"`.

EF Core migrations (`dotnet-ef` must be installed globally: `dotnet tool install -g dotnet-ef`):
```bash
dotnet ef database update
```

## Seed / demo data

`scripts/seed-sample-data.sql` generates realistic ~6-month demo data (income, expenses, budgets, goals) for one account. It's idempotent per-account and meant to be piped in with the target email substituted first:
```bash
{ echo "SET @email = 'demo.preview@example.com';"; cat scripts/seed-sample-data.sql; } | \
  docker exec -i pfms-mysql mysql -uroot -p12345 PersonalFinanceManagementDb
```
Never run it against a real/live user's account — it replaces that account's data for the affected modules.

## Codebase map

- `Controllers/` — one per entity (Income, Expenses, Budgets, Goals, Reports, Settings, Dashboard, Account) + `AppControllerBase` (shared base: `CurrentUser`, `BuildNavState`, `RedirectToReturnUrl`).
- `Views/<Entity>/*.cshtml` + `Views/Shared/` for `_Layout`, `_ListToolbar`, `_Pagination`, `_SideNav`, `_AuthAside`.
- `Services/` — business logic behind controllers (e.g. `ExpenseService`), injected via interfaces.
- `Models/` + `Models/ViewModels/` — EF entities vs. page-specific view models.
- `Helpers/` — `Format.cs` (money/percent/category-color formatting), `ChartTip.cs` (chart tooltip JSON), `ListHtml.cs` (sort-header/query-string helpers), `XlsxFile.cs` (Excel export writer, see below).
- `wwwroot/css/site.css` — single large shared stylesheet (~1700+ lines), no per-page CSS files.
- `wwwroot/js/site.js` — vanilla JS, no framework/bundler, no npm build step for the app itself.

Git note: this repo's local git identity is deliberately set to `Marinel Quimson` (differs from the global/session identity) — intentional, don't "fix" it. Commits on the `v2` branch were made without any `Co-Authored-By` trailer per explicit user instruction for that branch; don't assume that holds for branches created later unless told again.

## Excel (.xlsx) exports

All exports (Income, Expenses, and every Reports sub-page) produce real `.xlsx` files via a **hand-rolled OOXML writer**, `Helpers/XlsxFile.cs` — no NuGet dependency (no ClosedXML/EPPlus). Don't reach for a package; extend this class instead.

Usage pattern:
```csharp
var xlsx = new Helpers.XlsxFile(Helpers.Format.Symbol(CurrentUser.Currency),
    new[] { "Date", "Category", "Amount" },
    Helpers.XlsxColumn.Text, Helpers.XlsxColumn.Text, Helpers.XlsxColumn.Currency);
foreach (var row in items) xlsx.AddRow(row.Date, row.Category, row.Amount);
xlsx.AddTotalRow("", "Total", items.Sum(r => r.Amount));
// Optional second stacked table on the same sheet:
xlsx.AddSection(new[] { "Month", "Income", "Expenses" },
    Helpers.XlsxColumn.Text, Helpers.XlsxColumn.Currency, Helpers.XlsxColumn.Currency);
return xlsx.ToFileResult("expenses-2026-10.xlsx");
```
`XlsxColumn` is `Text | Currency | Integer | Percent`. The writer auto-applies: brand green bold header row, zebra striping (odd/even), currency/thousands/percent number formats, thin borders, a frozen header pane, and `AutoFilter` (only enabled when the sheet has exactly one section — multi-section sheets skip it since AutoFilter can't span two header rows).

**Verification method that was used and should be repeated for any new export**: download the file via curl with a logged-in session cookie, then validate with Python's `openpyxl` (an independent library, not this app's own code) — check cell types, `number_format` strings, fill colors, bold header, `freeze_panes`, and `auto_filter`. Don't just check file size > 0. Run the validation script without `-I` only because it's your own trusted code reading (not executing) a data file — still write it to the scratchpad, not the repo.

## Known-fixed bug patterns — don't reintroduce these

- **Lost query string on redirect**: any mutating POST (add/delete/add-funds) on a paginated/sorted/searched list page must accept a hidden `returnUrl` field (`Context.Request.Path + Context.Request.QueryString`) and redirect via `AppControllerBase.RedirectToReturnUrl(returnUrl)`, not a bare `RedirectToAction(nameof(Index))` — otherwise the user's sort/search/page resets. This was fixed on Goals; Income/Expenses/Budgets controllers may still need the same fix if asked.
- **Percent/Count silently zero**: category breakdowns must call the `CategorySliceExtensions.WithPercentOf()` helper after building raw amounts, or `Percent`/`Count` stay 0.
- **Prefixed form binding**: a sub-form posting fields like `changePassword.CurrentPassword` needs the controller action parameter bound with `[Bind(Prefix = "changePassword")]`, or validation silently always fails with no visible error.
- **String vs numeric enum comparison in JS**: don't compare a `<select>`'s string value directly against a C# enum's numeric index — map string→index explicitly first.
