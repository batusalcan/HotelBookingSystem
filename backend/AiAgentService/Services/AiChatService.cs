using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AiAgentService.Facade;
using AiAgentService.Models;
using AiAgentService.Providers;

namespace AiAgentService.Services;

public class AiChatService(
    IAiProvider aiProvider,
    IHotelSystemFacade facade,
    ILogger<AiChatService> logger) : IAiChatService
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    private static readonly string[] ConfirmKeywords =
        ["yes", "book it", "book", "confirm", "reserve", "sure", "ok", "yep", "yeah",
         "evet", "onayla", "rezerve et", "rezervasyon yap"];

    /// <precondition>request.UserMessage is non-empty; userId is non-empty</precondition>
    /// <postcondition>
    /// Returns ChatResponse with reply and updated contextState.
    /// If contextState.PendingAction == "BOOK" and user confirms → booking executed.
    /// If all search params present → hotels searched and presented (requiresConfirmation = true).
    /// Otherwise → clarifying question returned (requiresConfirmation = false).
    /// </postcondition>
    public async Task<ChatResponse> ProcessAsync(ChatRequest request, string userId, string? authToken)
    {
        // Step 1: Booking confirmation turn — no Gemini call needed
        if (request.ContextState?.PendingAction == "BOOK" && IsConfirmation(request.UserMessage))
        {
            return await ExecuteBookingAsync(request.ContextState, authToken);
        }

        // Step 2: Parse intent via Gemini
        var prompt = BuildPrompt(request.UserMessage, request.ContextState);
        string aiRaw;
        try
        {
            aiRaw = await aiProvider.GenerateAsync(prompt);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "AI provider call failed");
            return Clarify("I'm having trouble understanding that right now. Could you rephrase your request?", request.ContextState);
        }

        var intent = ParseIntent(aiRaw);
        if (intent is null)
        {
            return Clarify("Could you give me a bit more detail? I need the destination, dates, and number of guests.", request.ContextState);
        }

        // Step 3: All params available → search hotels
        if (intent.Intent == "SEARCH"
            && intent.Destination is not null
            && intent.StartDate is not null
            && intent.EndDate is not null
            && intent.GuestCount is not null
            && DateOnly.TryParse(intent.StartDate, out var startDate)
            && DateOnly.TryParse(intent.EndDate, out var endDate))
        {
            return await SearchAndPresentAsync(intent, startDate, endDate, authToken);
        }

        // Step 4: Missing params → clarify
        return Clarify(intent.Reply ?? "Could you tell me the destination, check-in/out dates, and number of guests?",
            BuildPartialContext(intent));
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task<ChatResponse> SearchAndPresentAsync(
        GeminiIntent intent, DateOnly startDate, DateOnly endDate, string? authToken)
    {
        List<HotelOptionDto> hotels;
        try
        {
            hotels = await facade.SearchHotelsAsync(
                intent.Destination!, startDate, endDate, intent.GuestCount!.Value, authToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Facade.SearchHotels failed");
            return Clarify("I couldn't reach the hotel service right now. Please try again in a moment.", null);
        }

        if (hotels.Count == 0)
        {
            return new ChatResponse
            {
                Reply = $"I couldn't find any available hotels in {intent.Destination} for those dates. " +
                        "Would you like to try different dates or a nearby destination?",
                RequiresConfirmation = false,
                ContextState = null
            };
        }

        // Get room detail for the top hotel to capture inventoryId + rowVersion
        var top = hotels[0];
        RoomDetailDto roomDetail;
        try
        {
            roomDetail = await facade.GetRoomDetailAsync(top.HotelId, top.RoomTypeId, startDate, endDate);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Facade.GetRoomDetail failed for hotel {HotelId}", top.HotelId);
            return Clarify("I found some hotels but couldn't retrieve availability details. Please try again.", null);
        }

        var reply = FormatHotelOptions(hotels, intent.Destination!, startDate, endDate);

        return new ChatResponse
        {
            Reply = reply,
            RequiresConfirmation = true,
            ContextState = new ContextState
            {
                PendingAction = "BOOK",
                Destination = intent.Destination,
                StartDate = startDate.ToString("yyyy-MM-dd"),
                EndDate = endDate.ToString("yyyy-MM-dd"),
                GuestCount = intent.GuestCount,
                TargetHotelId = top.HotelId,
                TargetRoomTypeId = top.RoomTypeId,
                TargetInventoryId = roomDetail.InventoryId,
                RowVersion = roomDetail.RowVersion,
                HotelName = top.Name
            }
        };
    }

    private async Task<ChatResponse> ExecuteBookingAsync(ContextState context, string? authToken)
    {
        if (string.IsNullOrEmpty(authToken))
        {
            return new ChatResponse
            {
                Reply = "You need to be logged in to complete a booking. Please sign in and try again.",
                RequiresConfirmation = false,
                ContextState = null
            };
        }

        try
        {
            var result = await facade.BookRoomAsync(context, authToken);
            logger.LogInformation("AI booking confirmed BookingId={BookingId}", result.BookingId);
            return new ChatResponse
            {
                Reply = $"Your reservation at {context.HotelName} from {context.StartDate} to {context.EndDate} " +
                        $"is confirmed! Booking ID: {result.BookingId}.",
                RequiresConfirmation = false,
                ContextState = null
            };
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            return new ChatResponse
            {
                Reply = "The room was just taken by another guest. Let me search again for available options.",
                RequiresConfirmation = false,
                ContextState = null
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Booking via facade failed");
            return new ChatResponse
            {
                Reply = "Something went wrong while booking. Please try again.",
                RequiresConfirmation = false,
                ContextState = null
            };
        }
    }

    private static string BuildPrompt(string userMessage, ContextState? context)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");
        var sb = new StringBuilder();

        sb.AppendLine("You are a hotel booking assistant for a Hotels.com-like service in Turkey.");
        sb.AppendLine("Extract the user's hotel search intent and return ONLY valid JSON (no markdown, no code blocks).");
        sb.AppendLine();
        sb.AppendLine("JSON schema:");
        sb.AppendLine("{");
        sb.AppendLine("  \"intent\": \"SEARCH\" | \"CLARIFY\",");
        sb.AppendLine("  \"destination\": \"city name as string, or null\",");
        sb.AppendLine("  \"startDate\": \"YYYY-MM-DD or null\",");
        sb.AppendLine("  \"endDate\": \"YYYY-MM-DD or null\",");
        sb.AppendLine("  \"guestCount\": \"integer or null\",");
        sb.AppendLine("  \"reply\": \"your friendly response to the user\"");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("Rules:");
        sb.AppendLine("- Use intent SEARCH only when destination, startDate, endDate, AND guestCount are ALL present or determinable.");
        sb.AppendLine("- Use intent CLARIFY when any required parameter is missing; ask for only the missing ones in reply.");
        sb.AppendLine($"- Today's date is {today}. Calculate exact dates for relative expressions like 'next weekend', 'this Friday'.");
        sb.AppendLine("- Reply in the same language as the user (Turkish or English).");

        if (context?.PendingAction == "CLARIFY")
        {
            sb.AppendLine();
            sb.AppendLine("Partial context from previous turn:");
            if (context.Destination is not null) sb.AppendLine($"- destination already known: {context.Destination}");
            if (context.StartDate is not null) sb.AppendLine($"- startDate already known: {context.StartDate}");
            if (context.EndDate is not null) sb.AppendLine($"- endDate already known: {context.EndDate}");
            if (context.GuestCount is not null) sb.AppendLine($"- guestCount already known: {context.GuestCount}");
        }

        sb.AppendLine();
        sb.AppendLine($"User message: \"{userMessage}\"");

        return sb.ToString();
    }

    private static string FormatHotelOptions(
        List<HotelOptionDto> hotels, string destination, DateOnly startDate, DateOnly endDate)
    {
        var nights = endDate.DayNumber - startDate.DayNumber;
        var sb = new StringBuilder();
        sb.AppendLine($"Here are the top {Math.Min(hotels.Count, 3)} hotels in {destination} ({startDate:d MMM} – {endDate:d MMM}, {nights} night{(nights > 1 ? "s" : "")}):");
        sb.AppendLine();

        foreach (var h in hotels.Take(3))
        {
            sb.AppendLine($"🏨 {h.Name} — {h.Location}");
            sb.AppendLine($"   ⭐ {h.Rating}/10 ({h.TotalReviews} reviews) | 💰 {h.PricePerNight:N0} TL/night");
            sb.AppendLine($"   Room type: {h.RoomTypeName} | Available rooms: {h.AvailableRooms}");
            sb.AppendLine();
        }

        sb.AppendLine($"Would you like to confirm a reservation at {hotels[0].Name}? Just say \"Yes, book it\".");
        return sb.ToString().TrimEnd();
    }

    private static GeminiIntent? ParseIntent(string raw)
    {
        try
        {
            // Strip markdown code fences if present
            var json = raw.Trim();
            if (json.StartsWith("```"))
            {
                var start = json.IndexOf('\n') + 1;
                var end = json.LastIndexOf("```");
                if (end > start) json = json[start..end].Trim();
            }

            return JsonSerializer.Deserialize<GeminiIntent>(json, JsonOpts);
        }
        catch
        {
            return null;
        }
    }

    private static bool IsConfirmation(string message)
    {
        var lower = message.Trim().ToLowerInvariant();
        return ConfirmKeywords.Any(k => lower.Contains(k));
    }

    private static ChatResponse Clarify(string reply, ContextState? carry) => new()
    {
        Reply = reply,
        RequiresConfirmation = false,
        ContextState = carry is null ? null : new ContextState
        {
            PendingAction = "CLARIFY",
            Destination = carry.Destination,
            StartDate = carry.StartDate,
            EndDate = carry.EndDate,
            GuestCount = carry.GuestCount
        }
    };

    private static ContextState BuildPartialContext(GeminiIntent intent) => new()
    {
        PendingAction = "CLARIFY",
        Destination = intent.Destination,
        StartDate = intent.StartDate,
        EndDate = intent.EndDate,
        GuestCount = intent.GuestCount
    };

    // ── Gemini JSON response shape ────────────────────────────────────────────
    private record GeminiIntent(
        [property: JsonPropertyName("intent")] string Intent,
        [property: JsonPropertyName("destination")] string? Destination,
        [property: JsonPropertyName("startDate")] string? StartDate,
        [property: JsonPropertyName("endDate")] string? EndDate,
        [property: JsonPropertyName("guestCount")] int? GuestCount,
        [property: JsonPropertyName("reply")] string? Reply);
}
