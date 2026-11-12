namespace ElevateWorkforceSolution.Domain.Common;

/// <summary>Canonical role names. Referenced instead of inline strings so roles are defined in one place.</summary>
public static class Roles
{
    public const string Administrator = "Administrator";
    public const string Employer = "Employer";
    public const string JobSeeker = "JobSeeker";

    public static readonly IReadOnlyList<string> All = new[] { Administrator, Employer, JobSeeker };
}
