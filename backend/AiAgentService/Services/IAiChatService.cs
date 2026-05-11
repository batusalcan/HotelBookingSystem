using AiAgentService.Models;

namespace AiAgentService.Services;

public interface IAiChatService
{
    /// <precondition>request.UserMessage is non-empty; userId is non-empty</precondition>
    /// <postcondition>
    /// Returns a ChatResponse with:
    /// - reply: natural language response to the user
    /// - requiresConfirmation: true when hotel options are presented and user must confirm to book
    /// - contextState: client-owned state echoed back on the next turn (null when conversation ends)
    /// </postcondition>
    Task<ChatResponse> ProcessAsync(ChatRequest request, string userId, string? authToken);
}
