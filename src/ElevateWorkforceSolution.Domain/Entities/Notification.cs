using ElevateWorkforceSolution.Domain.Enums;

namespace ElevateWorkforceSolution.Domain.Entities;

/// <summary>Database-backed notification. Users only ever read their own rows.</summary>
public class Notification
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public NotificationType Type { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>Optional deep-link target, e.g. an application id.</summary>
    public string? RelatedEntityId { get; set; }
    public string? RelatedEntityType { get; set; }
}
