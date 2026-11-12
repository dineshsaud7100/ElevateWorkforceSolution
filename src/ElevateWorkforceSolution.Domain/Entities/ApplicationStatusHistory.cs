using ElevateWorkforceSolution.Domain.Enums;

namespace ElevateWorkforceSolution.Domain.Entities;

/// <summary>Immutable record of an application status transition, used to build the candidate timeline.</summary>
public class ApplicationStatusHistory
{
    public int Id { get; set; }
    public int ApplicationId { get; set; }
    public Application? Application { get; set; }

    public ApplicationStatus Status { get; set; }
    public string? Remarks { get; set; }
    public string? ChangedByUserId { get; set; }
    public DateTime ChangedDate { get; set; } = DateTime.UtcNow;
}
