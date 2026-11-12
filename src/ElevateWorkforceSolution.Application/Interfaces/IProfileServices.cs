using ElevateWorkforceSolution.Application.Common;
using ElevateWorkforceSolution.Application.DTOs;
using ElevateWorkforceSolution.Domain.Enums;

namespace ElevateWorkforceSolution.Application.Interfaces;

/// <summary>Candidate profile management and completion scoring.</summary>
public interface ICandidateProfileService
{
    Task<CandidateProfileDto?> GetAsync(int jobSeekerId, CancellationToken cancellationToken = default);

    Task<Result<int>> EnsureProfileAsync(string userId, string? professionalTitle, string? location, CancellationToken cancellationToken = default);

    Task<Result> UpdateBasicsAsync(int jobSeekerId, CandidateBasicsDto model, CancellationToken cancellationToken = default);

    Task<Result<int>> AddEducationAsync(int jobSeekerId, EducationDto model, CancellationToken cancellationToken = default);

    Task<Result> RemoveEducationAsync(int educationId, int jobSeekerId, CancellationToken cancellationToken = default);

    Task<Result<int>> AddExperienceAsync(int jobSeekerId, ExperienceDto model, CancellationToken cancellationToken = default);

    Task<Result> RemoveExperienceAsync(int experienceId, int jobSeekerId, CancellationToken cancellationToken = default);

    Task<Result<int>> AddSkillsAsync(int jobSeekerId, string? commaSeparatedSkills, CancellationToken cancellationToken = default);

    Task<Result> RemoveSkillAsync(int skillId, int jobSeekerId, CancellationToken cancellationToken = default);

    Task<Result<int>> AddCertificationAsync(int jobSeekerId, CertificationDto model, CancellationToken cancellationToken = default);

    Task<Result> RemoveCertificationAsync(int certificationId, int jobSeekerId, CancellationToken cancellationToken = default);

    Task<Result<int>> AddLanguageAsync(int jobSeekerId, LanguageDto model, CancellationToken cancellationToken = default);

    Task<Result> RemoveLanguageAsync(int languageId, int jobSeekerId, CancellationToken cancellationToken = default);

    /// <summary>Recomputes and persists the stored completion percentage.</summary>
    Task<ProfileCompletionDto> RecalculateCompletionAsync(int jobSeekerId, CancellationToken cancellationToken = default);
}

public class CandidateBasicsDto
{
    public string? ProfessionalTitle { get; set; }
    public string? Location { get; set; }
    public string? Phone { get; set; }
    public string? About { get; set; }
    public decimal? YearsOfExperience { get; set; }
    public bool IsOpenToWork { get; set; } = true;
}

/// <summary>Employer registration profile and company profile management.</summary>
public interface IEmployerProfileService
{
    Task<Result<int>> EnsureEmployerAsync(string userId, string companyName, CancellationToken cancellationToken = default);

    Task<CompanyDto?> GetCompanyAsync(int employerId, CancellationToken cancellationToken = default);

    Task<CompanyDto?> GetCompanyForUserAsync(string userId, CancellationToken cancellationToken = default);

    Task<Result<int>> CreateOrUpdateCompanyAsync(int employerId, CompanyFormDto model, CancellationToken cancellationToken = default);

    /// <summary>Public company page. Only non-sensitive fields are projected.</summary>
    Task<CompanyPublicDto?> GetPublicCompanyAsync(int companyId, CancellationToken cancellationToken = default);

    Task<PagedResult<CompanyPublicDto>> SearchPublicCompaniesAsync(string? keyword, string? industry, int page, int pageSize, CancellationToken cancellationToken = default);
}

public class CompanyFormDto
{
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.StringLength(150)]
    public string? Industry { get; set; }

    [System.ComponentModel.DataAnnotations.StringLength(1000)]
    public string? Description { get; set; }

    [System.ComponentModel.DataAnnotations.StringLength(4000)]
    public string? About { get; set; }

    [System.ComponentModel.DataAnnotations.Url]
    [System.ComponentModel.DataAnnotations.StringLength(300)]
    public string? Website { get; set; }

    [System.ComponentModel.DataAnnotations.StringLength(100)]
    public string? Location { get; set; }

    [System.ComponentModel.DataAnnotations.EmailAddress]
    [System.ComponentModel.DataAnnotations.StringLength(256)]
    public string? ContactEmail { get; set; }

    [System.ComponentModel.DataAnnotations.Phone]
    [System.ComponentModel.DataAnnotations.StringLength(30)]
    public string? ContactPhone { get; set; }

    [System.ComponentModel.DataAnnotations.Range(1, 1_000_000)]
    public int? CompanySize { get; set; }

    [System.ComponentModel.DataAnnotations.Range(1800, 2100)]
    public int? FoundedYear { get; set; }
}

/// <summary>Public-safe company view. Contact details are intentionally omitted.</summary>
public class CompanyPublicDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public string? Industry { get; set; }
    public string? Description { get; set; }
    public string? About { get; set; }
    public string? Website { get; set; }
    public string? Location { get; set; }
    public int? CompanySize { get; set; }
    public int? FoundedYear { get; set; }
    public int ActiveVacancyCount { get; set; }
    public List<JobCardDto> Vacancies { get; set; } = new();
}

/// <summary>Transparent, rule-based candidate/vacancy comparison. No external AI services are used.</summary>
public interface ISkillMatchService
{
    Task<SkillMatchResultDto?> EvaluateAsync(int jobId, int jobSeekerId, CancellationToken cancellationToken = default);

    Task<List<JobCardDto>> RecommendAsync(int jobSeekerId, int take, CancellationToken cancellationToken = default);
}
