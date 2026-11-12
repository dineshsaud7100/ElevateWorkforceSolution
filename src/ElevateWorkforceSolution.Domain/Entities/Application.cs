using ElevateWorkforceSolution.Domain.Enums;

namespace ElevateWorkforceSolution.Domain.Entities;

/// <summary>A candidate's submission for a vacancy. A candidate may submit at most one per vacancy.</summary>
public class Application
{
    public int Id { get; set; }
    public int JobId { get; set; }
    public Job? Job { get; set; }
    public int JobSeekerId { get; set; }
    public JobSeeker? JobSeeker { get; set; }
    public int EmployerId { get; set; }
    public Employer? Employer { get; set; }

    public int ResumeId { get; set; }
    public Resume? Resume { get; set; }

    public ApplicationStatus Status { get; set; } = ApplicationStatus.Applied;
    public string? CoverLetter { get; set; }
    public DateTime AppliedDate { get; set; } = DateTime.UtcNow;
    public DateTime? LastUpdated { get; set; }
    public string? EmployerNotes { get; set; }
    public int? CompatibilityScore { get; set; }

    public ICollection<ApplicationStatusHistory> StatusHistory { get; set; } = new List<ApplicationStatusHistory>();

    public bool IsOpen => Status is ApplicationStatus.Applied or ApplicationStatus.UnderReview
        or ApplicationStatus.Shortlisted or ApplicationStatus.Interview;
}
