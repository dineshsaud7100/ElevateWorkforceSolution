using ElevateWorkforceSolution.Domain.Enums;

namespace ElevateWorkforceSolution.Domain.Common;

/// <summary>Transition rules for vacancy and application statuses, kept out of controllers and views.</summary>
public static class StatusRules
{
    /// <summary>Vacancies that are publicly listed on the careers site.</summary>
    public static bool IsPubliclyVisible(VacancyStatus status) => status == VacancyStatus.Active;

    /// <summary>Vacancies that will still accept a new application.</summary>
    public static bool AcceptsApplications(VacancyStatus status) => status == VacancyStatus.Active;

    /// <summary>Every new vacancy must be reviewed and approved by a platform administrator.</summary>
    public static VacancyStatus InitialVacancyStatus() => VacancyStatus.PendingVerification;

    /// <summary>Statuses only a platform administrator may assign.</summary>
    public static readonly IReadOnlyList<VacancyStatus> AdminOnlyStatuses = new[]
    {
        VacancyStatus.Rejected,
        VacancyStatus.Removed
    };

    /// <summary>Statuses an employer may move an application to.</summary>
    public static readonly IReadOnlyList<ApplicationStatus> EmployerAssignableStatuses = new[]
    {
        ApplicationStatus.UnderReview,
        ApplicationStatus.Shortlisted,
        ApplicationStatus.Interview,
        ApplicationStatus.Selected,
        ApplicationStatus.Rejected
    };

    /// <summary>True when the transition is allowed by the recruitment workflow.</summary>
    public static bool IsValidEmployerTransition(ApplicationStatus current, ApplicationStatus next) =>
        current switch
        {
            ApplicationStatus.Applied =>
                next is ApplicationStatus.UnderReview or ApplicationStatus.Shortlisted
                    or ApplicationStatus.Rejected or ApplicationStatus.Withdrawn,
            ApplicationStatus.UnderReview =>
                next is ApplicationStatus.Shortlisted or ApplicationStatus.Interview
                    or ApplicationStatus.Rejected or ApplicationStatus.Withdrawn,
            ApplicationStatus.Shortlisted =>
                next is ApplicationStatus.Interview or ApplicationStatus.Selected
                    or ApplicationStatus.Rejected or ApplicationStatus.Withdrawn,
            ApplicationStatus.Interview =>
                next is ApplicationStatus.Selected or ApplicationStatus.Rejected
                    or ApplicationStatus.Withdrawn,
            _ => false
        };

    /// <summary>Display text for timeline entries and status badges.</summary>
    public static string Describe(ApplicationStatus status) => status switch
    {
        ApplicationStatus.Applied => "Application submitted",
        ApplicationStatus.UnderReview => "Application under review",
        ApplicationStatus.Shortlisted => "Candidate shortlisted",
        ApplicationStatus.Interview => "Moved to interview",
        ApplicationStatus.Selected => "Candidate selected",
        ApplicationStatus.Rejected => "Application rejected",
        ApplicationStatus.Withdrawn => "Application withdrawn",
        _ => status.ToString()
    };
}
