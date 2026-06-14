using SumGravity.Models;

namespace SumGravity.Services;

public class SkillsService
{
    private readonly IConfiguration _config;
    private readonly TerminalService _terminal;
    private readonly ILogger<SkillsService> _logger;

    public SkillsService(
        IConfiguration config,
        TerminalService terminal,
        ILogger<SkillsService> logger)
    {
        _config = config;
        _terminal = terminal;
        _logger = logger;
    }

    public string SkillsRoot => _config["Skills:RootPath"] ?? @"C:\Users\John\.gemini\config\skills";

    // ── Scan skills directory ──────────────────────────────────
    public List<SkillManifest> GetSkills()
    {
        var manifests = new List<SkillManifest>();

        if (!Directory.Exists(SkillsRoot))
        {
            _logger.LogWarning("Skills directory not found: {Path}", SkillsRoot);
            return manifests;
        }

        // Scan for PowerShell scripts
        foreach (var ps1 in Directory.GetFiles(SkillsRoot, "*.ps1", SearchOption.AllDirectories))
        {
            manifests.Add(new SkillManifest
            {
                Name = Path.GetFileNameWithoutExtension(ps1),
                FilePath = ps1,
                FileType = "ps1",
                Description = ExtractDescription(ps1)
            });
        }

        // Scan for skill folders with SKILL.md
        foreach (var dir in Directory.GetDirectories(SkillsRoot))
        {
            var skillMd = Path.Combine(dir, "SKILL.md");
            if (!File.Exists(skillMd)) continue;

            var name = Path.GetFileName(dir);

            // Check if there's also a .ps1 we can run
            var ps1Files = Directory.GetFiles(dir, "*.ps1");
            var filePath = ps1Files.Length > 0 ? ps1Files[0] : skillMd;
            var fileType = ps1Files.Length > 0 ? "ps1" : "md";

            manifests.Add(new SkillManifest
            {
                Name = name,
                FilePath = filePath,
                FileType = fileType,
                Description = ExtractSkillMdDescription(skillMd)
            });
        }

        return manifests.OrderBy(m => m.Name).ToList();
    }

    // ── Run a skill ───────────────────────────────────────────
    public IAsyncEnumerable<string> RunSkillAsync(
        SkillManifest skill,
        CancellationToken ct = default)
    {
        skill.Status = SkillStatus.Running;
        skill.LastRun = DateTime.Now;

        if (skill.FileType == "ps1")
            return _terminal.RunScriptAsync(skill.FilePath, ct: ct);

        // For SKILL.md — just echo description, can't auto-run
        return AsyncLines(new[]
        {
            $"# Skill: {skill.Name}",
            $"This skill uses a SKILL.md instruction file.",
            $"Path: {skill.FilePath}",
            "To run this skill, open it in SumGravity chat and ask the model to execute it."
        });
    }

    // ── Helpers ───────────────────────────────────────────────
    private static string ExtractDescription(string ps1Path)
    {
        try
        {
            var lines = File.ReadLines(ps1Path).Take(10);
            foreach (var line in lines)
            {
                var trimmed = line.TrimStart('#', ' ');
                if (!string.IsNullOrWhiteSpace(trimmed) && trimmed.Length > 5)
                    return trimmed;
            }
        }
        catch { }
        return "PowerShell automation script";
    }

    private static string ExtractSkillMdDescription(string skillMdPath)
    {
        try
        {
            foreach (var line in File.ReadLines(skillMdPath).Take(30))
            {
                if (line.StartsWith("description:", StringComparison.OrdinalIgnoreCase))
                    return line["description:".Length..].Trim().Trim('"');
                if (line.StartsWith("#") && line.Length > 2)
                    return line.TrimStart('#', ' ');
            }
        }
        catch { }
        return "Antigravity skill";
    }

    private static async IAsyncEnumerable<string> AsyncLines(IEnumerable<string> lines)
    {
        foreach (var line in lines)
        {
            await Task.Yield();
            yield return line;
        }
    }
}
