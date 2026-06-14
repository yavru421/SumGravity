namespace SumGravity.Models;

public class SkillManifest
{
    public string Name { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty; // "ps1" or "md"
    public string Description { get; set; } = string.Empty;
    public SkillStatus Status { get; set; } = SkillStatus.Idle;
    public DateTime? LastRun { get; set; }
}

public enum SkillStatus
{
    Idle,
    Running,
    Done,
    Error
}
