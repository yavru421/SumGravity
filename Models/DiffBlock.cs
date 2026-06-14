namespace SumGravity.Models;

public class DiffBlock
{
    public string TargetFilePath { get; set; } = string.Empty;
    public string SearchText { get; set; } = string.Empty;
    public string ReplaceText { get; set; } = string.Empty;
    public DiffBlockStatus Status { get; set; } = DiffBlockStatus.Pending;
    public string? ErrorMessage { get; set; }
}

public enum DiffBlockStatus
{
    Pending,
    Applied,
    Failed,
    Skipped
}

public class DiffResult
{
    public string FilePath { get; set; } = string.Empty;
    public bool Success { get; set; }
    public int BlocksApplied { get; set; }
    public int BlocksFailed { get; set; }
    public List<string> Errors { get; set; } = new();
    public string? Backup { get; set; } // path to .bak file created before apply
}
