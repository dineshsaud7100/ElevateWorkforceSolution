using System.Security.Claims;
using ElevateWorkforceSolution.Application.Interfaces;
using ElevateWorkforceSolution.Application.Interfaces.Repositories;
using ElevateWorkforceSolution.Domain.Common;
using ElevateWorkforceSolution.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ElevateWorkforceSolution.Web.Services;

/// <summary>
/// Resolves the signed-in principal to its role-specific profile. Lookups are cached for the
/// lifetime of the request because the id is used on nearly every page.
/// </summary>
public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _accessor;
    private readonly IRepository<JobSeeker> _jobSeekers;
    private readonly IRepository<Employer> _employers;
    private int? _employerId;
    private int? _jobSeekerId;
    private bool _resolved;

    public CurrentUserService(
        IHttpContextAccessor accessor,
        IRepository<JobSeeker> jobSeekers,
        IRepository<Employer> employers)
    {
        _accessor = accessor;
        _jobSeekers = jobSeekers;
        _employers = employers;
    }

    private ClaimsPrincipal? Principal => _accessor.HttpContext?.User;

    public string? UserId => Principal?.FindFirstValue(ClaimTypes.NameIdentifier);

    public string? Email => Principal?.FindFirstValue(ClaimTypes.Email);

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public bool IsAdministrator => Principal?.IsInRole(Roles.Administrator) == true;

    public bool IsEmployer => Principal?.IsInRole(Roles.Employer) == true;

    public bool IsJobSeeker => Principal?.IsInRole(Roles.JobSeeker) == true;

    public async Task<int?> GetEmployerIdAsync(CancellationToken cancellationToken = default)
    {
        if (!IsEmployer)
        {
            return null;
        }

        if (_resolved)
        {
            return _employerId;
        }

        var userId = UserId;
        if (string.IsNullOrEmpty(userId))
        {
            return null;
        }

        _employerId = await _employers.Query()
            .Where(e => e.UserId == userId && e.IsVerified)
            .Select(e => (int?)e.Id)
            .FirstOrDefaultAsync(cancellationToken);

        _resolved = true;
        return _employerId;
    }

    public async Task<int?> GetJobSeekerIdAsync(CancellationToken cancellationToken = default)
    {
        if (!IsJobSeeker)
        {
            return null;
        }

        if (_resolved)
        {
            return _jobSeekerId;
        }

        var userId = UserId;
        if (string.IsNullOrEmpty(userId))
        {
            return null;
        }

        _jobSeekerId = await _jobSeekers.Query()
            .Where(s => s.UserId == userId)
            .Select(s => (int?)s.Id)
            .FirstOrDefaultAsync(cancellationToken);

        _resolved = true;
        return _jobSeekerId;
    }
}
