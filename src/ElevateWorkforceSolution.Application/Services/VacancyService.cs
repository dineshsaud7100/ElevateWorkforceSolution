using ElevateWorkforceSolution.Application.Common;
using ElevateWorkforceSolution.Application.DTOs;
using ElevateWorkforceSolution.Application.Interfaces;
using ElevateWorkforceSolution.Application.Interfaces.Repositories;
using ElevateWorkforceSolution.Domain.Common;
using ElevateWorkforceSolution.Domain.Entities;
using ElevateWorkforceSolution.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using ApplicationEntity = ElevateWorkforceSolution.Domain.Entities.Application;
using Microsoft.Extensions.Logging;

namespace ElevateWorkforceSolution.Application.Services;

/// <summary>
/// Vacancy lifecycle. Every new vacancy starts as PendingVerification and only becomes
/// publicly visible once an administrator approves it. Employers are always restricted
/// to their own vacancies.
/// </summary>
public class VacancyService : IVacancyService
{
    private readonly IRepository<Job> _jobs;
    private readonly IRepository<JobRequiredSkill> _skills;
    private readonly IRepository<Company> _companies;
    private readonly IRepository<Employer> _employers;
    private readonly IRepository<ApplicationEntity> _applications;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<VacancyService> _logger;

    public VacancyService(
        IRepository<Job> jobs,
        IRepository<JobRequiredSkill> skills,
        IRepository<Company> companies,
        IRepository<Employer> employers,
        IRepository<ApplicationEntity> applications,
        IUnitOfWork unitOfWork,
        ILogger<VacancyService> logger)
    {
        _jobs = jobs;
        _skills = skills;
        _companies = companies;
        _employers = employers;
        _applications = applications;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<VacancyFormDto?> GetForEditAsync(int jobId, int employerId, CancellationToken cancellationToken = default)
    {
        var job = await _jobs.QueryTracked()
            .Include(j => j.RequiredSkills)
            .FirstOrDefaultAsync(j => j.Id == jobId && j.EmployerId == employerId, cancellationToken);

        if (job is null)
        {
            return null;
        }

        return new VacancyFormDto
        {
            Id = job.Id,
            Title = job.Title,
            Category = job.Category,
            Description = job.Description ?? string.Empty,
            Responsibilities = job.Responsibilities,
            Requirements = job.Requirements,
            Benefits = job.Benefits,
            JobType = job.JobType,
            WorkArrangement = job.WorkArrangement,
            ExperienceLevel = job.ExperienceLevel,
            Headcount = job.Headcount ?? 1,
            SalaryMin = job.SalaryMin,
            SalaryMax = job.SalaryMax,
            SalaryCurrency = job.SalaryCurrency ?? "NPR",
            SalaryPeriod = job.SalaryPeriod ?? "Monthly",
            WorkLocation = job.WorkLocation,
            ApplicationDeadline = job.ApplicationDeadline,
            Skills = string.Join(", ", job.RequiredSkills.Select(s => s.Name))
        };
    }

    public async Task<Result<int>> CreateAsync(VacancyFormDto form, int employerId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(form);

        var company = await _companies.Query()
            .FirstOrDefaultAsync(c => c.EmployerId == employerId, cancellationToken);
        if (company is null)
        {
            return Result.Failure<int>("Create your company profile before posting a vacancy.");
        }

        var employer = await _employers.Query()
            .FirstOrDefaultAsync(e => e.Id == employerId, cancellationToken);
        if (employer is null)
        {
            return Result.Failure<int>("Your employer profile could not be found.");
        }

        var errors = Validate(form);
        if (errors.Count > 0)
        {
            return Result.Failure<int>(string.Join(" ", errors));
        }

        var job = new Job
        {
            EmployerId = employerId,
            CompanyId = company.Id,
            Title = form.Title.Trim(),
            Category = form.Category?.Trim(),
            Description = form.Description?.Trim(),
            Responsibilities = form.Responsibilities?.Trim(),
            Requirements = form.Requirements?.Trim(),
            Benefits = form.Benefits?.Trim(),
            JobType = form.JobType,
            WorkArrangement = form.WorkArrangement,
            ExperienceLevel = form.ExperienceLevel,
            Headcount = form.Headcount,
            SalaryMin = form.SalaryMin,
            SalaryMax = form.SalaryMax,
            SalaryCurrency = string.IsNullOrWhiteSpace(form.SalaryCurrency) ? "NPR" : form.SalaryCurrency.Trim(),
            SalaryPeriod = string.IsNullOrWhiteSpace(form.SalaryPeriod) ? "Monthly" : form.SalaryPeriod.Trim(),
            WorkLocation = form.WorkLocation?.Trim(),
            ApplicationDeadline = ConvertDeadlineToUtc(form.ApplicationDeadline),
            PostedDate = DateTime.UtcNow,
            Status = StatusRules.InitialVacancyStatus()
        };

        await _jobs.AddAsync(job, cancellationToken);

        var skills = VacancyFormDto.NormaliseSkills(form.Skills);
        foreach (var skill in skills)
        {
            job.RequiredSkills.Add(new JobRequiredSkill { Job = job, Name = skill });
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Vacancy {JobId} created by employer {EmployerId} and submitted for verification.",
            job.Id, employerId);

        return Result.Success(job.Id);
    }

    public async Task<Result> UpdateAsync(VacancyFormDto form, int employerId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(form);

        if (!form.Id.HasValue)
        {
            return Result.Failure("A vacancy id is required to update a vacancy.");
        }

        var job = await _jobs.QueryTracked()
            .Include(j => j.RequiredSkills)
            .FirstOrDefaultAsync(j => j.Id == form.Id.Value && j.EmployerId == employerId, cancellationToken);

        if (job is null)
        {
            return Result.Failure("Vacancy not found, or it does not belong to your company.");
        }

        var errors = Validate(form);
        if (errors.Count > 0)
        {
            return Result.Failure(string.Join(" ", errors));
        }

        job.Title = form.Title.Trim();
        job.Category = form.Category?.Trim();
        job.Description = form.Description?.Trim();
        job.Responsibilities = form.Responsibilities?.Trim();
        job.Requirements = form.Requirements?.Trim();
        job.Benefits = form.Benefits?.Trim();
        job.JobType = form.JobType;
        job.WorkArrangement = form.WorkArrangement;
        job.ExperienceLevel = form.ExperienceLevel;
        job.Headcount = form.Headcount;
        job.SalaryMin = form.SalaryMin;
        job.SalaryMax = form.SalaryMax;
        job.SalaryCurrency = string.IsNullOrWhiteSpace(form.SalaryCurrency) ? "NPR" : form.SalaryCurrency.Trim();
        job.SalaryPeriod = string.IsNullOrWhiteSpace(form.SalaryPeriod) ? "Monthly" : form.SalaryPeriod.Trim();
        job.WorkLocation = form.WorkLocation?.Trim();
        job.ApplicationDeadline = ConvertDeadlineToUtc(form.ApplicationDeadline);

        SyncSkills(job, VacancyFormDto.NormaliseSkills(form.Skills));

        // Material edits to a live vacancy must be re-verified before they are public again.
        if (job.Status == VacancyStatus.Active)
        {
            job.Status = VacancyStatus.PendingVerification;
            job.VerifiedDate = null;
            job.VerifiedByUserId = null;
            job.ModerationNotes = "The vacancy was edited and is awaiting re-verification.";
        }

        _jobs.Update(job);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Vacancy {JobId} updated by employer {EmployerId}.", job.Id, employerId);
        return Result.Success();
    }

    public async Task<VacancyManagementDto?> GetForEmployerAsync(
        int jobId, int employerId, CancellationToken cancellationToken = default)
    {
        return await _jobs.Query()
            .Where(j => j.Id == jobId && j.EmployerId == employerId)
            .Select(j => new VacancyManagementDto
            {
                Id = j.Id,
                Title = j.Title,
                Category = j.Category,
                JobType = j.JobType,
                WorkArrangement = j.WorkArrangement,
                WorkLocation = j.WorkLocation,
                SalaryMin = j.SalaryMin,
                SalaryMax = j.SalaryMax,
                PostedDate = j.PostedDate,
                ApplicationDeadline = j.ApplicationDeadline,
                Status = j.Status,
                ApplicationCount = j.Applications.Count,
                ShortlistedCount = j.Applications.Count(a => a.Status == ApplicationStatus.Shortlisted),
                InterviewCount = j.Applications.Count(a => a.Status == ApplicationStatus.Interview),
                ModerationNotes = j.ModerationNotes,
                VerifiedDate = j.VerifiedDate
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<PagedResult<VacancyManagementDto>> ListForEmployerAsync(
        int employerId, VacancyStatus? status, string? keyword, int page, int pageSize,
        CancellationToken cancellationToken = default)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 100 ? 10 : pageSize;

        var query = _jobs.Query().Where(j => j.EmployerId == employerId);

        if (status.HasValue)
        {
            var s = status.Value;
            query = query.Where(j => j.Status == s);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim();
            query = query.Where(j => EF.Functions.ILike(j.Title, $"%{kw}%"));
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(j => j.PostedDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(j => new VacancyManagementDto
            {
                Id = j.Id,
                Title = j.Title,
                Category = j.Category,
                JobType = j.JobType,
                WorkArrangement = j.WorkArrangement,
                WorkLocation = j.WorkLocation,
                SalaryMin = j.SalaryMin,
                SalaryMax = j.SalaryMax,
                PostedDate = j.PostedDate,
                ApplicationDeadline = j.ApplicationDeadline,
                Status = j.Status,
                ApplicationCount = j.Applications.Count,
                ShortlistedCount = j.Applications.Count(a => a.Status == ApplicationStatus.Shortlisted),
                InterviewCount = j.Applications.Count(a => a.Status == ApplicationStatus.Interview),
                ModerationNotes = j.ModerationNotes,
                VerifiedDate = j.VerifiedDate
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<VacancyManagementDto>(items, total, page, pageSize);
    }

    public async Task<Result> CloseAsync(int jobId, int employerId, CancellationToken cancellationToken = default)
    {
        var job = await _jobs.GetByIdAsync(jobId, cancellationToken);

        if (job is null || job.EmployerId != employerId)
        {
            return Result.Failure("Vacancy not found, or it does not belong to your company.");
        }

        if (job.Status == VacancyStatus.Closed)
        {
            return Result.Failure("This vacancy is already closed.");
        }

        job.Status = VacancyStatus.Closed;
        job.ClosedDate = DateTime.UtcNow;

        _jobs.Update(job);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Vacancy {JobId} closed by employer {EmployerId}.", jobId, employerId);
        return Result.Success();
    }

    public async Task<Result> ReopenAsync(int jobId, int employerId, CancellationToken cancellationToken = default)
    {
        var job = await _jobs.GetByIdAsync(jobId, cancellationToken);

        if (job is null || job.EmployerId != employerId)
        {
            return Result.Failure("Vacancy not found, or it does not belong to your company.");
        }

        if (job.Status is not (VacancyStatus.Closed or VacancyStatus.Expired))
        {
            return Result.Failure("Only closed or expired vacancies can be reopened.");
        }

        // Reopening always requires administrator re-approval.
        job.Status = VacancyStatus.PendingVerification;
        job.ClosedDate = null;
        job.VerifiedDate = null;
        job.VerifiedByUserId = null;
        job.ModerationNotes = "The vacancy was reopened and is awaiting verification.";

        _jobs.Update(job);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Vacancy {JobId} reopened and resubmitted for verification.", jobId);
        return Result.Success();
    }

    public async Task<Result> ApproveAsync(int jobId, string adminUserId, CancellationToken cancellationToken = default)
    {
        var job = await _jobs.GetByIdAsync(jobId, cancellationToken);

        if (job is null)
        {
            return Result.Failure("Vacancy not found.");
        }

        if (job.Status != VacancyStatus.PendingVerification)
        {
            return Result.Failure($"Only pending vacancies can be approved. This vacancy is {job.Status}.");
        }

        job.Status = VacancyStatus.Active;
        job.VerifiedDate = DateTime.UtcNow;
        job.VerifiedByUserId = adminUserId;
        job.ModerationNotes = null;
        job.PostedDate = DateTime.UtcNow;

        _jobs.Update(job);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Vacancy {JobId} approved by administrator {AdminUserId} and is now public.", jobId, adminUserId);
        return Result.Success();
    }

    public async Task<Result> RejectAsync(int jobId, string adminUserId, string? notes, CancellationToken cancellationToken = default)
    {
        var job = await _jobs.GetByIdAsync(jobId, cancellationToken);

        if (job is null)
        {
            return Result.Failure("Vacancy not found.");
        }

        if (job.Status != VacancyStatus.PendingVerification)
        {
            return Result.Failure($"Only pending vacancies can be rejected. This vacancy is {job.Status}.");
        }

        job.Status = VacancyStatus.Rejected;
        job.VerifiedDate = DateTime.UtcNow;
        job.VerifiedByUserId = adminUserId;
        job.ModerationNotes = string.IsNullOrWhiteSpace(notes)
            ? "The vacancy did not meet the platform requirements."
            : notes.Trim();

        _jobs.Update(job);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Vacancy {JobId} rejected by administrator {AdminUserId}.", jobId, adminUserId);
        return Result.Success();
    }

    public async Task<Result> RemoveAsync(int jobId, string adminUserId, string? notes, CancellationToken cancellationToken = default)
    {
        var job = await _jobs.GetByIdAsync(jobId, cancellationToken);

        if (job is null)
        {
            return Result.Failure("Vacancy not found.");
        }

        if (job.Status == VacancyStatus.Removed)
        {
            return Result.Failure("This vacancy has already been removed.");
        }

        job.Status = VacancyStatus.Removed;
        job.ModerationNotes = string.IsNullOrWhiteSpace(notes) ? "Removed by an administrator." : notes.Trim();

        _jobs.Update(job);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Vacancy {JobId} removed by administrator {AdminUserId}.", jobId, adminUserId);
        return Result.Success();
    }

    public async Task<int> ExpirePastDeadlineVacanciesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        // Tracked on purpose: the same context may still hold these vacancies from an earlier
        // operation (for example straight after seeding), and attaching a duplicate instance
        // would throw an identity conflict.
        var expired = await _jobs.QueryTracked()
            .Where(j => j.Status == VacancyStatus.Active
                        && j.ApplicationDeadline.HasValue
                        && j.ApplicationDeadline.Value < now)
            .ToListAsync(cancellationToken);

        if (expired.Count == 0)
        {
            return 0;
        }

        foreach (var job in expired)
        {
            job.Status = VacancyStatus.Expired;
            _jobs.Update(job);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Marked {Count} vacancies as expired after their deadline.", expired.Count);
        return expired.Count;
    }

    private static List<string> Validate(VacancyFormDto form)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(form.Title))
        {
            errors.Add("A job title is required.");
        }

        if (string.IsNullOrWhiteSpace(form.Description) || form.Description.Trim().Length < 30)
        {
            errors.Add("Provide a job description of at least 30 characters.");
        }

        if (form.ApplicationDeadline.HasValue && form.ApplicationDeadline.Value.Date < DateTime.UtcNow.Date)
        {
            errors.Add("The application deadline cannot be in the past.");
        }

        if (form.SalaryMin.HasValue && form.SalaryMax.HasValue && form.SalaryMin > form.SalaryMax)
        {
            errors.Add("The minimum salary cannot be greater than the maximum salary.");
        }

        if (form.Headcount < 1)
        {
            errors.Add("At least one vacancy is required.");
        }

        return errors;
    }

    private static DateTime? ConvertDeadlineToUtc(DateTime? deadline)
    {
        if (!deadline.HasValue)
        {
            return null;
        }

        return deadline.Value.Kind switch
        {
            DateTimeKind.Utc => deadline.Value,
            DateTimeKind.Local => deadline.Value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(deadline.Value, DateTimeKind.Utc)
        };
    }

    private static void SyncSkills(Job job, List<string> names)
    {
        var existing = job.RequiredSkills.Select(s => s.Name).ToList();

        foreach (var removed in existing.Where(n => !names.Contains(n, StringComparer.OrdinalIgnoreCase)))
        {
            var entity = job.RequiredSkills.First(s => string.Equals(s.Name, removed, StringComparison.OrdinalIgnoreCase));
            job.RequiredSkills.Remove(entity);
        }

        foreach (var added in names.Where(n => !existing.Contains(n, StringComparer.OrdinalIgnoreCase)))
        {
            job.RequiredSkills.Add(new JobRequiredSkill { Job = job, Name = added });
        }
    }
}
