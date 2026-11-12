using System.Security.Cryptography;
using ElevateWorkforceSolution.Application.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ElevateWorkforceSolution.Infrastructure.Storage;

public class FileStorageOptions
{
    public const string SectionName = "FileStorage";

    /// <summary>Absolute or content-root-relative directory that holds uploaded documents.</summary>
    public string RootPath { get; set; } = "App_Data/uploads";

    /// <summary>Maximum accepted upload size in bytes.</summary>
    public long MaxFileSizeBytes { get; set; } = 5 * 1024 * 1024;

    /// <summary>Permitted upload categories and their allowed extensions / content types.</summary>
    public Dictionary<string, FileCategoryOptions> Categories { get; set; } = new();
}

public class FileCategoryOptions
{
    public List<string> AllowedExtensions { get; set; } = new();
    public List<string> AllowedContentTypes { get; set; } = new();
}

/// <summary>
/// Local-disk implementation of <see cref="IFileStorageService"/>. Files are written outside
/// the web root with randomised names so that documents cannot be fetched anonymously or
/// guessed by URL.
/// </summary>
public class FileStorageService : IFileStorageService
{
    private readonly FileStorageOptions _options;
    private readonly string _rootPath;
    private readonly ILogger<FileStorageService> _logger;

    public FileStorageService(
        IOptions<FileStorageOptions> options,
        IWebHostEnvironment environment,
        ILogger<FileStorageService> logger)
    {
        _options = options.Value;
        _logger = logger;

        var configured = _options.RootPath;
        _rootPath = Path.IsPathRooted(configured)
            ? configured
            : Path.Combine(environment.ContentRootPath, configured);

        Directory.CreateDirectory(_rootPath);
    }

    public async Task<StoredFile> SaveAsync(Stream content, string originalFileName, string? folder = null)
    {
        ArgumentNullException.ThrowIfNull(content);

        var safeOriginal = Path.GetFileName(originalFileName);
        var extension = Path.GetExtension(safeOriginal).ToLowerInvariant();
        var storedName = $"{Guid.NewGuid():N}{extension}";

        var directory = ResolveDirectory(folder);
        var fullPath = Path.Combine(directory, storedName);

        // Stored outside the web root; served only after an authorisation check.
        await using (var target = new FileStream(
            fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
        {
            await content.CopyToAsync(target);
        }

        var size = new FileInfo(fullPath).Length;
        var contentType = GuessContentType(extension);

        _logger.LogInformation(
            "Stored file {StoredFileName} ({SizeBytes} bytes) in {Category} category.",
            storedName, size, folder ?? "default");

        var relativePath = Path.GetRelativePath(_rootPath, fullPath);
        return new StoredFile(storedName, safeOriginal, contentType, size, relativePath);
    }

    public Task<Stream?> OpenReadAsync(string storedFileName, string? folder = null)
    {
        var fullPath = ResolveSafePath(storedFileName, folder);
        if (fullPath is null || !File.Exists(fullPath))
        {
            return Task.FromResult<Stream?>(null);
        }

        Stream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult<Stream?>(stream);
    }

    public Task<bool> DeleteAsync(string storedFileName, string? folder = null)
    {
        var fullPath = ResolveSafePath(storedFileName, folder);
        if (fullPath is null || !File.Exists(fullPath))
        {
            return Task.FromResult(false);
        }

        File.Delete(fullPath);
        return Task.FromResult(true);
    }

    public Task<bool> ExistsAsync(string storedFileName, string? folder = null)
    {
        var fullPath = ResolveSafePath(storedFileName, folder);
        return Task.FromResult(fullPath is not null && File.Exists(fullPath));
    }

    private string ResolveDirectory(string? folder)
    {
        var directory = string.IsNullOrWhiteSpace(folder)
            ? _rootPath
            : Path.Combine(_rootPath, SanitiseFolder(folder));

        Directory.CreateDirectory(directory);
        return directory;
    }

    /// <summary>
    /// Resolves a stored name to an absolute path, rejecting anything that escapes the
    /// storage root (path traversal via "..", absolute paths or rooted segments).
    /// </summary>
    private string? ResolveSafePath(string storedFileName, string? folder)
    {
        if (string.IsNullOrWhiteSpace(storedFileName) || storedFileName != Path.GetFileName(storedFileName))
        {
            _logger.LogWarning("Rejected file access attempt with an invalid stored name.");
            return null;
        }

        var directory = string.IsNullOrWhiteSpace(folder)
            ? _rootPath
            : Path.Combine(_rootPath, SanitiseFolder(folder));

        var fullPath = Path.GetFullPath(Path.Combine(directory, storedFileName));
        var root = Path.GetFullPath(_rootPath);

        if (!fullPath.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            _logger.LogWarning("Rejected file access attempt outside the storage root.");
            return null;
        }

        return fullPath;
    }

    private static string SanitiseFolder(string folder)
    {
        var cleaned = new string(folder.Where(char.IsLetterOrDigit).ToArray());
        return string.IsNullOrEmpty(cleaned) ? "default" : cleaned;
    }

    private static string GuessContentType(string extension) => extension switch
    {
        ".pdf" => "application/pdf",
        ".doc" => "application/msword",
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        _ => "application/octet-stream"
    };
}
