namespace SumGravity.Models;

public class FileNode
{
    public string Name { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public bool IsDirectory { get; set; }
    public bool IsExpanded { get; set; } = false;
    public bool IsSelected { get; set; } = false;
    public List<FileNode> Children { get; set; } = new();
    public long? SizeBytes { get; set; }
    public string Extension => Path.GetExtension(Name).TrimStart('.').ToLowerInvariant();

    public string Icon => IsDirectory ? "📁" : Extension switch
    {
        "cs" => "⚙️",
        "razor" => "🔷",
        "json" => "📋",
        "md" => "📝",
        "ps1" => "💻",
        "css" => "🎨",
        "js" => "🟨",
        "html" => "🌐",
        "txt" => "📄",
        "csproj" => "🏗️",
        "sln" => "🏛️",
        _ => "📄"
    };
}
