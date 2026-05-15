using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AiAgentService.Models;

namespace AiAgentService.Facade;

/// <summary>
/// Facade Pattern — hides all internal HTTP calls to HotelService.
/// Controllers and services never call HotelService directly.
/// </summary>
public class HotelSystemFacade(HttpClient http, ILogger<HotelSystemFacade> logger) : IHotelSystemFacade
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    /// <precondition>destination non-empty; startDate < endDate; guestCount >= 1</precondition>
    /// <postcondition>Returns available hotels from HotelService; empty list on error or no results</postcondition>
    public async Task<List<HotelOptionDto>> SearchHotelsAsync(
        string destination, DateOnly startDate, DateOnly endDate, int guestCount, string? authToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"/api/v1/search/hotels?destination={Uri.EscapeDataString(destination)}" +
            $"&startDate={startDate:yyyy-MM-dd}&endDate={endDate:yyyy-MM-dd}" +
            $"&guestCount={guestCount}&page=1&pageSize=5");

        if (!string.IsNullOrEmpty(authToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authToken);

        var response = await http.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("HotelService search returned {Status}", response.StatusCode);
            return [];
        }

        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<SearchPageDto>>(JsonOpts);
        return envelope?.Data?.Data?.Select(h => new HotelOptionDto
        {
            HotelId = h.HotelId,
            Name = h.Name,
            Location = h.Location,
            RoomTypeId = h.RoomTypeId,
            RoomTypeName = h.RoomTypeName,
            PricePerNight = h.PricePerNight,
            AvailableRooms = h.AvailableRooms,
            Rating = h.Rating,
            TotalReviews = h.TotalReviews
        }).ToList() ?? [];
    }

    /// <precondition>hotelId and roomTypeId are valid GUIDs</precondition>
    /// <postcondition>Returns inventoryId and rowVersion; throws HttpRequestException if room not found</postcondition>
    public async Task<RoomDetailDto> GetRoomDetailAsync(
        Guid hotelId, Guid roomTypeId, DateOnly startDate, DateOnly endDate)
    {
        var url = $"/api/v1/hotels/{hotelId}/rooms/{roomTypeId}?startDate={startDate:yyyy-MM-dd}&endDate={endDate:yyyy-MM-dd}";
        var envelope = await http.GetFromJsonAsync<ApiEnvelope<RoomDetailResponseDto>>(url, JsonOpts)
            ?? throw new HttpRequestException("Room detail response was empty.");

        return new RoomDetailDto
        {
            InventoryId = envelope.Data!.InventoryId,
            RowVersion = envelope.Data.RowVersion
        };
    }

    /// <precondition>contextState has all required booking fields; authToken is a valid JWT</precondition>
    /// <postcondition>Booking created in HotelService; HTTP 409 propagated as HttpRequestException on concurrency conflict</postcondition>
    public async Task<BookingResultDto> BookRoomAsync(ContextState contextState, string authToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/bookings");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authToken);

        var payload = new
        {
            hotelId = contextState.TargetHotelId,
            roomTypeId = contextState.TargetRoomTypeId,
            inventoryId = contextState.TargetInventoryId,
            startDate = contextState.StartDate,
            endDate = contextState.EndDate,
            guestCount = contextState.GuestCount ?? 1,
            rowVersion = contextState.RowVersion
        };

        request.Content = JsonContent.Create(payload, options: JsonOpts);
        var response = await http.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<BookingConfirmationDto>>(JsonOpts)
            ?? throw new HttpRequestException("Booking response was empty.");

        return new BookingResultDto
        {
            BookingId = envelope.Data!.BookingId,
            Status = envelope.Data.Status
        };
    }

    /// <precondition>authToken is a valid user JWT</precondition>
    /// <postcondition>Returns confirmed+cancelled bookings from HotelService for the authenticated user</postcondition>
    public async Task<List<AiBookingDto>> GetUserBookingsAsync(string authToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/bookings");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authToken);

        var response = await http.SendAsync(request);
        if (!response.IsSuccessStatusCode) return [];

        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<List<AiBookingDto>>>(JsonOpts);
        return envelope?.Data ?? [];
    }

    /// <precondition>bookingId exists and belongs to the authenticated user</precondition>
    /// <postcondition>Booking cancelled and inventory restored via HotelService; throws on failure</postcondition>
    public async Task CancelBookingAsync(Guid bookingId, string authToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/bookings/{bookingId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authToken);
        var response = await http.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    // ── Internal deserialization types ───────────────────────────────────────
    private record ApiEnvelope<T>(
        [property: JsonPropertyName("success")] bool Success,
        [property: JsonPropertyName("data")] T? Data);

    private record SearchPageDto(
        [property: JsonPropertyName("data")] List<HotelSearchResultDto>? Data);

    private record HotelSearchResultDto(
        [property: JsonPropertyName("hotelId")] Guid HotelId,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("location")] string Location,
        [property: JsonPropertyName("roomTypeId")] Guid RoomTypeId,
        [property: JsonPropertyName("roomTypeName")] string RoomTypeName,
        [property: JsonPropertyName("pricePerNight")] decimal PricePerNight,
        [property: JsonPropertyName("availableRooms")] int AvailableRooms,
        [property: JsonPropertyName("rating")] decimal Rating,
        [property: JsonPropertyName("totalReviews")] int TotalReviews);

    private record RoomDetailResponseDto(
        [property: JsonPropertyName("inventoryId")] Guid InventoryId,
        [property: JsonPropertyName("rowVersion")] uint RowVersion);

    private record BookingConfirmationDto(
        [property: JsonPropertyName("bookingId")] Guid BookingId,
        [property: JsonPropertyName("status")] string Status);
}
