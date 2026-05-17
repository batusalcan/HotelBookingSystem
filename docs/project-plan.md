# Hotel Booking System — Project Plan

**Course:** SE 4458 Software Architecture & Design of Modern Large Scale Systems  
**Project:** Group 1 — Hotel Booking System (Hotels.com-like)  
**Stack:** C# .NET 8, Ocelot, Supabase PostgreSQL, MongoDB, Redis, RabbitMQ, Supabase Auth

---

## Architecture Overview

```
Client / Admin UI
        │
        ▼
   API Gateway (Ocelot)          ← single entry point, JWT validation, rate limiting
        │
   ┌────┼────────────────────────┐
   │    │                        │
   ▼    ▼                        ▼
HotelService          CommentsService     AiAgentService
(SQL CatalogDb        (MongoDB)           (Gemini API + Facade → HotelService)
 + SQL BookingDb
 + Redis
 + RabbitMQ publisher)
        │
   RabbitMQ queue
        │
  NotificationService
  (queue consumer + nightly cron)
```

**Services (5 deployments + gateway):**

| Service | Responsibility |
|---|---|
| **HotelService** | Admin inventory management + hotel search (Redis cache-aside) + room booking (optimistic concurrency) |
| **CommentsService** | Per-category review scores + paginated comments (MongoDB) |
| **NotificationService** | RabbitMQ consumer + nightly cron capacity alerts |
| **AiAgentService** | Gemini AI conversational search & booking (Facade over HotelService) |
| **ApiGateway** | Ocelot routing, JWT validation, rate limiting |

**Design Patterns in use:**
- Strategy → HotelService pricing (15% discount for JWT users)
- Singleton → Redis ConnectionMultiplexer, RabbitMQ IConnection
- Factory Method → NotificationService alert types
- Facade → AiAgentService hides HTTP calls to HotelService
- Cache-Aside → HotelService Redis pattern

---

## Rules That Apply to Every Phase

1. Every implemented method must have XML doc spec comments:
   - `<precondition>` — what must be true before the method runs
   - `<postcondition>` — what is guaranteed after it completes
2. After each phase, write xUnit smoke tests:
   - Happy path (success case)
   - Key failure/edge case
3. All REST endpoints must be under `/api/v1/` (versioned)
4. All list endpoints must support pagination (`page`, `pageSize`)
5. Never share a DbContext between services
6. Never add hard FK constraints across service boundaries
7. Never cache discounted prices in Redis — only base prices
8. No SQLite, no local auth implementations

---

## Phase 1 — Shared Infrastructure ✅ DONE

**Goal:** Foundation every service depends on. No business logic yet.

### What was done:
- [x] Downgraded all services from .NET 10 → .NET 8
- [x] Replaced `Microsoft.AspNetCore.OpenApi` with `Swashbuckle.AspNetCore 6.9.0`
- [x] Created `SharedKernel` class library (net8.0), added to solution
- [x] `SharedKernel/Models/PaginatedResult<T>` — paginated response wrapper
- [x] `SharedKernel/Models/ApiResponse<T>` — standard success/error envelope
- [x] `SharedKernel/Exceptions/AppException` — base typed exception (maps to HTTP status)
- [x] `SharedKernel/Exceptions/NotFoundException` — 404
- [x] `SharedKernel/Exceptions/ConflictException` — 409
- [x] `SharedKernel/Events/ReservationCreatedEvent` — AMQP contract (HotelService → Notification)
- [x] Added SharedKernel project reference to all services
- [x] Added `Serilog.AspNetCore` + `Serilog.Sinks.Console` to all services
- [x] Updated all `Program.cs` files: Serilog, Swagger, `/health` endpoint
- [x] Updated all `appsettings.json` files with service-specific config structure
- [x] Merged HotelAdminService + HotelSearchService + BookHotelService → single HotelService

### No specs or tests needed — only DTOs and infrastructure wiring.

---

## Phase 2 — Hotel Service

**Goal:** Single service handling all hotel operations: admin inventory management, public/authenticated search with Redis cache-aside, and authenticated room booking with optimistic concurrency.

### NuGet packages to add:
- `Microsoft.EntityFrameworkCore.SqlServer`
- `Microsoft.EntityFrameworkCore.Tools`
- `Microsoft.AspNetCore.Authentication.JwtBearer`
- `StackExchange.Redis`
- `RabbitMQ.Client`

### Files to create:

```
HotelService/
  Data/
    CatalogDbContext.cs           ← Hotels, RoomTypes, InventoryBlocks
    BookingDbContext.cs           ← Bookings (separate DbContext, separate DB)
    Migrations/
      Catalog/                   ← EF migrations for CatalogDb
      Booking/                   ← EF migrations for BookingDb
  Entities/
    Hotel.cs                       ← includes TotalReviews (denormalized from Comments); ImageUrl? (nullable, nice-to-have)
    RoomType.cs
    InventoryBlock.cs             ← critical: has RowVersion concurrency token
    Booking.cs                    ← soft references to HotelId and RoomTypeId (no FK)
  DTOs/
    UpsertInventoryRequest.cs
    HotelDto.cs
    RoomTypeDto.cs
    HotelSearchRequest.cs
    HotelSearchResult.cs          ← includes coordinates for map
    RoomDetailDto.cs              ← includes rowVersion token
    CreateBookingRequest.cs       ← includes rowVersion field
    BookingConfirmationDto.cs
  Pricing/
    IPricingStrategy.cs           ← Strategy Pattern interface
    GuestPricingStrategy.cs       ← returns base price (no discount)
    AuthenticatedPricingStrategy.cs ← applies 15% discount
  Cache/
    ICacheService.cs
    RedisCacheService.cs          ← Singleton ConnectionMultiplexer
  Messaging/
    IRabbitMqPublisher.cs
    RabbitMqPublisher.cs          ← Singleton IConnection, publishes ReservationCreatedEvent
  Services/
    IInventoryService.cs
    InventoryService.cs           ← admin business logic with precondition/postcondition specs
    IHotelSearchService.cs
    HotelSearchService.cs         ← cache-aside logic + pricing strategy selection
    IBookingService.cs
    BookingService.cs             ← optimistic concurrency + event publish
  Controllers/
    v1/
      AdminController.cs          ← POST /api/v1/admin/inventory, hotels CRUD
      SearchController.cs         ← GET /api/v1/search/hotels
      HotelsController.cs         ← GET /api/v1/hotels/{hotelId}/rooms/{roomId}
      BookingsController.cs       ← POST /api/v1/bookings
```

### API Endpoints:

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| `POST` | `/api/v1/admin/hotels` | Admin JWT | Create a new hotel |
| `PUT` | `/api/v1/admin/hotels/{hotelId}` | Admin JWT | Update hotel details |
| `GET` | `/api/v1/admin/hotels` | Admin JWT | List all hotels (paginated) |
| `DELETE` | `/api/v1/admin/hotels/{hotelId}` | Admin JWT | Delete a hotel |
| `POST` | `/api/v1/admin/hotels/{hotelId}/roomtypes` | Admin JWT | Create a room type for a hotel (e.g. "Standard", "Aile") |
| `GET` | `/api/v1/admin/hotels/{hotelId}/roomtypes` | Admin JWT | List room types for a hotel — populates the "Oda Tipi" dropdown in the admin UI |
| `POST` | `/api/v1/admin/inventory` | Admin JWT | Add/update room availability block |
| `POST` | `/api/v1/admin/cache/clear` | Admin JWT | Clear Redis cache entries by pattern (used after inventory updates) |
| `GET` | `/api/v1/admin/debug-auth` | Public (no auth) | Decode and inspect JWT claims — development/debugging aid |
| `GET` | `/api/v1/search/hotels` | Public (JWT optional) | Search hotels with cache-aside + pricing |
| `GET` | `/api/v1/hotels/{hotelId}/roomtypes` | Public | List room types for a hotel |
| `GET` | `/api/v1/hotels/{hotelId}/rooms/{roomTypeId}` | Public | Get room details + RowVersion token |
| `POST` | `/api/v1/bookings` | User JWT | Create reservation with RowVersion check |
| `GET` | `/api/v1/bookings` | User JWT | List all bookings for the authenticated user |
| `DELETE` | `/api/v1/bookings/{bookingId}` | User JWT | Cancel a booking |
| `GET` | `/api/v1/admin/hotels/capacity-report?days=30` | No auth (AllowAnonymous — internal) | Returns hotels with InventoryBlocks below 20% capacity — called by NotificationService cron job |

### Key logic:

**Admin (InventoryService):**
- **Nice-to-have:** `Hotel.ImageUrl` — `POST /api/v1/admin/hotels` optionally accepts an `imageUrl` string (URL to an image uploaded separately to Azure Blob Storage or similar). Field is nullable; omitting it is valid.
- **Precondition:** `StartDate < EndDate` AND `AvailableCount >= 0`
- **Postcondition:** `InventoryBlock` upserted in SQL, `hotel:detail:{hotelId}` cache key evicted from Redis. On **create**: `TotalCount = AvailableCount` (set once from admin "Oda Adedi" input, never changed after). On **update**: only `AvailableCount` and `IsAvailable` change; `TotalCount` is preserved for nightly cron ratio `AvailableCount / TotalCount < 0.20`.
- JWT Admin role validation via `[Authorize(Roles = "admin")]`
- EF Core Code-First migrations run on startup for both DbContexts

**Search (HotelSearchService):**
- Cache key: `search:{destination}:{startDate}:{endDate}:{guestCount}` → TTL 15 min
- Cache key: `hotel:detail:{hotelId}` → TTL 60 min
- **Cache-aside flow:** Redis hit → apply pricing → return. Redis miss → SQL query → populate cache → apply pricing → return
- **Pricing strategy selection:** JWT present and valid → `AuthenticatedPricingStrategy` (15% off). No JWT → `GuestPricingStrategy`
- Response must include `coordinates: { lat, lng }` for "Haritada göster" and `totalReviews` for the review count shown in search results ("3 yorum")
- SQL filter: `IsAvailable = true AND AvailableCount > 0 AND RoomType.MaxGuests >= guestCount AND dates overlap` — the `MaxGuests` check excludes rooms that cannot physically accommodate the requested party

**Booking (BookingService):**
- **Precondition:** Valid JWT AND `rowVersion` provided AND `StartDate < EndDate` AND `GuestCount >= 1`
- **Postcondition:** `AvailableCount -= 1` in SQL AND `Booking` record created AND `ReservationCreatedEvent` published to RabbitMQ
- EF Core optimistic concurrency: `IsRowVersion().IsConcurrencyToken()` on `InventoryBlock.RowVersion`
- Catch `DbUpdateConcurrencyException` → return HTTP 409 Conflict
- RabbitMQ exchange: `hotel.reservations`, queue: `reservation.created`, routing key: `reservation.created`
- `TotalAmount` stored at booking time (price snapshot)
- Polly circuit breaker on SQL calls → 503 if DB unreachable

### Data seeding (run once on first startup):
- 7 hotels in Istanbul (×2), Izmir (×1), Bodrum (×2), Antalya (×2) with real lat/lng coordinates
- 2 RoomTypes per hotel ("Standard", "Family")
- 3–5 InventoryBlocks per RoomType covering next 90 days
- At least one block where `AvailableCount < 20% of TotalCount` (to trigger nightly cron alert)

### Tests (xUnit):
- Unit test: `AuthenticatedPricingStrategy.Apply(1000)` → returns `850`
- Unit test: `GuestPricingStrategy.Apply(1000)` → returns `1000`
- Unit test: `InventoryService.UpsertInventory` precondition guard throws on invalid dates
- Unit test: `BookingService.CreateBooking` with RowVersion mismatch → throws `ConflictException`
- Smoke test: `POST /api/v1/admin/inventory` with valid payload → 200 OK
- Smoke test: `POST /api/v1/admin/inventory` with `StartDate >= EndDate` → 400 Bad Request
- Smoke test: `GET /api/v1/search/hotels?destination=Istanbul&...` → 200 OK with paginated results
- Smoke test: `POST /api/v1/bookings` with valid payload + JWT → 200 OK
- Smoke test: `POST /api/v1/bookings` without JWT → 401 Unauthorized
- Smoke test: `POST /api/v1/bookings` with stale `rowVersion` → 409 Conflict

---

## Phase 3 — Comments Service

**Goal:** Users view per-category review scores and paginated comments from MongoDB.

### NuGet packages to add:
- `MongoDB.Driver`

### Files to create:

```
CommentsService/
  Data/
    MongoDbContext.cs             ← IMongoClient Singleton, collection: hotelReviews
  Models/
    HotelReview.cs                ← MongoDB document model
    CategoryScores.cs
    ReviewEntry.cs
    HotelReply.cs
  DTOs/
    CommentsResponseDto.cs        ← categoryBreakdown + paginated comments[]
    CommentDto.cs
  Services/
    ICommentsService.cs
    CommentsService.cs
  Controllers/
    v1/
      CommentsController.cs       ← GET /api/v1/comments/{hotelId}
```

### API Endpoints:

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| `GET` | `/api/v1/comments/{hotelId}` | Public | Get category scores + paginated reviews |
| `POST` | `/api/v1/comments/{hotelId}` | User JWT | Submit a review — **assumption** (see note below) |

> **Architectural Decision / Assumption:** Although the project mock-ups only show the comments display UI, `POST /api/v1/comments/{hotelId}` is implemented to populate the NoSQL database and support dynamic review data. Based on the "verified" and stay-duration labels in the mock-ups, only authenticated users (validated via IAM JWT) may post comments. This prevents spam and ensures verified status integrity. This endpoint integrates with the IAM service to validate the user session before accepting any submission. **This assumption will be documented in the project README.**

### Key logic:
- **GET Precondition:** `hotelId` is a valid GUID
- **GET Postcondition:** Returns aggregated `categoryBreakdown` + paginated `comments[]` from MongoDB
- **POST Precondition:** Valid JWT AND `rating` between 1.0–10.0 AND `text` non-empty
- **POST Postcondition:** Review appended to `reviews[]` AND `overallScore` and all `categoryScores` recalculated in the same MongoDB document
- 5 score categories: `cleanliness`, `staff`, `facilities`, `locationCondition`, `ecoFriendly`
- Seed MongoDB with sample review documents matching the seeded SQL hotels
- 404 if no document found for the given `hotelId`

### Tests (xUnit):
- Smoke test: `GET /api/v1/comments/{validHotelId}` → 200 OK with `categoryBreakdown`
- Smoke test: `GET /api/v1/comments/{unknownId}` → 404 Not Found
- Smoke test: `POST /api/v1/comments/{hotelId}` with valid JWT + payload → 201 Created
- Smoke test: `POST /api/v1/comments/{hotelId}` without JWT → 401 Unauthorized

---

## Phase 4 — Notification Service

**Goal:** Two independent responsibilities — async queue consumer (BP-07) and nightly cron job (BP-06).

### NuGet packages to add:
- `RabbitMQ.Client`

> **Architecture note:** NotificationService does NOT connect to HotelService's database directly. The Database-per-Service requirement forbids cross-service database access. Instead, the nightly cron job calls the HotelService internal endpoint `GET /api/v1/admin/hotels/capacity-report?days=30` via typed HttpClient to retrieve low-capacity data.

### NuGet packages to add:
- `RabbitMQ.Client`
- `Npgsql.EntityFrameworkCore.PostgreSQL` — for NotificationAlerts persistence
- `Microsoft.Extensions.Http.Resilience` — resilience handler on HotelServiceClient

### Files to create:

```
NotificationService/
  Data/
    NotificationAlert.cs          ← EF Core entity for in-website alert records
    NotificationsDbContext.cs     ← EF Core DbContext for NotificationAlerts table
  HttpClients/
    IHotelServiceClient.cs        ← typed HttpClient interface
    HotelServiceClient.cs         ← calls GET /api/v1/admin/hotels/capacity-report
  Messaging/
    INotificationFactory.cs       ← Factory Method Pattern
    BookingConfirmationNotification.cs
    LowCapacityAlertNotification.cs
    NotificationFactory.cs
    RabbitMqConsumer.cs           ← IHostedService, always-on queue subscriber
  Jobs/
    CapacityAlertJob.cs           ← nightly cron: calls HotelServiceClient → persists NotificationAlert rows
  Services/
    INotificationService.cs
    CapacityNotificationService.cs
  Controllers/
    v1/
      NotificationController.cs   ← POST /capacity-check (trigger), GET / (list), PATCH /{id}/read
```

### Key logic:

**Task A — Queue Consumer (BP-07, always-on):**
- `RabbitMqConsumer` registered as `IHostedService` — starts on app startup
- Subscribes to `reservation.created` queue on exchange `hotel.reservations`
- Deserializes `ReservationCreatedEvent`, logs confirmation to console (simulated notification)
- ACK on success → message removed from queue
- NACK + requeue on failure → message retried

**Task B — Nightly Cron (BP-06):**
- Triggered nightly via cloud scheduler hitting `POST /api/v1/notifications/capacity-check`
- `CapacityAlertJob` calls `HotelServiceClient.GetLowCapacityHotels(days: 30)` → HotelService internal endpoint executes the SQL query and returns results
- NotificationService never queries HotelService's database directly — all data comes via the REST API call
- **Clears previous run's snapshot** first (`ExecuteDeleteAsync` on `NotificationAlerts`) to prevent accumulation
- Persists fresh `NotificationAlert` rows to PostgreSQL for each low-capacity hotel/room-type block found
- Admin polls `GET /api/v1/notifications` (paginated, ordered by `CreatedAt` desc) to see current alerts
- `PATCH /api/v1/notifications/{id}/read` marks an alert as read; unread count shown as badge in admin UI

**NotificationAlert entity fields:** `NotificationId` (PK GUID), `HotelId`, `HotelName`, `RoomTypeName`, `AvailableCount`, `TotalCount`, `CapacityRatio` (double), `StartDate`, `EndDate` (DateOnly), `CreatedAt` (DateTime UTC), `IsRead` (bool, default false)

**Table creation:** `NotificationsDbContext` table is created on startup via raw SQL `CREATE TABLE IF NOT EXISTS "NotificationAlerts"` (EF Core `EnsureCreated` does not work on a shared Supabase DB that already has other tables).

**Factory Method:** `NotificationFactory.Create(type)` returns correct `INotification`.

### Tests (xUnit):
- Unit test: `NotificationFactory.Create(NotificationType.BookingConfirmation)` → returns `BookingConfirmationNotification`
- Unit test: `NotificationFactory.Create(NotificationType.LowCapacity)` → returns `LowCapacityAlertNotification`
- Smoke test: `GET /health` → 200 OK with RabbitMQ status
- Smoke test: `POST /api/v1/notifications/capacity-check` → 200 OK

---

## Phase 5 — AI Agent Service

**Goal:** Conversational chat window that drives hotel search and booking through a mandatory 2-step confirmation flow.

### NuGet packages to add:
- `Mscc.GenerativeAI` (or direct HttpClient to Gemini REST API — preferred for provider portability)
- `Microsoft.AspNetCore.Authentication.JwtBearer`

### Files to create:

```
AiAgentService/
  Facade/
    IHotelSystemFacade.cs         ← Facade Pattern interface
    HotelSystemFacade.cs          ← wraps HttpClient calls to HotelService
  Models/
    ChatRequest.cs                ← sessionId, userMessage, contextState
    ChatResponse.cs               ← reply, requiresConfirmation, contextState
    ContextState.cs               ← pendingAction, targetHotelId, rowVersion, dates...
  Providers/
    IAiProvider.cs                ← abstraction interface — swap models without touching business logic
    GeminiAiProvider.cs           ← calls Gemini API (gemini-1.5-flash); model name read from appsettings
  Services/
    IAiChatService.cs
    AiChatService.cs              ← intent parsing + IAiProvider calls + dialogue flow
  Controllers/
    v1/
      AiController.cs             ← POST /api/v1/ai/chat
```

### API Endpoints:

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| `POST` | `/api/v1/ai/chat` | User JWT | Stateless chat — all state in contextState |

### Key logic:
- **Facade Pattern:** `HotelSystemFacade.SearchHotels()` calls HotelService `/api/v1/search/hotels`. `HotelSystemFacade.BookRoom()` calls HotelService `/api/v1/bookings`. Controllers never call downstream services directly.
- **Stateless design:** `contextState` is client-owned and echoed back each request
- **Mandatory 2-step confirmation flow:**
  1. Parse intent from user message
  2. If parameters incomplete → return clarifying question (`requiresConfirmation: false`)
  3. If complete → call Facade → Search → format options → return (`requiresConfirmation: true`, `contextState` with `pendingAction: "BOOK"`)
  4. User confirms → call Facade → Book → return confirmation
- **AI Provider abstraction:** `AiChatService` depends on `IAiProvider`, not on any concrete SDK. To switch from Gemini to another model, implement a new `IAiProvider` and update DI registration — zero business logic changes required.
- Default provider: `GeminiAiProvider` using model `gemini-1.5-flash`. Model name and API key configured in `appsettings.json` under `AI:ModelName` and `AI:ApiKey` — never hardcoded.
- Real-time messaging NOT required — standard HTTP request/response is sufficient

### Tests (xUnit):
- Unit test: `AiChatService` with incomplete params → returns `requiresConfirmation: false`
- Unit test: `AiChatService` with `pendingAction: "BOOK"` context → calls Facade.BookRoom
- Smoke test: `POST /api/v1/ai/chat` without JWT → 401 Unauthorized
- Smoke test: `POST /api/v1/ai/chat` with search intent → 200 OK with hotel options

---

## Phase 6 — API Gateway (Ocelot)

**Goal:** Single entry point; routes all traffic to downstream services; validates JWTs; rate limits.

### NuGet packages to add:
- `Ocelot`
- `Microsoft.AspNetCore.Authentication.JwtBearer`

### Files to create:

```
ApiGateway/
  ocelot.json                     ← Ocelot route configuration
  Program.cs                      ← updated: AddOcelot(), UseOcelot()
```

### Ocelot route table:

| Upstream (Client calls) | Downstream (Ocelot forwards to) | Auth Required |
|-------------------------|----------------------------------|---------------|
| `POST /gateway/v1/admin/inventory` | `HotelService/api/v1/admin/inventory` | Bearer JWT (gateway validates) |
| `GET\|POST /gateway/v1/admin/hotels` | `HotelService/api/v1/admin/hotels` | Bearer JWT (gateway validates) |
| `GET\|POST\|PUT\|DELETE /gateway/v1/admin/hotels/{everything}` | `HotelService/api/v1/admin/hotels/{everything}` | Bearer JWT (gateway validates) |
| `GET /gateway/v1/admin/debug-auth` | `HotelService/api/v1/admin/debug-auth` | None |
| `GET /gateway/v1/search/hotels` | `HotelService/api/v1/search/hotels` | None (rate limited: 100 req/min) |
| `GET /gateway/v1/hotels/{everything}` | `HotelService/api/v1/hotels/{everything}` | None |
| `GET\|POST /gateway/v1/bookings` | `HotelService/api/v1/bookings` | Bearer JWT |
| `DELETE /gateway/v1/bookings/{bookingId}` | `HotelService/api/v1/bookings/{bookingId}` | Bearer JWT |
| `GET /gateway/v1/notifications` | `NotificationService/api/v1/notifications` | Bearer JWT |
| `GET /gateway/v1/comments/{hotelId}` | `CommentsService/api/v1/comments/{hotelId}` | None |
| `POST /gateway/v1/comments/{hotelId}` | `CommentsService/api/v1/comments/{hotelId}` | Bearer JWT |
| `POST /gateway/v1/ai/chat` | `AiAgentService/api/v1/ai/chat` | Bearer JWT (rate limited: 30 req/min) |

### Key logic:
- JWT validated at gateway level via `AuthenticationOptions: { AuthenticationProviderKey: "Bearer" }` — Ocelot rejects requests with missing or invalid tokens before they reach downstream services. Ocelot forwards the `Authorization` header unchanged, so downstream services can also read the JWT to extract user claims (`sub`, `app_metadata.roles`).
- **Two-layer auth on admin routes:** Gateway validates token authenticity; HotelService `IsAdmin()` decodes `app_metadata.roles` from the forwarded JWT to check admin role. This matches the project spec's authentication (gateway) + authorization (service) separation.
- Rate limiting on search (100 req/min) and AI chat (30 req/min) via `X-Client-IP` header
- `X-Correlation-Id` header injected on every forwarded request for end-to-end tracing
- Gateway does NOT have its own Swagger — each downstream service exposes its own `/swagger` endpoint; the gateway Swagger UI aggregates them all

### Tests:
- Smoke test: `GET /gateway/v1/search/hotels?...` routes correctly → 200 OK
- Smoke test: `POST /gateway/v1/bookings` without JWT → 401 from gateway (never reaches HotelService)

---

## Phase 7 — Frontend UI

**Goal:** Working UI that satisfies the PDF mockups. All business use cases must be accessible via the UI per the common requirements.

### Tech:
- React + Vite, Tailwind CSS v3 (teal color scheme, Booking.com-inspired design)
- react-router-dom v6 for client-side routing
- Supabase Auth JS SDK (`@supabase/supabase-js`) for login, sign-up, session management
- Axios with request interceptor for injecting `Authorization: Bearer <token>` on all API calls
- react-leaflet + Leaflet for "Show on Map" feature (OpenStreetMap tiles)
- All API calls go through `VITE_GATEWAY_URL` (Ocelot API Gateway) — never directly to services
- Multi-stage Dockerfile (Node 20 Alpine build → nginx Alpine runtime) + `nginx.conf` for SPA routing

### Pages / Screens:

| Screen | Route | Auth Required | Key Features |
|---|---|---|---|
| **Home / Search** | `/` | No | Hero with luxury hotel image + tagline; destination / check-in / check-out / guests search form; popular destinations grid; member discount banner |
| **Search Results** | `/search` | No | Paginated hotel cards (name, rating, `totalReviews`, price with discount); sort dropdown (Price Low/High, Highest Rated); "Show on Map" Leaflet toggle; loading skeletons |
| **Hotel Detail** | `/hotels/:hotelId` | No (booking requires login) | Room type selection grid; 15% discount banner if signed in; availability check; "Book Now" / "Sign in to Book" button; 409 Conflict handling; `CommentSection` with category scores and review form |
| **Booking Confirmation** | `/bookings/confirm` | User JWT (`ProtectedRoute`) | Booking ID (monospace), room type, check-in/out dates, guest count, confirmed status badge |
| **My Bookings** | `/my-bookings` | User JWT (`ProtectedRoute`) | List all user reservations; check-in/out dates, nights count, total amount, status badge (Confirmed/Cancelled); cancel button with confirmation dialog; calls `GET /bookings` + `DELETE /bookings/{id}` |
| **Admin Panel** | `/admin` | Admin JWT (`ProtectedRoute adminOnly`) | 4 tabs: Hotels (create form + list + delete), Room Types (create with hotel selector), Inventory (upsert with hotel+roomtype selectors, date range, room count, occupied/vacant), Notifications (low-capacity alert cards with capacity bar visualization, urgency color coding, unread badge, 60-second background polling) |
| **AI Chat Widget** | Floating on all pages | User JWT (widget hidden if not signed in) | Floating 🤖 button; chat window with message history; sends `messages[]` + `contextState` to `POST /gateway/v1/ai/chat`; "Thinking..." pulse state |
| **Sign-In** | `/login` | No | Email + password form; error message on failed login; link to Sign-Up |
| **Sign-Up / Registration** | `/signup` | No | Email + password + confirm password; client-side validation; `supabase.auth.signUp()` via dynamic import; auto-signs in on success; shows "Check your email" screen if Supabase email confirmation is required _(Assumption — see requirements.md §8)_ |

### Key implementation notes:
- Login/logout via Supabase Auth JS SDK — client receives JWT, stores in memory, sends as `Authorization: Bearer <token>` header via Axios interceptor
- Admin role detected from decoded JWT `app_metadata.roles` claim
- Map feature (`MapView`): lazily imported with `React.lazy` + `Suspense`; Leaflet marker icon Vite fix applied via `L.Icon.Default.mergeOptions` with CDN URLs
- Sign-Up: uses `supabase.auth.signUp()` dynamically imported in `SignUpPage` (not in `AuthContext`); handles both auto-confirm and email-verification Supabase project modes
- If user is not signed in and clicks "Book Now", button reads "Sign in to Book" and redirects to `/login`
- Admin routes and AI widget guarded by `isAdmin` / `session` from `AuthContext`

### Files created:
```
frontend/
  src/
    lib/supabase.js               ← Supabase client singleton
    context/AuthContext.jsx       ← session, token, isAdmin, signIn, signOut
    api/client.js                 ← Axios instance with JWT interceptor
    api/hotelApi.js               ← search, room detail, booking, admin CRUD
    api/commentsApi.js            ← getComments, postComment
    api/aiApi.js                  ← sendChatMessage
    components/
      Layout.jsx
      Navbar.jsx                  ← Sign Up link + Sign In button (unauthenticated); Admin Panel + Sign Out (authenticated)
      ProtectedRoute.jsx
      MapView.jsx                 ← Leaflet map (lazy loaded)
      AiChatWidget.jsx            ← floating chat widget
      CommentSection.jsx          ← category scores + paginated reviews + submit form
    pages/
      HomePage.jsx
      SearchResultsPage.jsx
      HotelDetailPage.jsx
      BookingConfirmPage.jsx
      MyBookingsPage.jsx            ← user's bookings list + cancel
      AdminPage.jsx                 ← 4 tabs: Hotels, Room Types, Inventory, Notifications
      LoginPage.jsx
      SignUpPage.jsx
  Dockerfile                      ← multi-stage Node build → nginx runtime
  nginx.conf                      ← try_files for SPA routing
  .env.example                    ← VITE_GATEWAY_URL, VITE_SUPABASE_URL, VITE_SUPABASE_ANON_KEY
```

---

## Phase 8 — Cross-Cutting Concerns

**Goal:** Polly circuit breakers, health checks, correlation IDs across all services.

### What to add to each applicable service:
- `Polly` circuit breaker on SQL `DbContext` calls in HotelService and NotificationService → 503 if SQL unreachable
- `/health` endpoints already registered in Phase 1 — extend with real checks:
  - HotelService: verify EF Core (CatalogDb + BookingDb) + Redis ping
  - CommentsService: verify MongoDB ping
  - NotificationService: verify RabbitMQ connection + SQL
- Serilog `CorrelationId` enricher: read `X-Correlation-Id` from header (set by Gateway) and include in every log line

### Tests:
- Smoke test: `GET /health` on each service → 200 OK with `{ status: "Healthy" }`

---

## Phase 9 — Dockerfiles & CI/CD

**Goal:** Each service is containerizable; CI verifies every push.

### Dockerfile structure (per service, multi-stage build):
```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "<ServiceName>.dll"]
```

### GitHub Actions CI (`.github/workflows/ci.yml`):
```
Trigger: push to main / PR to main
Steps:
  1. dotnet restore (solution)
  2. dotnet build (solution, no restore)
  3. dotnet test (all xUnit projects)
  4. docker build (each service Dockerfile) — verifies image compiles, no push
```

---

## Phase 10 — Deployment

**Goal:** All services live in the cloud, end-to-end functional.

### Infrastructure to provision:

| Resource | Service | Purpose |
|----------|---------|---------|
| Supabase PostgreSQL | CatalogDb + BookingDb schemas | Relational data |
| Azure Cache for Redis | Redis | Hotel search cache |
| MongoDB Atlas (free) or Azure Cosmos DB | hotelReviews collection | Comments |
| CloudAMQP (free) or Azure Service Bus | RabbitMQ | Message broker |
| Supabase Auth | IAM | JWT issuer |
| Azure App Services (×5) | HotelService, CommentsService, NotificationService, AiAgentService, ApiGateway | Hosting |
| Azure App Logic / Google Cloud Scheduler | Notification cron | Nightly capacity job |

### Deployment steps:
1. Set all connection strings as environment variables / Azure App Service config (never committed to git)
2. Run EF Core migrations: `dotnet ef database update` for CatalogDb and BookingDb (both from HotelService, targeting Supabase PostgreSQL)
3. Seed MongoDB with sample hotel review documents
4. Deploy each service independently to its own Azure App Service
5. Configure Ocelot downstream URLs to point to production App Service URLs
6. Verify end-to-end: search → book → check RabbitMQ consumer fires → check cron job endpoint

---

## Deliverables Checklist

- [ ] Public GitHub repository
- [ ] README with:
  - [ ] Deployed URLs for all services
  - [ ] Design decisions and documented assumptions
  - [ ] ER diagram (from `docs/Database-Design-ER-Modeling.md`)
  - [ ] Link to 5-minute presentation video
- [ ] Dockerfiles in every service folder
- [ ] GitHub Actions CI pipeline passing
- [ ] All services deployed and reachable

---

## Current Status

| Phase | Description | Status |
|-------|-------------|--------|
| Phase 1 | Shared Infrastructure + service restructure | ✅ Done |
| Phase 2 | Hotel Service (Admin + Search + Booking) | ✅ Done |
| Phase 3 | Comments Service | ✅ Done |
| Phase 4 | Notification Service | ✅ Done |
| Phase 5 | AI Agent Service | ✅ Done |
| Phase 6 | API Gateway | ✅ Done |
| Phase 7 | Frontend UI (React) | ✅ Done |
| Phase 8 | Cross-Cutting Concerns | ✅ Done |
| Phase 9 | Dockerfiles & CI/CD | ✅ Done |
| Phase 10 | Deployment | ✅ Done — all 5 services + gateway live on Azure Italy North App Services |
