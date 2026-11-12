using ElevateWorkforceSolution.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ElevateWorkforceSolution.Web.Controllers;

/// <summary>
/// Shared plumbing for every page controller: resolves the signed-in user's role profile and
/// centralises flash-message and redirect-after-post handling so controllers stay thin.
/// </summary>
public abstract class ApplicationControllerBase : Controller
{
    private readonly ICurrentUserService _currentUser;

    protected ApplicationControllerBase(ICurrentUserService currentUser)
    {
        _currentUser = currentUser;
    }

    protected ICurrentUserService CurrentUser => _currentUser;

    protected bool IsSignedIn => _currentUser.IsAuthenticated;

    protected bool IsAdministrator => _currentUser.IsAdministrator;

    protected bool IsEmployer => _currentUser.IsEmployer;

    protected bool IsJobSeeker => _currentUser.IsJobSeeker;

    /// <summary>Resolves the employer's profile id, redirecting to onboarding when it is missing.</summary>
    protected async Task<int?> RequireEmployerIdAsync()
    {
        var id = await _currentUser.GetEmployerIdAsync(HttpContext.RequestAborted);
        if (id is null)
        {
            TempData["Error"] = "Your employer profile is still being set up. Please complete your company profile.";
        }

        return id;
    }

    protected async Task<int?> RequireJobSeekerIdAsync()
    {
        var id = await _currentUser.GetJobSeekerIdAsync(HttpContext.RequestAborted);
        if (id is null)
        {
            TempData["Error"] = "Your candidate profile is still being set up. Please refresh the page.";
        }

        return id;
    }

    protected void FlashSuccess(string message) => TempData["Success"] = message;

    protected void FlashError(string message) => TempData["Error"] = message;

    /// <summary>Redirects back to the referring page when it is local, otherwise to the action.</summary>
    protected IActionResult RedirectBack(string fallback)
    {
        var referer = Request.Headers.Referer.ToString();

        if (!string.IsNullOrWhiteSpace(referer)
            && Uri.TryCreate(referer, UriKind.Absolute, out var uri)
            && Url.IsLocalUrl(uri.PathAndQuery))
        {
            return Redirect(uri.PathAndQuery!);
        }

        return RedirectToAction(fallback);
    }
}
