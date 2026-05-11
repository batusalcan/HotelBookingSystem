using System.ComponentModel.DataAnnotations;

namespace AiAgentService.Models;

public class ChatRequest
{
    public string? SessionId { get; set; }

    [Required]
    public string UserMessage { get; set; } = string.Empty;

    public ContextState? ContextState { get; set; }
}
