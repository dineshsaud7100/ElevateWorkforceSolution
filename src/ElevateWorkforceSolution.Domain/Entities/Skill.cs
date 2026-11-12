namespace ElevateWorkforceSolution.Domain.Entities;

public class Skill
{
    public int Id { get; set; }
    public int JobSeekerId { get; set; }
    public JobSeeker? JobSeeker { get; set; }

    public string Name { get; set; } = string.Empty;
    public SkillProficiency Proficiency { get; set; } = SkillProficiency.Intermediate;
    public int? YearsOfExperience { get; set; }
}

public enum SkillProficiency
{
    Beginner = 1,
    Intermediate = 2,
    Advanced = 3,
    Expert = 4
}
