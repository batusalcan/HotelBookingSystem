using System.ComponentModel.DataAnnotations;

namespace AiAgentService.Models;

public class ChatRequest
{
    public string? SessionId { get; set; }

    [Required]
    public string UserMessage { get; set; } = string.Empty;

    /// <summary>
    /// Full conversation history sent by the client on every request.
    /// The frontend appends each user/assistant turn and echoes the full array back.
    /// Used to build a multi-turn prompt so Gemini has context of prior dialogue.
    /// </summary>
    public List<ChatMessage> Messages { get; set; } = [];

    public ContextState? ContextState { get; set; }
}

public record ChatMessage(string Role, string Text);
