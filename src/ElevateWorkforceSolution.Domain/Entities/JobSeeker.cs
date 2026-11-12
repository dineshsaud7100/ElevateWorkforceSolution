namespace ElevateWorkforceSolution.Domain.Entities;

/// <summary>Candidate profile. One-to-one with <see cref="ApplicationUser"/>.</summary>
public class JobSeeker
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    public string? ProfessionalTitle { get; set; }
    public string? Location { get; set; }
    public string? Phone { get; set; }
    public string? About { get; set; }
    public decimal? YearsOfExperience { get; set; }
    public bool IsOpenToWork { get; set; } = true;
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedDate { get; set; }

    public ICollection<Education> Educations { get; set; } = new List<Education>();
    public ICollection<Experience> Experiences { get; set; } = new List<Experience>();
    public ICollection<Skill> Skills { get; set; } = new List<Skill>();
    public ICollection<Certification> Certifications { get; set; } = new List<Certification>();
    public ICollection<Language> Languages { get; set; } = new List<Language>();
    public ICollection<Resume> Resumes { get; set; } = new List<Resume>();
    public ICollection<Application> Applications { get; set; } = new List<Application>();
    public ICollection<SavedJob> SavedJobs { get; set; } = new List<SavedJob>();

    public Resume? LatestResume => Resumes
        .OrderByDescending(r => r.UploadedDate)
        .FirstOrDefault();
}
