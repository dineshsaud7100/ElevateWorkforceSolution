namespace ElevateWorkforceSolution.Domain.Enums;

/// <summary>Lifecycle of an application. Employers drive transitions; candidates may withdraw.</summary>
public enum ApplicationStatus
{
    Applied = 1,
    UnderReview = 2,
    Shortlisted = 3,
    Interview = 4,
    Selected = 5,
    Rejected = 6,
    Withdrawn = 7
}
