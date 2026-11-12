namespace ElevateWorkforceSolution.Domain.Entities;

/// <summary>A skill explicitly requested by a vacancy. Used by the transparent rule-based matcher.</summary>
public class JobRequiredSkill
{
    public int Id { get; set; }
    public int JobId { get; set; }
    public Job? Job { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsMandatory { get; set; } = true;
}
