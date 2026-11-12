using ElevateWorkforceSolution.Domain.Enums;

namespace ElevateWorkforceSolution.Domain.Entities;

/// <summary>A vacancy posted by an employer. Public visibility is driven solely by <see cref="Status"/>.</summary>
public class Job
{
    public int Id { get; set; }
    public int EmployerId { get; set; }
    public Employer? Employer { get; set; }
    public int CompanyId { get; set; }
    public Company? Company { get; set; }

    public string Title { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string? Description { get; set; }
    public string? Responsibilities { get; set; }
    public string? Requirements { get; set; }
    public string? Benefits { get; set; }

    public JobType JobType { get; set; } = JobType.FullTime;
    public WorkArrangement WorkArrangement { get; set; } = WorkArrangement.OnSite;
    public ExperienceLevel ExperienceLevel { get; set; } = ExperienceLevel.Entry;
    public int? VacancyCount { get; set; } = 1;

    public decimal? SalaryMin { get; set; }
    public decimal? SalaryMax { get; set; }
    public string? SalaryCurrency { get; set; } = "NPR";
    public string? SalaryPeriod { get; set; } = "Monthly";

    public string? WorkLocation { get; set; }
    public int? Headcount { get; set; }

    public DateTime PostedDate { get; set; } = DateTime.UtcNow;
    public DateTime? ApplicationDeadline { get; set; }
    public VacancyStatus Status { get; set; } = VacancyStatus.PendingVerification;

    public DateTime? VerifiedDate { get; set; }
    public string? VerifiedByUserId { get; set; }
    public string? ModerationNotes { get; set; }
    public DateTime? ClosedDate { get; set; }
    public int? ViewsCount { get; set; }

    public ICollection<JobRequiredSkill> RequiredSkills { get; set; } = new List<JobRequiredSkill>();
    public ICollection<Application> Applications { get; set; } = new List<Application>();
    public ICollection<SavedJob> SavedJobs { get; set; } = new List<SavedJob>();

    /// <summary>Only Active vacancies are publicly listed.</summary>
    public bool IsPublic => Status == VacancyStatus.Active;

    /// <summary>Closed, expired and removed vacancies no longer accept applications.</summary>
    public bool AcceptsApplications => Status == VacancyStatus.Active;

    public bool IsDeadlinePassed => ApplicationDeadline.HasValue && ApplicationDeadline.Value < DateTime.UtcNow;
}
