using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SumGravity.Models;

namespace SumGravity.Services;

public class CliRunner
{
    private readonly KoboldCppClient _client;
    private readonly SearchReplaceService _srService;
    private readonly FileSystemService _fs;
    private readonly IConfiguration _config;
    private readonly ILogger<CliRunner> _logger;

    public CliRunner(
        KoboldCppClient client,
        SearchReplaceService srService,
        FileSystemService fs,
        IConfiguration config,
        ILogger<CliRunner> logger)
    {
        _client = client;
        _srService = srService;
        _fs = fs;
        _config = config;
        _logger = logger;
    }

    public async Task RunAsync(string[] args)
    {
        try { Console.Clear(); Console.Title = "SumGravity - RTX Homeslice CLI Mode"; } catch { }

        // Set client vibe to Homeslice!
        _client.UseHomesliceVibe = true;

        var memory = HomesliceMemory.Load();
        
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("=========================================================");
        Console.WriteLine($"   SumGravity v1.0 -- RTX Homeslice Edition 🚀");
        Console.WriteLine($"   Vibe: {memory.VibeDescription}");
        Console.WriteLine("=========================================================");
        Console.ResetColor();
        Console.WriteLine($"Yo {memory.Nickname}! Type your message and hit Enter. Type '/bye' to bounce.");
        Console.WriteLine("Type '/facts' to see what I remember about you.");
        Console.WriteLine();

        var history = new List<ChatMessage>();
        var cts = new CancellationTokenSource();

        Console.CancelKeyPress += (s, e) =>
        {
            e.Cancel = true; // don't kill the CLI process instantly
            cts.Cancel();
            cts = new CancellationTokenSource();
            Console.WriteLine("\n[!] Cancelled generation.");
        };

        // Check KoboldCPP connectivity
        Console.Write("Checking RTX GPU connection...");
        if (await _client.IsReachableAsync(cts.Token))
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(" Connected to KoboldCPP! Let's get it. ⚡");
            Console.ResetColor();
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(" Offline.");
            Console.WriteLine($"[!] Couldn't hit KoboldCPP at '{_config["KoboldCpp:BaseUrl"] ?? "http://localhost:5001/v1"}'.");
            Console.WriteLine("Start KoboldCPP and run me again!");
            Console.ResetColor();
            return;
        }

        while (true)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write($"\n{memory.Nickname}> ");
            Console.ResetColor();

            var input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input)) continue;

            var trimmedInput = input.Trim();

            // Commands
            if (trimmedInput.Equals("/bye", StringComparison.OrdinalIgnoreCase) ||
                trimmedInput.Equals("exit", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("\nCatch you on the flip side! ✌️");
                break;
            }

            if (trimmedInput.Equals("/facts", StringComparison.OrdinalIgnoreCase))
            {
                Console.ForegroundColor = ConsoleColor.DarkCyan;
                Console.WriteLine("\n--- What I know about you ---");
                foreach (var fact in memory.Facts)
                {
                    Console.WriteLine($"- {fact}");
                }
                Console.WriteLine("-----------------------------");
                Console.ResetColor();
                continue;
            }

            // Injects fact context implicitly to guide the prompt slightly
            var factContext = string.Join(" ", memory.Facts);
            var contextGuidedMessage = $"[Context: {factContext}] {trimmedInput}";

            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.Write("\nHomeslice> ");
            Console.ResetColor();

            var responseBuffer = new System.Text.StringBuilder();

            try
            {
                await foreach (var token in _client.StreamAsync(history, contextGuidedMessage, cts.Token))
                {
                    Console.Write(token);
                    responseBuffer.Append(token);
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\n[!] Error during stream: {ex.Message}");
                Console.ResetColor();
                continue;
            }

            Console.WriteLine(); // final newline

            var responseText = responseBuffer.ToString();

            // Save to history
            history.Add(new ChatMessage { Role = MessageRole.User, Content = trimmedInput });
            history.Add(new ChatMessage { Role = MessageRole.Assistant, Content = responseText });

            // Truncate history to avoid exceeding local model context limits (max 20)
            if (history.Count > 20)
            {
                history.RemoveRange(0, history.Count - 20);
            }

            // Look for diff blocks or file patches
            if (SearchReplaceService.ContainsDiffBlocks(responseText))
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("\n[i] Detected file modification blocks in my response.");
                Console.ResetColor();

                var blocks = _srService.ParseBlocks(responseText);
                foreach (var block in blocks)
                {
                    var isNew = block.SearchText == "__NEW_FILE__";
                    var action = isNew ? "Create new file" : "Modify existing file";
                    
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.Write($"\n[?] {action} '{block.TargetFilePath}'? (y/n): ");
                    Console.ResetColor();

                    var key = Console.ReadLine()?.Trim().ToLower();
                    if (key == "y" || key == "yes")
                    {
                        Console.Write("Applying patch...");
                        var result = await _srService.ApplyBlockAsync(block);
                        if (result.Success)
                        {
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine(" Applied successfully! Write complete. ✓");
                        }
                        else
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($" Failed: {string.Join(", ", result.Errors)}");
                        }
                        Console.ResetColor();
                    }
                    else
                    {
                        Console.WriteLine("Patch skipped.");
                    }
                }
            }
        }
    }
}
