namespace AiAgentService.Providers;

public interface IAiProvider
{
    /// <precondition>prompt is non-empty</precondition>
    /// <postcondition>Returns the LLM's text response for the given prompt</postcondition>
    Task<string> GenerateAsync(string prompt);
}
