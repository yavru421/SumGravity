using System.Text.RegularExpressions;
using SumGravity.Models;

namespace SumGravity.Services;

/// <summary>
/// Parses and applies Search/Replace diff blocks produced by the LLM.
///
/// Supported formats:
///   <<<<<<< SEARCH
///   :path: relative/path/to/file.cs
///   [search text]
///   =======
///   [replace text]
///   >>>>>>> REPLACE
///
///   <<<<<<< NEW_FILE :path: relative/path/to/newfile.cs
///   [file content]
///   >>>>>>> END_FILE
/// </summary>
public class SearchReplaceService
{
    private readonly FileSystemService _fs;
    private readonly ILogger<SearchReplaceService> _logger;

    // Regex for Search/Replace blocks
    private static readonly Regex SrBlockRegex = new(
        @"<{7}\s*SEARCH\s*\n(?::path:\s*(?<path>[^\n]+)\n)?(?<search>.*?)={7}\s*\n(?<replace>.*?)>{7}\s*REPLACE",
        RegexOptions.Singleline | RegexOptions.Compiled);

    // Regex for New File blocks
    private static readonly Regex NewFileRegex = new(
        @"<{7}\s*NEW_FILE\s+:path:\s*(?<path>[^\n]+)\n(?<content>.*?)>{7}\s*END_FILE",
        RegexOptions.Singleline | RegexOptions.Compiled);

    public SearchReplaceService(FileSystemService fs, ILogger<SearchReplaceService> logger)
    {
        _fs = fs;
        _logger = logger;
    }

    // ── Parse blocks from LLM output ──────────────────────────
    public List<DiffBlock> ParseBlocks(string llmOutput, string? contextFilePath = null)
    {
        var blocks = new List<DiffBlock>();

        // Parse Search/Replace blocks
        foreach (Match m in SrBlockRegex.Matches(llmOutput))
        {
            var path = m.Groups["path"].Success
                ? m.Groups["path"].Value.Trim()
                : contextFilePath ?? string.Empty;

            blocks.Add(new DiffBlock
            {
                TargetFilePath = NormalizePath(path),
                SearchText = m.Groups["search"].Value,
                ReplaceText = m.Groups["replace"].Value,
                Status = DiffBlockStatus.Pending
            });
        }

        // Parse New File blocks
        foreach (Match m in NewFileRegex.Matches(llmOutput))
        {
            var path = m.Groups["path"].Value.Trim();
            blocks.Add(new DiffBlock
            {
                TargetFilePath = NormalizePath(path),
                SearchText = "__NEW_FILE__",          // sentinel
                ReplaceText = m.Groups["content"].Value,
                Status = DiffBlockStatus.Pending
            });
        }

        return blocks;
    }

    // ── Apply a single DiffBlock ───────────────────────────────
    public async Task<DiffResult> ApplyBlockAsync(DiffBlock block)
    {
        var result = new DiffResult { FilePath = block.TargetFilePath };

        try
        {
            // New file creation
            if (block.SearchText == "__NEW_FILE__")
            {
                await _fs.WriteFileAtomicAsync(block.TargetFilePath, block.ReplaceText);
                block.Status = DiffBlockStatus.Applied;
                result.Success = true;
                result.BlocksApplied = 1;
                return result;
            }

            // Verify file exists
            if (!_fs.FileExists(block.TargetFilePath))
            {
                block.Status = DiffBlockStatus.Failed;
                block.ErrorMessage = $"File not found: {block.TargetFilePath}";
                result.Errors.Add(block.ErrorMessage);
                result.BlocksFailed = 1;
                return result;
            }

            // Read current content
            var current = await _fs.ReadFileAsync(block.TargetFilePath);

            // Verify search text exists verbatim
            if (!current.Contains(block.SearchText))
            {
                // Try normalizing line endings
                var normalizedSearch = block.SearchText
                    .Replace("\r\n", "\n")
                    .Replace("\r", "\n");
                var normalizedCurrent = current
                    .Replace("\r\n", "\n")
                    .Replace("\r", "\n");

                if (!normalizedCurrent.Contains(normalizedSearch))
                {
                    block.Status = DiffBlockStatus.Failed;
                    block.ErrorMessage = "SEARCH text not found verbatim in target file.";
                    result.Errors.Add(block.ErrorMessage);
                    result.BlocksFailed = 1;
                    return result;
                }

                // Use normalized versions for replacement
                current = normalizedCurrent;
                block.SearchText = normalizedSearch;
            }

            // Create backup before first modification
            result.Backup = _fs.CreateBackup(block.TargetFilePath);

            // Apply replacement (first occurrence only for safety)
            var idx = current.IndexOf(block.SearchText, StringComparison.Ordinal);
            var newContent = string.Concat(
                current.AsSpan(0, idx),
                block.ReplaceText,
                current.AsSpan(idx + block.SearchText.Length));

            await _fs.WriteFileAtomicAsync(block.TargetFilePath, newContent);

            block.Status = DiffBlockStatus.Applied;
            result.Success = true;
            result.BlocksApplied = 1;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply diff block to {Path}", block.TargetFilePath);
            block.Status = DiffBlockStatus.Failed;
            block.ErrorMessage = ex.Message;
            result.Errors.Add(ex.Message);
            result.BlocksFailed = 1;
        }

        return result;
    }

    // ── Apply all blocks from a message ───────────────────────
    public async Task<List<DiffResult>> ApplyAllBlocksAsync(List<DiffBlock> blocks)
    {
        var results = new List<DiffResult>();
        foreach (var block in blocks)
        {
            var result = await ApplyBlockAsync(block);
            results.Add(result);
        }
        return results;
    }

    // ── Helpers ───────────────────────────────────────────────
    private static string NormalizePath(string path)
    {
        // Convert relative paths to absolute using CWD or c:\dev
        if (Path.IsPathRooted(path)) return path;
        return Path.GetFullPath(path, @"c:\dev");
    }

    // Check if model output contains diff blocks
    public static bool ContainsDiffBlocks(string text)
        => text.Contains("<<<<<<<") && text.Contains(">>>>>>>");
}
