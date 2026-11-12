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

/// <summary>
/// Application submission and pipeline transitions. Candidates may only touch their own
/// applications, and employers may only touch applications for their own vacancies.
/// </summary>
public class ApplicationService : IApplicationService
{
    private readonly IRepository<ApplicationEntity> _applications;
    private readonly IRepository<ApplicationStatusHistory> _history;
    private readonly IRepository<Job> _jobs;
    private readonly IRepository<JobSeeker> _jobSeekers;
    private readonly IRepository<Resume> _resumes;
    private readonly INotificationService _notifications;
    private readonly ISkillMatchService _matcher;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ApplicationService> _logger;

    public ApplicationService(
        IRepository<ApplicationEntity> applications,
        IRepository<ApplicationStatusHistory> history,
        IRepository<Job> jobs,
        IRepository<JobSeeker> jobSeekers,
        IRepository<Resume> resumes,
        INotificationService notifications,
        ISkillMatchService matcher,
        IUnitOfWork unitOfWork,
        ILogger<ApplicationService> logger)
    {
        _applications = applications;
        _history = history;
        _jobs = jobs;
        _jobSeekers = jobSeekers;
        _resumes = resumes;
        _notifications = notifications;
        _matcher = matcher;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<ApplyContextDto?> GetApplyContextAsync(
        int jobId, int jobSeekerId, CancellationToken cancellationToken = default)
    {
        var job = await _jobs.Query()
            .Include(j => j.Company)
            .FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);

        if (job is null)
        {
            return null;
        }

        var resumes = await _resumes.Query()
            .Where(r => r.JobSeekerId == jobSeekerId)
            .OrderByDescending(r => r.UploadedDate)
            .Select(r => new ResumeDto
            {
                Id = r.Id,
                OriginalFileName = r.OriginalFileName,
                ContentType = r.ContentType,
                FileSizeBytes = r.FileSizeBytes,
                FileSizeDisplay = r.FileSizeBytes < 1024
                    ? r.FileSizeBytes + " B"
                    : r.FileSizeBytes < 1048576
                        ? (r.FileSizeBytes / 1024.0).ToString("0.#") + " KB"
                        : (r.FileSizeBytes / 1048576.0).ToString("0.#") + " MB",
                UploadedDate = r.UploadedDate,
                Label = r.Label
            })
            .ToListAsync(cancellationToken);

        var hasApplied = await _applications.AnyAsync(
            a => a.JobId == jobId && a.JobSeekerId == jobSeekerId, cancellationToken);

        var accepts = StatusRules.AcceptsApplications(job.Status) && !job.IsDeadlinePassed;

        string? blockedReason = job.Status switch
        {
            VacancyStatus.PendingVerification => "This vacancy is still being verified by an administrator.",
            VacancyStatus.Rejected => "This vacancy was not approved by an administrator.",
            VacancyStatus.Closed => "This vacancy has been closed by the employer.",
            VacancyStatus.Expired => "The application deadline for this vacancy has passed.",
            VacancyStatus.Removed => "This vacancy has been removed from the platform.",
            _ when job.IsDeadlinePassed => "The application deadline for this vacancy has passed.",
            _ => null
        };

        SkillMatchResultDto? match = null;
        if (accepts && !hasApplied)
        {
            match = await _matcher.EvaluateAsync(jobId, jobSeekerId, cancellationToken);
        }

        return new ApplyContextDto
        {
            JobId = job.Id,
            JobTitle = job.Title,
            CompanyName = job.Company?.Name ?? string.Empty,
            AcceptsApplications = accepts,
            HasApplied = hasApplied,
            HasResume = resumes.Count > 0,
            AvailableResumes = resumes,
            Match = match,
            BlockedReason = blockedReason
        };
    }

    public async Task<Result<int>> SubmitAsync(
        SubmitApplicationCommand command, int jobSeekerId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var seeker = await _jobSeekers.GetByIdAsync(jobSeekerId, cancellationToken);
        if (seeker is null)
        {
            return Result.Failure<int>("Your candidate profile could not be loaded.");
        }

        var job = await _jobs.GetByIdAsync(command.JobId, cancellationToken);
        if (job is null)
        {
            return Result.Failure<int>("This vacancy no longer exists.");
        }

        if (!StatusRules.AcceptsApplications(job.Status))
        {
            return Result.Failure<int>("This vacancy is not currently accepting applications.");
        }

        if (job.IsDeadlinePassed)
        {
            return Result.Failure<int>("The application deadline for this vacancy has passed.");
        }

        var duplicate = await _applications.AnyAsync(
            a => a.JobId == command.JobId && a.JobSeekerId == jobSeekerId, cancellationToken);

        if (duplicate)
        {
            return Result.Failure<int>("You have already applied to this vacancy.");
        }

        // A candidate may only submit a resume that belongs to their own profile.
        var resume = await _resumes.GetByIdAsync(command.ResumeId, cancellationToken);
        if (resume is null || resume.JobSeekerId != jobSeekerId)
        {
            return Result.Failure<int>("Select one of your own uploaded resumes before submitting.");
        }

        if (string.IsNullOrWhiteSpace(command.CoverLetter) || command.CoverLetter.Trim().Length < 20)
        {
            return Result.Failure<int>("Add a cover letter of at least 20 characters.");
        }

        var match = await _matcher.EvaluateAsync(command.JobId, jobSeekerId, cancellationToken);

        var application = new ApplicationEntity
        {
            JobId = job.Id,
            JobSeekerId = jobSeekerId,
            EmployerId = job.EmployerId,
            ResumeId = resume.Id,
            CoverLetter = command.CoverLetter.Trim(),
            Status = ApplicationStatus.Applied,
            AppliedDate = DateTime.UtcNow,
            LastUpdated = DateTime.UtcNow,
            CompatibilityScore = match?.CompatibilityPercentage
        };

        application.StatusHistory.Add(new ApplicationStatusHistory
        {
            Status = ApplicationStatus.Applied,
            Remarks = "Application submitted.",
            ChangedDate = DateTime.UtcNow
        });

        await _applications.AddAsync(application, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Application {ApplicationId} submitted by job seeker {JobSeekerId} for vacancy {JobId}.",
            application.Id, jobSeekerId, job.Id);

        await _notifications.NotifyAsync(new NotificationRequest
        {
            UserId = job.Employer!.UserId,
            Title = "New application received",
            Message = $"A candidate applied for \"{job.Title}\".",
            Type = NotificationType.NewApplication,
            RelatedEntityId = job.Id.ToString(),
            RelatedEntityType = "Job"
        }, cancellationToken);

        return Result.Success(application.Id);
    }

    public async Task<ApplicationSummaryDto?> GetForCandidateAsync(
        int applicationId, int jobSeekerId, CancellationToken cancellationToken = default)
    {
        var application = await _applications.Query()
            .Where(a => a.Id == applicationId && a.JobSeekerId == jobSeekerId)
            .Select(a => new ApplicationSummaryDto
            {
                Id = a.Id,
                JobId = a.JobId,
                JobTitle = a.Job!.Title,
                CompanyName = a.Job.Company!.Name,
                CompanyLogoUrl = a.Job.Company.LogoUrl,
                WorkLocation = a.Job.WorkLocation,
                Status = a.Status,
                AppliedDate = a.AppliedDate,
                LastUpdated = a.LastUpdated,
                CoverLetter = a.CoverLetter,
                CompatibilityScore = a.CompatibilityScore,
                CanWithdraw = a.Status == ApplicationStatus.Applied || a.Status == ApplicationStatus.UnderReview,
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
            .FirstOrDefaultAsync(cancellationToken);

        DecorateLabels(application);
        return application;
    }

    public async Task<PagedResult<ApplicationSummaryDto>> ListForCandidateAsync(
        ApplicationStatus? status, int jobSeekerId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 100 ? 10 : pageSize;

        var query = _applications.Query().Where(a => a.JobSeekerId == jobSeekerId);

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
                CompanyLogoUrl = a.Job.Company.LogoUrl,
                WorkLocation = a.Job.WorkLocation,
                Status = a.Status,
                AppliedDate = a.AppliedDate,
                LastUpdated = a.LastUpdated,
                CoverLetter = a.CoverLetter,
                CompatibilityScore = a.CompatibilityScore,
                CanWithdraw = a.Status == ApplicationStatus.Applied || a.Status == ApplicationStatus.UnderReview,
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

        DecorateLabels(items);
        return new PagedResult<ApplicationSummaryDto>(items, total, page, pageSize);
    }

    public async Task<Result> WithdrawAsync(int applicationId, int jobSeekerId, CancellationToken cancellationToken = default)
    {
        var application = await _applications.GetByIdAsync(applicationId, cancellationToken);

        if (application is null || application.JobSeekerId != jobSeekerId)
        {
            return Result.Failure("Application not found.");
        }

        if (application.Status is not (ApplicationStatus.Applied or ApplicationStatus.UnderReview))
        {
            return Result.Failure("This application can no longer be withdrawn. Please contact the employer.");
        }

        application.Status = ApplicationStatus.Withdrawn;
        application.LastUpdated = DateTime.UtcNow;

        application.StatusHistory.Add(new ApplicationStatusHistory
        {
            Status = ApplicationStatus.Withdrawn,
            Remarks = "Withdrawn by the candidate.",
            ChangedDate = DateTime.UtcNow
        });

        _applications.Update(application);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Application {ApplicationId} withdrawn by job seeker {JobSeekerId}.", applicationId, jobSeekerId);

        await _notifications.NotifyAsync(new NotificationRequest
        {
            UserId = application.Employer!.UserId,
            Title = "Application withdrawn",
            Message = $"A candidate withdrew their application for \"{application.Job?.Title}\".",
            Type = NotificationType.ApplicationStatusChanged,
            RelatedEntityId = application.JobId.ToString(),
            RelatedEntityType = "Job"
        }, cancellationToken);

        return Result.Success();
    }

    public async Task<ApplicantDto?> GetApplicantAsync(
        int applicationId, int employerId, CancellationToken cancellationToken = default)
    {
        var applicant = await ProjectApplicants(
                _applications.Query().Where(a => a.Id == applicationId && a.EmployerId == employerId))
            .FirstOrDefaultAsync(cancellationToken);

        if (applicant is not null)
        {
            await EnrichApplicantSkillsAsync(applicant, cancellationToken);
        }

        DecorateLabels(applicant);
        return applicant;
    }

    public async Task<PagedResult<ApplicantDto>> ListApplicantsAsync(
        int employerId, int? jobId, ApplicationStatus? status, string? keyword, int page, int pageSize,
        CancellationToken cancellationToken = default)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 100 ? 10 : pageSize;

        // Ownership is enforced here, not in the UI: employers only ever see their own applicants.
        var query = _applications.Query().Where(a => a.EmployerId == employerId);

        if (jobId.HasValue)
        {
            var id = jobId.Value;
            query = query.Where(a => a.JobId == id);
        }

        if (status.HasValue)
        {
            var s = status.Value;
            query = query.Where(a => a.Status == s);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim();
            query = query.Where(a =>
                EF.Functions.ILike(a.JobSeeker!.User!.FirstName, $"%{kw}%") ||
                EF.Functions.ILike(a.JobSeeker.User.LastName, $"%{kw}%") ||
                EF.Functions.ILike(a.Job!.Title, $"%{kw}%"));
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await ProjectApplicants(query)
            .OrderByDescending(a => a.AppliedDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        foreach (var item in items)
        {
            await EnrichApplicantSkillsAsync(item, cancellationToken);
        }

        DecorateLabels(items);
        return new PagedResult<ApplicantDto>(items, total, page, pageSize);
    }

    public async Task<Result<ApplicantDto>> UpdateStatusAsync(
        UpdateApplicationStatusCommand command, int employerId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var application = await _applications.QueryTracked()
            .Include(a => a.Job)
            .Include(a => a.JobSeeker).ThenInclude(j => j!.User)
            .Include(a => a.StatusHistory)
            .FirstOrDefaultAsync(a => a.Id == command.ApplicationId, cancellationToken);

        if (application is null)
        {
            return Result.Failure<ApplicantDto>("Application not found.");
        }

        if (application.EmployerId != employerId)
        {
            return Result.Failure<ApplicantDto>("You are not authorised to update this application.");
        }

        if (!StatusRules.EmployerAssignableStatuses.Contains(command.Status))
        {
            return Result.Failure<ApplicantDto>("That status cannot be set by an employer.");
        }

        if (application.Status == command.Status)
        {
            return Result.Failure<ApplicantDto>("The application is already in that status.");
        }

        if (!StatusRules.IsValidEmployerTransition(application.Status, command.Status))
        {
            return Result.Failure<ApplicantDto>(
                $"An application cannot move from {StatusRules.Describe(application.Status)} to {StatusRules.Describe(command.Status)}.");
        }

        application.Status = command.Status;
        application.LastUpdated = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(command.Remarks))
        {
            application.EmployerNotes = command.Remarks.Trim();
        }

        application.StatusHistory.Add(new ApplicationStatusHistory
        {
            Status = command.Status,
            Remarks = string.IsNullOrWhiteSpace(command.Remarks)
                ? StatusRules.Describe(command.Status)
                : command.Remarks.Trim(),
            ChangedDate = DateTime.UtcNow
        });

        _applications.Update(application);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Application {ApplicationId} moved to {Status} by employer {EmployerId}.",
            application.Id, command.Status, employerId);

        var candidateUserId = application.JobSeeker?.UserId;
        if (!string.IsNullOrEmpty(candidateUserId))
        {
            var type = command.Status switch
            {
                ApplicationStatus.Shortlisted => NotificationType.CandidateShortlisted,
                ApplicationStatus.Interview => NotificationType.CandidateMovedToInterview,
                ApplicationStatus.Rejected => NotificationType.CandidateRejected,
                _ => NotificationType.ApplicationStatusChanged
            };

            await _notifications.NotifyAsync(new NotificationRequest
            {
                UserId = candidateUserId,
                Title = StatusRules.Describe(command.Status),
                Message = $"Your application for \"{application.Job?.Title}\" is now {StatusRules.Describe(command.Status).ToLowerInvariant()}.",
                Type = type,
                RelatedEntityId = application.Id.ToString(),
                RelatedEntityType = "Application"
            }, cancellationToken);
        }

        var updated = await ProjectApplicants(_applications.Query().Where(a => a.Id == application.Id))
            .FirstOrDefaultAsync(cancellationToken);

        if (updated is not null)
        {
            await EnrichApplicantSkillsAsync(updated, cancellationToken);
        }

        DecorateLabels(updated);
        return Result.Success(updated!);
    }

    public async Task<CandidateProfileDto?> GetCandidateProfileAsync(
        int jobSeekerId, int employerId, CancellationToken cancellationToken = default)
    {
        // An employer may only open a candidate profile reached through one of their own applicants.
        var hasRelationship = await _applications.AnyAsync(
            a => a.JobSeekerId == jobSeekerId && a.EmployerId == employerId, cancellationToken);

        if (!hasRelationship)
        {
            return null;
        }

        return await _jobSeekers.Query()
            .Where(s => s.Id == jobSeekerId)
            .Select(s => new CandidateProfileDto
            {
                JobSeekerId = s.Id,
                FullName = s.User == null ? string.Empty : s.User.FirstName + " " + s.User.LastName,
                Email = s.User == null ? string.Empty : s.User.Email!,
                Phone = s.Phone,
                Location = s.Location,
                ProfessionalTitle = s.ProfessionalTitle,
                About = s.About,
                YearsOfExperience = s.YearsOfExperience,
                ProfileCompletion = CompletionScoreSql(s),
                HasResume = s.Resumes.Any(),
                LatestResumeFileName = s.Resumes
                    .OrderByDescending(r => r.UploadedDate)
                    .Select(r => r.OriginalFileName)
                    .FirstOrDefault(),
                Skills = s.Skills
                    .OrderBy(k => k.Name)
                    .Select(k => new SkillDto
                    {
                        Id = k.Id,
                        Name = k.Name,
                        Proficiency = k.Proficiency,
                        YearsOfExperience = k.YearsOfExperience
                    })
                    .ToList(),
                Educations = s.Educations
                    .OrderByDescending(e => e.GraduationYear)
                    .Select(e => new EducationDto
                    {
                        Id = e.Id,
                        Institution = e.Institution,
                        Degree = e.Degree,
                        FieldOfStudy = e.FieldOfStudy,
                        GraduationYear = e.GraduationYear,
                        Grade = e.Grade,
                        Description = e.Description
                    })
                    .ToList(),
                Experiences = s.Experiences
                    .OrderByDescending(x => x.StartDate)
                    .Select(x => new ExperienceDto
                    {
                        Id = x.Id,
                        CompanyName = x.CompanyName,
                        Position = x.Position,
                        Location = x.Location,
                        StartDate = x.StartDate,
                        EndDate = x.EndDate,
                        IsCurrent = x.IsCurrent,
                        Description = x.Description
                    })
                    .ToList(),
                Certifications = s.Certifications
                    .Select(c => new CertificationDto
                    {
                        Id = c.Id,
                        Name = c.Name,
                        IssuingOrganization = c.IssuingOrganization,
                        IssueDate = c.IssueDate,
                        CredentialId = c.CredentialId
                    })
                    .ToList(),
                Languages = s.Languages
                    .Select(l => new LanguageDto
                    {
                        Id = l.Id,
                        Name = l.Name,
                        Proficiency = l.Proficiency
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Fills display labels after materialisation. Status text is produced by application code
    /// because <c>StatusRules.Describe</c> cannot be translated to SQL.
    /// </summary>
    private static void DecorateLabels(ApplicationSummaryDto? dto)
    {
        if (dto is null)
        {
            return;
        }

        dto.StatusLabel = StatusRules.Describe(dto.Status);
        foreach (var entry in dto.Timeline)
        {
            entry.Label = StatusRules.Describe(entry.Status);
        }
    }

    private static void DecorateLabels(IEnumerable<ApplicationSummaryDto> dtos)
    {
        foreach (var dto in dtos)
        {
            DecorateLabels(dto);
        }
    }

    private static void DecorateLabels(ApplicantDto? dto)
    {
        if (dto is not null)
        {
            dto.StatusLabel = StatusRules.Describe(dto.Status);
        }
    }

    private static void DecorateLabels(IEnumerable<ApplicantDto> dtos)
    {
        foreach (var dto in dtos)
        {
            DecorateLabels(dto);
        }
    }

    private async Task EnrichApplicantSkillsAsync(ApplicantDto applicant, CancellationToken cancellationToken)
    {
        if (applicant.JobSeekerId <= 0)
        {
            return;
        }

        var skills = await _jobSeekers.Query()
            .Where(s => s.Id == applicant.JobSeekerId)
            .SelectMany(s => s.Skills)
            .Select(s => s.Name)
            .ToListAsync(cancellationToken);

        applicant.Skills = skills;
    }

    private static IQueryable<ApplicantDto> ProjectApplicants(IQueryable<ApplicationEntity> query) =>
        query.Select(a => new ApplicantDto
        {
            ApplicationId = a.Id,
            JobId = a.JobId,
            JobTitle = a.Job!.Title,
            JobSeekerId = a.JobSeekerId,
            CandidateName = a.JobSeeker == null || a.JobSeeker.User == null
                ? string.Empty
                : a.JobSeeker.User.FirstName + " " + a.JobSeeker.User.LastName,
            ProfessionalTitle = a.JobSeeker == null ? null : a.JobSeeker.ProfessionalTitle,
            CandidateLocation = a.JobSeeker == null ? null : a.JobSeeker.Location,
            CandidatePhone = a.JobSeeker == null ? null : a.JobSeeker.Phone,
            CandidateEmail = a.JobSeeker == null || a.JobSeeker.User == null ? null : a.JobSeeker.User.Email,
            YearsOfExperience = a.JobSeeker == null ? null : a.JobSeeker.YearsOfExperience,
            ProfileCompletion = a.JobSeeker == null ? 0 : CompletionScoreSql(a.JobSeeker),
            Status = a.Status,
            AppliedDate = a.AppliedDate,
            LastUpdated = a.LastUpdated,
            CoverLetter = a.CoverLetter,
            ResumeId = a.ResumeId,
            ResumeFileName = a.Resume == null ? null : a.Resume.OriginalFileName,
            CompatibilityScore = a.CompatibilityScore
        });

    /// <summary>
    /// Weighted profile-completion score. The body is written with only SQL-translatable
    /// operations (null/length checks, Any() and arithmetic) so it can be evaluated by
    /// PostgreSQL inside an EF projection rather than pulling rows into memory.
    /// Weights reflect how much each section helps an employer assess a candidate.
    /// </summary>
    private static int CompletionScoreSql(JobSeeker s) =>
        (s.ProfessionalTitle != null && s.ProfessionalTitle.Length > 0 ? 10 : 0)
        + (s.Location != null && s.Location.Length > 0 ? 8 : 0)
        + (s.Phone != null && s.Phone.Length > 0 ? 5 : 0)
        + (s.About != null && s.About.Length >= 80 ? 12 : 0)
        + (s.Skills.Any() ? 20 : 0)
        + (s.Experiences.Any() ? 20 : 0)
        + (s.Educations.Any() ? 15 : 0)
        + (s.Certifications.Any() ? 5 : 0)
        + (s.Languages.Any() ? 5 : 0);
}
