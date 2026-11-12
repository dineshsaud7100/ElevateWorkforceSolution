namespace ElevateWorkforceSolution.Domain.Entities;

public class Education
{
    public int Id { get; set; }
    public int JobSeekerId { get; set; }
    public JobSeeker? JobSeeker { get; set; }

    public string Institution { get; set; } = string.Empty;
    public string Degree { get; set; } = string.Empty;
    public string? FieldOfStudy { get; set; }
    public int? GraduationYear { get; set; }
    public string? Grade { get; set; }
    public string? Description { get; set; }
}
