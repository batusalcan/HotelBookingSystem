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

    // "book" removed — too ambiguous ("I want to book Hilton" ≠ final confirmation)
    private static readonly string[] ConfirmKeywords =
        ["yes", "book it", "confirm", "reserve", "sure", "ok", "yep", "yeah",
         "evet", "onayla", "rezerve et", "rezervasyon yap"];

    /// <precondition>request.UserMessage is non-empty; userId is non-empty</precondition>
    /// <postcondition>
    /// Three-step flow:
    ///   1. SEARCH — Gemini extracts params → hotels presented, PendingAction="SELECT"
    ///   2. SELECT — user picks a hotel by name/number → room detail fetched, PendingAction="BOOK"
    ///   3. BOOK   — user confirms → booking executed
    /// </postcondition>
    public async Task<ChatResponse> ProcessAsync(ChatRequest request, string userId, string? authToken)
    {
        // Step BOOK: user is confirming a pre-selected hotel
        if (request.ContextState?.PendingAction == "BOOK" && IsConfirmation(request.UserMessage))
            return await ExecuteBookingAsync(request.ContextState, authToken);

        // Step SELECT: user is choosing which hotel from the presented list
        if (request.ContextState?.PendingAction == "SELECT")
            return await HandleHotelSelectionAsync(request, authToken);

        // Step SEARCH: parse intent via Gemini
        var prompt = BuildSearchPrompt(request.UserMessage, request.ContextState, request.Messages);
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
            return Clarify("Could you give me a bit more detail? I need the destination, dates, and number of guests.", request.ContextState);

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

        return Clarify(intent.Reply ?? "Could you tell me the destination, check-in/out dates, and number of guests?",
            BuildPartialContext(intent));
    }

    // ── Hotel selection step ──────────────────────────────────────────────────

    private async Task<ChatResponse> HandleHotelSelectionAsync(ChatRequest request, string? authToken)
    {
        var hotels = DeserializeHotels(request.ContextState!.HotelOptionsJson);
        if (hotels.Count == 0)
            return Clarify("I lost track of the hotel options. Could you start your search again?", null);

        var selectionPrompt = BuildSelectionPrompt(request.UserMessage, hotels);
        string aiRaw;
        try
        {
            aiRaw = await aiProvider.GenerateAsync(selectionPrompt);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "AI provider call failed during hotel selection");
            return Clarify("I'm having trouble right now. Which hotel would you like — say the name or number?", request.ContextState);
        }

        var selection = ParseSelection(aiRaw);
        if (selection is null || selection.SelectedIndex < 0 || selection.SelectedIndex >= hotels.Count)
        {
            var relist = "Please tell me which hotel you'd like — say the hotel name or its number:\n\n" +
                string.Join("\n", hotels.Select((h, i) => $"{i + 1}. {h.Name} — {h.PricePerNight:N0} TL/night ({h.RoomTypeName})"));
            return Clarify(relist, request.ContextState);
        }

        var chosen = hotels[selection.SelectedIndex];

        RoomDetailDto roomDetail;
        try
        {
            roomDetail = await facade.GetRoomDetailAsync(
                chosen.HotelId, chosen.RoomTypeId,
                DateOnly.Parse(request.ContextState.StartDate!),
                DateOnly.Parse(request.ContextState.EndDate!));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "GetRoomDetail failed for hotel {HotelId}", chosen.HotelId);
            return Clarify("I couldn't confirm availability for that hotel. Please try another or search again.", request.ContextState);
        }

        var nights = DateOnly.Parse(request.ContextState.EndDate!).DayNumber
                   - DateOnly.Parse(request.ContextState.StartDate!).DayNumber;
        var total = chosen.PricePerNight * nights;

        return new ChatResponse
        {
            Reply = $"Great choice! Here is your booking summary:\n\n" +
                    $"🏨 **{chosen.Name}**\n" +
                    $"🛏 Room: {chosen.RoomTypeName} | {chosen.PricePerNight:N0} TL/night\n" +
                    $"📅 {request.ContextState.StartDate} → {request.ContextState.EndDate} ({nights} nights)\n" +
                    $"👥 Guests: {request.ContextState.GuestCount}\n" +
                    $"💰 Total: {total:N0} TL\n\n" +
                    "Say **\"Yes, book it\"** to confirm your reservation.",
            RequiresConfirmation = true,
            ContextState = new ContextState
            {
                PendingAction = "BOOK",
                Destination = request.ContextState.Destination,
                StartDate = request.ContextState.StartDate,
                EndDate = request.ContextState.EndDate,
                GuestCount = request.ContextState.GuestCount,
                TargetHotelId = chosen.HotelId,
                TargetRoomTypeId = chosen.RoomTypeId,
                TargetInventoryId = roomDetail.InventoryId,
                RowVersion = roomDetail.RowVersion,
                HotelName = chosen.Name
            }
        };
    }

    // ── Search and present ────────────────────────────────────────────────────

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

        var reply = FormatHotelOptions(hotels, intent.Destination!, startDate, endDate);

        return new ChatResponse
        {
            Reply = reply,
            RequiresConfirmation = true,
            ContextState = new ContextState
            {
                PendingAction = "SELECT",
                Destination = intent.Destination,
                StartDate = startDate.ToString("yyyy-MM-dd"),
                EndDate = endDate.ToString("yyyy-MM-dd"),
                GuestCount = intent.GuestCount,
                HotelOptionsJson = JsonSerializer.Serialize(hotels, JsonOpts)
            }
        };
    }

    // ── Execute booking ───────────────────────────────────────────────────────

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
                Reply = $"✅ Your reservation at **{context.HotelName}** from {context.StartDate} to {context.EndDate} " +
                        $"is confirmed!\n\nBooking ID: `{result.BookingId}`",
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

    // ── Prompt builders ───────────────────────────────────────────────────────

    private static string BuildSearchPrompt(string userMessage, ContextState? context, List<ChatMessage>? history)
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
        sb.AppendLine("- Use the conversation history below to understand references to previous messages.");

        if (context?.PendingAction == "CLARIFY")
        {
            sb.AppendLine();
            sb.AppendLine("Partial context from previous turn:");
            if (context.Destination is not null) sb.AppendLine($"- destination already known: {context.Destination}");
            if (context.StartDate is not null) sb.AppendLine($"- startDate already known: {context.StartDate}");
            if (context.EndDate is not null) sb.AppendLine($"- endDate already known: {context.EndDate}");
            if (context.GuestCount is not null) sb.AppendLine($"- guestCount already known: {context.GuestCount}");
        }

        if (history is { Count: > 0 })
        {
            sb.AppendLine();
            sb.AppendLine("Conversation history (oldest first):");
            foreach (var msg in history)
            {
                var label = msg.Role == "user" ? "User" : "Assistant";
                sb.AppendLine($"{label}: {msg.Text}");
            }
        }

        sb.AppendLine();
        sb.AppendLine($"User message: \"{userMessage}\"");

        return sb.ToString();
    }

    private static string BuildSelectionPrompt(string userMessage, List<HotelOptionDto> hotels)
    {
        var sb = new StringBuilder();
        sb.AppendLine("A user was shown a list of hotels and is now choosing one.");
        sb.AppendLine("Return ONLY valid JSON (no markdown): {\"selectedIndex\": N, \"reply\": \"...\"}");
        sb.AppendLine("selectedIndex is 0-based. Return -1 if the user is not making a clear hotel selection.");
        sb.AppendLine();
        sb.AppendLine("Available hotels:");
        for (int i = 0; i < hotels.Count; i++)
            sb.AppendLine($"{i + 1}. {hotels[i].Name} ({hotels[i].Location}) — {hotels[i].PricePerNight:N0} TL/night ({hotels[i].RoomTypeName})");
        sb.AppendLine();
        sb.AppendLine($"User said: \"{userMessage}\"");
        return sb.ToString();
    }

    // ── Format helpers ────────────────────────────────────────────────────────

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
            sb.AppendLine($"   Room: {h.RoomTypeName} | Available rooms: {h.AvailableRooms}");
            sb.AppendLine();
        }

        sb.AppendLine("Which hotel would you like? Say the hotel name or its number (1, 2, etc.).");
        return sb.ToString().TrimEnd();
    }

    // ── Parsers ───────────────────────────────────────────────────────────────

    private static GeminiIntent? ParseIntent(string raw)
    {
        try
        {
            var json = raw.Trim();
            if (json.StartsWith("```"))
            {
                var start = json.IndexOf('\n') + 1;
                var end = json.LastIndexOf("```");
                if (end > start) json = json[start..end].Trim();
            }
            return JsonSerializer.Deserialize<GeminiIntent>(json, JsonOpts);
        }
        catch { return null; }
    }

    private static HotelSelection? ParseSelection(string raw)
    {
        try
        {
            var json = raw.Trim();
            if (json.StartsWith("```"))
            {
                var start = json.IndexOf('\n') + 1;
                var end = json.LastIndexOf("```");
                if (end > start) json = json[start..end].Trim();
            }
            return JsonSerializer.Deserialize<HotelSelection>(json, JsonOpts);
        }
        catch { return null; }
    }

    private static bool IsConfirmation(string message)
    {
        var lower = message.Trim().ToLowerInvariant();
        return ConfirmKeywords.Any(k => lower.Contains(k));
    }

    private static List<HotelOptionDto> DeserializeHotels(string? json)
    {
        if (string.IsNullOrEmpty(json)) return [];
        try { return JsonSerializer.Deserialize<List<HotelOptionDto>>(json, JsonOpts) ?? []; }
        catch { return []; }
    }

    private static ChatResponse Clarify(string reply, ContextState? carry) => new()
    {
        Reply = reply,
        RequiresConfirmation = false,
        ContextState = carry is null ? null : new ContextState
        {
            PendingAction = carry.PendingAction == "SELECT" ? "SELECT" : "CLARIFY",
            Destination = carry.Destination,
            StartDate = carry.StartDate,
            EndDate = carry.EndDate,
            GuestCount = carry.GuestCount,
            HotelOptionsJson = carry.HotelOptionsJson
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

    // ── Gemini response shapes ────────────────────────────────────────────────
    private record GeminiIntent(
        [property: JsonPropertyName("intent")] string Intent,
        [property: JsonPropertyName("destination")] string? Destination,
        [property: JsonPropertyName("startDate")] string? StartDate,
        [property: JsonPropertyName("endDate")] string? EndDate,
        [property: JsonPropertyName("guestCount")] int? GuestCount,
        [property: JsonPropertyName("reply")] string? Reply);

    private record HotelSelection(
        [property: JsonPropertyName("selectedIndex")] int SelectedIndex,
        [property: JsonPropertyName("reply")] string? Reply);
}
