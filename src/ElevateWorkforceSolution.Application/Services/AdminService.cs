using ElevateWorkforceSolution.Application.Common;
using ElevateWorkforceSolution.Application.DTOs;
using ElevateWorkforceSolution.Application.Interfaces;
using ElevateWorkforceSolution.Application.Interfaces.Repositories;
using ElevateWorkforceSolution.Domain.Common;
using ElevateWorkforceSolution.Domain.Entities;
using ElevateWorkforceSolution.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ApplicationEntity = ElevateWorkforceSolution.Domain.Entities.Application;

namespace ElevateWorkforceSolution.Application.Services;

/// <summary>Platform administration: user oversight, vacancy moderation and reports.</summary>
public class AdminService : IAdminService
{
    private readonly IRepository<ApplicationUser> _users;
    private readonly IRepository<Company> _companies;
    private readonly IRepository<Job> _jobs;
    private readonly IRepository<ApplicationEntity> _applications;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AdminService> _logger;

    public AdminService(
        IRepository<ApplicationUser> users,
        IRepository<Company> companies,
        IRepository<Job> jobs,
        IRepository<ApplicationEntity> applications,
        IUnitOfWork unitOfWork,
        ILogger<AdminService> logger)
    {
        _users = users;
        _companies = companies;
        _jobs = jobs;
        _applications = applications;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<PagedResult<AdminUserRowDto>> SearchUsersAsync(
        string? keyword, string? role, bool? activeOnly, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 100 ? 15 : pageSize;

        var query = _users.Query();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim();
            query = query.Where(u =>
                EF.Functions.ILike(u.FirstName, $"%{kw}%") ||
                EF.Functions.ILike(u.LastName, $"%{kw}%") ||
                EF.Functions.ILike(u.Email!, $"%{kw}%"));
        }

        if (!string.IsNullOrWhiteSpace(role))
        {
            var r = role.Trim();
            query = r switch
            {
                Roles.JobSeeker => query.Where(u => u.JobSeeker != null),
                Roles.Employer => query.Where(u => u.Employer != null),
                Roles.Administrator => query.Where(u => u.JobSeeker == null && u.Employer == null),
                _ => query
            };
        }

        if (activeOnly == true)
        {
            query = query.Where(u => u.IsActive);
        }
        else if (activeOnly == false)
        {
            query = query.Where(u => !u.IsActive);
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await ProjectUsersAsync(query
            .OrderByDescending(u => u.CreatedDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize), cancellationToken);

        return new PagedResult<AdminUserRowDto>(items, total, page, pageSize);
    }

    public async Task<AdminUserRowDto?> GetUserAsync(
        string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        var query = _users.Query().Where(u => u.Id == userId);
        return (await ProjectUsersAsync(query, cancellationToken)).FirstOrDefault();
    }

    private static Task<List<AdminUserRowDto>> ProjectUsersAsync(
        IQueryable<Domain.Entities.ApplicationUser> query, CancellationToken cancellationToken) =>
        query
            .Select(u => new AdminUserRowDto
            {
                UserId = u.Id,
                FullName = u.FirstName + " " + u.LastName,
                Email = u.Email!,
                Role = u.JobSeeker != null ? Roles.JobSeeker : u.Employer != null ? Roles.Employer : Roles.Administrator,
                IsActive = u.IsActive,
                IsVerified = u.Employer != null && u.Employer.IsVerified,
                IsLockedOut = u.LockoutEnd > DateTime.UtcNow,
                CreatedDate = u.CreatedDate,
                LastLoginDate = u.LastLoginDate,
                JobSeekerId = u.JobSeeker == null ? null : u.JobSeeker.Id,
                EmployerId = u.Employer == null ? null : u.Employer.Id
            })
            .ToListAsync(cancellationToken);

    public async Task<Result> SetUserActiveAsync(
        string targetUserId, bool isActive, string adminUserId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(targetUserId))
        {
            return Result.Failure("A user must be selected.");
        }

        if (targetUserId == adminUserId)
        {
            return Result.Failure("You cannot change the status of your own account.");
        }

        var user = await _users.GetByIdAsync(targetUserId, cancellationToken);
        if (user is null)
        {
            return Result.Failure("User not found.");
        }

        user.IsActive = isActive;

        if (!isActive)
        {
            // Deactivate access by locking the account out until an administrator reverses it.
            user.LockoutEnd = DateTime.UtcNow.AddYears(5);
        }
        else
        {
            user.LockoutEnd = null;
        }

        _users.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Administrator {AdminUserId} set user {TargetUserId} active={IsActive}.",
            adminUserId, targetUserId, isActive);

        return Result.Success();
    }

    public async Task<Result> SetUserRoleAsync(
        string targetUserId, string newRole, string adminUserId, CancellationToken cancellationToken = default)
    {
        if (targetUserId == adminUserId)
        {
            return Result.Failure("You cannot change your own role.");
        }

        if (!Roles.All.Contains(newRole))
        {
            return Result.Failure("That role is not recognised.");
        }

        var user = await _users.GetByIdAsync(targetUserId, cancellationToken);
        if (user is null)
        {
            return Result.Failure("User not found.");
        }

        // Roles are implied by the presence of a candidate or employer profile. Switching
        // between candidate and employer therefore requires the matching profile to exist.
        var hasSeeker = await JobSeekerProfileExistsAsync(targetUserId, cancellationToken);
        var hasEmployer = await EmployerProfileExistsAsync(targetUserId, cancellationToken);

        var canSwitch = newRole switch
        {
            Roles.Administrator => !hasSeeker && !hasEmployer,
            Roles.Employer => hasEmployer && !hasSeeker,
            Roles.JobSeeker => hasSeeker && !hasEmployer,
            _ => false
        };

        if (!canSwitch)
        {
            return Result.Failure(
                "Roles follow the account's registration type. A candidate cannot be promoted to employer, or the reverse, because each needs a different profile.");
        }

        // Nothing to persist: the role claim is derived from the profile. Confirm and report.
        return Result.Success();
    }

    public async Task<Result> SetEmployerVerificationAsync(
        string targetUserId, bool isVerified, string adminUserId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(targetUserId))
        {
            return Result.Failure("A user must be selected.");
        }

        if (targetUserId == adminUserId)
        {
            return Result.Failure("You cannot change your own verification status.");
        }

        var user = await _users.Query()
            .Include(u => u.Employer)
            .FirstOrDefaultAsync(u => u.Id == targetUserId, cancellationToken);

        if (user is null)
        {
            return Result.Failure("User not found.");
        }

        if (user.Employer is null)
        {
            return Result.Failure("Only employer accounts can be verified.");
        }

        user.Employer.IsVerified = isVerified;
        user.IsActive = isVerified;
        user.LockoutEnd = isVerified ? null : DateTime.UtcNow.AddYears(5);

        _users.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Administrator {AdminUserId} set employer {TargetUserId} verified={IsVerified}.",
            adminUserId, targetUserId, isVerified);

        return Result.Success();
    }

    public async Task<PagedResult<VacancyModerationDto>> SearchVacanciesAsync(
        string? keyword, VacancyStatus? status, string? location, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 100 ? 15 : pageSize;

        var query = _jobs.Query();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim();
            query = query.Where(j =>
                EF.Functions.ILike(j.Title, $"%{kw}%") ||
                (j.Category != null && EF.Functions.ILike(j.Category, $"%{kw}%")) ||
                EF.Functions.ILike(j.Company!.Name, $"%{kw}%"));
        }

        if (status.HasValue)
        {
            var s = status.Value;
            query = query.Where(j => j.Status == s);
        }

        if (!string.IsNullOrWhiteSpace(location))
        {
            var loc = location.Trim();
            query = query.Where(j => j.WorkLocation != null && EF.Functions.ILike(j.WorkLocation, $"%{loc}%"));
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await ProjectAsync(query
            .OrderByDescending(j => j.PostedDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize), cancellationToken);

        return new PagedResult<VacancyModerationDto>(items, total, page, pageSize);
    }

    public async Task<VacancyModerationDto?> GetVacancyForReviewAsync(
        int jobId, CancellationToken cancellationToken = default)
    {
        var query = _jobs.Query().Where(j => j.Id == jobId);
        return (await ProjectAsync(query, cancellationToken)).FirstOrDefault();
    }

    private static Task<List<VacancyModerationDto>> ProjectAsync(
        IQueryable<Domain.Entities.Job> query, CancellationToken cancellationToken) =>
        query
            .Select(j => new VacancyModerationDto
            {
                Id = j.Id,
                Title = j.Title,
                CompanyName = j.Company!.Name,
                EmployerName = j.Employer!.CompanyName,
                EmployerEmail = j.Employer.User!.Email!,
                WorkLocation = j.WorkLocation,
                Category = j.Category,
                JobType = j.JobType,
                WorkArrangement = j.WorkArrangement,
                ExperienceLevel = j.ExperienceLevel,
                SalaryMin = j.SalaryMin,
                SalaryMax = j.SalaryMax,
                PostedDate = j.PostedDate,
                ApplicationDeadline = j.ApplicationDeadline,
                Status = j.Status,
                ApplicationCount = j.Applications.Count,
                Skills = j.RequiredSkills.Select(s => s.Name).ToList(),
                Description = j.Description,
                Responsibilities = j.Responsibilities,
                Requirements = j.Requirements,
                Benefits = j.Benefits,
                ModerationNotes = j.ModerationNotes
            })
            .ToListAsync(cancellationToken);

    public async Task<PagedResult<CompanyDto>> SearchCompaniesAsync(
        string? keyword, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 100 ? 15 : pageSize;

        var query = _companies.Query();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim();
            query = query.Where(c =>
                EF.Functions.ILike(c.Name, $"%{kw}%") ||
                (c.Industry != null && EF.Functions.ILike(c.Industry, $"%{kw}%")) ||
                (c.Location != null && EF.Functions.ILike(c.Location, $"%{kw}%")));
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(c => c.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new CompanyDto
            {
                Id = c.Id,
                EmployerId = c.EmployerId,
                Name = c.Name,
                LogoUrl = c.LogoUrl,
                Industry = c.Industry,
                Description = c.Description,
                Website = c.Website,
                Location = c.Location,
                ContactEmail = c.ContactEmail,
                ContactPhone = c.ContactPhone,
                CompanySize = c.CompanySize,
                FoundedYear = c.FoundedYear,
                ActiveVacancyCount = c.Jobs.Count(j => j.Status == VacancyStatus.Active),
                HasProfile = true
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<CompanyDto>(items, total, page, pageSize);
    }

    public async Task<PagedResult<ApplicationSummaryDto>> SearchApplicationsAsync(
        string? keyword, ApplicationStatus? status, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 100 ? 15 : pageSize;

        var query = _applications.Query();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim();
            query = query.Where(a =>
                EF.Functions.ILike(a.Job!.Title, $"%{kw}%") ||
                EF.Functions.ILike(a.Job!.Company!.Name, $"%{kw}%") ||
                EF.Functions.ILike(a.JobSeeker!.User!.FirstName, $"%{kw}%") ||
                EF.Functions.ILike(a.JobSeeker!.User!.LastName, $"%{kw}%"));
        }

        if (status.HasValue)
        {
            var s = status.Value;
            query = query.Where(a => a.Status == s);
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(a => a.AppliedDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new ApplicationSummaryDto
            {
                Id = a.Id,
                JobId = a.JobId,
                JobTitle = a.Job!.Title,
                CompanyName = a.Job.Company!.Name,
                Status = a.Status,
                AppliedDate = a.AppliedDate,
                LastUpdated = a.LastUpdated,
                CoverLetter = a.CoverLetter,
                CompatibilityScore = a.CompatibilityScore,
                CanWithdraw = false,
                Timeline = a.StatusHistory
                    .OrderBy(h => h.ChangedDate)
                    .Select(h => new ApplicationTimelineEntryDto
                    {
                        Status = h.Status,
                        Remarks = h.Remarks,
                        ChangedDate = h.ChangedDate
                    })
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        // Status wording is applied after materialisation; Describe() cannot be translated to SQL.
        foreach (var item in items)
        {
            item.StatusLabel = StatusRules.Describe(item.Status);
            foreach (var entry in item.Timeline)
            {
                entry.Label = StatusRules.Describe(entry.Status);
            }
        }

        return new PagedResult<ApplicationSummaryDto>(items, total, page, pageSize);
    }

    public async Task<AdminReportDto> BuildReportAsync(CancellationToken cancellationToken = default)
    {
        var report = new AdminReportDto();

        var jobSeekers = await _users.CountAsync(u => u.JobSeeker != null, cancellationToken);
        var employers = await _users.CountAsync(u => u.Employer != null, cancellationToken);
        var admins = await _users.CountAsync(u => u.JobSeeker == null && u.Employer == null, cancellationToken);

        report.UsersByRole = new List<GroupCountDto>
        {
            new() { Label = Roles.JobSeeker, Count = jobSeekers },
            new() { Label = Roles.Employer, Count = employers },
            new() { Label = Roles.Administrator, Count = admins }
        };

        report.VacanciesByStatus = await _jobs.Query()
            .GroupBy(j => j.Status)
            .Select(g => new GroupCountDto { Label = g.Key.ToString(), Count = g.Count() })
            .OrderByDescending(g => g.Count)
            .ToListAsync(cancellationToken);

        report.VacanciesByCategory = await _jobs.Query()
            .Where(j => j.Category != null)
            .GroupBy(j => j.Category!)
            .Select(g => new GroupCountDto { Label = g.Key, Count = g.Count() })
            .OrderByDescending(g => g.Count)
            .Take(12)
            .ToListAsync(cancellationToken);

        report.VacanciesByLocation = await _jobs.Query()
            .Where(j => j.WorkLocation != null)
            .GroupBy(j => j.WorkLocation!)
            .Select(g => new GroupCountDto { Label = g.Key, Count = g.Count() })
            .OrderByDescending(g => g.Count)
            .Take(12)
            .ToListAsync(cancellationToken);

        report.VacanciesByType = await _jobs.Query()
            .GroupBy(j => j.JobType)
            .Select(g => new GroupCountDto { Label = g.Key.ToString(), Count = g.Count() })
            .OrderByDescending(g => g.Count)
            .ToListAsync(cancellationToken);

        report.ApplicationsByStatus = await _applications.Query()
            .GroupBy(a => a.Status)
            .Select(g => new GroupCountDto { Label = StatusRules.Describe(g.Key), Count = g.Count() })
            .OrderByDescending(g => g.Count)
            .ToListAsync(cancellationToken);

        report.ApplicationsByLocation = await _applications.Query()
            .Where(a => a.Job!.WorkLocation != null)
            .GroupBy(a => a.Job!.WorkLocation!)
            .Select(g => new GroupCountDto { Label = g.Key, Count = g.Count() })
            .OrderByDescending(g => g.Count)
            .Take(12)
            .ToListAsync(cancellationToken);

        report.TopCategoriesByApplications = await _applications.Query()
            .Where(a => a.Job!.Category != null)
            .GroupBy(a => a.Job!.Category!)
            .Select(g => new GroupCountDto { Label = g.Key, Count = g.Count() })
            .OrderByDescending(g => g.Count)
            .Take(12)
            .ToListAsync(cancellationToken);

        report.TopLocationsByApplications = await _applications.Query()
            .Where(a => a.Job!.WorkLocation != null)
            .GroupBy(a => a.Job!.WorkLocation!)
            .Select(g => new GroupCountDto { Label = g.Key, Count = g.Count() })
            .OrderByDescending(g => g.Count)
            .Take(12)
            .ToListAsync(cancellationToken);

        report.TotalVacancies = await _jobs.CountAsync(cancellationToken);
        report.TotalApplications = await _applications.CountAsync(cancellationToken);

        if (report.TotalVacancies > 0)
        {
            report.ApplicationsPerVacancy = Math.Round((decimal)report.TotalApplications / report.TotalVacancies, 2);
        }

        var shortlistedOrBetter = await _applications.CountAsync(
            a => a.Status == ApplicationStatus.Shortlisted
                 || a.Status == ApplicationStatus.Interview
                 || a.Status == ApplicationStatus.Selected,
            cancellationToken);

        if (report.TotalApplications > 0)
        {
            report.ApplicationToShortlistRate =
                Math.Round(100.0 * shortlistedOrBetter / report.TotalApplications, 1);
        }

        var since = DateTime.UtcNow.AddMonths(-6);
        var startOfMonth = new DateTime(since.Year, since.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        report.MonthlyActivity = await _jobs.Query()
            .Where(j => j.PostedDate >= startOfMonth)
            .GroupBy(j => new { j.PostedDate.Year, j.PostedDate.Month })
            .Select(g => new MonthlyActivityDto
            {
                Month = new DateTime(g.Key.Year, g.Key.Month, 1).ToString("MMM yyyy"),
                Vacancies = g.Count(),
                Applications = 0,
                NewJobSeekers = 0
            })
            .ToListAsync(cancellationToken);

        var applicationsByMonth = await _applications.Query()
            .Where(a => a.AppliedDate >= startOfMonth)
            .GroupBy(a => new { a.AppliedDate.Year, a.AppliedDate.Month })
            .Select(g => new
            {
                Label = new DateTime(g.Key.Year, g.Key.Month, 1).ToString("MMM yyyy"),
                Count = g.Count()
            })
            .ToListAsync(cancellationToken);

        var seekersByMonth = await _users.Query()
            .Where(u => u.CreatedDate >= startOfMonth)
            .GroupBy(u => new { u.CreatedDate.Year, u.CreatedDate.Month })
            .Select(g => new
            {
                Label = new DateTime(g.Key.Year, g.Key.Month, 1).ToString("MMM yyyy"),
                Count = g.Count()
            })
            .ToListAsync(cancellationToken);

        foreach (var entry in report.MonthlyActivity)
        {
            entry.Applications = applicationsByMonth.FirstOrDefault(a => a.Label == entry.Month)?.Count ?? 0;
            entry.NewJobSeekers = seekersByMonth.FirstOrDefault(s => s.Label == entry.Month)?.Count ?? 0;
        }

        return report;
    }

    private Task<bool> JobSeekerProfileExistsAsync(string userId, CancellationToken cancellationToken) =>
        _users.Query().AnyAsync(u => u.Id == userId && u.JobSeeker != null, cancellationToken);

    private Task<bool> EmployerProfileExistsAsync(string userId, CancellationToken cancellationToken) =>
        _users.Query().AnyAsync(u => u.Id == userId && u.Employer != null, cancellationToken);
}
