using System.Security.Claims;
using AiAgentService.Models;
using AiAgentService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SharedKernel.Models;

namespace AiAgentService.Controllers.v1;

[ApiController]
[Route("api/v1/ai")]
[Authorize]
public class AiController(IAiChatService chatService) : ControllerBase
{
    /// <summary>
    /// Stateless AI chat endpoint. All conversation state is owned by the client via contextState.
    /// Implements the mandatory 2-step confirmation flow: search → confirm → book.
    /// </summary>
    [HttpPost("chat")]
    public async Task<IActionResult> Chat([FromBody] ChatRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.UserMessage))
            return BadRequest(ApiResponse<object>.Fail("UserMessage is required."));

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub")
            ?? string.Empty;

        // Forward the raw JWT so the facade can authenticate against HotelService for booking
        var authToken = HttpContext.Request.Headers.Authorization
            .FirstOrDefault()
            ?.Replace("Bearer ", string.Empty, StringComparison.OrdinalIgnoreCase);

        var response = await chatService.ProcessAsync(request, userId, authToken);
        return Ok(ApiResponse<ChatResponse>.Ok(response));
    }
}
