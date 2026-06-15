using System.Text.Json;

namespace SumGravity.Models;

public class HomesliceMemory
{
    public string Nickname { get; set; } = "John";
    public string VibeDescription { get; set; } = "chill, raw developer peer on the RTX GPU";
    public List<string> Facts { get; set; } = new() 
    {
        "Working on C# / Blazor template project named SumGravity.",
        "Prefixes commands or requests casually."
    };

    private static readonly string FilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "homeslice_memory.json");

    public static HomesliceMemory Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                return JsonSerializer.Deserialize<HomesliceMemory>(json) ?? new HomesliceMemory();
            }
        }
        catch
        {
            // Ignore load errors and fall back to default
        }
        return new HomesliceMemory();
    }

    public void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(FilePath, json);
        }
        catch
        {
            // Ignore save errors
        }
    }
}
