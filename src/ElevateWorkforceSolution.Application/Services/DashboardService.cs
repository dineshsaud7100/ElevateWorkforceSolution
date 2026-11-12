using ElevateWorkforceSolution.Application.DTOs;
using ElevateWorkforceSolution.Application.Interfaces;
using ElevateWorkforceSolution.Application.Interfaces.Repositories;
using ElevateWorkforceSolution.Domain.Common;
using ElevateWorkforceSolution.Domain.Entities;
using ElevateWorkforceSolution.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using ApplicationEntity = ElevateWorkforceSolution.Domain.Entities.Application;

namespace ElevateWorkforceSolution.Application.Services;

/// <summary>Dashboard metrics. Every number is aggregated from the database, never hardcoded.</summary>
public class DashboardService : IDashboardService
{
    private readonly IRepository<JobSeeker> _jobSeekers;
    private readonly IRepository<Employer> _employers;
    private readonly IRepository<ApplicationUser> _users;
    private readonly IRepository<Company> _companies;
    private readonly IRepository<Job> _jobs;
    private readonly IRepository<ApplicationEntity> _applications;
    private readonly IRepository<SavedJob> _savedJobs;
    private readonly IRepository<Resume> _resumes;
    private readonly INotificationService _notifications;
    private readonly ICandidateProfileService _profiles;
    private readonly ISkillMatchService _matcher;

    public DashboardService(
        IRepository<JobSeeker> jobSeekers,
        IRepository<Employer> employers,
        IRepository<ApplicationUser> users,
        IRepository<Company> companies,
        IRepository<Job> jobs,
        IRepository<ApplicationEntity> applications,
        IRepository<SavedJob> savedJobs,
        IRepository<Resume> resumes,
        INotificationService notifications,
        ICandidateProfileService profiles,
        ISkillMatchService matcher)
    {
        _jobSeekers = jobSeekers;
        _employers = employers;
        _users = users;
        _companies = companies;
        _jobs = jobs;
        _applications = applications;
        _savedJobs = savedJobs;
        _resumes = resumes;
        _notifications = notifications;
        _profiles = profiles;
        _matcher = matcher;
    }

    public async Task<JobSeekerDashboardDto> GetForJobSeekerAsync(int jobSeekerId, CancellationToken cancellationToken = default)
    {
        var seeker = await _jobSeekers.Query()
            .Where(s => s.Id == jobSeekerId)
            .Select(s => new
            {
                s.Id,
                s.UserId,
                FirstName = s.User!.FirstName
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (seeker is null)
        {
            return new JobSeekerDashboardDto();
        }

        var applications = _applications.Query().Where(a => a.JobSeekerId == jobSeekerId);

        var total = await applications.CountAsync(cancellationToken);
        var underReview = await applications.CountAsync(a => a.Status == ApplicationStatus.UnderReview, cancellationToken);
        var shortlisted = await applications.CountAsync(a => a.Status == ApplicationStatus.Shortlisted, cancellationToken);
        var interview = await applications.CountAsync(a => a.Status == ApplicationStatus.Interview, cancellationToken);
        var selected = await applications.CountAsync(a => a.Status == ApplicationStatus.Selected, cancellationToken);
        var rejected = await applications.CountAsync(a => a.Status == ApplicationStatus.Rejected, cancellationToken);
        var saved = await _savedJobs.CountAsync(s => s.JobSeekerId == jobSeekerId, cancellationToken);

        var recentApplications = await applications
            .OrderByDescending(a => a.AppliedDate)
            .Take(5)
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
                CompatibilityScore = a.CompatibilityScore,
                CanWithdraw = a.Status == ApplicationStatus.Applied || a.Status == ApplicationStatus.UnderReview
            })
            .ToListAsync(cancellationToken);

        foreach (var item in recentApplications)
        {
            item.StatusLabel = StatusRules.Describe(item.Status);
        }

        var notifications = await _notifications.ListAsync(seeker.UserId, false, 1, 5, cancellationToken);
        var unread = await _notifications.GetUnreadCountAsync(seeker.UserId, cancellationToken);
        var completion = await _profiles.RecalculateCompletionAsync(jobSeekerId, cancellationToken);
        var recommended = await _matcher.RecommendAsync(jobSeekerId, 6, cancellationToken);

        return new JobSeekerDashboardDto
        {
            FirstName = seeker.FirstName,
            TotalApplications = total,
            UnderReview = underReview,
            Shortlisted = shortlisted,
            Interview = interview,
            Selected = selected,
            Rejected = rejected,
            SavedJobs = saved,
            UnreadNotifications = unread,
            ProfileCompletion = completion.Percentage,
            RecentApplications = recentApplications,
            RecentNotifications = notifications.Items.ToList(),
            RecommendedJobs = recommended,
            IncompleteSections = completion.Sections.Where(s => !s.IsComplete).ToList()
        };
    }

    public async Task<EmployerDashboardDto> GetForEmployerAsync(int employerId, CancellationToken cancellationToken = default)
    {
        var employer = await _employers.Query()
            .Where(e => e.Id == employerId)
            .Select(e => new
            {
                e.Id,
                e.UserId,
                e.CompanyName,
                HasCompany = e.Company != null
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (employer is null)
        {
            return new EmployerDashboardDto();
        }

        var jobs = _jobs.Query().Where(j => j.EmployerId == employerId);
        var applications = _applications.Query().Where(a => a.EmployerId == employerId);

        var totalVacancies = await jobs.CountAsync(cancellationToken);
        var activeVacancies = await jobs.CountAsync(j => j.Status == VacancyStatus.Active, cancellationToken);
        var pendingVacancies = await jobs.CountAsync(j => j.Status == VacancyStatus.PendingVerification, cancellationToken);
        var closedVacancies = await jobs.CountAsync(j => j.Status == VacancyStatus.Closed, cancellationToken);
        var rejectedVacancies = await jobs.CountAsync(j => j.Status == VacancyStatus.Rejected, cancellationToken);

        var totalApplications = await applications.CountAsync(cancellationToken);
        var shortlisted = await applications.CountAsync(a => a.Status == ApplicationStatus.Shortlisted, cancellationToken);
        var interview = await applications.CountAsync(a => a.Status == ApplicationStatus.Interview, cancellationToken);
        var selected = await applications.CountAsync(a => a.Status == ApplicationStatus.Selected, cancellationToken);

        var recentApplications = await applications
            .OrderByDescending(a => a.AppliedDate)
            .Take(6)
            .Select(a => new ApplicantDto
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
                Status = a.Status,
                AppliedDate = a.AppliedDate,
                LastUpdated = a.LastUpdated,
                ResumeId = a.ResumeId,
                ResumeFileName = a.Resume == null ? null : a.Resume.OriginalFileName,
                CompatibilityScore = a.CompatibilityScore
            })
            .ToListAsync(cancellationToken);

        foreach (var item in recentApplications)
        {
            item.StatusLabel = StatusRules.Describe(item.Status);
        }

        var recentVacancies = await jobs
            .OrderByDescending(j => j.PostedDate)
            .Take(6)
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
                ModerationNotes = j.ModerationNotes
            })
            .ToListAsync(cancellationToken);

        var byStatus = await applications
            .GroupBy(a => a.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var pipeline = Enum.GetValues<ApplicationStatus>()
            .Select(status => new PipelineStageDto
            {
                Status = status,
                Label = StatusRules.Describe(status),
                Count = byStatus.FirstOrDefault(b => b.Status == status)?.Count ?? 0
            })
            .Where(p => p.Count > 0)
            .ToList();

        var notifications = await _notifications.ListAsync(employer.UserId, false, 1, 5, cancellationToken);
        var unread = await _notifications.GetUnreadCountAsync(employer.UserId, cancellationToken);

        return new EmployerDashboardDto
        {
            CompanyName = employer.CompanyName,
            HasCompanyProfile = employer.HasCompany,
            ActiveVacancies = activeVacancies,
            PendingVacancies = pendingVacancies,
            ClosedVacancies = closedVacancies,
            RejectedVacancies = rejectedVacancies,
            TotalVacancies = totalVacancies,
            TotalApplications = totalApplications,
            ShortlistedCandidates = shortlisted,
            InterviewCandidates = interview,
            SelectedCandidates = selected,
            UnreadNotifications = unread,
            RecentApplications = recentApplications,
            RecentVacancies = recentVacancies,
            Pipeline = pipeline,
            RecentNotifications = notifications.Items.ToList()
        };
    }

    public async Task<AdminDashboardDto> GetForAdministratorAsync(CancellationToken cancellationToken = default)
    {
        var totalUsers = await _users.CountAsync(cancellationToken);
        var jobSeekers = await _jobSeekers.CountAsync(cancellationToken);
        var employers = await _employers.CountAsync(cancellationToken);
        var administrators = totalUsers - jobSeekers - employers;
        var activeUsers = await _users.CountAsync(u => u.IsActive, cancellationToken);
        var companies = await _companies.CountAsync(cancellationToken);

        var totalVacancies = await _jobs.CountAsync(cancellationToken);
        var pendingVacancies = await _jobs.CountAsync(j => j.Status == VacancyStatus.PendingVerification, cancellationToken);
        var activeVacancies = await _jobs.CountAsync(j => j.Status == VacancyStatus.Active, cancellationToken);
        var rejectedVacancies = await _jobs.CountAsync(j => j.Status == VacancyStatus.Rejected, cancellationToken);
        var removedVacancies = await _jobs.CountAsync(j => j.Status == VacancyStatus.Removed, cancellationToken);

        var totalApplications = await _applications.CountAsync(cancellationToken);
        var shortlisted = await _applications.CountAsync(a => a.Status == ApplicationStatus.Shortlisted, cancellationToken);
        var interview = await _applications.CountAsync(a => a.Status == ApplicationStatus.Interview, cancellationToken);
        var resumes = await _resumes.CountAsync(cancellationToken);

        var recentPending = await _jobs.Query()
            .Where(j => j.Status == VacancyStatus.PendingVerification)
            .OrderBy(j => j.PostedDate)
            .Take(6)
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
                Benefits = j.Benefits
            })
            .ToListAsync(cancellationToken);

        var recentRegistrations = await _users.Query()
            .OrderByDescending(u => u.CreatedDate)
            .Take(8)
            .Select(u => new AdminUserRowDto
            {
                UserId = u.Id,
                FullName = u.FirstName + " " + u.LastName,
                Email = u.Email!,
                Role = u.JobSeeker != null
                    ? Roles.JobSeeker
                    : u.Employer != null
                        ? Roles.Employer
                        : Roles.Administrator,
                IsActive = u.IsActive,
                IsLockedOut = u.LockoutEnd > DateTime.UtcNow,
                CreatedDate = u.CreatedDate,
                LastLoginDate = u.LastLoginDate,
                JobSeekerId = u.JobSeeker == null ? null : u.JobSeeker.Id,
                EmployerId = u.Employer == null ? null : u.Employer.Id
            })
            .ToListAsync(cancellationToken);

        var applicationsByStatus = await _applications.Query()
            .GroupBy(a => a.Status)
            .Select(g => new GroupCountDto { Label = StatusRules.Describe(g.Key), Count = g.Count() })
            .OrderByDescending(g => g.Count)
            .ToListAsync(cancellationToken);

        var vacanciesByCategory = await _jobs.Query()
            .Where(j => j.Category != null)
            .GroupBy(j => j.Category!)
            .Select(g => new GroupCountDto { Label = g.Key, Count = g.Count() })
            .OrderByDescending(g => g.Count)
            .Take(8)
            .ToListAsync(cancellationToken);

        var vacanciesByLocation = await _jobs.Query()
            .Where(j => j.WorkLocation != null)
            .GroupBy(j => j.WorkLocation!)
            .Select(g => new GroupCountDto { Label = g.Key, Count = g.Count() })
            .OrderByDescending(g => g.Count)
            .Take(8)
            .ToListAsync(cancellationToken);

        var usersByRole = new List<GroupCountDto>
        {
            new() { Label = Roles.JobSeeker, Count = jobSeekers },
            new() { Label = Roles.Employer, Count = employers },
            new() { Label = Roles.Administrator, Count = Math.Max(0, administrators) }
        };

        return new AdminDashboardDto
        {
            TotalUsers = totalUsers,
            JobSeekers = jobSeekers,
            Employers = employers,
            Administrators = Math.Max(0, administrators),
            ActiveUsers = activeUsers,
            Companies = companies,
            TotalVacancies = totalVacancies,
            PendingVacancies = pendingVacancies,
            ActiveVacancies = activeVacancies,
            RejectedVacancies = rejectedVacancies,
            RemovedVacancies = removedVacancies,
            TotalApplications = totalApplications,
            ShortlistedApplications = shortlisted,
            InterviewApplications = interview,
            TotalResumes = resumes,
            PendingVacanciesAwaitingReview = pendingVacancies,
            RecentPendingVacancies = recentPending,
            RecentRegistrations = recentRegistrations,
            ApplicationsByStatus = applicationsByStatus,
            VacanciesByCategory = vacanciesByCategory,
            VacanciesByLocation = vacanciesByLocation,
            UsersByRole = usersByRole
        };
    }
}
