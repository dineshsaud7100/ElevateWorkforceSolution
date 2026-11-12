using ElevateWorkforceSolution.Application.Common;
using ElevateWorkforceSolution.Application.DTOs;
using ElevateWorkforceSolution.Domain.Enums;

namespace ElevateWorkforceSolution.Application.Interfaces;

/// <summary>Read side of vacancies, including the public careers search.</summary>
public interface IJobSearchService
{
    Task<PagedResult<JobCardDto>> SearchPublicJobsAsync(JobSearchFilter filter, int? jobSeekerId = null, CancellationToken cancellationToken = default);

    Task<JobDetailDto?> GetPublicJobDetailsAsync(int jobId, int? jobSeekerId = null, CancellationToken cancellationToken = default);

    /// <summary>Distinct values used to populate the search filter dropdowns, taken from the database.</summary>
    Task<JobSearchFacets> GetFacetsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<JobCardDto>> GetLatestJobsAsync(int take, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<JobCardDto>> GetJobsForCompanyAsync(int companyId, int take, CancellationToken cancellationToken = default);

    Task<JobSearchFacets> GetEmployerFacetsAsync(int employerId, CancellationToken cancellationToken = default);
}

public class JobSearchFacets
{
    public List<string> Locations { get; set; } = new();
    public List<string> Categories { get; set; } = new();
    public List<CategoryCountDto> PopularCategories { get; set; } = new();
    public int TotalActiveVacancies { get; set; }
    public int TotalCompaniesHiring { get; set; }
}

public class CategoryCountDto
{
    public string Name { get; set; } = string.Empty;
    public int Count { get; set; }
}

/// <summary>Employer-side vacancy management, including the verification workflow.</summary>
public interface IVacancyService
{
    Task<VacancyFormDto?> GetForEditAsync(int jobId, int employerId, CancellationToken cancellationToken = default);

    Task<Result<int>> CreateAsync(VacancyFormDto form, int employerId, CancellationToken cancellationToken = default);

    Task<Result> UpdateAsync(VacancyFormDto form, int employerId, CancellationToken cancellationToken = default);

    Task<VacancyManagementDto?> GetForEmployerAsync(int jobId, int employerId, CancellationToken cancellationToken = default);

    Task<PagedResult<VacancyManagementDto>> ListForEmployerAsync(
        int employerId, VacancyStatus? status, string? keyword, int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>Employer closes a vacancy. Closing stops new applications but keeps existing ones.</summary>
    Task<Result> CloseAsync(int jobId, int employerId, CancellationToken cancellationToken = default);

    /// <summary>Employer reopens a closed vacancy, which returns it to pending verification.</summary>
    Task<Result> ReopenAsync(int jobId, int employerId, CancellationToken cancellationToken = default);

    /// <summary>Administrator approves a pending vacancy, making it publicly visible.</summary>
    Task<Result> ApproveAsync(int jobId, string adminUserId, CancellationToken cancellationToken = default);

    /// <summary>Administrator rejects a pending vacancy with a reason the employer can read.</summary>
    Task<Result> RejectAsync(int jobId, string adminUserId, string? notes, CancellationToken cancellationToken = default);

    /// <summary>Administrator removes a vacancy from public view without deleting records.</summary>
    Task<Result> RemoveAsync(int jobId, string adminUserId, string? notes, CancellationToken cancellationToken = default);

    /// <summary>Marks past-deadline active vacancies as expired. Invoked on application start-up.</summary>
    Task<int> ExpirePastDeadlineVacanciesAsync(CancellationToken cancellationToken = default);
}
