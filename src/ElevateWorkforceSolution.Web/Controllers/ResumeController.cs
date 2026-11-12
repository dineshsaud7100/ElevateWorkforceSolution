using ElevateWorkforceSolution.Application.Interfaces;
using ElevateWorkforceSolution.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ElevateWorkforceSolution.Web.Controllers;

/// <summary>
/// Serves candidate resume files. Resumes are never served from wwwroot; every request is checked
/// against the caller's role and ownership by the application service before a byte is written.
/// </summary>
[Authorize]
public class ResumeController : ApplicationControllerBase
{
    private readonly IResumeService _resumes;
    private readonly ILogger<ResumeController> _logger;

    public ResumeController(
        ICurrentUserService currentUser,
        IResumeService resumes,
        ILogger<ResumeController> logger)
        : base(currentUser)
    {
        _resumes = resumes;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Download(int id, CancellationToken cancellationToken)
    {
        var userId = CurrentUser.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        var role = CurrentUser.IsAdministrator
            ? Roles.Administrator
            : CurrentUser.IsEmployer
                ? Roles.Employer
                : Roles.JobSeeker;

        var resume = await _resumes.GetForDownloadAsync(id, userId, role, cancellationToken);
        if (resume is null)
        {
            // Deliberately the same message for "does not exist" and "not yours".
            Response.StatusCode = StatusCodes.Status404NotFound;
            _logger.LogWarning("Resume {ResumeId} requested by {UserId} ({Role}) was refused.",
                id, userId, role);
            return NotFound();
        }

        // Force a download rather than rendering inline, and never let the browser sniff the type.
        return File(resume.Content, resume.ContentType, resume.FileName, enableRangeProcessing: true);
    }
}
