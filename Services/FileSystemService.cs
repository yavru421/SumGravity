using SumGravity.Models;

namespace SumGravity.Services;

public class FileSystemService
{
    private readonly IConfiguration _config;
    private static readonly HashSet<string> IgnoredDirs = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", "bin", "obj", "node_modules", ".vs", "__pycache__", ".idea"
    };

    public FileSystemService(IConfiguration config)
    {
        _config = config;
    }

    public string DefaultRoot => _config["FileExplorer:DefaultRoot"] ?? @"c:\dev";

    // ── Tree Building ──────────────────────────────────────────
    public FileNode BuildTree(string rootPath, int maxDepth = 3)
    {
        var root = new FileNode
        {
            Name = Path.GetFileName(rootPath) is { Length: > 0 } n ? n : rootPath,
            FullPath = rootPath,
            IsDirectory = true,
            IsExpanded = true
        };

        if (Directory.Exists(rootPath))
            PopulateChildren(root, rootPath, 0, maxDepth);

        return root;
    }

    private void PopulateChildren(FileNode node, string path, int depth, int maxDepth)
    {
        if (depth >= maxDepth) return;

        try
        {
            // Dirs first
            foreach (var dir in Directory.GetDirectories(path).OrderBy(d => d))
            {
                var dirName = Path.GetFileName(dir);
                if (IgnoredDirs.Contains(dirName)) continue;

                var child = new FileNode
                {
                    Name = dirName,
                    FullPath = dir,
                    IsDirectory = true
                };
                PopulateChildren(child, dir, depth + 1, maxDepth);
                node.Children.Add(child);
            }

            // Files
            foreach (var file in Directory.GetFiles(path).OrderBy(f => f))
            {
                var info = new FileInfo(file);
                node.Children.Add(new FileNode
                {
                    Name = info.Name,
                    FullPath = file,
                    IsDirectory = false,
                    SizeBytes = info.Length
                });
            }
        }
        catch (UnauthorizedAccessException) { /* skip locked dirs */ }
        catch (IOException) { }
    }

    // ── File Read/Write ────────────────────────────────────────
    public async Task<string> ReadFileAsync(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"File not found: {path}");

        return await File.ReadAllTextAsync(path);
    }

    public async Task WriteFileAsync(string path, string content)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        await File.WriteAllTextAsync(path, content);
    }

    public async Task WriteFileAtomicAsync(string path, string content)
    {
        var tempPath = path + ".tmp";
        await File.WriteAllTextAsync(tempPath, content);
        File.Move(tempPath, path, overwrite: true);
    }

    public bool FileExists(string path) => File.Exists(path);
    public bool DirectoryExists(string path) => Directory.Exists(path);

    public string[] GetFilesInDirectory(string dirPath, string pattern = "*.*")
        => Directory.Exists(dirPath)
            ? Directory.GetFiles(dirPath, pattern, SearchOption.TopDirectoryOnly)
            : Array.Empty<string>();

    // ── Backup ────────────────────────────────────────────────
    public string CreateBackup(string path)
    {
        if (!File.Exists(path)) return string.Empty;
        var backupPath = path + $".bak.{DateTime.Now:yyyyMMddHHmmss}";
        File.Copy(path, backupPath, overwrite: false);
        return backupPath;
    }

    public List<string> GetRecentProjects()
    {
        var root = DefaultRoot;
        if (!Directory.Exists(root)) return new();

        return Directory.GetDirectories(root)
            .Where(d => Directory.GetFiles(d, "*.csproj").Length > 0 ||
                        Directory.GetFiles(d, "*.sln").Length > 0)
            .OrderByDescending(d => Directory.GetLastWriteTime(d))
            .Take(10)
            .ToList();
    }
}
