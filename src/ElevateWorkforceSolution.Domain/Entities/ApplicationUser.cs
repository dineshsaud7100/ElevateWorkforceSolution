using Microsoft.AspNetCore.Identity;

namespace ElevateWorkforceSolution.Domain.Entities;

/// <summary>Identity account. Exactly one of <see cref="JobSeeker"/> or <see cref="Employer"/> is populated.</summary>
public class ApplicationUser : IdentityUser
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? ProfileImageUrl { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginDate { get; set; }
    public bool IsActive { get; set; } = true;

    public string FullName => $"{FirstName} {LastName}".Trim();

    public JobSeeker? JobSeeker { get; set; }
    public Employer? Employer { get; set; }
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
}
