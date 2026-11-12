using ElevateWorkforceSolution.Application.DTOs;
using ElevateWorkforceSolution.Application.Interfaces;
using ElevateWorkforceSolution.Domain.Enums;
using ElevateWorkforceSolution.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace ElevateWorkforceSolution.Web.Controllers;

/// <summary>
/// Candidate side of the recruiting workflow: submit an application, track its status timeline,
/// withdraw, and manage bookmarked vacancies.
/// </summary>
[Authorize(Policy = "JobSeekerOnly")]
public class ApplicationsController : ApplicationControllerBase
{
    private readonly IApplicationService _applications;
    private readonly IJobSearchService _jobSearch;
    private readonly ISavedJobService _savedJobs;

    public ApplicationsController(
        ICurrentUserService currentUser,
        IApplicationService applications,
        IJobSearchService jobSearch,
        ISavedJobService savedJobs)
        : base(currentUser)
    {
        _applications = applications;
        _jobSearch = jobSearch;
        _savedJobs = savedJobs;
    }

    /// <summary>Apply form. Shows the match breakdown and the candidate's own resumes.</summary>
    [HttpGet]
    public async Task<IActionResult> Apply(int jobId, CancellationToken cancellationToken)
    {
        var jobSeekerId = await RequireJobSeekerIdAsync();
        if (jobSeekerId is null)
        {
            return RedirectToAction("Dashboard", "Candidate");
        }

        var context = await _applications.GetApplyContextAsync(jobId, jobSeekerId.Value, cancellationToken);
        if (context is null)
        {
            TempData["Error"] = "That vacancy is not available.";
            return RedirectToAction("Details", "Jobs", new { id = jobId });
        }

        return View(new ApplyViewModel
        {
            JobId = jobId,
            Context = context,
            ResumeId = context.AvailableResumes.FirstOrDefault()?.Id ?? 0,
            ResumeOptions = context.AvailableResumes
                .Select(r => new SelectListItem
                {
                    Value = r.Id.ToString(),
                    Text = $"{r.Label ?? "Resume"} - {r.OriginalFileName} ({r.FileSizeDisplay})"
                })
                .ToList()
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Apply(ApplyViewModel model, CancellationToken cancellationToken)
    {
        var jobSeekerId = await RequireJobSeekerIdAsync();
        if (jobSeekerId is null)
        {
            return RedirectToAction("Dashboard", "Candidate");
        }

        var context = await _applications.GetApplyContextAsync(model.JobId, jobSeekerId.Value, cancellationToken);
        if (context is null)
        {
            TempData["Error"] = "That vacancy is not available.";
            return RedirectToAction("Details", "Jobs", new { id = model.JobId });
        }

        model.Context = context;
        model.ResumeOptions = context.AvailableResumes
            .Select(r => new SelectListItem
            {
                Value = r.Id.ToString(),
                Text = $"{r.Label ?? "Resume"} - {r.OriginalFileName} ({r.FileSizeDisplay})"
            })
            .ToList();

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        if (!context.AcceptsApplications)
        {
            FlashError(context.BlockedReason ?? "This vacancy is not accepting applications.");
            return RedirectToAction("Details", "Jobs", new { id = model.JobId });
        }

        var result = await _applications.SubmitAsync(new SubmitApplicationCommand
        {
            JobId = model.JobId,
            ResumeId = model.ResumeId,
            CoverLetter = model.CoverLetter?.Trim()
        }, jobSeekerId.Value, cancellationToken);

        if (result.Succeeded)
        {
            FlashSuccess("Your application has been submitted. You can track its progress from My applications.");
            return RedirectToAction(nameof(MyApplications));
        }

        FlashError(result.Error ?? "We could not submit your application.");
        return View(model);
    }

    public async Task<IActionResult> MyApplications(
        [FromQuery] ApplicationStatus? status,
        [FromQuery] int page = 1,
        CancellationToken cancellationToken = default)
    {
        var jobSeekerId = await RequireJobSeekerIdAsync();
        if (jobSeekerId is null)
        {
            return RedirectToAction("Dashboard", "Candidate");
        }

        var results = await _applications.ListForCandidateAsync(status, jobSeekerId.Value, page, 10, cancellationToken);

        return View(new MyApplicationsViewModel
        {
            Status = status,
            Page = page,
            Results = results,
            StatusOptions = SelectListFactory.ApplicationStatuses(status, "All statuses")
        });
    }

    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var jobSeekerId = await RequireJobSeekerIdAsync();
        if (jobSeekerId is null)
        {
            return RedirectToAction("Dashboard", "Candidate");
        }

        var application = await _applications.GetForCandidateAsync(id, jobSeekerId.Value, cancellationToken);
        if (application is null)
        {
            TempData["Error"] = "That application could not be found.";
            return RedirectToAction(nameof(MyApplications));
        }

        return View(new ApplicationDetailsViewModel { Application = application });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Withdraw(int id, CancellationToken cancellationToken)
    {
        var jobSeekerId = await RequireJobSeekerIdAsync();
        if (jobSeekerId is null)
        {
            return RedirectToAction("Dashboard", "Candidate");
        }

        var result = await _applications.WithdrawAsync(id, jobSeekerId.Value, cancellationToken);

        if (result.Succeeded)
        {
            FlashSuccess("Your application has been withdrawn.");
        }
        else
        {
            FlashError(result.Error ?? "We could not withdraw that application.");
        }

        return RedirectToAction(nameof(MyApplications));
    }

    public async Task<IActionResult> SavedJobs([FromQuery] int page = 1, CancellationToken cancellationToken = default)
    {
        var jobSeekerId = await RequireJobSeekerIdAsync();
        if (jobSeekerId is null)
        {
            return RedirectToAction("Dashboard", "Candidate");
        }

        var results = await _savedJobs.ListAsync(jobSeekerId.Value, page, 12, cancellationToken);

        return View(new SavedJobsViewModel { Page = page, Results = results });
    }
}
