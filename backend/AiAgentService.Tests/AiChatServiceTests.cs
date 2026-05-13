using AiAgentService.Facade;
using AiAgentService.Models;
using AiAgentService.Providers;
using AiAgentService.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AiAgentService.Tests;

public class AiChatServiceTests
{
    // ── Test doubles ─────────────────────────────────────────────────────────

    /// <summary>Returns a fixed CLARIFY intent JSON (simulates incomplete params).</summary>
    private sealed class ClarifyAiProvider : IAiProvider
    {
        public Task<string> GenerateAsync(string prompt) => Task.FromResult("""
            {"intent":"CLARIFY","destination":"Izmir","startDate":null,"endDate":null,"guestCount":null,"reply":"What dates are you looking for?"}
            """);
    }

    /// <summary>Returns a full SEARCH intent JSON with all params present.</summary>
    private sealed class SearchAiProvider : IAiProvider
    {
        public Task<string> GenerateAsync(string prompt) => Task.FromResult("""
            {"intent":"SEARCH","destination":"Izmir","startDate":"2026-07-01","endDate":"2026-07-05","guestCount":2,"reply":"Let me search for you!"}
            """);
    }

    private sealed class StubFacade : IHotelSystemFacade
    {
        public bool BookCalled { get; private set; }

        public Task<List<HotelOptionDto>> SearchHotelsAsync(
            string destination, DateOnly startDate, DateOnly endDate, int guestCount, string? authToken)
            => Task.FromResult(new List<HotelOptionDto>
            {
                new()
                {
                    HotelId = Guid.NewGuid(), Name = "Test Hotel", Location = "Izmir",
                    RoomTypeId = Guid.NewGuid(), RoomTypeName = "Standard",
                    PricePerNight = 1500m, AvailableRooms = 3, Rating = 8.5m, TotalReviews = 50
                }
            });

        public Task<RoomDetailDto> GetRoomDetailAsync(
            Guid hotelId, Guid roomTypeId, DateOnly startDate, DateOnly endDate)
            => Task.FromResult(new RoomDetailDto
            {
                InventoryId = Guid.NewGuid(),
                RowVersion = 1u
            });

        public Task<BookingResultDto> BookRoomAsync(ContextState contextState, string authToken)
        {
            BookCalled = true;
            return Task.FromResult(new BookingResultDto
            {
                BookingId = Guid.NewGuid(),
                Status = "Confirmed"
            });
        }
    }

    private static AiChatService Build(IAiProvider provider, IHotelSystemFacade facade) =>
        new(provider, facade, NullLogger<AiChatService>.Instance);

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ProcessAsync_IncompleteParams_ReturnsClarifyWithRequiresConfirmationFalse()
    {
        var sut = Build(new ClarifyAiProvider(), new StubFacade());
        var request = new ChatRequest { UserMessage = "I want a hotel in Izmir" };

        var response = await sut.ProcessAsync(request, "user-1", null);

        Assert.False(response.RequiresConfirmation);
        Assert.NotNull(response.Reply);
    }

    [Fact]
    public async Task ProcessAsync_AllParamsPresent_ReturnsHotelOptionsWithRequiresConfirmationTrue()
    {
        var sut = Build(new SearchAiProvider(), new StubFacade());
        var request = new ChatRequest { UserMessage = "Hotel in Izmir July 1–5 for 2 guests" };

        var response = await sut.ProcessAsync(request, "user-1", "fake-token");

        Assert.True(response.RequiresConfirmation);
        Assert.Equal("BOOK", response.ContextState?.PendingAction);
        Assert.NotNull(response.ContextState?.RowVersion);
    }

    [Fact]
    public async Task ProcessAsync_PendingBookWithConfirmation_CallsFacadeBookRoom()
    {
        var facade = new StubFacade();
        var sut = Build(new ClarifyAiProvider(), facade); // AI not called for confirmation turn

        var request = new ChatRequest
        {
            UserMessage = "Yes, book it",
            ContextState = new ContextState
            {
                PendingAction = "BOOK",
                TargetHotelId = Guid.NewGuid(),
                TargetRoomTypeId = Guid.NewGuid(),
                TargetInventoryId = Guid.NewGuid(),
                StartDate = "2026-07-01",
                EndDate = "2026-07-05",
                GuestCount = 2,
                RowVersion = 1u,
                HotelName = "Test Hotel"
            }
        };

        var response = await sut.ProcessAsync(request, "user-1", "valid-token");

        Assert.True(facade.BookCalled);
        Assert.False(response.RequiresConfirmation);
        Assert.Null(response.ContextState);
    }

    [Fact]
    public async Task ProcessAsync_PendingBookWithoutAuthToken_ReturnsLoginPrompt()
    {
        var sut = Build(new ClarifyAiProvider(), new StubFacade());

        var request = new ChatRequest
        {
            UserMessage = "Yes, book it",
            ContextState = new ContextState { PendingAction = "BOOK" }
        };

        var response = await sut.ProcessAsync(request, "user-1", authToken: null);

        Assert.False(response.RequiresConfirmation);
        Assert.Contains("logged in", response.Reply, StringComparison.OrdinalIgnoreCase);
    }
}
