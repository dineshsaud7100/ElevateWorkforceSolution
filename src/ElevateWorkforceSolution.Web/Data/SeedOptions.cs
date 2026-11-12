namespace ElevateWorkforceSolution.Web.Data;

/// <summary>Controls first-run database setup and development seeding.</summary>
public class SeedOptions
{
    public const string SectionName = "Seed";

    public bool Enabled { get; set; } = true;
    public string AdministratorEmail { get; set; } = "admin@elevateworkforce.com";
    public string AdministratorPassword { get; set; } = "Admin@12345";
}
