using Microsoft.AspNetCore.Mvc;
using PersonalFinanceManagementSystem.Models;
using PersonalFinanceManagementSystem.Models.ViewModels;
using PersonalFinanceManagementSystem.Services;

namespace PersonalFinanceManagementSystem.Controllers;

/// <summary>Savings goals — create, delete, add funds; progress computed automatically.</summary>
public class GoalsController : AppControllerBase
{
    private readonly ISavingsGoalService _goals;

    public GoalsController(IAuthService auth, ISavingsGoalService goals) : base(auth)
        => _goals = goals;

    public IActionResult Index()
    {
        var now = DateTime.Now;
        var vm = new GoalListViewModel
        {
            Items = _goals.GetProgress(CurrentUser.Id),
            Currency = CurrentUser.Currency,
            ThisMonthSaved = _goals.SavedInMonth(CurrentUser.Id, now.Year, now.Month)
        };
        ViewData["Title"] = "Savings Goals";
        ViewData["ActiveNav"] = "goals";
        return View(vm);
    }

    // ------------------------------------------------------------ create

    [HttpGet]
    public IActionResult Create()
    {
        ViewData["Title"] = "New Savings Goal";
        ViewData["ActiveNav"] = "goals";
        return View("GoalForm", new GoalFormViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(GoalFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            ViewData["Title"] = "New Savings Goal";
            ViewData["ActiveNav"] = "goals";
            return View("GoalForm", model);
        }

        _goals.Add(new SavingsGoal
        {
            UserId = CurrentUser.Id,
            Name = model.Name,
            TargetAmount = model.TargetAmount,
            Deadline = model.Deadline,
            Note = model.Note ?? string.Empty
        });

        TempData["Success"] = $"Goal \"{model.Name}\" created. Add funds to start saving!";
        return RedirectToAction(nameof(Index));
    }

    // ------------------------------------------------------------ edit

    /// <summary>Edit a goal's name, target or target date. Countdown and the
    /// daily/weekly/monthly savings plan recalculate from the new values.</summary>
    [HttpGet]
    public IActionResult Edit(int id)
    {
        var goal = _goals.Get(CurrentUser.Id, id);
        if (goal is null) return NotFound();

        ViewData["Title"] = "Edit Savings Goal";
        ViewData["ActiveNav"] = "goals";
        return View("GoalForm", new GoalFormViewModel
        {
            Id = goal.Id,
            Name = goal.Name,
            TargetAmount = goal.TargetAmount,
            Deadline = goal.Deadline,
            Note = goal.Note
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Edit(int id, GoalFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            ViewData["Title"] = "Edit Savings Goal";
            ViewData["ActiveNav"] = "goals";
            return View("GoalForm", model);
        }

        var updated = _goals.Update(CurrentUser.Id, new SavingsGoal
        {
            Id = id,
            UserId = CurrentUser.Id,
            Name = model.Name,
            TargetAmount = model.TargetAmount,
            Deadline = model.Deadline,
            Note = model.Note ?? string.Empty
        });

        TempData[updated ? "Success" : "Error"] = updated
            ? $"Goal \"{model.Name}\" updated — savings plan recalculated."
            : "Goal was not found.";
        return RedirectToAction(nameof(Index));
    }

    // ------------------------------------------------------------ add funds

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult AddFunds(AddFundsViewModel model)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = string.Join(" ", ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage));
            return RedirectToAction(nameof(Index));
        }

        var ok = _goals.AddFunds(CurrentUser.Id, model.GoalId, model.Amount, model.Date, model.Note ?? string.Empty);
        TempData[ok ? "Success" : "Error"] = ok
            ? $"{Helpers.Format.Money(model.Amount, CurrentUser.Currency)} added to \"{model.GoalName}\"."
            : "Goal was not found.";
        return RedirectToAction(nameof(Index));
    }

    // ------------------------------------------------------------ delete

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Delete(int id)
    {
        var deleted = _goals.Delete(CurrentUser.Id, id);
        TempData[deleted ? "Success" : "Error"] = deleted
            ? "Goal deleted and its funds returned to your balance."
            : "Goal was not found.";
        return RedirectToAction(nameof(Index));
    }
}
