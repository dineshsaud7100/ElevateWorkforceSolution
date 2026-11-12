namespace ElevateWorkforceSolution.Application.Interfaces;

/// <summary>
/// Abstraction over physical file storage. Implementations can be swapped for cloud
/// storage (S3, Azure Blob) without changing controllers or services.
/// </summary>
public interface IFileStorageService
{
    /// <summary>Persists a file and returns the generated stored (non-guessable) name.</summary>
    Task<StoredFile> SaveAsync(Stream content, string originalFileName, string? folder = null);

    /// <summary>Opens a stored file for reading. Returns null when the file no longer exists.</summary>
    Task<Stream?> OpenReadAsync(string storedFileName, string? folder = null);

    /// <summary>Deletes a stored file. Returns false when nothing was removed.</summary>
    Task<bool> DeleteAsync(string storedFileName, string? folder = null);

    /// <summary>True when a file with the given stored name exists.</summary>
    Task<bool> ExistsAsync(string storedFileName, string? folder = null);
}

/// <summary>Result of persisting a file, plus the metadata persisted in the database.</summary>
public sealed record StoredFile(
    string StoredFileName,
    string OriginalFileName,
    string ContentType,
    long SizeBytes,
    string RelativePath);
