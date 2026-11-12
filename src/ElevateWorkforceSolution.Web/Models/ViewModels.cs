using ElevateWorkforceSolution.Application.Common;
using ElevateWorkforceSolution.Application.Interfaces;
using System.ComponentModel.DataAnnotations;
using ElevateWorkforceSolution.Application.DTOs;
using ElevateWorkforceSolution.Domain.Entities;
using ElevateWorkforceSolution.Domain.Enums;
using ElevateWorkforceSolution.Web.Extensions;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace ElevateWorkforceSolution.Web.Models;

/// <summary>Landing page model. Counts are read from the database, never hard-coded.</summary>
public class HomeViewModel
{
    public int TotalActiveVacancies { get; set; }
    public int TotalCompaniesHiring { get; set; }
    public List<CategoryCountDto> PopularCategories { get; set; } = new();
    public IReadOnlyList<JobCardDto> LatestJobs { get; set; } = Array.Empty<JobCardDto>();
    public IReadOnlyList<CompanyPublicDto> FeaturedCompanies { get; set; } = Array.Empty<CompanyPublicDto>();}

public class ErrorViewModel
{
    public string? RequestId { get; set; }
    public int StatusCode { get; set; }

    public string Title => StatusCode switch
    {
        404 => "We could not find that page",
        403 => "You do not have access to that page",
        401 => "Please sign in to continue",
        >= 500 => "Something went wrong on our side",
        _ => "Request could not be completed"
    };

    public string Message => StatusCode switch
    {
        404 => "The page or vacancy you were looking for has moved, or was removed by an administrator.",
        403 => "This area is limited to a different type of account. If that seems wrong, sign in with another account.",
        401 => "Sign in with your candidate, employer or administrator account to continue.",
        >= 500 => "We have logged the problem. Please try again in a moment.",
        _ => "Please review the details and try again."
    };
}

/// <summary>Search page model: the filter, the page of results and the database-derived facets.</summary>
public class JobSearchViewModel
{
    public JobSearchFilter Filter { get; set; } = new();
    public PagedResult<JobCardDto> Results { get; set; } = PagedResult<JobCardDto>.Empty();
    public JobSearchFacets Facets { get; set; } = new();

    public List<SelectListItem> JobTypeOptions { get; set; } = new();
    public List<SelectListItem> WorkArrangementOptions { get; set; } = new();
    public List<SelectListItem> ExperienceLevelOptions { get; set; } = new();
    public List<SelectListItem> SortOptions { get; set; } = new();
    public List<SelectListItem> LocationOptions { get; set; } = new();
    public List<SelectListItem> CategoryOptions { get; set; } = new();
}

public class JobDetailsViewModel
{
    public JobDetailDto Job { get; set; } = new();
    public List<JobCardDto> SimilarJobs { get; set; } = new();
    public SkillMatchResultDto? Match { get; set; }
}

public class CompanyListViewModel
{
    public string? Keyword { get; set; }
    public string? Industry { get; set; }
    public int Page { get; set; } = 1;
    public PagedResult<CompanyPublicDto> Results { get; set; } = PagedResult<CompanyPublicDto>.Empty();
    public List<string> Industries { get; set; } = new();
}

public class CompanyDetailsViewModel
{
    public CompanyPublicDto Company { get; set; } = new();
}

/// <summary>Sign-in form.</summary>
public class LoginViewModel
{
    [Required]
    [EmailAddress]
    [Display(Name = "Email address")]
    public string Email { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Remember me")]
    public bool RememberMe { get; set; }
}

public enum AccountType
{
    JobSeeker = 1,
    Employer = 2
}

/// <summary>
/// Registration form. The role is chosen by the applicant but restricted to the two public
/// account types; an administrator can only be created by seeding.
/// </summary>
public class RegisterViewModel
{
    [Required]
    [StringLength(50, MinimumLength = 2)]
    [Display(Name = "First name")]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [StringLength(50, MinimumLength = 2)]
    [Display(Name = "Last name")]
    public string LastName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [Display(Name = "Email address")]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(30, MinimumLength = 6)]
    [Display(Name = "Phone number")]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Use at least 8 characters.")]
    [Display(Name = "Password")]
    public string Password { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "The passwords do not match.")]
    [Display(Name = "Confirm password")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Required]
    [Display(Name = "I am looking for work")]
    public bool RegisterAsJobSeeker { get; set; } = true;

    [Display(Name = "Company name (employers only)")]
    [StringLength(200)]
    public string? CompanyName { get; set; }

    [Display(Name = "I agree to the terms of use and privacy policy")]
    [Range(typeof(bool), "true", "true", ErrorMessage = "Please accept the terms to continue.")]
    public bool AcceptTerms { get; set; }

    public AccountType AccountType => RegisterAsJobSeeker ? AccountType.JobSeeker : AccountType.Employer;
}

/// <summary>Account settings for a signed-in user of any role.</summary>
public class AccountProfileViewModel
{
    [Required]
    [StringLength(50, MinimumLength = 2)]
    [Display(Name = "First name")]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [StringLength(50, MinimumLength = 2)]
    [Display(Name = "Last name")]
    public string LastName { get; set; } = string.Empty;

    [EmailAddress]
    [StringLength(256)]
    [Display(Name = "Email address")]
    public string Email { get; set; } = string.Empty;

    [Phone]
    [StringLength(30)]
    [Display(Name = "Phone number")]
    public string? PhoneNumber { get; set; }
}

public class ChangePasswordViewModel
{
    [Required]
    [DataType(DataType.Password)]
    [Display(Name = "Current password")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Use at least 8 characters.")]
    [Display(Name = "New password")]
    public string NewPassword { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [Compare(nameof(NewPassword), ErrorMessage = "The passwords do not match.")]
    [Display(Name = "Confirm new password")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

/// <summary>Candidate workspace pages.</summary>
public class CandidateDashboardViewModel
{
    public JobSeekerDashboardDto Dashboard { get; set; } = new();
    public ProfileCompletionDto Completion { get; set; } = new();
}

public class CandidateProfileViewModel
{
    public CandidateProfileDto Profile { get; set; } = new();
    public ProfileCompletionDto Completion { get; set; } = new();
    public CandidateBasicsDto Basics { get; set; } = new();
}

public class EducationFormViewModel
{
    public int? Id { get; set; }
    public int JobSeekerId { get; set; }

    [Required]
    [StringLength(200)]
    [Display(Name = "Institution")]
    public string Institution { get; set; } = string.Empty;

    [Required]
    [StringLength(150)]
    [Display(Name = "Degree")]
    public string Degree { get; set; } = string.Empty;

    [StringLength(150)]
    [Display(Name = "Field of study")]
    public string? FieldOfStudy { get; set; }

    [Range(1950, 2100)]
    [Display(Name = "Graduation year")]
    public int? GraduationYear { get; set; }

    [StringLength(50)]
    [Display(Name = "Grade / GPA")]
    public string? Grade { get; set; }

    [StringLength(1000)]
    [Display(Name = "Description")]
    public string? Description { get; set; }
}

public class ExperienceFormViewModel
{
    public int? Id { get; set; }
    public int JobSeekerId { get; set; }

    [Required]
    [StringLength(200)]
    [Display(Name = "Company")]
    public string CompanyName { get; set; } = string.Empty;

    [Required]
    [StringLength(150)]
    [Display(Name = "Position")]
    public string Position { get; set; } = string.Empty;

    [StringLength(100)]
    [Display(Name = "Location")]
    public string? Location { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Start date")]
    public DateTime StartDate { get; set; } = DateTime.UtcNow.AddYears(-1);

    [DataType(DataType.Date)]
    [Display(Name = "End date")]
    public DateTime? EndDate { get; set; }

    [Display(Name = "I currently work here")]
    public bool IsCurrent { get; set; } = true;

    [StringLength(2000)]
    [Display(Name = "Description")]
    public string? Description { get; set; }
}

public class CertificationFormViewModel
{
    public int? Id { get; set; }
    public int JobSeekerId { get; set; }

    [Required]
    [StringLength(200)]
    [Display(Name = "Certification name")]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    [Display(Name = "Issuing organisation")]
    public string IssuingOrganization { get; set; } = string.Empty;

    [DataType(DataType.Date)]
    [Display(Name = "Issue date")]
    public DateTime? IssueDate { get; set; }

    [StringLength(100)]
    [Display(Name = "Credential ID")]
    public string? CredentialId { get; set; }
}

public class LanguageFormViewModel
{
    public int? Id { get; set; }
    public int JobSeekerId { get; set; }

    [Required]
    [StringLength(80)]
    [Display(Name = "Language")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Proficiency")]
    public LanguageProficiency Proficiency { get; set; } = LanguageProficiency.Conversational;

    public List<SelectListItem> ProficiencyOptions { get; set; } = new();
}

public class SkillsFormViewModel
{
    public int JobSeekerId { get; set; }
    public List<SkillDto> Skills { get; set; } = new();
    public string? NewSkills { get; set; }
    public string? Proficiency { get; set; }
}

public class ResumeListViewModel
{
    public int JobSeekerId { get; set; }
    public List<ResumeDto> Resumes { get; set; } = new();
}

/// <summary>Candidate application pages.</summary>
public class ApplyViewModel
{
    public int JobId { get; set; }
    public ApplyContextDto Context { get; set; } = new();
    public List<SelectListItem> ResumeOptions { get; set; } = new();

    [Required]
    [Display(Name = "Resume / CV")]
    public int ResumeId { get; set; }

    [StringLength(4000)]
    [Display(Name = "Cover letter")]
    public string? CoverLetter { get; set; }
}

public class MyApplicationsViewModel
{
    public ApplicationStatus? Status { get; set; }
    public int Page { get; set; } = 1;
    public PagedResult<ApplicationSummaryDto> Results { get; set; } = PagedResult<ApplicationSummaryDto>.Empty();
    public List<SelectListItem> StatusOptions { get; set; } = new();
}

public class ApplicationDetailsViewModel
{
    public ApplicationSummaryDto Application { get; set; } = new();
}

public class SavedJobsViewModel
{
    public int Page { get; set; } = 1;
    public PagedResult<JobCardDto> Results { get; set; } = PagedResult<JobCardDto>.Empty();
}

/// <summary>Employer workspace pages.</summary>
public class EmployerDashboardViewModel
{
    public EmployerDashboardDto Dashboard { get; set; } = new();
}

public class EmployerVacanciesViewModel
{
    public VacancyStatus? Status { get; set; }
    public string? Keyword { get; set; }
    public int Page { get; set; } = 1;
    public PagedResult<VacancyManagementDto> Results { get; set; } = PagedResult<VacancyManagementDto>.Empty();
    public List<SelectListItem> StatusOptions { get; set; } = new();
}

public class VacancyFormViewModel
{
    public int EmployerId { get; set; }
    public bool HasCompanyProfile { get; set; }
    public VacancyFormDto Form { get; set; } = new();

    public List<SelectListItem> JobTypeOptions { get; set; } = new();
    public List<SelectListItem> WorkArrangementOptions { get; set; } = new();
    public List<SelectListItem> ExperienceLevelOptions { get; set; } = new();
    public List<SelectListItem> CategoryOptions { get; set; } = new();
    public List<SelectListItem> LocationOptions { get; set; } = new();
}

public class EmployerCompanyViewModel
{
    public int EmployerId { get; set; }
    public CompanyDto? Company { get; set; }
    public CompanyFormDto Form { get; set; } = new();
}

public class ApplicantsViewModel
{
    public int? JobId { get; set; }
    public ApplicationStatus? Status { get; set; }
    public string? Keyword { get; set; }
    public int Page { get; set; } = 1;
    public PagedResult<ApplicantDto> Results { get; set; } = PagedResult<ApplicantDto>.Empty();
    public List<SelectListItem> StatusOptions { get; set; } = new();
    public List<SelectListItem> JobOptions { get; set; } = new();
}

public class ApplicantDetailsViewModel
{
    public ApplicantDto Applicant { get; set; } = new();
    public CandidateProfileDto? Profile { get; set; }
    public JobDetailDto? Job { get; set; }
    public List<SelectListItem> StatusOptions { get; set; } = new();
    public int ApplicationId { get; set; }
    public int JobSeekerId { get; set; }
    public ApplicationStatus Status { get; set; } = ApplicationStatus.UnderReview;
    public string? Remarks { get; set; }
}

public class UpdateApplicantStatusViewModel
{
    public int ApplicationId { get; set; }
    public int JobSeekerId { get; set; }

    [Required]
    [Display(Name = "Move to")]
    public ApplicationStatus Status { get; set; } = ApplicationStatus.UnderReview;

    [StringLength(1000)]
    [Display(Name = "Note for the candidate (optional)")]
    public string? Remarks { get; set; }
}

/// <summary>Administrator pages.</summary>
public class AdminDashboardViewModel
{
    public AdminDashboardDto Dashboard { get; set; } = new();
}

public class AdminUsersViewModel
{
    public string? Keyword { get; set; }
    public string? Role { get; set; }
    public bool? ActiveOnly { get; set; }
    public int Page { get; set; } = 1;
    public PagedResult<AdminUserRowDto> Results { get; set; } = PagedResult<AdminUserRowDto>.Empty();
    public List<SelectListItem> RoleOptions { get; set; } = new();
}

public class AdminVacanciesViewModel
{
    public string? Keyword { get; set; }
    public VacancyStatus? Status { get; set; }
    public string? Location { get; set; }
    public int Page { get; set; } = 1;
    public PagedResult<VacancyModerationDto> Results { get; set; } = PagedResult<VacancyModerationDto>.Empty();
    public List<SelectListItem> StatusOptions { get; set; } = new();
}

public class AdminVacancyReviewViewModel
{
    public VacancyModerationDto Vacancy { get; set; } = new();
}

public class AdminCompaniesViewModel
{
    public string? Keyword { get; set; }
    public int Page { get; set; } = 1;
    public PagedResult<CompanyDto> Results { get; set; } = PagedResult<CompanyDto>.Empty();
}

public class AdminApplicationsViewModel
{
    public string? Keyword { get; set; }
    public ApplicationStatus? Status { get; set; }
    public int Page { get; set; } = 1;
    public PagedResult<ApplicationSummaryDto> Results { get; set; } = PagedResult<ApplicationSummaryDto>.Empty();
    public List<SelectListItem> StatusOptions { get; set; } = new();
}

public class AdminReportsViewModel
{
    public AdminReportDto Report { get; set; } = new();
}

/// <summary>Notification centre.</summary>
public class NotificationListViewModel
{
    public bool UnreadOnly { get; set; }
    public int Page { get; set; } = 1;
    public PagedResult<NotificationDto> Results { get; set; } = PagedResult<NotificationDto>.Empty();
}

/// <summary>
/// Pager state, flattened from any <see cref="PagedResult{T}"/> so a single partial can render
/// pagination for every list in the application.
/// </summary>
public class PagerViewModel
{
    public int PageNumber { get; set; } = 1;
    public int TotalPages { get; set; }
    public int TotalCount { get; set; }
    public int FirstItemIndex { get; set; }
    public int LastItemIndex { get; set; }

    /// <summary>URL without the page parameter, e.g. <c>/Jobs?keyword=dev&amp;</c> or <c>?</c>.</summary>
    public string BaseUrl { get; set; } = "?";

    public bool HasPrevious => PageNumber > 1;
    public bool HasNext => PageNumber < TotalPages;
    public bool Show => TotalPages > 1;

    public string UrlFor(int page) => BaseUrl + "page=" + page;
}
