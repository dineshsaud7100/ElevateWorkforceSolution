using ElevateWorkforceSolution.Domain.Enums;

namespace ElevateWorkforceSolution.Web.Extensions;

/// <summary>
/// Single place where enum values are turned into human-readable labels, so status, job type
/// and arrangement wording stays consistent across the UI and reports.
/// </summary>
public static class DisplayExtensions
{
    public static string Display(this JobType value) => value switch
    {
        JobType.FullTime => "Full Time",
        JobType.PartTime => "Part Time",
        JobType.Contract => "Contract",
        JobType.Internship => "Internship",
        JobType.Temporary => "Temporary",
        JobType.Freelance => "Freelance",
        _ => value.ToString()
    };

    public static string Display(this WorkArrangement value) => value switch
    {
        WorkArrangement.OnSite => "On-site",
        WorkArrangement.Remote => "Remote",
        WorkArrangement.Hybrid => "Hybrid",
        _ => value.ToString()
    };

    public static string Display(this ExperienceLevel value) => value switch
    {
        ExperienceLevel.Entry => "Entry Level",
        ExperienceLevel.Mid => "Mid Level",
        ExperienceLevel.Senior => "Senior Level",
        ExperienceLevel.Lead => "Lead",
        ExperienceLevel.Executive => "Executive",
        _ => value.ToString()
    };

    public static string Display(this VacancyStatus value) => value switch
    {
        VacancyStatus.PendingVerification => "Pending Verification",
        VacancyStatus.Active => "Active",
        VacancyStatus.Rejected => "Rejected",
        VacancyStatus.Closed => "Closed",
        VacancyStatus.Expired => "Expired",
        VacancyStatus.Removed => "Removed",
        _ => value.ToString()
    };

    public static string Display(this ApplicationStatus value) => value switch
    {
        ApplicationStatus.Applied => "Applied",
        ApplicationStatus.UnderReview => "Under Review",
        ApplicationStatus.Shortlisted => "Shortlisted",
        ApplicationStatus.Interview => "Interview",
        ApplicationStatus.Selected => "Selected",
        ApplicationStatus.Rejected => "Rejected",
        ApplicationStatus.Withdrawn => "Withdrawn",
        _ => value.ToString()
    };

    /// <summary>
    /// Resolves the human-readable label for any of the platform enums. Lets generic code
    /// (option builders, reports) use the same wording as the specific overloads.
    /// </summary>
    public static string Display(this Enum value) => value switch
    {
        JobType jobType => jobType.Display(),
        WorkArrangement arrangement => arrangement.Display(),
        ExperienceLevel level => level.Display(),
        VacancyStatus vacancy => vacancy.Display(),
        ApplicationStatus application => application.Display(),
        _ => value.ToString()
    };

    /// <summary>CSS modifier used to colour status badges consistently.</summary>
    public static string CssClass(this VacancyStatus value) => value switch
    {
        VacancyStatus.Active => "status-active",
        VacancyStatus.PendingVerification => "status-pending",
        VacancyStatus.Rejected => "status-rejected",
        VacancyStatus.Closed => "status-closed",
        VacancyStatus.Expired => "status-expired",
        VacancyStatus.Removed => "status-removed",
        _ => "status-closed"
    };

    public static string CssClass(this ApplicationStatus value) => value switch
    {
        ApplicationStatus.Applied => "status-applied",
        ApplicationStatus.UnderReview => "status-review",
        ApplicationStatus.Shortlisted => "status-shortlisted",
        ApplicationStatus.Interview => "status-interview",
        ApplicationStatus.Selected => "status-selected",
        ApplicationStatus.Rejected => "status-rejected",
        ApplicationStatus.Withdrawn => "status-withdrawn",
        _ => "status-closed"
    };

    /// <summary>Formats an NPR salary range for display, hiding the figures when not disclosed.</summary>
    public static string FormatSalary(this decimal? min, decimal? max, string? currency, string? period)
    {
        if (min is null && max is null)
        {
            return "Not disclosed";
        }

        var symbol = string.IsNullOrWhiteSpace(currency) ? string.Empty : currency + " ";
        var suffix = string.IsNullOrWhiteSpace(period) ? string.Empty : $" / {period.ToLowerInvariant()}";

        if (min.HasValue && max.HasValue)
        {
            return $"{symbol}{min.Value:N0} - {max.Value:N0}{suffix}";
        }

        var single = min ?? max;
        if (!single.HasValue)
        {
            return "Not disclosed";
        }

        return min.HasValue
            ? $"From {symbol}{single.Value:N0}{suffix}"
            : $"Up to {symbol}{single.Value:N0}{suffix}";
    }

    public static string TimeAgo(this DateTime value)
    {
        var span = DateTime.UtcNow - value;

        if (span.TotalMinutes < 1) return "just now";
        if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m ago";
        if (span.TotalHours < 24) return $"{(int)span.TotalHours}h ago";
        if (span.TotalDays < 7) return $"{(int)span.TotalDays}d ago";
        if (span.TotalDays < 31) return $"{(int)(span.TotalDays / 7)}w ago";
        return value.ToString("dd MMM yyyy");
    }
}
