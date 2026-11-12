namespace ElevateWorkforceSolution.Domain.Entities;

public class Certification
{
    public int Id { get; set; }
    public int JobSeekerId { get; set; }
    public JobSeeker? JobSeeker { get; set; }

    public string Name { get; set; } = string.Empty;
    public string IssuingOrganization { get; set; } = string.Empty;
    public DateTime? IssueDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public string? CredentialId { get; set; }
    public string? Description { get; set; }
}
