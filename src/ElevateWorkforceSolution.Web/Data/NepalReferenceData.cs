namespace ElevateWorkforceSolution.Web.Data;

/// <summary>Nepal-focused reference data used by the seeder, search filters and views.</summary>
public static class NepalReferenceData
{
    public static readonly IReadOnlyList<string> Locations = new[]
    {
        "Kathmandu", "Lalitpur", "Bhaktapur", "Pokhara", "Chitwan", "Butwal", "Biratnagar", "Dharan"
    };

    public static readonly IReadOnlyList<string> Categories = new[]
    {
        "Software Developer",
        "Frontend Developer",
        "Backend Developer",
        "Web Developer",
        "UI/UX Designer",
        "Data Analyst",
        "Data Scientist",
        "DevOps Engineer",
        "Cloud Engineer",
        "Cybersecurity Analyst",
        "Digital Marketing Specialist",
        "HR Officer",
        "Sales Executive",
        "Customer Support",
        "Accountant"
    };
}
