using ElevateWorkforceSolution.Application.Common;
using ElevateWorkforceSolution.Application.DTOs;
using ElevateWorkforceSolution.Application.Interfaces;
using ElevateWorkforceSolution.Application.Interfaces.Repositories;
using ElevateWorkforceSolution.Domain.Entities;
using ElevateWorkforceSolution.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ElevateWorkforceSolution.Application.Services;

/// <summary>Employer registration and public company profile management.</summary>
public class EmployerProfileService : IEmployerProfileService
{
    private readonly IRepository<Employer> _employers;
    private readonly IRepository<Company> _companies;
    private readonly IRepository<Job> _jobs;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<EmployerProfileService> _logger;

    public EmployerProfileService(
        IRepository<Employer> employers,
        IRepository<Company> companies,
        IRepository<Job> jobs,
        IUnitOfWork unitOfWork,
        ILogger<EmployerProfileService> logger)
    {
        _employers = employers;
        _companies = companies;
        _jobs = jobs;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<int>> EnsureEmployerAsync(
        string userId, string companyName, CancellationToken cancellationToken = default)
    {
        var existing = await _employers.Query()
            .Where(e => e.UserId == userId)
            .Select(e => new { e.Id })
            .FirstOrDefaultAsync(cancellationToken);

        if (existing is not null)
        {
            return Result.Success(existing.Id);
        }

        var employer = new Employer
        {
            UserId = userId,
            CompanyName = string.IsNullOrWhiteSpace(companyName) ? "My Company" : companyName.Trim(),
            CreatedDate = DateTime.UtcNow
        };

        await _employers.AddAsync(employer, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Employer profile {EmployerId} created for user {UserId}.", employer.Id, userId);
        return Result.Success(employer.Id);
    }

    public async Task<CompanyDto?> GetCompanyAsync(int employerId, CancellationToken cancellationToken = default)
    {
        var company = await _companies.Query()
            .Where(c => c.EmployerId == employerId)
            .FirstOrDefaultAsync(cancellationToken);

        if (company is null)
        {
            return null;
        }

        var activeVacancies = await _jobs.CountAsync(
            j => j.CompanyId == company.Id && j.Status == VacancyStatus.Active, cancellationToken);

        return Map(company, activeVacancies, hasProfile: true);
    }

    public async Task<CompanyDto?> GetCompanyForUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        var company = await _companies.Query()
            .Where(c => c.Employer!.UserId == userId)
            .FirstOrDefaultAsync(cancellationToken);

        if (company is null)
        {
            return null;
        }

        var activeVacancies = await _jobs.CountAsync(
            j => j.CompanyId == company.Id && j.Status == VacancyStatus.Active, cancellationToken);

        return Map(company, activeVacancies, hasProfile: true);
    }

    public async Task<Result<int>> CreateOrUpdateCompanyAsync(
        int employerId, CompanyFormDto model, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);

        if (!await _employers.AnyAsync(e => e.Id == employerId, cancellationToken))
        {
            return Result.Failure<int>("Employer profile not found.");
        }

        if (string.IsNullOrWhiteSpace(model.Name))
        {
            return Result.Failure<int>("A company name is required.");
        }

        var company = await _companies.Query()
            .Where(c => c.EmployerId == employerId)
            .FirstOrDefaultAsync(cancellationToken);

        if (company is null)
        {
            company = new Company
            {
                EmployerId = employerId,
                CreatedDate = DateTime.UtcNow
            };
        }

        company.Name = model.Name.Trim();
        company.Industry = model.Industry?.Trim();
        company.Description = model.Description?.Trim();
        company.About = model.About?.Trim();
        company.Website = NormaliseWebsite(model.Website);
        company.Location = model.Location?.Trim();
        company.ContactEmail = model.ContactEmail?.Trim();
        company.ContactPhone = model.ContactPhone?.Trim();
        company.CompanySize = model.CompanySize;
        company.FoundedYear = model.FoundedYear;
        company.UpdatedDate = DateTime.UtcNow;

        // Keep the employer's display name in step with the company profile.
        var employer = await _employers.GetByIdAsync(employerId, cancellationToken);
        if (employer is not null)
        {
            employer.CompanyName = company.Name;
            _employers.Update(employer);
        }

        if (company.Id == 0)
        {
            await _companies.AddAsync(company, cancellationToken);
        }
        else
        {
            _companies.Update(company);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Company profile {CompanyId} saved for employer {EmployerId}.", company.Id, employerId);
        return Result.Success(company.Id);
    }

    public async Task<CompanyPublicDto?> GetPublicCompanyAsync(int companyId, CancellationToken cancellationToken = default)
    {
        var company = await _companies.Query()
            .Where(c => c.Id == companyId)
            .FirstOrDefaultAsync(cancellationToken);

        if (company is null)
        {
            return null;
        }

        var vacancies = await _jobs.Query()
            .Where(j => j.CompanyId == companyId && j.Status == VacancyStatus.Active)
            .OrderByDescending(j => j.PostedDate)
            .Take(12)
            .Select(j => new JobCardDto
            {
                Id = j.Id,
                Title = j.Title,
                CompanyName = company.Name,
                CompanyLogoUrl = company.LogoUrl,
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

        var activeCount = await _jobs.CountAsync(
            j => j.CompanyId == companyId && j.Status == VacancyStatus.Active, cancellationToken);

        return new CompanyPublicDto
        {
            Id = company.Id,
            Name = company.Name,
            LogoUrl = company.LogoUrl,
            Industry = company.Industry,
            Description = company.Description,
            About = company.About,
            Website = company.Website,
            Location = company.Location,
            CompanySize = company.CompanySize,
            FoundedYear = company.FoundedYear,
            ActiveVacancyCount = activeCount,
            Vacancies = vacancies
        };
    }

    public async Task<PagedResult<CompanyPublicDto>> SearchPublicCompaniesAsync(
        string? keyword, string? industry, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 48 ? 12 : pageSize;

        var query = _companies.Query().AsQueryable();

        // Only companies with at least one live vacancy appear in the public showcase.
        query = query.Where(c => c.Jobs.Any(j => j.Status == VacancyStatus.Active));

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim();
            query = query.Where(c =>
                EF.Functions.ILike(c.Name, $"%{kw}%") ||
                (c.Industry != null && EF.Functions.ILike(c.Industry, $"%{kw}%")) ||
                (c.Location != null && EF.Functions.ILike(c.Location, $"%{kw}%")));
        }

        if (!string.IsNullOrWhiteSpace(industry))
        {
            var ind = industry.Trim();
            query = query.Where(c => c.Industry != null && EF.Functions.ILike(c.Industry, ind));
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(c => c.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new CompanyPublicDto
            {
                Id = c.Id,
                Name = c.Name,
                LogoUrl = c.LogoUrl,
                Industry = c.Industry,
                Description = c.Description,
                About = c.About,
                Website = c.Website,
                Location = c.Location,
                CompanySize = c.CompanySize,
                FoundedYear = c.FoundedYear,
                ActiveVacancyCount = c.Jobs.Count(j => j.Status == VacancyStatus.Active)
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<CompanyPublicDto>(items, total, page, pageSize);
    }

    private static string? NormaliseWebsite(string? website)
    {
        if (string.IsNullOrWhiteSpace(website))
        {
            return null;
        }

        var value = website.Trim();
        if (!value.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            && !value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            value = "https://" + value;
        }

        return value;
    }

    private static CompanyDto Map(Company c, int activeVacancies, bool hasProfile) => new()
    {
        Id = c.Id,
        EmployerId = c.EmployerId,
        Name = c.Name,
        LogoUrl = c.LogoUrl,
        Industry = c.Industry,
        Description = c.Description,
        About = c.About,
        Website = c.Website,
        Location = c.Location,
        ContactEmail = c.ContactEmail,
        ContactPhone = c.ContactPhone,
        CompanySize = c.CompanySize,
        FoundedYear = c.FoundedYear,
        ActiveVacancyCount = activeVacancies,
        HasProfile = hasProfile
    };
}
