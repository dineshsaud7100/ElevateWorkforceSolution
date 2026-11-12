using ElevateWorkforceSolution.Application.DTOs;
using ElevateWorkforceSolution.Application.Interfaces;
using ElevateWorkforceSolution.Domain.Common;
using ElevateWorkforceSolution.Domain.Enums;
using ElevateWorkforceSolution.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ElevateWorkforceSolution.Web.Controllers;

/// <summary>
/// Platform administration. The only place a vacancy can be approved or rejected, which is what
/// gates the public careers site.
/// </summary>
[Authorize(Policy = "AdministratorOnly")]
public class AdminController : ApplicationControllerBase
{
    private readonly IDashboardService _dashboard;
    private readonly IAdminService _admin;
    private readonly IVacancyService _vacancies;

    public AdminController(
        ICurrentUserService currentUser,
        IDashboardService dashboard,
        IAdminService admin,
        IVacancyService vacancies)
        : base(currentUser)
    {
        _dashboard = dashboard;
        _admin = admin;
        _vacancies = vacancies;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var dashboard = await _dashboard.GetForAdministratorAsync(cancellationToken);
        return View(new AdminDashboardViewModel { Dashboard = dashboard });
    }

    // ---------- Vacancy moderation ----------

    public async Task<IActionResult> Vacancies(
        [FromQuery] string? keyword,
        [FromQuery] VacancyStatus? status,
        [FromQuery] string? location,
        [FromQuery] int page = 1,
        CancellationToken cancellationToken = default)
    {
        var results = await _admin.SearchVacanciesAsync(keyword, status, location, page, 10, cancellationToken);

        return View(new AdminVacanciesViewModel
        {
            Keyword = keyword,
            Status = status,
            Location = location,
            Page = page,
            Results = results,
            StatusOptions = SelectListFactory.VacancyStatuses(status, "All statuses")
        });
    }

    public async Task<IActionResult> ReviewVacancy(int id, CancellationToken cancellationToken)
    {
        var vacancy = await _admin.GetVacancyForReviewAsync(id, cancellationToken);
        if (vacancy is null)
        {
            TempData["Error"] = "That vacancy could not be found.";
            return RedirectToAction(nameof(Vacancies));
        }

        return View(new AdminVacancyReviewViewModel { Vacancy = vacancy });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveVacancy(int id, CancellationToken cancellationToken)
    {
        var result = await _vacancies.ApproveAsync(id, CurrentUser.UserId!, cancellationToken);

        if (result.Succeeded)
        {
            FlashSuccess("Vacancy approved and published.");
        }
        else
        {
            FlashError(result.Error ?? "We could not approve that vacancy.");
        }

        return RedirectToAction(nameof(Vacancies));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectVacancy(int id, string? notes, CancellationToken cancellationToken)
    {
        var result = await _vacancies.RejectAsync(id, CurrentUser.UserId!, notes, cancellationToken);

        if (result.Succeeded)
        {
            FlashSuccess("Vacancy rejected. The employer has been notified with your notes.");
        }
        else
        {
            FlashError(result.Error ?? "We could not reject that vacancy.");
        }

        return RedirectToAction(nameof(Vacancies));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveVacancy(int id, string? notes, CancellationToken cancellationToken)
    {
        var result = await _vacancies.RemoveAsync(id, CurrentUser.UserId!, notes, cancellationToken);

        if (result.Succeeded)
        {
            FlashSuccess("Vacancy removed from public view.");
        }
        else
        {
            FlashError(result.Error ?? "We could not remove that vacancy.");
        }

        return RedirectToAction(nameof(Vacancies));
    }

    // ---------- Users ----------

    public async Task<IActionResult> Users(
        [FromQuery] string? keyword,
        [FromQuery] string? role,
        [FromQuery] bool? activeOnly,
        [FromQuery] int page = 1,
        CancellationToken cancellationToken = default)
    {
        var results = await _admin.SearchUsersAsync(keyword, role, activeOnly, page, 15, cancellationToken);

        return View(new AdminUsersViewModel
        {
            Keyword = keyword,
            Role = role,
            ActiveOnly = activeOnly,
            Page = page,
            Results = results,
            RoleOptions = SelectListFactory.RoleNames(role, "All roles")
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleUserActive(string userId, CancellationToken cancellationToken)
    {
        if (userId == CurrentUser.UserId)
        {
            FlashError("You cannot deactivate your own administrator account.");
            return RedirectToAction(nameof(Users));
        }

        var target = await _admin.GetUserAsync(userId, cancellationToken);
        if (target is null)
        {
            FlashError("That user could not be found.");
            return RedirectToAction(nameof(Users));
        }

        var result = await _admin.SetUserActiveAsync(userId, !target.IsActive, CurrentUser.UserId!, cancellationToken);

        if (result.Succeeded)
        {
            FlashSuccess(target.IsActive ? "Account deactivated." : "Account reactivated.");
        }
        else
        {
            FlashError(result.Error ?? "We could not change that account.");
        }

        return RedirectToAction(nameof(Users));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleEmployerVerification(string userId, CancellationToken cancellationToken)
    {
        if (userId == CurrentUser.UserId)
        {
            FlashError("You cannot change your own verification status.");
            return RedirectToAction(nameof(Users));
        }

        var target = await _admin.GetUserAsync(userId, cancellationToken);
        if (target is null)
        {
            FlashError("That employer could not be found.");
            return RedirectToAction(nameof(Users));
        }

        if (target.Role != Roles.Employer)
        {
            FlashError("Only employer accounts can be approved.");
            return RedirectToAction(nameof(Users));
        }

        var result = await _admin.SetEmployerVerificationAsync(userId, !target.IsVerified, CurrentUser.UserId!, cancellationToken);

        if (result.Succeeded)
        {
            FlashSuccess(target.IsVerified ? "Employer verification removed." : "Employer verified and activated.");
        }
        else
        {
            FlashError(result.Error ?? "We could not update the employer verification status.");
        }

        return RedirectToAction(nameof(Users));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeUserRole(string userId, string role, CancellationToken cancellationToken)
    {
        if (userId == CurrentUser.UserId)
        {
            FlashError("You cannot change your own role.");
            return RedirectToAction(nameof(Users));
        }

        if (!Roles.All.Contains(role))
        {
            FlashError("That role does not exist.");
            return RedirectToAction(nameof(Users));
        }

        var result = await _admin.SetUserRoleAsync(userId, role, CurrentUser.UserId!, cancellationToken);

        if (result.Succeeded)
        {
            FlashSuccess($"Role changed to {role}.");
        }
        else
        {
            FlashError(result.Error ?? "We could not change that role.");
        }

        return RedirectToAction(nameof(Users));
    }

    // ---------- Companies, applications, reports ----------

    public async Task<IActionResult> Companies(
        [FromQuery] string? keyword,
        [FromQuery] int page = 1,
        CancellationToken cancellationToken = default)
    {
        var results = await _admin.SearchCompaniesAsync(keyword, page, 15, cancellationToken);

        return View(new AdminCompaniesViewModel { Keyword = keyword, Page = page, Results = results });
    }

    public async Task<IActionResult> Applications(
        [FromQuery] string? keyword,
        [FromQuery] ApplicationStatus? status,
        [FromQuery] int page = 1,
        CancellationToken cancellationToken = default)
    {
        var results = await _admin.SearchApplicationsAsync(keyword, status, page, 15, cancellationToken);

        return View(new AdminApplicationsViewModel
        {
            Keyword = keyword,
            Status = status,
            Page = page,
            Results = results,
            StatusOptions = SelectListFactory.ApplicationStatuses(status, "All statuses")
        });
    }

    public async Task<IActionResult> Reports(CancellationToken cancellationToken)
    {
        var report = await _admin.BuildReportAsync(cancellationToken);
        return View(new AdminReportsViewModel { Report = report });
    }
}
