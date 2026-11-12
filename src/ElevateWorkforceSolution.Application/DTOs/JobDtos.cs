using ElevateWorkforceSolution.Domain.Enums;

namespace ElevateWorkforceSolution.Application.DTOs;

/// <summary>Filter criteria for the public careers search. All filters are applied server-side.</summary>
public class JobSearchFilter
{
    public string? Keyword { get; set; }
    public string? Location { get; set; }
    public string? Category { get; set; }
    public JobType? JobType { get; set; }
    public WorkArrangement? WorkArrangement { get; set; }
    public ExperienceLevel? ExperienceLevel { get; set; }
    public decimal? SalaryMin { get; set; }
    public decimal? SalaryMax { get; set; }
    public DateTime? PostedAfter { get; set; }
    public JobSortOption Sort { get; set; } = JobSortOption.Newest;
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 9;

    public void Normalise()
    {
        PageNumber = PageNumber < 1 ? 1 : PageNumber;
        PageSize = PageSize switch
        {
            < 1 => 9,
            > 50 => 50,
            _ => PageSize
        };

        if (SalaryMin is > 0 && SalaryMax is > 0 && SalaryMin > SalaryMax)
        {
            (SalaryMin, SalaryMax) = (SalaryMax, SalaryMin);
        }
    }
}

public enum JobSortOption
{
    Newest = 1,
    SalaryHighToLow = 2,
    SalaryLowToHigh = 3,
    ClosingDateSoonest = 4
}

/// <summary>Compact vacancy projection used in list views. Avoids loading full entity graphs.</summary>
public class JobCardDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string? CompanyLogoUrl { get; set; }
    public string? WorkLocation { get; set; }
    public string? Category { get; set; }
    public JobType JobType { get; set; }
    public WorkArrangement WorkArrangement { get; set; }
    public ExperienceLevel ExperienceLevel { get; set; }
    public decimal? SalaryMin { get; set; }
    public decimal? SalaryMax { get; set; }
    public string? SalaryCurrency { get; set; }
    public string? SalaryPeriod { get; set; }
    public DateTime PostedDate { get; set; }
    public DateTime? ApplicationDeadline { get; set; }
    public VacancyStatus Status { get; set; }
    public int EmployerId { get; set; }
    public int CompanyId { get; set; }
    public int ApplicationCount { get; set; }
    public List<string> Skills { get; set; } = new();
    public bool IsSaved { get; set; }
    public bool HasApplied { get; set; }
}

/// <summary>Full vacancy view for the details page.</summary>
public class JobDetailDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string? Description { get; set; }
    public string? Responsibilities { get; set; }
    public string? Requirements { get; set; }
    public string? Benefits { get; set; }
    public JobType JobType { get; set; }
    public WorkArrangement WorkArrangement { get; set; }
    public ExperienceLevel ExperienceLevel { get; set; }
    public decimal? SalaryMin { get; set; }
    public decimal? SalaryMax { get; set; }
    public string? SalaryCurrency { get; set; }
    public string? SalaryPeriod { get; set; }
    public string? WorkLocation { get; set; }
    public int? Headcount { get; set; }
    public DateTime PostedDate { get; set; }
    public DateTime? ApplicationDeadline { get; set; }
    public VacancyStatus Status { get; set; }

    public int EmployerId { get; set; }
    public string EmployerName { get; set; } = string.Empty;
    public int CompanyId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string? CompanyLogoUrl { get; set; }
    public string? CompanyIndustry { get; set; }
    public string? CompanyAbout { get; set; }
    public string? CompanyWebsite { get; set; }
    public string? CompanyLocation { get; set; }
    public int? CompanySize { get; set; }
    public int? CompanyFoundedYear { get; set; }
    public int CompanyVacancyCount { get; set; }

    public List<string> RequiredSkills { get; set; } = new();
    public int ApplicationCount { get; set; }

    public bool AcceptsApplications { get; set; }
    public bool IsSaved { get; set; }
    public bool HasApplied { get; set; }
    public bool IsDeadlinePassed { get; set; }
    public string? ModerationNotes { get; set; }
}

/// <summary>Vacancy summary for the employer workspace.</summary>
public class VacancyManagementDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Category { get; set; }
    public JobType JobType { get; set; }
    public WorkArrangement WorkArrangement { get; set; }
    public string? WorkLocation { get; set; }
    public decimal? SalaryMin { get; set; }
    public decimal? SalaryMax { get; set; }
    public DateTime PostedDate { get; set; }
    public DateTime? ApplicationDeadline { get; set; }
    public VacancyStatus Status { get; set; }
    public int ApplicationCount { get; set; }
    public int ShortlistedCount { get; set; }
    public int InterviewCount { get; set; }
    public string? ModerationNotes { get; set; }
    public DateTime? VerifiedDate { get; set; }
}
