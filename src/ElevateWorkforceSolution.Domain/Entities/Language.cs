namespace ElevateWorkforceSolution.Domain.Entities;

public class Language
{
    public int Id { get; set; }
    public int JobSeekerId { get; set; }
    public JobSeeker? JobSeeker { get; set; }

    public string Name { get; set; } = string.Empty;
    public LanguageProficiency Proficiency { get; set; } = LanguageProficiency.Conversational;

    /// <summary>Separated list of ISO codes, e.g. "ne,en,hi".</summary>
    public string? IsoCodes { get; set; }
}

public enum LanguageProficiency
{
    Basic = 1,
    Conversational = 2,
    Fluent = 3,
    Native = 4
}
