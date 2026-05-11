namespace AiAgentService.Models;

public class ChatResponse
{
    public string Reply { get; set; } = string.Empty;
    public bool RequiresConfirmation { get; set; }
    public ContextState? ContextState { get; set; }
}
