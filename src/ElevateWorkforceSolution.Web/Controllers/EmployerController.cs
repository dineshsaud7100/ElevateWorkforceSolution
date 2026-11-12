using ElevateWorkforceSolution.Application.DTOs;
using ElevateWorkforceSolution.Application.Interfaces;
using ElevateWorkforceSolution.Domain.Common;
using ElevateWorkforceSolution.Domain.Enums;
using ElevateWorkforceSolution.Web.Extensions;
using ElevateWorkforceSolution.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace ElevateWorkforceSolution.Web.Controllers;

/// <summary>
/// Employer workspace: company profile, vacancy lifecycle and the applicant pipeline.
/// A newly posted vacancy starts as PendingVerification and only an administrator can publish it.
/// </summary>
[Authorize(Policy = "EmployerOnly")]
public class EmployerController : ApplicationControllerBase
{
    private readonly IDashboardService _dashboard;
    private readonly IVacancyService _vacancies;
    private readonly IApplicationService _applications;
    private readonly IEmployerProfileService _company;
    private readonly IJobSearchService _jobSearch;

    public EmployerController(
        ICurrentUserService currentUser,
        IDashboardService dashboard,
        IVacancyService vacancies,
        IApplicationService applications,
        IEmployerProfileService company,
        IJobSearchService jobSearch)
        : base(currentUser)
    {
        _dashboard = dashboard;
        _vacancies = vacancies;
        _applications = applications;
        _company = company;
        _jobSearch = jobSearch;
    }

    public async Task<IActionResult> Dashboard(CancellationToken cancellationToken)
    {
        var employerId = await RequireEmployerIdAsync();
        if (employerId is null)
        {
            return RedirectToAction(nameof(Company));
        }

        var dashboard = await _dashboard.GetForEmployerAsync(employerId.Value, cancellationToken);
        return View(new EmployerDashboardViewModel { Dashboard = dashboard });
    }

    // ---------- Company profile ----------

    public async Task<IActionResult> Company(CancellationToken cancellationToken)
    {
        var employerId = await RequireEmployerIdAsync();
        if (employerId is null)
        {
            return RedirectToAction(nameof(Dashboard));
        }

        var company = await _company.GetCompanyAsync(employerId.Value, cancellationToken);

        return View(new EmployerCompanyViewModel
        {
            EmployerId = employerId.Value,
            Company = company,
            Form = company is null
                ? new CompanyFormDto()
                : new CompanyFormDto
                {
                    Name = company.Name,
                    Industry = company.Industry,
                    Description = company.Description,
                    About = company.About,
                    Website = company.Website,
                    Location = company.Location,
                    ContactEmail = company.ContactEmail,
                    ContactPhone = company.ContactPhone,
                    CompanySize = company.CompanySize,
                    FoundedYear = company.FoundedYear
                }
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Company(EmployerCompanyViewModel model, CancellationToken cancellationToken)
    {
        var employerId = await RequireEmployerIdAsync();
        if (employerId is null)
        {
            return RedirectToAction(nameof(Dashboard));
        }

        model.EmployerId = employerId.Value;

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _company.CreateOrUpdateCompanyAsync(employerId.Value, model.Form, cancellationToken);

        if (result.Succeeded)
        {
            FlashSuccess("Your company profile has been saved.");
        }
        else
        {
            FlashError(result.Error ?? "We could not save your company profile.");
        }

        return RedirectToAction(nameof(Company));
    }

    // ---------- Vacancies ----------

    public async Task<IActionResult> Vacancies(
        [FromQuery] VacancyStatus? status,
        [FromQuery] string? keyword,
        [FromQuery] int page = 1,
        CancellationToken cancellationToken = default)
    {
        var employerId = await RequireEmployerIdAsync();
        if (employerId is null)
        {
            return RedirectToAction(nameof(Company));
        }

        var results = await _vacancies.ListForEmployerAsync(employerId.Value, status, keyword, page, 10, cancellationToken);

        return View(new EmployerVacanciesViewModel
        {
            Status = status,
            Keyword = keyword,
            Page = page,
            Results = results,
            StatusOptions = SelectListFactory.VacancyStatuses(status, "All statuses")
        });
    }

    [HttpGet]
    public async Task<IActionResult> CreateVacancy(CancellationToken cancellationToken)
    {
        var employerId = await RequireEmployerIdAsync();
        if (employerId is null)
        {
            return RedirectToAction(nameof(Company));
        }

        var company = await _company.GetCompanyAsync(employerId.Value, cancellationToken);
        if (company is null)
        {
            FlashError("Add your company profile before posting a vacancy.");
            return RedirectToAction(nameof(Company));
        }

        var facets = await _jobSearch.GetEmployerFacetsAsync(employerId.Value, cancellationToken);

        return View("VacancyForm", new VacancyFormViewModel
        {
            EmployerId = employerId.Value,
            HasCompanyProfile = true,
            Form = new VacancyFormDto
            {
                SalaryCurrency = "NPR",
                SalaryPeriod = "Monthly",
                ApplicationDeadline = DateTime.UtcNow.AddDays(30)
            },
            JobTypeOptions = SelectListFactory.RequiredJobTypes(JobType.FullTime),
            WorkArrangementOptions = SelectListFactory.RequiredWorkArrangements(WorkArrangement.OnSite),
            ExperienceLevelOptions = SelectListFactory.RequiredExperienceLevels(ExperienceLevel.Entry),
            CategoryOptions = SelectListFactory.Strings(facets.Categories, null, "Select or type a category"),
            LocationOptions = SelectListFactory.Strings(facets.Locations, null, "Select a location")
        });
    }

    [HttpGet]
    public async Task<IActionResult> EditVacancy(int id, CancellationToken cancellationToken)
    {
        var employerId = await RequireEmployerIdAsync();
        if (employerId is null)
        {
            return RedirectToAction(nameof(Vacancies));
        }

        var form = await _vacancies.GetForEditAsync(id, employerId.Value, cancellationToken);
        if (form is null)
        {
            TempData["Error"] = "That vacancy could not be found.";
            return RedirectToAction(nameof(Vacancies));
        }

        var facets = await _jobSearch.GetEmployerFacetsAsync(employerId.Value, cancellationToken);

        return View("VacancyForm", new VacancyFormViewModel
        {
            EmployerId = employerId.Value,
            HasCompanyProfile = true,
            Form = form,
            JobTypeOptions = SelectListFactory.RequiredJobTypes(form.JobType),
            WorkArrangementOptions = SelectListFactory.RequiredWorkArrangements(form.WorkArrangement),
            ExperienceLevelOptions = SelectListFactory.RequiredExperienceLevels(form.ExperienceLevel),
            CategoryOptions = SelectListFactory.Strings(facets.Categories, form.Category, "Select or type a category"),
            LocationOptions = SelectListFactory.Strings(facets.Locations, form.WorkLocation, "Select a location")
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveVacancy(VacancyFormViewModel model, CancellationToken cancellationToken)
    {
        var employerId = await RequireEmployerIdAsync();
        if (employerId is null)
        {
            return RedirectToAction(nameof(Vacancies));
        }

        model.EmployerId = employerId.Value;

        if (!ModelState.IsValid)
        {
            model.HasCompanyProfile = true;
            PopulateVacancyOptions(model);
            return View("VacancyForm", model);
        }

        var isEdit = model.Form.Id.HasValue;
        var result = isEdit
            ? await _vacancies.UpdateAsync(model.Form, employerId.Value, cancellationToken)
            : await _vacancies.CreateAsync(model.Form, employerId.Value, cancellationToken);

        if (!result.Succeeded)
        {
            foreach (var error in (result.Error ?? "The vacancy could not be saved.").Split(";", StringSplitOptions.RemoveEmptyEntries))
            {
                ModelState.AddModelError(string.Empty, error.Trim());
            }

            model.HasCompanyProfile = true;
            PopulateVacancyOptions(model);
            return View("VacancyForm", model);
        }

        FlashSuccess(isEdit
            ? "Vacancy updated and resubmitted for verification."
            : "Vacancy saved. Its visibility is controlled by the employer verification status and administrator moderation.");

        return RedirectToAction(nameof(Vacancies));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CloseVacancy(int id, CancellationToken cancellationToken)
    {
        var employerId = await RequireEmployerIdAsync();
        if (employerId is null)
        {
            return RedirectToAction(nameof(Vacancies));
        }

        var result = await _vacancies.CloseAsync(id, employerId.Value, cancellationToken);

        if (result.Succeeded)
        {
            FlashSuccess("Vacancy closed. Existing applications are kept.");
        }
        else
        {
            FlashError(result.Error ?? "We could not close that vacancy.");
        }

        return RedirectToAction(nameof(Vacancies));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReopenVacancy(int id, CancellationToken cancellationToken)
    {
        var employerId = await RequireEmployerIdAsync();
        if (employerId is null)
        {
            return RedirectToAction(nameof(Vacancies));
        }

        var result = await _vacancies.ReopenAsync(id, employerId.Value, cancellationToken);

        if (result.Succeeded)
        {
            FlashSuccess("Vacancy reopened and sent back for verification.");
        }
        else
        {
            FlashError(result.Error ?? "We could not reopen that vacancy.");
        }

        return RedirectToAction(nameof(Vacancies));
    }

    // ---------- Applicants ----------

    public async Task<IActionResult> Applicants(
        [FromQuery] int? jobId,
        [FromQuery] ApplicationStatus? status,
        [FromQuery] string? keyword,
        [FromQuery] int page = 1,
        CancellationToken cancellationToken = default)
    {
        var employerId = await RequireEmployerIdAsync();
        if (employerId is null)
        {
            return RedirectToAction(nameof(Dashboard));
        }

        var results = await _applications.ListApplicantsAsync(
            employerId.Value, jobId, status, keyword, page, 10, cancellationToken);

        return View(new ApplicantsViewModel
        {
            JobId = jobId,
            Status = status,
            Keyword = keyword,
            Page = page,
            Results = results,
            StatusOptions = SelectListFactory.ApplicationStatuses(status, "All statuses"),
            JobOptions = await BuildJobOptionsAsync(employerId.Value, jobId, cancellationToken)
        });
    }

    public async Task<IActionResult> ApplicantDetails(int id, CancellationToken cancellationToken)
    {
        var employerId = await RequireEmployerIdAsync();
        if (employerId is null)
        {
            return RedirectToAction(nameof(Applicants));
        }

        var applicant = await _applications.GetApplicantAsync(id, employerId.Value, cancellationToken);
        if (applicant is null)
        {
            TempData["Error"] = "That applicant could not be found.";
            return RedirectToAction(nameof(Applicants));
        }

        var profile = await _applications.GetCandidateProfileAsync(applicant.JobSeekerId, employerId.Value, cancellationToken);
        var job = await _jobSearch.GetPublicJobDetailsAsync(applicant.JobId, null, cancellationToken);
        var statusOptions = StatusRules.EmployerAssignableStatuses
            .Where(status => status != applicant.Status)
            .Select(status => new SelectListItem(status.Display(), status.ToString()))
            .ToList();

        return View(new ApplicantDetailsViewModel
        {
            Applicant = applicant,
            Profile = profile,
            Job = job,
            StatusOptions = statusOptions,
            ApplicationId = applicant.ApplicationId,
            JobSeekerId = applicant.JobSeekerId,
            Status = applicant.Status
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateApplicantStatus(UpdateApplicantStatusViewModel model, CancellationToken cancellationToken)
    {
        var employerId = await RequireEmployerIdAsync();
        if (employerId is null)
        {
            return RedirectToAction(nameof(Applicants));
        }

        if (!ModelState.IsValid)
        {
            return RedirectToAction(nameof(ApplicantDetails), new { id = model.ApplicationId });
        }

        var result = await _applications.UpdateStatusAsync(new UpdateApplicationStatusCommand
        {
            ApplicationId = model.ApplicationId,
            Status = model.Status,
            Remarks = model.Remarks?.Trim()
        }, employerId.Value, cancellationToken);

        if (result.Succeeded)
        {
            FlashSuccess($"Applicant moved to {model.Status.Display()}.");
        }
        else
        {
            FlashError(result.Error ?? "We could not update that application.");
        }

        return RedirectToAction(nameof(ApplicantDetails), new { id = model.ApplicationId });
    }

    private void PopulateVacancyOptions(VacancyFormViewModel model)
    {
        model.JobTypeOptions = SelectListFactory.RequiredJobTypes(model.Form.JobType);
        model.WorkArrangementOptions = SelectListFactory.RequiredWorkArrangements(model.Form.WorkArrangement);
        model.ExperienceLevelOptions = SelectListFactory.RequiredExperienceLevels(model.Form.ExperienceLevel);
        model.CategoryOptions = SelectListFactory.Strings(model.CategoryOptions.Select(o => o.Text), model.Form.Category, "Select or type a category");
        model.LocationOptions = SelectListFactory.Strings(model.LocationOptions.Select(o => o.Text), model.Form.WorkLocation, "Select a location");
    }

    private async Task<List<SelectListItem>> BuildJobOptionsAsync(int employerId, int? selected, CancellationToken cancellationToken)
    {
        var jobs = await _vacancies.ListForEmployerAsync(employerId, null, null, 1, 100, cancellationToken);

        return jobs.Items
            .Select(j => new SelectListItem { Value = j.Id.ToString(), Text = j.Title, Selected = j.Id == selected })
            .ToList();
    }
}
