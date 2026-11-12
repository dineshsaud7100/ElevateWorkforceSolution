using ElevateWorkforceSolution.Domain.Enums;

namespace ElevateWorkforceSolution.Application.DTOs;

/// <summary>Candidate dashboard metrics, all computed from the database.</summary>
public class JobSeekerDashboardDto
{
    public string FirstName { get; set; } = string.Empty;
    public int TotalApplications { get; set; }
    public int UnderReview { get; set; }
    public int Shortlisted { get; set; }
    public int Interview { get; set; }
    public int Selected { get; set; }
    public int Rejected { get; set; }
    public int SavedJobs { get; set; }
    public int UnreadNotifications { get; set; }
    public int ProfileCompletion { get; set; }
    public List<ApplicationSummaryDto> RecentApplications { get; set; } = new();
    public List<NotificationDto> RecentNotifications { get; set; } = new();
    public List<JobCardDto> RecommendedJobs { get; set; } = new();
    public List<ProfileSectionDto> IncompleteSections { get; set; } = new();
}

/// <summary>Employer dashboard metrics, all computed from the database.</summary>
public class EmployerDashboardDto
{
    public string CompanyName { get; set; } = string.Empty;
    public bool HasCompanyProfile { get; set; }
    public int ActiveVacancies { get; set; }
    public int PendingVacancies { get; set; }
    public int ClosedVacancies { get; set; }
    public int RejectedVacancies { get; set; }
    public int TotalVacancies { get; set; }
    public int TotalApplications { get; set; }
    public int ShortlistedCandidates { get; set; }
    public int InterviewCandidates { get; set; }
    public int SelectedCandidates { get; set; }
    public int UnreadNotifications { get; set; }
    public List<ApplicantDto> RecentApplications { get; set; } = new();
    public List<VacancyManagementDto> RecentVacancies { get; set; } = new();
    public List<PipelineStageDto> Pipeline { get; set; } = new();
    public List<NotificationDto> RecentNotifications { get; set; } = new();
}

public class PipelineStageDto
{
    public ApplicationStatus Status { get; set; }
    public string Label { get; set; } = string.Empty;
    public int Count { get; set; }
}

/// <summary>Administrator platform dashboard metrics.</summary>
public class AdminDashboardDto
{
    public int TotalUsers { get; set; }
    public int JobSeekers { get; set; }
    public int Employers { get; set; }
    public int Administrators { get; set; }
    public int ActiveUsers { get; set; }
    public int Companies { get; set; }
    public int TotalVacancies { get; set; }
    public int PendingVacancies { get; set; }
    public int ActiveVacancies { get; set; }
    public int RejectedVacancies { get; set; }
    public int RemovedVacancies { get; set; }
    public int TotalApplications { get; set; }
    public int ShortlistedApplications { get; set; }
    public int InterviewApplications { get; set; }
    public int TotalResumes { get; set; }
    public int PendingVacanciesAwaitingReview { get; set; }
    public List<VacancyModerationDto> RecentPendingVacancies { get; set; } = new();
    public List<AdminUserRowDto> RecentRegistrations { get; set; } = new();
    public List<GroupCountDto> ApplicationsByStatus { get; set; } = new();
    public List<GroupCountDto> VacanciesByCategory { get; set; } = new();
    public List<GroupCountDto> VacanciesByLocation { get; set; } = new();
    public List<GroupCountDto> UsersByRole { get; set; } = new();
}

public class GroupCountDto
{
    public string Label { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class AdminUserRowDto
{
    public string UserId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool IsVerified { get; set; }
    public bool IsLockedOut { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? LastLoginDate { get; set; }
    public int? JobSeekerId { get; set; }
    public int? EmployerId { get; set; }
}

/// <summary>Vacancy row in the administrator moderation queue.</summary>
public class VacancyModerationDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string EmployerName { get; set; } = string.Empty;
    public string EmployerEmail { get; set; } = string.Empty;
    public string? WorkLocation { get; set; }
    public string? Category { get; set; }
    public JobType JobType { get; set; }
    public WorkArrangement WorkArrangement { get; set; }
    public ExperienceLevel ExperienceLevel { get; set; }
    public decimal? SalaryMin { get; set; }
    public decimal? SalaryMax { get; set; }
    public DateTime PostedDate { get; set; }
    public DateTime? ApplicationDeadline { get; set; }
    public VacancyStatus Status { get; set; }
    public int ApplicationCount { get; set; }
    public List<string> Skills { get; set; } = new();
    public string? Description { get; set; }
    public string? Responsibilities { get; set; }
    public string? Requirements { get; set; }
    public string? Benefits { get; set; }
    public string? ModerationNotes { get; set; }
}
