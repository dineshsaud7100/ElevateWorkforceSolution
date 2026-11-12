namespace ElevateWorkforceSolution.Domain.Entities;

/// <summary>Public-facing company profile. Owned by a single <see cref="Employer"/>.</summary>
public class Company
{
    public int Id { get; set; }
    public int EmployerId { get; set; }
    public Employer? Employer { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public string? Industry { get; set; }
    public string? Description { get; set; }
    public string? Website { get; set; }
    public string? Location { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public int? CompanySize { get; set; }
    public int? FoundedYear { get; set; }
    public string? About { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedDate { get; set; }

    public ICollection<Job> Jobs { get; set; } = new List<Job>();
}
