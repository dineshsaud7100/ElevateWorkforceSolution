using ElevateWorkforceSolution.Domain.Entities;
using ElevateWorkforceSolution.Domain.Enums;

namespace ElevateWorkforceSolution.Application.DTOs;

/// <summary>Command for creating or editing a vacancy. Skills are supplied as a list of names.</summary>
public class VacancyFormDto
{
    public int? Id { get; set; }

    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.StringLength(150)]
    public string? Category { get; set; }

    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.StringLength(8000)]
    public string Description { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.StringLength(5000)]
    public string? Responsibilities { get; set; }

    [System.ComponentModel.DataAnnotations.StringLength(5000)]
    public string? Requirements { get; set; }

    [System.ComponentModel.DataAnnotations.StringLength(3000)]
    public string? Benefits { get; set; }

    public JobType JobType { get; set; } = JobType.FullTime;
    public WorkArrangement WorkArrangement { get; set; } = WorkArrangement.OnSite;
    public ExperienceLevel ExperienceLevel { get; set; } = ExperienceLevel.Entry;

    [System.ComponentModel.DataAnnotations.Range(1, 500)]
    public int Headcount { get; set; } = 1;

    [System.ComponentModel.DataAnnotations.Range(0, 100_000_000)]
    public decimal? SalaryMin { get; set; }

    [System.ComponentModel.DataAnnotations.Range(0, 100_000_000)]
    public decimal? SalaryMax { get; set; }

    public string SalaryCurrency { get; set; } = "NPR";
    public string SalaryPeriod { get; set; } = "Monthly";

    [System.ComponentModel.DataAnnotations.StringLength(100)]
    public string? WorkLocation { get; set; }

    [System.ComponentModel.DataAnnotations.DataType(System.ComponentModel.DataAnnotations.DataType.Date)]
    public DateTime? ApplicationDeadline { get; set; }

    /// <summary>Comma-separated skill names; normalised to a trimmed, de-duplicated list.</summary>
    public string? Skills { get; set; }

    public List<string> ParseSkills() => NormaliseSkills(Skills);

    public static List<string> NormaliseSkills(string? raw) =>
        (raw ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => s.Trim())
            .Where(s => s.Length is > 0 and <= 100)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(30)
            .ToList();
}

/// <summary>Command to change a vacancy's lifecycle state.</summary>
public class VacancyModerationCommand
{
    public int JobId { get; set; }
    public VacancyStatus Status { get; set; }
    public string? Notes { get; set; }
}

/// <summary>Result of applying the transparent rule-based matcher.</summary>
public class SkillMatchResultDto
{
    public int JobId { get; set; }
    public string JobTitle { get; set; } = string.Empty;
    public int CompatibilityPercentage { get; set; }
    public int SkillMatchPercentage { get; set; }
    public int ExperienceScore { get; set; }
    public int LocationScore { get; set; }
    public int ProfileCompletenessScore { get; set; }
    public List<SkillMatchItemDto> MatchingSkills { get; set; } = new();
    public List<string> MissingSkills { get; set; } = new();
    public string LocationNote { get; set; } = string.Empty;
    public string ExperienceNote { get; set; } = string.Empty;
    public string Disclaimer { get; set; } =
        "This score is calculated from transparent keyword, experience and location comparisons. " +
        "It is informational only and is never used to automatically reject or hire a candidate.";
}

public class SkillMatchItemDto
{
    public string Skill { get; set; } = string.Empty;
    public bool IsMandatory { get; set; }
    public SkillProficiency? CandidateProficiency { get; set; }
}

/// <summary>Company profile shown publicly, plus the fields an employer can edit.</summary>
public class CompanyDto
{
    public int Id { get; set; }
    public int EmployerId { get; set; }
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
    public int ActiveVacancyCount { get; set; }
    public bool HasProfile { get; set; }
}

public class NotificationDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public NotificationType Type { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedDate { get; set; }
    public string? RelatedEntityId { get; set; }
    public string? RelatedEntityType { get; set; }
}

public class ResumeDto
{
    public int Id { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string FileSizeDisplay { get; set; } = string.Empty;
    public DateTime UploadedDate { get; set; }
    public string? Label { get; set; }
}

/// <summary>Percentage of profile sections a candidate has filled in, computed from stored data.</summary>
public class ProfileCompletionDto
{
    public int JobSeekerId { get; set; }
    public int Percentage { get; set; }
    public List<ProfileSectionDto> Sections { get; set; } = new();
    public List<string> NextSteps { get; set; } = new();
}

public class ProfileSectionDto
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public bool IsComplete { get; set; }
    public int Weight { get; set; }
    public string? Hint { get; set; }
}
