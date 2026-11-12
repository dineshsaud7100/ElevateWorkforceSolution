namespace ElevateWorkforceSolution.Domain.Entities;

/// <summary>A vacancy bookmarked by a candidate. Unique per (JobSeeker, Job) pair.</summary>
public class SavedJob
{
    public int Id { get; set; }
    public int JobSeekerId { get; set; }
    public JobSeeker? JobSeeker { get; set; }
    public int JobId { get; set; }
    public Job? Job { get; set; }
    public DateTime SavedDate { get; set; } = DateTime.UtcNow;
}
