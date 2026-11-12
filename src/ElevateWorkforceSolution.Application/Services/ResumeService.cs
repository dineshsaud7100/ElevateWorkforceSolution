using ElevateWorkforceSolution.Application.Common;
using ElevateWorkforceSolution.Application.DTOs;
using ElevateWorkforceSolution.Application.Interfaces;
using ElevateWorkforceSolution.Application.Interfaces.Repositories;
using ElevateWorkforceSolution.Domain.Common;
using ElevateWorkforceSolution.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ApplicationEntity = ElevateWorkforceSolution.Domain.Entities.Application;

namespace ElevateWorkforceSolution.Application.Services;

/// <summary>
/// Resume upload and authorised retrieval. Validation covers extension, size, declared
/// content type and the actual file signature.
/// </summary>
public class ResumeService : IResumeService
{
    private static readonly string[] AllowedExtensions = { ".pdf", ".doc", ".docx" };

    private static readonly string[] AllowedContentTypes =
    {
        "application/pdf",
        "application/msword",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
    };

    private static readonly Dictionary<string, string[]> AllowedSignatures = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = new[] { "%PDF" },
        [".doc"] = new[] { "Ð»Ñ€Ñ", "Ð»Ñ\x93Ñ\x93" },
        [".docx"] = new[] { "PK" }
    };

    private readonly IRepository<Resume> _resumes;
    private readonly IRepository<JobSeeker> _jobSeekers;
    private readonly IRepository<ApplicationEntity> _applications;
    private readonly IRepository<Employer> _employers;
    private readonly IFileStorageService _storage;
    private readonly FileStoragePolicy _policy;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ResumeService> _logger;

    public ResumeService(
        IRepository<Resume> resumes,
        IRepository<JobSeeker> jobSeekers,
        IRepository<ApplicationEntity> applications,
        IRepository<Employer> employers,
        IFileStorageService storage,
        IOptions<FileStoragePolicy> policy,
        IUnitOfWork unitOfWork,
        ILogger<ResumeService> logger)
    {
        _resumes = resumes;
        _jobSeekers = jobSeekers;
        _applications = applications;
        _employers = employers;
        _storage = storage;
        _policy = policy.Value;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ResumeDto>> ListForCandidateAsync(
        int jobSeekerId, CancellationToken cancellationToken = default)
    {
        var resumes = await _resumes.Query()
            .Where(r => r.JobSeekerId == jobSeekerId)
            .OrderByDescending(r => r.UploadedDate)
            .ToListAsync(cancellationToken);

        return resumes.Select(Map).ToList();
    }

    public async Task<Result<int>> UploadAsync(
        int jobSeekerId, Stream content, string fileName, long fileSize,
        string contentType, string? label, CancellationToken cancellationToken = default)
    {
        if (!await _jobSeekers.AnyAsync(s => s.Id == jobSeekerId, cancellationToken))
        {
            return Result.Failure<int>("Your candidate profile could not be loaded.");
        }

        var extension = Path.GetExtension(fileName).ToLowerInvariant();

        if (!AllowedExtensions.Contains(extension))
        {
            return Result.Failure<int>("Only PDF, DOC and DOCX files are accepted.");
        }

        if (!string.IsNullOrWhiteSpace(contentType) && !AllowedContentTypes.Contains(contentType))
        {
            return Result.Failure<int>("The uploaded file type is not supported.");
        }

        if (fileSize <= 0)
        {
            return Result.Failure<int>("The uploaded file is empty.");
        }

        if (fileSize > _policy.MaxResumeSizeBytes)
        {
            return Result.Failure<int>(
                $"The file is larger than the {_policy.MaxResumeSizeBytes / (1024 * 1024)} MB limit.");
        }

        // Confirm the file signature so a renamed executable cannot be stored as a CV.
        content.Position = 0;
        var header = new byte[8];
        var read = await content.ReadAsync(header.AsMemory(0, header.Length), cancellationToken);
        content.Position = 0;

        if (read < 4 || !HasValidSignature(extension, header, read))
        {
            return Result.Failure<int>("The file content does not match its file extension.");
        }

        var stored = await _storage.SaveAsync(content, fileName, _policy.ResumeFolder);

        var resume = new Resume
        {
            JobSeekerId = jobSeekerId,
            OriginalFileName = stored.OriginalFileName,
            StoredFileName = stored.StoredFileName,
            ContentType = stored.ContentType,
            FileSizeBytes = stored.SizeBytes,
            Label = string.IsNullOrWhiteSpace(label) ? null : label.Trim(),
            UploadedDate = DateTime.UtcNow
        };

        await _resumes.AddAsync(resume, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Resume {ResumeId} uploaded for job seeker {JobSeekerId}.", resume.Id, jobSeekerId);
        return Result.Success(resume.Id);
    }

    public async Task<Result> DeleteAsync(int resumeId, int jobSeekerId, CancellationToken cancellationToken = default)
    {
        var resume = await _resumes.GetByIdAsync(resumeId, cancellationToken);

        if (resume is null || resume.JobSeekerId != jobSeekerId)
        {
            return Result.Failure("Resume not found.");
        }

        // Keep historical applications valid: only allow removal when nothing references it.
        var isReferenced = await _applications.AnyAsync(a => a.ResumeId == resumeId, cancellationToken);
        if (isReferenced)
        {
            return Result.Failure("This resume is attached to an existing application and cannot be deleted.");
        }

        _resumes.Remove(resume);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _storage.DeleteAsync(resume.StoredFileName, _policy.ResumeFolder);
        _logger.LogInformation("Resume {ResumeId} deleted for job seeker {JobSeekerId}.", resumeId, jobSeekerId);

        return Result.Success();
    }

    public async Task<ResumeDownload?> GetForDownloadAsync(
        int resumeId, string requesterUserId, string requesterRole, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(requesterUserId))
        {
            return null;
        }

        var resume = await _resumes.GetByIdAsync(resumeId, cancellationToken);
        if (resume is null)
        {
            return null;
        }

        var authorised = false;

        if (requesterRole == Roles.Administrator)
        {
            authorised = true;
        }
        else if (requesterRole == Roles.JobSeeker)
        {
            authorised = await _jobSeekers.AnyAsync(
                s => s.Id == resume.JobSeekerId && s.UserId == requesterUserId, cancellationToken);
        }
        else if (requesterRole == Roles.Employer)
        {
            // Employers may only open documents attached to an application for their own vacancy.
            var employerId = await _employers.Query()
                .Where(e => e.UserId == requesterUserId)
                .Select(e => (int?)e.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (employerId.HasValue)
            {
                authorised = await _applications.AnyAsync(
                    a => a.ResumeId == resumeId && a.EmployerId == employerId.Value, cancellationToken);
            }
        }

        if (!authorised)
        {
            _logger.LogWarning(
                "Unauthorised resume access attempt for resume {ResumeId} by user {UserId}.",
                resumeId, requesterUserId);
            return null;
        }

        var stream = await _storage.OpenReadAsync(resume.StoredFileName, _policy.ResumeFolder);
        if (stream is null)
        {
            _logger.LogWarning("Stored file {StoredFileName} for resume {ResumeId} was missing on disk.",
                resume.StoredFileName, resumeId);
            return null;
        }

        return new ResumeDownload(resume.OriginalFileName, resume.ContentType, stream);
    }

    private static bool HasValidSignature(string extension, byte[] header, int read)
    {
        if (!AllowedSignatures.TryGetValue(extension, out var signatures))
        {
            return false;
        }

        var text = System.Text.Encoding.Latin1.GetString(header, 0, read);

        return signatures.Any(signature => text.StartsWith(signature, StringComparison.Ordinal));
    }

    private static ResumeDto Map(Resume r) => new()
    {
        Id = r.Id,
        OriginalFileName = r.OriginalFileName,
        ContentType = r.ContentType,
        FileSizeBytes = r.FileSizeBytes,
        FileSizeDisplay = r.FileSizeDisplay,
        UploadedDate = r.UploadedDate,
        Label = r.Label
    };
}

/// <summary>Storage policy for candidate documents.</summary>
public class FileStoragePolicy
{
    public const string SectionName = "FileStorage";

    public long MaxResumeSizeBytes { get; set; } = 5 * 1024 * 1024;
    public long MaxLogoSizeBytes { get; set; } = 2 * 1024 * 1024;
    public string ResumeFolder { get; set; } = "resumes";
    public string LogoFolder { get; set; } = "logos";
}
