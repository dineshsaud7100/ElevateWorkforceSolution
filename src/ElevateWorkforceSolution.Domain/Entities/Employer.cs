namespace ElevateWorkforceSolution.Domain.Entities;

/// <summary>Recruiting organisation. One-to-one with <see cref="ApplicationUser"/>.</summary>
public class Employer
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    public string CompanyName { get; set; } = string.Empty;
    public string? BusinessRegistrationNumber { get; set; }
    public string? Designation { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public bool IsVerified { get; set; }

    public Company? Company { get; set; }
    public ICollection<Job> Jobs { get; set; } = new List<Job>();
}
