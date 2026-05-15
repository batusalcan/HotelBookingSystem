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
         "cancel it", "evet", "onayla", "rezerve et", "rezervasyon yap", "iptal et"];

    /// <precondition>request.UserMessage non-empty; userId non-empty</precondition>
    /// <postcondition>
    /// Four-step flow:
    ///   SEARCH  → Gemini extracts params → hotels presented, PendingAction=SELECT
    ///   SELECT  → user picks hotel by name/number → room detail fetched, PendingAction=BOOK
    ///   BOOK    → user confirms → booking executed
    ///   CANCEL  → Gemini detects cancel intent → show bookings, PendingAction=CANCEL_SELECT
    ///   CANCEL_SELECT → user picks booking → PendingAction=CANCEL_CONFIRM
    ///   CANCEL_CONFIRM → user confirms → cancellation executed
    /// </postcondition>
    public async Task<ChatResponse> ProcessAsync(ChatRequest request, string userId, string? authToken)
    {
        var state = request.ContextState?.PendingAction;

        if (state == "BOOK" && IsConfirmation(request.UserMessage))
            return await ExecuteBookingAsync(request.ContextState!, authToken);

        if (state == "CANCEL_CONFIRM" && IsConfirmation(request.UserMessage))
            return await ExecuteCancellationAsync(request.ContextState!, authToken);

        if (state == "CANCEL_SELECT")
            return await HandleCancelSelectionAsync(request, authToken);

        if (state == "SELECT")
            return await HandleHotelSelectionAsync(request, authToken);

        // Parse intent via Gemini
        var prompt = BuildSearchPrompt(request.UserMessage, request.ContextState, request.Messages);
        string aiRaw;
        try { aiRaw = await aiProvider.GenerateAsync(prompt); }
        catch (Exception ex)
        {
            logger.LogError(ex, "AI provider call failed");
            return Clarify("I'm having trouble right now. Could you rephrase your request?", request.ContextState);
        }

        var intent = ParseIntent(aiRaw);
        if (intent is null)
            return Clarify("Could you give me more detail? I need the destination, dates, and number of guests.", request.ContextState);

        if (intent.Intent == "CANCEL")
            return await HandleCancelIntentAsync(authToken);

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
        var hotels = DeserializeList<HotelOptionDto>(request.ContextState!.HotelOptionsJson);
        if (hotels.Count == 0)
            return Clarify("I lost track of the hotel options. Could you start your search again?", null);

        // Single hotel: accept any confirmation as auto-select
        if (hotels.Count == 1 && IsConfirmation(request.UserMessage))
            return await BuildBookingConfirmation(hotels[0], request.ContextState!, authToken);

        var selPrompt = BuildSelectionPrompt(request.UserMessage, hotels.Select(h => (h.Name, h.Location, h.PricePerNight, h.RoomTypeName)).ToList());
        string aiRaw;
        try { aiRaw = await aiProvider.GenerateAsync(selPrompt); }
        catch (Exception ex)
        {
            logger.LogError(ex, "AI selection call failed");
            return Clarify("Could you tell me which hotel you'd like — by name or number?", request.ContextState);
        }

        var sel = ParseSelection(aiRaw);
        if (sel is null || sel.SelectedIndex < 0 || sel.SelectedIndex >= hotels.Count)
        {
            var relist = "Please tell me which hotel you'd like — say the hotel name or its number:\n\n" +
                string.Join("\n", hotels.Select((h, i) => $"{i + 1}. {h.Name} — {h.PricePerNight:N0} TL/night ({h.RoomTypeName})"));
            return Clarify(relist, request.ContextState);
        }

        return await BuildBookingConfirmation(hotels[sel.SelectedIndex], request.ContextState!, authToken);
    }

    private async Task<ChatResponse> BuildBookingConfirmation(HotelOptionDto chosen, ContextState ctx, string? authToken)
    {
        RoomDetailDto roomDetail;
        try
        {
            roomDetail = await facade.GetRoomDetailAsync(
                chosen.HotelId, chosen.RoomTypeId,
                DateOnly.Parse(ctx.StartDate!),
                DateOnly.Parse(ctx.EndDate!));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "GetRoomDetail failed for hotel {HotelId}", chosen.HotelId);
            return Clarify("I couldn't confirm availability for that hotel. Please try another or search again.", ctx);
        }

        var nights = DateOnly.Parse(ctx.EndDate!).DayNumber - DateOnly.Parse(ctx.StartDate!).DayNumber;
        var total = chosen.PricePerNight * nights;

        return new ChatResponse
        {
            Reply = $"Great choice! Here is your booking summary:\n\n" +
                    $"🏨 **{chosen.Name}**\n" +
                    $"🛏 Room: {chosen.RoomTypeName} | {chosen.PricePerNight:N0} TL/night\n" +
                    $"📅 {ctx.StartDate} → {ctx.EndDate} ({nights} nights)\n" +
                    $"👥 Guests: {ctx.GuestCount}\n" +
                    $"💰 Total: {total:N0} TL\n\n" +
                    "Say **\"Yes, book it\"** to confirm.",
            RequiresConfirmation = true,
            ContextState = new ContextState
            {
                PendingAction = "BOOK",
                Destination = ctx.Destination,
                StartDate = ctx.StartDate,
                EndDate = ctx.EndDate,
                GuestCount = ctx.GuestCount,
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
            return Clarify("I couldn't reach the hotel service right now. Please try again.", null);
        }

        if (hotels.Count == 0)
            return new ChatResponse
            {
                Reply = $"No available hotels found in {intent.Destination} for those dates. " +
                        "Try different dates or a nearby destination?",
                RequiresConfirmation = false,
                ContextState = null
            };

        return new ChatResponse
        {
            Reply = FormatHotelOptions(hotels, intent.Destination!, startDate, endDate),
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
            return new ChatResponse { Reply = "You need to be logged in to complete a booking. Please sign in and try again.", RequiresConfirmation = false, ContextState = null };

        try
        {
            var result = await facade.BookRoomAsync(context, authToken);
            logger.LogInformation("AI booking confirmed BookingId={BookingId}", result.BookingId);
            return new ChatResponse
            {
                Reply = $"✅ Your reservation at **{context.HotelName}** from {context.StartDate} to {context.EndDate} is confirmed!\n\nBooking ID: `{result.BookingId}`",
                RequiresConfirmation = false,
                ContextState = null
            };
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            return new ChatResponse { Reply = "The room was just taken by another guest. Let me search again for available options.", RequiresConfirmation = false, ContextState = null };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Booking via facade failed");
            return new ChatResponse { Reply = "Something went wrong while booking. Please try again.", RequiresConfirmation = false, ContextState = null };
        }
    }

    // ── Cancel flow ───────────────────────────────────────────────────────────

    private async Task<ChatResponse> HandleCancelIntentAsync(string? authToken)
    {
        if (string.IsNullOrEmpty(authToken))
            return new ChatResponse { Reply = "You need to be logged in to view or cancel bookings.", RequiresConfirmation = false, ContextState = null };

        List<AiBookingDto> bookings;
        try { bookings = await facade.GetUserBookingsAsync(authToken); }
        catch (Exception ex)
        {
            logger.LogError(ex, "GetUserBookings failed");
            return new ChatResponse { Reply = "I couldn't retrieve your bookings right now. Please try again.", RequiresConfirmation = false, ContextState = null };
        }

        var active = bookings.Where(b => b.Status == "Confirmed").ToList();
        if (active.Count == 0)
            return new ChatResponse { Reply = "You have no active bookings to cancel.", RequiresConfirmation = false, ContextState = null };

        var list = string.Join("\n", active.Select((b, i) =>
            $"{i + 1}. {b.HotelName} — {b.CheckInDate:d MMM} → {b.CheckOutDate:d MMM} ({b.GuestCount} guests, {b.TotalAmount:N0} TL)"));

        return new ChatResponse
        {
            Reply = $"Here are your active bookings:\n\n{list}\n\nWhich one would you like to cancel? Say the hotel name or its number.",
            RequiresConfirmation = false,
            ContextState = new ContextState
            {
                PendingAction = "CANCEL_SELECT",
                CancelBookingsJson = JsonSerializer.Serialize(active, JsonOpts)
            }
        };
    }

    private async Task<ChatResponse> HandleCancelSelectionAsync(ChatRequest request, string? authToken)
    {
        var bookings = DeserializeList<AiBookingDto>(request.ContextState!.CancelBookingsJson);
        if (bookings.Count == 0)
            return Clarify("I lost track of your bookings. Please try again.", null);

        if (bookings.Count == 1 && IsConfirmation(request.UserMessage))
            return BuildCancelConfirmation(bookings[0], request.ContextState!);

        var selPrompt = BuildSelectionPrompt(request.UserMessage,
            bookings.Select(b => (b.HotelName, $"{b.CheckInDate:d MMM}–{b.CheckOutDate:d MMM}", b.TotalAmount, b.RoomTypeName)).ToList());

        string aiRaw;
        try { aiRaw = await aiProvider.GenerateAsync(selPrompt); }
        catch (Exception ex)
        {
            logger.LogError(ex, "AI cancel selection failed");
            return Clarify("Which booking would you like to cancel?", request.ContextState);
        }

        var sel = ParseSelection(aiRaw);
        if (sel is null || sel.SelectedIndex < 0 || sel.SelectedIndex >= bookings.Count)
        {
            var relist = "Please tell me which booking to cancel — say the hotel name or number:\n\n" +
                string.Join("\n", bookings.Select((b, i) => $"{i + 1}. {b.HotelName} ({b.CheckInDate:d MMM})"));
            return Clarify(relist, request.ContextState);
        }

        return BuildCancelConfirmation(bookings[sel.SelectedIndex], request.ContextState!);
    }

    private static ChatResponse BuildCancelConfirmation(AiBookingDto booking, ContextState ctx)
    {
        _ = ctx;
        return new ChatResponse
        {
            Reply = $"Are you sure you want to cancel your booking at **{booking.HotelName}**?\n" +
                    $"📅 {booking.CheckInDate:d MMM} → {booking.CheckOutDate:d MMM} | 💰 {booking.TotalAmount:N0} TL\n\n" +
                    "Say **\"Yes, cancel it\"** to confirm.",
            RequiresConfirmation = true,
            ContextState = new ContextState
            {
                PendingAction = "CANCEL_CONFIRM",
                CancelBookingId = booking.BookingId,
                CancelHotelName = booking.HotelName
            }
        };
    }

    private async Task<ChatResponse> ExecuteCancellationAsync(ContextState context, string? authToken)
    {
        if (string.IsNullOrEmpty(authToken))
            return new ChatResponse { Reply = "You need to be logged in to cancel bookings.", RequiresConfirmation = false, ContextState = null };

        try
        {
            await facade.CancelBookingAsync(context.CancelBookingId!.Value, authToken);
            return new ChatResponse
            {
                Reply = $"✅ Your booking at **{context.CancelHotelName}** has been cancelled successfully.",
                RequiresConfirmation = false,
                ContextState = null
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Cancellation failed for booking {BookingId}", context.CancelBookingId);
            return new ChatResponse { Reply = "Something went wrong while cancelling. Please try again.", RequiresConfirmation = false, ContextState = null };
        }
    }

    // ── Prompt builders ───────────────────────────────────────────────────────

    private static string BuildSearchPrompt(string userMessage, ContextState? context, List<ChatMessage>? history)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");
        var sb = new StringBuilder();

        sb.AppendLine("You are a hotel booking assistant for a Hotels.com-like service in Turkey.");
        sb.AppendLine("Analyze the user's message and return ONLY valid JSON (no markdown, no code blocks).");
        sb.AppendLine();
        sb.AppendLine("JSON schema:");
        sb.AppendLine("{");
        sb.AppendLine("  \"intent\": \"SEARCH\" | \"CLARIFY\" | \"CANCEL\",");
        sb.AppendLine("  \"destination\": \"city name or null\",");
        sb.AppendLine("  \"startDate\": \"YYYY-MM-DD or null\",");
        sb.AppendLine("  \"endDate\": \"YYYY-MM-DD or null\",");
        sb.AppendLine("  \"guestCount\": integer or null,");
        sb.AppendLine("  \"reply\": \"your friendly response\"");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("Rules:");
        sb.AppendLine("- SEARCH: all four params (destination, startDate, endDate, guestCount) are determinable.");
        sb.AppendLine("- CLARIFY: any required param is missing — ask for only the missing ones.");
        sb.AppendLine("- CANCEL: user wants to cancel, view, or manage existing reservations.");
        sb.AppendLine($"- Today is {today}. Resolve relative dates ('next weekend', 'tomorrow') to exact dates.");
        sb.AppendLine("- Reply in the same language as the user (Turkish or English).");

        if (context?.PendingAction == "CLARIFY")
        {
            sb.AppendLine();
            sb.AppendLine("Already known from previous turn:");
            if (context.Destination is not null) sb.AppendLine($"- destination: {context.Destination}");
            if (context.StartDate is not null) sb.AppendLine($"- startDate: {context.StartDate}");
            if (context.EndDate is not null) sb.AppendLine($"- endDate: {context.EndDate}");
            if (context.GuestCount is not null) sb.AppendLine($"- guestCount: {context.GuestCount}");
        }

        if (history is { Count: > 0 })
        {
            sb.AppendLine();
            sb.AppendLine("Conversation history:");
            foreach (var msg in history)
                sb.AppendLine($"{(msg.Role == "user" ? "User" : "Assistant")}: {msg.Text}");
        }

        sb.AppendLine();
        sb.AppendLine($"User: \"{userMessage}\"");
        return sb.ToString();
    }

    private static string BuildSelectionPrompt(string userMessage, List<(string Name, string Info, decimal Price, string RoomType)> options)
    {
        var sb = new StringBuilder();
        sb.AppendLine("A user is selecting an item from a numbered list. Return ONLY valid JSON:");
        sb.AppendLine("{\"selectedIndex\": N, \"reply\": \"...\"}");
        sb.AppendLine("selectedIndex is 0-based. Return -1 if no clear selection.");
        sb.AppendLine();
        sb.AppendLine("Options:");
        for (int i = 0; i < options.Count; i++)
            sb.AppendLine($"{i + 1}. {options[i].Name} ({options[i].Info}) — {options[i].Price:N0} TL ({options[i].RoomType})");
        sb.AppendLine();
        sb.AppendLine($"User said: \"{userMessage}\"");
        return sb.ToString();
    }

    // ── Format helpers ────────────────────────────────────────────────────────

    private static string FormatHotelOptions(List<HotelOptionDto> hotels, string dest, DateOnly start, DateOnly end)
    {
        var nights = end.DayNumber - start.DayNumber;
        var sb = new StringBuilder();
        sb.AppendLine($"Here are the top {Math.Min(hotels.Count, 3)} hotels in {dest} ({start:d MMM} – {end:d MMM}, {nights} night{(nights != 1 ? "s" : "")}):");
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
            var json = StripFences(raw);
            return JsonSerializer.Deserialize<GeminiIntent>(json, JsonOpts);
        }
        catch { return null; }
    }

    private static HotelSelection? ParseSelection(string raw)
    {
        try
        {
            var json = StripFences(raw);
            return JsonSerializer.Deserialize<HotelSelection>(json, JsonOpts);
        }
        catch { return null; }
    }

    private static string StripFences(string raw)
    {
        var json = raw.Trim();
        if (!json.StartsWith("```")) return json;
        var start = json.IndexOf('\n') + 1;
        var end = json.LastIndexOf("```");
        return end > start ? json[start..end].Trim() : json;
    }

    private static bool IsConfirmation(string message)
    {
        var lower = message.Trim().ToLowerInvariant();
        return ConfirmKeywords.Any(k => lower.Contains(k));
    }

    private static List<T> DeserializeList<T>(string? json)
    {
        if (string.IsNullOrEmpty(json)) return [];
        try { return JsonSerializer.Deserialize<List<T>>(json, JsonOpts) ?? []; }
        catch { return []; }
    }

    private static ChatResponse Clarify(string reply, ContextState? carry) => new()
    {
        Reply = reply,
        RequiresConfirmation = false,
        ContextState = carry is null ? null : new ContextState
        {
            PendingAction = carry.PendingAction is "SELECT" or "CANCEL_SELECT" ? carry.PendingAction : "CLARIFY",
            Destination = carry.Destination,
            StartDate = carry.StartDate,
            EndDate = carry.EndDate,
            GuestCount = carry.GuestCount,
            HotelOptionsJson = carry.HotelOptionsJson,
            CancelBookingsJson = carry.CancelBookingsJson
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
