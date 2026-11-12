using ElevateWorkforceSolution.Web.Models;
using System.Diagnostics;
using ElevateWorkforceSolution.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ElevateWorkforceSolution.Web.Controllers;

/// <summary>Public landing pages: marketing home, privacy and error handling.</summary>
public class HomeController : ApplicationControllerBase
{
    private readonly IJobSearchService _jobSearch;
    private readonly IEmployerProfileService _employerProfile;
    private readonly ILogger<HomeController> _logger;

    public HomeController(
        ICurrentUserService currentUser,
        IJobSearchService jobSearch,
        IEmployerProfileService employerProfile,
        ILogger<HomeController> logger)
        : base(currentUser)
    {
        _jobSearch = jobSearch;
        _employerProfile = employerProfile;
        _logger = logger;
    }

    /// <summary>
    /// Landing page. Every figure on the page comes from the database so the site never shows
    /// placeholder counts.
    /// </summary>
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var facets = await _jobSearch.GetFacetsAsync(cancellationToken);
        var latestJobs = await _jobSearch.GetLatestJobsAsync(6, cancellationToken);

        var companies = await _employerProfile.SearchPublicCompaniesAsync(null, null, 1, 6, cancellationToken);

        return View(new HomeViewModel
        {
            TotalActiveVacancies = facets.TotalActiveVacancies,
            TotalCompaniesHiring = facets.TotalCompaniesHiring,
            PopularCategories = facets.PopularCategories,
            LatestJobs = latestJobs,
            FeaturedCompanies = companies.Items
        });
    }

    public IActionResult Privacy() => View();

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => View(new ErrorViewModel
    {
        RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
    });

    /// <summary>Renders a friendly page for any status code raised by the pipeline.</summary>
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult StatusCode(int? code)
    {
        Response.StatusCode = code is >= 400 and <= 599 ? code.Value : 404;

        return View(new ErrorViewModel
        {
            RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
            StatusCode = Response.StatusCode
        });
    }
}
