using ElevateWorkforceSolution.Application.Common;
using ElevateWorkforceSolution.Application.DTOs;
using ElevateWorkforceSolution.Domain.Enums;

namespace ElevateWorkforceSolution.Application.Interfaces;

/// <summary>Application submission and the recruitment pipeline.</summary>
public interface IApplicationService
{
    /// <summary>Context needed to render the apply form: existing resume plus match preview.</summary>
    Task<ApplyContextDto?> GetApplyContextAsync(int jobId, int jobSeekerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Submits an application. Rejects duplicate applications, applications to vacancies that
    /// are not active, applications past the deadline, and resumes the candidate does not own.
    /// </summary>
    Task<Result<int>> SubmitAsync(SubmitApplicationCommand command, int jobSeekerId, CancellationToken cancellationToken = default);

    Task<ApplicationSummaryDto?> GetForCandidateAsync(int applicationId, int jobSeekerId, CancellationToken cancellationToken = default);

    Task<PagedResult<ApplicationSummaryDto>> ListForCandidateAsync(
        ApplicationStatus? status, int jobSeekerId, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<Result> WithdrawAsync(int applicationId, int jobSeekerId, CancellationToken cancellationToken = default);

    Task<ApplicantDto?> GetApplicantAsync(int applicationId, int employerId, CancellationToken cancellationToken = default);

    Task<PagedResult<ApplicantDto>> ListApplicantsAsync(
        int employerId, int? jobId, ApplicationStatus? status, string? keyword, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<Result<ApplicantDto>> UpdateStatusAsync(UpdateApplicationStatusCommand command, int employerId, CancellationToken cancellationToken = default);

    Task<CandidateProfileDto?> GetCandidateProfileAsync(int jobSeekerId, int employerId, CancellationToken cancellationToken = default);
}

public class ApplyContextDto
{
    public int JobId { get; set; }
    public string JobTitle { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public bool AcceptsApplications { get; set; }
    public bool HasApplied { get; set; }
    public bool HasResume { get; set; }
    public List<ResumeDto> AvailableResumes { get; set; } = new();
    public SkillMatchResultDto? Match { get; set; }
    public string? BlockedReason { get; set; }
}

/// <summary>Saved (bookmarked) vacancies.</summary>
public interface ISavedJobService
{
    Task<bool> IsSavedAsync(int jobId, int jobSeekerId, CancellationToken cancellationToken = default);

    /// <summary>Bookmarks a vacancy. Returns the stored id, or a failure when already saved or unavailable.</summary>
    Task<Result<int>> SaveAsync(int jobId, int jobSeekerId, CancellationToken cancellationToken = default);

    Task<Result> UnsaveAsync(int jobId, int jobSeekerId, CancellationToken cancellationToken = default);

    Task<PagedResult<JobCardDto>> ListAsync(int jobSeekerId, int page, int pageSize, CancellationToken cancellationToken = default);
}

/// <summary>Database-backed notifications, always scoped to the requesting user.</summary>
public interface INotificationService
{
    Task<PagedResult<NotificationDto>> ListAsync(string userId, bool unreadOnly, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<int> GetUnreadCountAsync(string userId, CancellationToken cancellationToken = default);

    Task<Result> MarkAsReadAsync(int notificationId, string userId, CancellationToken cancellationToken = default);

    Task<Result> MarkAllAsReadAsync(string userId, CancellationToken cancellationToken = default);

    Task NotifyAsync(NotificationRequest request, CancellationToken cancellationToken = default);
}

public class NotificationRequest
{
    public required string UserId { get; init; }
    public required string Title { get; init; }
    public required string Message { get; init; }
    public NotificationType Type { get; init; }
    public string? RelatedEntityId { get; init; }
    public string? RelatedEntityType { get; init; }
}

/// <summary>Resume upload and authorised access to candidate documents.</summary>
public interface IResumeService
{
    Task<IReadOnlyList<ResumeDto>> ListForCandidateAsync(int jobSeekerId, CancellationToken cancellationToken = default);

    Task<Result<int>> UploadAsync(int jobSeekerId, Stream content, string fileName, long fileSize, string contentType, string? label, CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(int resumeId, int jobSeekerId, CancellationToken cancellationToken = default);

    /// <summary>Authorised read. Candidates may read their own; employers only for their own applicants.</summary>
    Task<ResumeDownload?> GetForDownloadAsync(int resumeId, string requesterUserId, string requesterRole, CancellationToken cancellationToken = default);
}

public record ResumeDownload(string FileName, string ContentType, Stream Content);
