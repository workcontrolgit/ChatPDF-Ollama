using Microsoft.Extensions.AI;

namespace ChatPDF.Web.Models;

public class ChatSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; } = false;
    public List<ChatMessage> Messages { get; set; } = new();
    
    // Generate title from first user message
    public void GenerateTitle()
    {
        var firstUserMessage = Messages.FirstOrDefault(m => m.Role == ChatRole.User);
        if (firstUserMessage != null)
        {
            var text = firstUserMessage.Text ?? "";
            Title = text.Length > 50 ? text.Substring(0, 47) + "..." : text;
        }
        
        if (string.IsNullOrEmpty(Title))
        {
            Title = $"Chat {CreatedAt:MMM dd, HH:mm}";
        }
    }
}