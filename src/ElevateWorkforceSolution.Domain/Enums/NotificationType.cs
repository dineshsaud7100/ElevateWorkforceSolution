namespace ElevateWorkforceSolution.Domain.Enums;

/// <summary>Category of a database-backed notification.</summary>
public enum NotificationType
{
    ApplicationSubmitted = 1,
    ApplicationStatusChanged = 2,
    CandidateShortlisted = 3,
    CandidateMovedToInterview = 4,
    CandidateRejected = 5,
    VacancyApproved = 6,
    VacancyRejected = 7,
    NewApplication = 8
}
