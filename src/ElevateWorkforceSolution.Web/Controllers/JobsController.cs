using ElevateWorkforceSolution.Domain.Enums;
using ElevateWorkforceSolution.Application.DTOs;
using ElevateWorkforceSolution.Application.Interfaces;
using ElevateWorkforceSolution.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ElevateWorkforceSolution.Web.Controllers;

/// <summary>
/// Public careers search and vacancy details. Anonymous visitors may browse and search; the
/// bookmark action is explicitly restricted to candidates.
/// </summary>
public class JobsController : ApplicationControllerBase
{
    private readonly IJobSearchService _jobSearch;
    private readonly ISkillMatchService _skillMatch;
    private readonly ISavedJobService _savedJobs;

    public JobsController(
        ICurrentUserService currentUser,
        IJobSearchService jobSearch,
        ISkillMatchService skillMatch,
        ISavedJobService savedJobs)
        : base(currentUser)
    {
        _jobSearch = jobSearch;
        _skillMatch = skillMatch;
        _savedJobs = savedJobs;
    }

    /// <summary>
    /// Search with server-side filtering, sorting and pagination. Facet values come from the
    /// database, so the filter lists only ever offer options that actually exist.
    /// </summary>
    [AllowAnonymous]
    public async Task<IActionResult> Index(
        [FromQuery] string? keyword,
        [FromQuery] string? location,
        [FromQuery] string? category,
        [FromQuery] JobType? jobType,
        [FromQuery] WorkArrangement? workArrangement,
        [FromQuery] ExperienceLevel? experienceLevel,
        [FromQuery] decimal? salaryMin,
        [FromQuery] decimal? salaryMax,
        [FromQuery] JobSortOption? sort,
        [FromQuery] int page = 1,
        CancellationToken cancellationToken = default)
    {
        var filter = new JobSearchFilter
        {
            Keyword = keyword,
            Location = location,
            Category = category,
            JobType = jobType,
            WorkArrangement = workArrangement,
            ExperienceLevel = experienceLevel,
            SalaryMin = salaryMin,
            SalaryMax = salaryMax,
            Sort = sort ?? JobSortOption.Newest,
            PageNumber = page
        };

        filter.Normalise();

        var jobSeekerId = await CurrentUser.GetJobSeekerIdAsync(cancellationToken);

        var results = await _jobSearch.SearchPublicJobsAsync(filter, jobSeekerId, cancellationToken);
        var facets = await _jobSearch.GetFacetsAsync(cancellationToken);

        return View(new JobSearchViewModel
        {
            Filter = filter,
            Results = results,
            Facets = facets,
            JobTypeOptions = SelectListFactory.JobTypes(filter.JobType),
            WorkArrangementOptions = SelectListFactory.WorkArrangements(filter.WorkArrangement),
            ExperienceLevelOptions = SelectListFactory.ExperienceLevels(filter.ExperienceLevel),
            SortOptions = SelectListFactory.JobSortOptions(filter.Sort),
            LocationOptions = SelectListFactory.Strings(facets.Locations, filter.Location, "All locations"),
            CategoryOptions = SelectListFactory.Strings(facets.Categories, filter.Category, "All categories")
        });
    }

    [AllowAnonymous]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var jobSeekerId = await CurrentUser.GetJobSeekerIdAsync(cancellationToken);
        var job = await _jobSearch.GetPublicJobDetailsAsync(id, jobSeekerId, cancellationToken);

        if (job is null)
        {
            TempData["Error"] = "That vacancy is not available. It may have been closed, expired or removed.";
            return RedirectToAction(nameof(Index));
        }

        // Candidates additionally see the transparent match breakdown against their own profile.
        SkillMatchResultDto? match = null;
        if (jobSeekerId is not null)
        {
            match = await _skillMatch.EvaluateAsync(id, jobSeekerId.Value, cancellationToken);
        }

        var similar = (await _jobSearch.SearchPublicJobsAsync(
            new JobSearchFilter { Category = job.Category, PageSize = 4 }, jobSeekerId, cancellationToken))
            .Items
            .Where(j => j.Id != id)
            .Take(3)
            .ToList();

        return View(new JobDetailsViewModel { Job = job, SimilarJobs = similar, Match = match });
    }

    /// <summary>Bookmarks or removes a bookmark. Candidates only.</summary>
    [HttpPost]
    [Authorize(Policy = "JobSeekerOnly")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleSave(int id, string? returnUrl, CancellationToken cancellationToken)
    {
        var jobSeekerId = await RequireJobSeekerIdAsync();
        if (jobSeekerId is null)
        {
            return RedirectBack(nameof(Index));
        }

        var saved = await _savedJobs.IsSavedAsync(id, jobSeekerId.Value, cancellationToken);

        var result = saved
            ? await _savedJobs.UnsaveAsync(id, jobSeekerId.Value, cancellationToken)
            : await _savedJobs.SaveAsync(id, jobSeekerId.Value, cancellationToken);

        if (result.Succeeded)
        {
            FlashSuccess(saved ? "Removed from your saved jobs." : "Saved to your shortlist.");
        }
        else
        {
            FlashError(result.Error ?? "We could not update your saved jobs.");
        }

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction(nameof(Details), new { id });
    }
}
