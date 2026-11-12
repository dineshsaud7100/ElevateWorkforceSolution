using ElevateWorkforceSolution.Application.Interfaces;
using ElevateWorkforceSolution.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace ElevateWorkforceSolution.Web.Controllers;

/// <summary>Public company directory and company profile pages.</summary>
public class CompaniesController : ApplicationControllerBase
{
    private readonly IEmployerProfileService _employerProfiles;
    private readonly IJobSearchService _jobSearch;

    public CompaniesController(
        ICurrentUserService currentUser,
        IEmployerProfileService employerProfiles,
        IJobSearchService jobSearch)
        : base(currentUser)
    {
        _employerProfiles = employerProfiles;
        _jobSearch = jobSearch;
    }

    public async Task<IActionResult> Index(
        [FromQuery] string? keyword,
        [FromQuery] string? industry,
        [FromQuery] int page = 1,
        CancellationToken cancellationToken = default)
    {
        var results = await _employerProfiles.SearchPublicCompaniesAsync(keyword, industry, page, 9, cancellationToken);

        // Industry list is derived from the companies actually in the database.
        var all = await _employerProfiles.SearchPublicCompaniesAsync(null, null, 1, 100, cancellationToken);
        var industries = all.Items
            .Select(c => c.Industry)
            .Where(i => !string.IsNullOrWhiteSpace(i))
            .Select(i => i!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(i => i, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return View(new CompanyListViewModel
        {
            Keyword = keyword,
            Industry = industry,
            Page = page,
            Results = results,
            Industries = industries
        });
    }

    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var company = await _employerProfiles.GetPublicCompanyAsync(id, cancellationToken);
        if (company is null)
        {
            TempData["Error"] = "That company profile is not available.";
            return RedirectToAction(nameof(Index));
        }

        return View(new CompanyDetailsViewModel { Company = company });
    }
}
