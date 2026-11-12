namespace ElevateWorkforceSolution.Domain.Enums;

/// <summary>Centralised vacancy status. Only <see cref="Active"/> vacancies are publicly listed.</summary>
public enum VacancyStatus
{
    PendingVerification = 1,
    Active = 2,
    Rejected = 3,
    Closed = 4,
    Expired = 5,
    Removed = 6
}
