namespace ElevateWorkforceSolution.Domain.Entities;

public class Experience
{
    public int Id { get; set; }
    public int JobSeekerId { get; set; }
    public JobSeeker? JobSeeker { get; set; }

    public string CompanyName { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
    public string? Location { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool IsCurrent { get; set; }
    public string? Description { get; set; }
}
