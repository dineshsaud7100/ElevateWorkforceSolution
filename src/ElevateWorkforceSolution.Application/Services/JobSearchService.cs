using ElevateWorkforceSolution.Application.Common;
using ElevateWorkforceSolution.Application.DTOs;
using ElevateWorkforceSolution.Application.Interfaces;
using ElevateWorkforceSolution.Application.Interfaces.Repositories;
using ElevateWorkforceSolution.Domain.Entities;
using ElevateWorkforceSolution.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using ApplicationEntity = ElevateWorkforceSolution.Domain.Entities.Application;
using ElevateWorkforceSolution.Domain.Common;

namespace ElevateWorkforceSolution.Application.Services;

/// <summary>
/// Read-only vacancy queries. Public listing is restricted to Active vacancies and every
/// filter, sort and page is executed in PostgreSQL.
/// </summary>
public class JobSearchService : IJobSearchService
{
    private readonly IRepository<Job> _jobs;
    private readonly IRepository<ApplicationEntity> _applications;
    private readonly IRepository<SavedJob> _savedJobs;
    private readonly IRepository<Company> _companies;

    public JobSearchService(
        IRepository<Job> jobs,
        IRepository<ApplicationEntity> applications,
        IRepository<SavedJob> savedJobs,
        IRepository<Company> companies)
    {
        _jobs = jobs;
        _applications = applications;
        _savedJobs = savedJobs;
        _companies = companies;
    }

    public async Task<PagedResult<JobCardDto>> SearchPublicJobsAsync(
        JobSearchFilter filter, int? jobSeekerId = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        filter.Normalise();

        var query = ApplyPublicFilters(_jobs.Query(), filter);

        var totalCount = await query.CountAsync(cancellationToken);

        query = ApplySort(query, filter.Sort);

        var page = await query
            .Skip((filter.PageNumber - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(j => new JobCardDto
            {
                Id = j.Id,
                Title = j.Title,
                CompanyName = j.Company!.Name,
                CompanyLogoUrl = j.Company.LogoUrl,
                WorkLocation = j.WorkLocation,
                Category = j.Category,
                JobType = j.JobType,
                WorkArrangement = j.WorkArrangement,
                ExperienceLevel = j.ExperienceLevel,
                SalaryMin = j.SalaryMin,
                SalaryMax = j.SalaryMax,
                SalaryCurrency = j.SalaryCurrency,
                SalaryPeriod = j.SalaryPeriod,
                PostedDate = j.PostedDate,
                ApplicationDeadline = j.ApplicationDeadline,
                Status = j.Status,
                EmployerId = j.EmployerId,
                CompanyId = j.CompanyId,
                Skills = j.RequiredSkills.Select(s => s.Name).ToList()
            })
            .ToListAsync(cancellationToken);

        if (page.Count == 0)
        {
            return new PagedResult<JobCardDto>(page, totalCount, filter.PageNumber, filter.PageSize);
        }

        await DecorateAsync(page, jobSeekerId, cancellationToken);

        return new PagedResult<JobCardDto>(page, totalCount, filter.PageNumber, filter.PageSize);
    }

    public async Task<JobDetailDto?> GetPublicJobDetailsAsync(
        int jobId, int? jobSeekerId = null, CancellationToken cancellationToken = default)
    {
        var job = await _jobs.Query()
            .Include(j => j.Company)
            .Include(j => j.Employer).ThenInclude(e => e!.User)
            .Include(j => j.RequiredSkills)
            .AsNoTracking()
            .FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);

        if (job is null)
        {
            return null;
        }

        var applicationCount = await _applications.CountAsync(a => a.JobId == jobId, cancellationToken);
        var companyVacancyCount = await _jobs.CountAsync(
            j => j.CompanyId == job.CompanyId && j.Status == VacancyStatus.Active, cancellationToken);

        bool isSaved = false;
        bool hasApplied = false;

        if (jobSeekerId.HasValue)
        {
            isSaved = await _savedJobs.AnyAsync(
                s => s.JobId == jobId && s.JobSeekerId == jobSeekerId.Value, cancellationToken);
            hasApplied = await _applications.AnyAsync(
                a => a.JobId == jobId && a.JobSeekerId == jobSeekerId.Value, cancellationToken);
        }

        return new JobDetailDto
        {
            Id = job.Id,
            Title = job.Title,
            Category = job.Category,
            Description = job.Description,
            Responsibilities = job.Responsibilities,
            Requirements = job.Requirements,
            Benefits = job.Benefits,
            JobType = job.JobType,
            WorkArrangement = job.WorkArrangement,
            ExperienceLevel = job.ExperienceLevel,
            SalaryMin = job.SalaryMin,
            SalaryMax = job.SalaryMax,
            SalaryCurrency = job.SalaryCurrency,
            SalaryPeriod = job.SalaryPeriod,
            WorkLocation = job.WorkLocation,
            Headcount = job.Headcount,
            PostedDate = job.PostedDate,
            ApplicationDeadline = job.ApplicationDeadline,
            Status = job.Status,
            EmployerId = job.EmployerId,
            EmployerName = job.Employer?.CompanyName ?? string.Empty,
            CompanyId = job.CompanyId,
            CompanyName = job.Company?.Name ?? string.Empty,
            CompanyLogoUrl = job.Company?.LogoUrl,
            CompanyIndustry = job.Company?.Industry,
            CompanyAbout = job.Company?.About ?? job.Company?.Description,
            CompanyWebsite = job.Company?.Website,
            CompanyLocation = job.Company?.Location,
            CompanySize = job.Company?.CompanySize,
            CompanyFoundedYear = job.Company?.FoundedYear,
            CompanyVacancyCount = companyVacancyCount,
            RequiredSkills = job.RequiredSkills.Select(s => s.Name).ToList(),
            ApplicationCount = applicationCount,
            AcceptsApplications = StatusRules.AcceptsApplications(job.Status) && !job.IsDeadlinePassed,
            IsSaved = isSaved,
            HasApplied = hasApplied,
            IsDeadlinePassed = job.IsDeadlinePassed,
            ModerationNotes = job.Status == VacancyStatus.Rejected ? job.ModerationNotes : null
        };
    }

    public async Task<JobSearchFacets> GetFacetsAsync(CancellationToken cancellationToken = default)
    {
        var active = _jobs.Query()
            .Where(j => j.Status == VacancyStatus.Active)
            .AsQueryable();

        var locations = await active
            .Where(j => j.WorkLocation != null)
            .Select(j => j.WorkLocation!)
            .Distinct()
            .OrderBy(l => l)
            .ToListAsync(cancellationToken);

        var categories = await active
            .Where(j => j.Category != null)
            .Select(j => j.Category!)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync(cancellationToken);

        var popular = await active
            .Where(j => j.Category != null)
            .GroupBy(j => j.Category!)
            .Select(g => new CategoryCountDto { Name = g.Key, Count = g.Count() })
            .OrderByDescending(c => c.Count)
            .Take(12)
            .ToListAsync(cancellationToken);

        var totalActive = await active.CountAsync(cancellationToken);
        var companiesHiring = await active.Select(j => j.CompanyId).Distinct().CountAsync(cancellationToken);

        return new JobSearchFacets
        {
            Locations = locations,
            Categories = categories,
            PopularCategories = popular,
            TotalActiveVacancies = totalActive,
            TotalCompaniesHiring = companiesHiring
        };
    }

    public async Task<IReadOnlyList<JobCardDto>> GetLatestJobsAsync(int take, CancellationToken cancellationToken = default)
    {
        var jobs = await _jobs.Query()
            .Where(j => j.Status == VacancyStatus.Active)
            .OrderByDescending(j => j.PostedDate)
            .Take(Math.Clamp(take, 1, 50))
            .Select(j => new JobCardDto
            {
                Id = j.Id,
                Title = j.Title,
                CompanyName = j.Company!.Name,
                CompanyLogoUrl = j.Company.LogoUrl,
                WorkLocation = j.WorkLocation,
                Category = j.Category,
                JobType = j.JobType,
                WorkArrangement = j.WorkArrangement,
                ExperienceLevel = j.ExperienceLevel,
                SalaryMin = j.SalaryMin,
                SalaryMax = j.SalaryMax,
                SalaryCurrency = j.SalaryCurrency,
                SalaryPeriod = j.SalaryPeriod,
                PostedDate = j.PostedDate,
                ApplicationDeadline = j.ApplicationDeadline,
                Status = j.Status,
                EmployerId = j.EmployerId,
                CompanyId = j.CompanyId,
                Skills = j.RequiredSkills.Select(s => s.Name).Take(6).ToList()
            })
            .ToListAsync(cancellationToken);

        await DecorateAsync(jobs, null, cancellationToken);
        return jobs;
    }

    public async Task<IReadOnlyList<JobCardDto>> GetJobsForCompanyAsync(
        int companyId, int take, CancellationToken cancellationToken = default)
    {
        var jobs = await _jobs.Query()
            .Where(j => j.CompanyId == companyId && j.Status == VacancyStatus.Active)
            .OrderByDescending(j => j.PostedDate)
            .Take(Math.Clamp(take, 1, 50))
            .Select(j => new JobCardDto
            {
                Id = j.Id,
                Title = j.Title,
                CompanyName = j.Company!.Name,
                CompanyLogoUrl = j.Company.LogoUrl,
                WorkLocation = j.WorkLocation,
                Category = j.Category,
                JobType = j.JobType,
                WorkArrangement = j.WorkArrangement,
                ExperienceLevel = j.ExperienceLevel,
                SalaryMin = j.SalaryMin,
                SalaryMax = j.SalaryMax,
                SalaryCurrency = j.SalaryCurrency,
                SalaryPeriod = j.SalaryPeriod,
                PostedDate = j.PostedDate,
                ApplicationDeadline = j.ApplicationDeadline,
                Status = j.Status,
                EmployerId = j.EmployerId,
                CompanyId = j.CompanyId,
                Skills = j.RequiredSkills.Select(s => s.Name).Take(6).ToList()
            })
            .ToListAsync(cancellationToken);

        await DecorateAsync(jobs, null, cancellationToken);
        return jobs;
    }

    public async Task<JobSearchFacets> GetEmployerFacetsAsync(int employerId, CancellationToken cancellationToken = default)
    {
        var own = _jobs.Query().Where(j => j.EmployerId == employerId);

        var locations = await own
            .Where(j => j.WorkLocation != null)
            .Select(j => j.WorkLocation!)
            .Distinct().OrderBy(l => l)
            .ToListAsync(cancellationToken);

        var categories = await own
            .Where(j => j.Category != null)
            .Select(j => j.Category!)
            .Distinct().OrderBy(c => c)
            .ToListAsync(cancellationToken);

        var popular = await own
            .Where(j => j.Category != null)
            .GroupBy(j => j.Category!)
            .Select(g => new CategoryCountDto { Name = g.Key, Count = g.Count() })
            .OrderByDescending(c => c.Count)
            .Take(12)
            .ToListAsync(cancellationToken);

        return new JobSearchFacets
        {
            Locations = locations,
            Categories = categories,
            PopularCategories = popular,
            TotalActiveVacancies = await own.CountAsync(j => j.Status == VacancyStatus.Active, cancellationToken),
            TotalCompaniesHiring = 1
        };
    }

    private static IQueryable<Job> ApplyPublicFilters(IQueryable<Job> query, JobSearchFilter filter)
    {
        // Only approved vacancies are ever publicly listed.
        query = query.Where(j => j.Status == VacancyStatus.Active);

        if (!string.IsNullOrWhiteSpace(filter.Keyword))
        {
            var keyword = filter.Keyword.Trim();
            query = query.Where(j =>
                EF.Functions.ILike(j.Title, $"%{keyword}%") ||
                (j.Description != null && EF.Functions.ILike(j.Description, $"%{keyword}%")) ||
                (j.Category != null && EF.Functions.ILike(j.Category, $"%{keyword}%")) ||
                (j.WorkLocation != null && EF.Functions.ILike(j.WorkLocation, $"%{keyword}%")) ||
                (j.Company != null && EF.Functions.ILike(j.Company.Name, $"%{keyword}%")) ||
                j.RequiredSkills.Any(s => EF.Functions.ILike(s.Name, $"%{keyword}%")));
        }

        if (!string.IsNullOrWhiteSpace(filter.Location))
        {
            var location = filter.Location.Trim();
            query = query.Where(j => j.WorkLocation != null && EF.Functions.ILike(j.WorkLocation, $"%{location}%"));
        }

        if (!string.IsNullOrWhiteSpace(filter.Category))
        {
            var category = filter.Category.Trim();
            query = query.Where(j => j.Category != null && EF.Functions.ILike(j.Category, category));
        }

        if (filter.JobType.HasValue)
        {
            var jobType = filter.JobType.Value;
            query = query.Where(j => j.JobType == jobType);
        }

        if (filter.WorkArrangement.HasValue)
        {
            var arrangement = filter.WorkArrangement.Value;
            query = query.Where(j => j.WorkArrangement == arrangement);
        }

        if (filter.ExperienceLevel.HasValue)
        {
            var level = filter.ExperienceLevel.Value;
            query = query.Where(j => j.ExperienceLevel == level);
        }

        if (filter.SalaryMin.HasValue)
        {
            // A vacancy matches when its upper bound reaches the candidate's floor.
            var min = filter.SalaryMin.Value;
            query = query.Where(j => (j.SalaryMax ?? j.SalaryMin) != null && (j.SalaryMax ?? j.SalaryMin) >= min);
        }

        if (filter.SalaryMax.HasValue)
        {
            var max = filter.SalaryMax.Value;
            query = query.Where(j => (j.SalaryMin ?? j.SalaryMax) != null && (j.SalaryMin ?? j.SalaryMax) <= max);
        }

        if (filter.PostedAfter.HasValue)
        {
            var from = filter.PostedAfter.Value;
            query = query.Where(j => j.PostedDate >= from);
        }

        return query;
    }

    private static IQueryable<Job> ApplySort(IQueryable<Job> query, JobSortOption sort) => sort switch
    {
        JobSortOption.SalaryHighToLow => query.OrderByDescending(j => j.SalaryMax ?? j.SalaryMin).ThenByDescending(j => j.PostedDate),
        JobSortOption.SalaryLowToHigh => query.OrderBy(j => j.SalaryMin ?? j.SalaryMax).ThenByDescending(j => j.PostedDate),
        JobSortOption.ClosingDateSoonest => query
            .OrderBy(j => j.ApplicationDeadline.HasValue ? 0 : 1)
            .ThenBy(j => j.ApplicationDeadline)
            .ThenByDescending(j => j.PostedDate),
        _ => query.OrderByDescending(j => j.PostedDate)
    };

    /// <summary>
    /// Fills in per-card counts and viewer-specific flags using a fixed number of
    /// batched queries, so a page of results never triggers N+1 round trips.
    /// </summary>
    private async Task DecorateAsync(List<JobCardDto> page, int? jobSeekerId, CancellationToken cancellationToken)
    {
        var jobIds = page.Select(j => j.Id).ToList();

        var counts = await _applications.Query()
            .Where(a => jobIds.Contains(a.JobId))
            .GroupBy(a => a.JobId)
            .Select(g => new { JobId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var countsByJob = counts.ToDictionary(c => c.JobId, c => c.Count);
        foreach (var card in page)
        {
            card.ApplicationCount = countsByJob.TryGetValue(card.Id, out var c) ? c : 0;
        }

        if (!jobSeekerId.HasValue)
        {
            return;
        }

        var savedIds = await _savedJobs.Query()
            .Where(s => jobIds.Contains(s.JobId) && s.JobSeekerId == jobSeekerId.Value)
            .Select(s => s.JobId)
            .ToListAsync(cancellationToken);

        var appliedIds = await _applications.Query()
            .Where(a => jobIds.Contains(a.JobId) && a.JobSeekerId == jobSeekerId.Value)
            .Select(a => a.JobId)
            .ToListAsync(cancellationToken);

        var saved = savedIds.ToHashSet();
        var applied = appliedIds.ToHashSet();

        foreach (var card in page)
        {
            card.IsSaved = saved.Contains(card.Id);
            card.HasApplied = applied.Contains(card.Id);
        }
    }
}
