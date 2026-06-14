namespace SumGravity.Models;

public enum MessageRole { System, User, Assistant }

public class ChatMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public MessageRole Role { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public bool IsStreaming { get; set; } = false;

    // If the message contains diff blocks, they get parsed here
    public List<DiffBlock> DiffBlocks { get; set; } = new();

    public string RoleName => Role switch
    {
        MessageRole.User => "You",
        MessageRole.Assistant => "SumGravity",
        MessageRole.System => "System",
        _ => "Unknown"
    };
}
