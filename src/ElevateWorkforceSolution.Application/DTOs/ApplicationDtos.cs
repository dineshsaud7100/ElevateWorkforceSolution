using ElevateWorkforceSolution.Domain.Entities;
using ElevateWorkforceSolution.Domain.Enums;

namespace ElevateWorkforceSolution.Application.DTOs;

/// <summary>Candidate-facing application row.</summary>
public class ApplicationSummaryDto
{
    public int Id { get; set; }
    public int JobId { get; set; }
    public string JobTitle { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string? CompanyLogoUrl { get; set; }
    public string? WorkLocation { get; set; }
    public ApplicationStatus Status { get; set; }
    public string StatusLabel { get; set; } = string.Empty;
    public DateTime AppliedDate { get; set; }
    public DateTime? LastUpdated { get; set; }
    public string? CoverLetter { get; set; }
    public int? CompatibilityScore { get; set; }
    public bool CanWithdraw { get; set; }
    public List<ApplicationTimelineEntryDto> Timeline { get; set; } = new();
}

/// <summary>One entry in an application's status timeline.</summary>
public class ApplicationTimelineEntryDto
{
    public ApplicationStatus Status { get; set; }
    public string Label { get; set; } = string.Empty;
    public string? Remarks { get; set; }
    public DateTime ChangedDate { get; set; }
}

/// <summary>Employer-facing applicant row.</summary>
public class ApplicantDto
{
    public int ApplicationId { get; set; }
    public int JobId { get; set; }
    public string JobTitle { get; set; } = string.Empty;
    public int JobSeekerId { get; set; }
    public string CandidateName { get; set; } = string.Empty;
    public string? ProfessionalTitle { get; set; }
    public string? CandidateLocation { get; set; }
    public string? CandidatePhone { get; set; }
    public string? CandidateEmail { get; set; }
    public decimal? YearsOfExperience { get; set; }
    public int? ProfileCompletion { get; set; }
    public ApplicationStatus Status { get; set; }
    public string StatusLabel { get; set; } = string.Empty;
    public DateTime AppliedDate { get; set; }
    public DateTime? LastUpdated { get; set; }
    public string? CoverLetter { get; set; }
    public int? ResumeId { get; set; }
    public string? ResumeFileName { get; set; }
    public int? CompatibilityScore { get; set; }
    public List<string> Skills { get; set; } = new();
    public List<ApplicationStatus> AllowedTransitions { get; set; } = new();
}

/// <summary>Candidate profile snapshot shown to an employer reviewing an applicant.</summary>
public class CandidateProfileDto
{
    public int JobSeekerId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Location { get; set; }
    public string? ProfessionalTitle { get; set; }
    public string? About { get; set; }
    public decimal? YearsOfExperience { get; set; }
    public int ProfileCompletion { get; set; }
    public bool HasResume { get; set; }
    public string? LatestResumeFileName { get; set; }
    public List<SkillDto> Skills { get; set; } = new();
    public List<EducationDto> Educations { get; set; } = new();
    public List<ExperienceDto> Experiences { get; set; } = new();
    public List<CertificationDto> Certifications { get; set; } = new();
    public List<LanguageDto> Languages { get; set; } = new();
}

public class SkillDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public SkillProficiency Proficiency { get; set; }
    public int? YearsOfExperience { get; set; }
}

public class EducationDto
{
    public int Id { get; set; }
    public string Institution { get; set; } = string.Empty;
    public string Degree { get; set; } = string.Empty;
    public string? FieldOfStudy { get; set; }
    public int? GraduationYear { get; set; }
    public string? Grade { get; set; }
    public string? Description { get; set; }
}

public class ExperienceDto
{
    public int Id { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
    public string? Location { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool IsCurrent { get; set; }
    public string? Description { get; set; }
}

public class CertificationDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string IssuingOrganization { get; set; } = string.Empty;
    public DateTime? IssueDate { get; set; }
    public string? CredentialId { get; set; }
}

public class LanguageDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public LanguageProficiency Proficiency { get; set; }
}

/// <summary>Command for submitting an application.</summary>
public class SubmitApplicationCommand
{
    public int JobId { get; set; }
    public int ResumeId { get; set; }
    public string? CoverLetter { get; set; }
}

/// <summary>Command for an employer to move an applicant through the pipeline.</summary>
public class UpdateApplicationStatusCommand
{
    public int ApplicationId { get; set; }
    public ApplicationStatus Status { get; set; }
    public string? Remarks { get; set; }
}
