using ElevateWorkforceSolution.Application.Common;
using ElevateWorkforceSolution.Application.DTOs;
using ElevateWorkforceSolution.Domain.Enums;

namespace ElevateWorkforceSolution.Application.Interfaces;

/// <summary>Aggregated, database-driven metrics for each role's dashboard.</summary>
public interface IDashboardService
{
    Task<JobSeekerDashboardDto> GetForJobSeekerAsync(int jobSeekerId, CancellationToken cancellationToken = default);

    Task<EmployerDashboardDto> GetForEmployerAsync(int employerId, CancellationToken cancellationToken = default);

    Task<AdminDashboardDto> GetForAdministratorAsync(CancellationToken cancellationToken = default);
}

/// <summary>Platform administration: user, vacancy and company oversight plus reports.</summary>
public interface IAdminService
{
    Task<PagedResult<AdminUserRowDto>> SearchUsersAsync(string? keyword, string? role, bool? activeOnly, int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>A single account by id, used to read the current state before acting on it.</summary>
    Task<AdminUserRowDto?> GetUserAsync(string userId, CancellationToken cancellationToken = default);

    Task<Result> SetUserActiveAsync(string targetUserId, bool isActive, string adminUserId, CancellationToken cancellationToken = default);

    Task<Result> SetUserRoleAsync(string targetUserId, string newRole, string adminUserId, CancellationToken cancellationToken = default);

    Task<Result> SetEmployerVerificationAsync(string targetUserId, bool isVerified, string adminUserId, CancellationToken cancellationToken = default);

    Task<PagedResult<VacancyModerationDto>> SearchVacanciesAsync(
        string? keyword, VacancyStatus? status, string? location, int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>Full vacancy content for the moderation review screen, whatever its status.</summary>
    Task<VacancyModerationDto?> GetVacancyForReviewAsync(int jobId, CancellationToken cancellationToken = default);

    Task<PagedResult<CompanyDto>> SearchCompaniesAsync(string? keyword, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<PagedResult<ApplicationSummaryDto>> SearchApplicationsAsync(
        string? keyword, ApplicationStatus? status, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<AdminReportDto> BuildReportAsync(CancellationToken cancellationToken = default);
}

public class AdminReportDto
{
    public List<GroupCountDto> UsersByRole { get; set; } = new();
    public List<GroupCountDto> VacanciesByStatus { get; set; } = new();
    public List<GroupCountDto> VacanciesByCategory { get; set; } = new();
    public List<GroupCountDto> VacanciesByLocation { get; set; } = new();
    public List<GroupCountDto> VacanciesByType { get; set; } = new();
    public List<GroupCountDto> ApplicationsByStatus { get; set; } = new();
    public List<GroupCountDto> ApplicationsByLocation { get; set; } = new();
    public List<GroupCountDto> TopCategoriesByApplications { get; set; } = new();
    public List<GroupCountDto> TopLocationsByApplications { get; set; } = new();
    public List<MonthlyActivityDto> MonthlyActivity { get; set; } = new();
    public int TotalApplications { get; set; }
    public int TotalVacancies { get; set; }
    public decimal? ApplicationsPerVacancy { get; set; }
    public double? ApplicationToShortlistRate { get; set; }
}

public class MonthlyActivityDto
{
    public string Month { get; set; } = string.Empty;
    public int Vacancies { get; set; }
    public int Applications { get; set; }
    public int NewJobSeekers { get; set; }
}

/// <summary>Resolves the current principal to its role-specific profile ids.</summary>
public interface ICurrentUserService
{
    string? UserId { get; }
    string? Email { get; }
    bool IsAuthenticated { get; }
    bool IsAdministrator { get; }
    bool IsEmployer { get; }
    bool IsJobSeeker { get; }

    Task<int?> GetEmployerIdAsync(CancellationToken cancellationToken = default);
    Task<int?> GetJobSeekerIdAsync(CancellationToken cancellationToken = default);
}
