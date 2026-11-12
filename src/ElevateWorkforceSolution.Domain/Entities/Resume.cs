namespace ElevateWorkforceSolution.Domain.Entities;

/// <summary>Uploaded CV. Files are stored outside the web root and served only through authorised checks.</summary>
public class Resume
{
    public int Id { get; set; }
    public int JobSeekerId { get; set; }
    public JobSeeker? JobSeeker { get; set; }

    public string OriginalFileName { get; set; } = string.Empty;
    public string StoredFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string? Label { get; set; }
    public DateTime UploadedDate { get; set; } = DateTime.UtcNow;

    public string FileSizeDisplay => FileSizeBytes switch
    {
        < 1024 => $"{FileSizeBytes} B",
        < 1024 * 1024 => $"{FileSizeBytes / 1024.0:0.#} KB",
        _ => $"{FileSizeBytes / (1024.0 * 1024.0):0.#} MB"
    };
}
