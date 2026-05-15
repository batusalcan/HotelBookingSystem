# Hotel Booking System

**SE 4458 Final Project — Group 1**
A Hotels.com-like microservices platform built with C# .NET Core 8.

---

## Deployed URLs

| Service | URL |
|---|---|
| **API Gateway** | https://hotel-gateway-axhpduheewacbvhc.italynorth-01.azurewebsites.net |
| **Hotel Service** | https://hotel-hotelservice-aje8f7f7dqb5f0a5.italynorth-01.azurewebsites.net |
| **Comments Service** | https://hotel-comments-c9ejhwftbch5eqey.italynorth-01.azurewebsites.net |
| **AI Agent Service** | https://hotel-aiagent-g2avhjfcfyhqcsfd.italynorth-01.azurewebsites.net |
| **Notification Service** | https://hotel-notification-eccyh5cpdxhpg9b2.italynorth-01.azurewebsites.net |
| **Frontend** | https://hotel-booking-system-henna.vercel.app |

All services are deployed to **Azure App Service (Italy North)** under the B1 plan.

### API Gateway Swagger
https://hotel-gateway-axhpduheewacbvhc.italynorth-01.azurewebsites.net/swagger

---

## Demo Video

> **[Video link — to be added]**

---

## Architecture

### Microservices + Database-per-Service

```
Client (React/Vite)
       │
       ▼
  API Gateway (Ocelot)
  ├── /gateway/v1/admin/*        → HotelService  (admin inventory)
  ├── /gateway/v1/search/hotels  → HotelService  (search, Redis cache)
  ├── /gateway/v1/bookings       → HotelService  (booking + RabbitMQ publish)
  ├── /gateway/v1/hotels/*       → HotelService  (hotel/room detail)
  ├── /gateway/v1/comments/*     → CommentsService (MongoDB)
  └── /gateway/v1/ai/chat        → AiAgentService (Gemini AI)

NotificationService
  ├── [always-on] RabbitMQ consumer → reservation.created events
  └── [nightly]   POST /api/v1/notifications/capacity-check  (< 20% capacity alert)
```

### Tech Stack

| Concern | Technology |
|---|---|
| Backend | C# .NET Core 8 Web API |
| API Gateway | Ocelot + MMLib.SwaggerForOcelot |
| Auth / IAM | Supabase Auth (JWT Bearer, OIDC) |
| SQL Database | Supabase PostgreSQL (session pooler) |
| NoSQL Database | MongoDB Atlas (Comments Service) |
| Cache | Upstash Redis (search + hotel detail) |
| Message Broker | CloudAMQP RabbitMQ (SSL, custom vhost) |
| Nightly Scheduler | Azure Logic App (capacity-check trigger) |
| AI Model | Google Gemini 1.5 Flash (via `IAiProvider` abstraction) |
| Containerization | Dockerfile per service |
| ORM | EF Core Code-First (separate DbContext per service) |
| Logging | Serilog with Correlation ID enrichment |
| Testing | xUnit |
| CI/CD | GitHub Actions (build, test, docker build verify) |
| Frontend | React + Vite + TypeScript |
| Frontend Hosting | Vercel |

### Design Patterns

| Pattern | Location | Purpose |
|---|---|---|
| Cache-Aside | HotelService / SearchService | Redis first → SQL fallback → repopulate |
| Optimistic Concurrency | HotelService / BookingService | `RowVersion` token prevents overbooking (409 on mismatch) |
| Strategy | HotelService pricing | 15% discount for authenticated users; swappable sorting |
| Singleton | Redis, RabbitMQ connections | One connection pool per service instance |
| Factory Method | NotificationService | Different alert types (booking vs. low-capacity) |
| Facade | AiAgentService | Hides internal REST calls to HotelService |
| Provider Abstraction | AiAgentService (`IAiProvider`) | Switching AI models requires only a new `IAiProvider` class |

---

## Data Models / ER Diagram

Full entity-relationship documentation: [docs/Database-Design-ER-Modeling.md](docs/Database-Design-ER-Modeling.md)

### SQL Tables (Supabase PostgreSQL — HotelService)

**Hotels** — `HotelId` (PK), `Name`, `Destination` (indexed), `Latitude`, `Longitude`, `BaseRating`, `TotalReviews`, `ImageUrl`, `IsActive`

**RoomTypes** — `RoomTypeId` (PK), `HotelId` (FK), `TypeName`, `MaxGuests`, `BasePricePerNight`

**InventoryBlocks** — `InventoryId` (PK), `RoomTypeId` (FK), `StartDate`, `EndDate`, `TotalCount`, `AvailableCount`, `IsAvailable`, `xmin` (PostgreSQL system column, concurrency token)

**Bookings** — `BookingId` (PK), `UserId` (JWT sub, no FK), `HotelId` (soft ref), `RoomTypeId` (soft ref), `CheckInDate`, `CheckOutDate`, `GuestCount`, `TotalAmount`, `Status`, `CreatedAt`

### MongoDB Collection (CommentsService)

`hotelReviews` — document per hotel: `hotelId`, `totalReviews`, `overallScore`, `categoryScores` (cleanliness / staff / facilities / locationCondition / ecoFriendly), `reviews[]`

### Redis Cache Keys

| Key Pattern | TTL | Notes |
|---|---|---|
| `search:{dest}:{start}:{end}:{guests}` | 15 min | Base prices only — discount applied at request time |
| `hotel:detail:{hotelId}` | 60 min | Invalidated on admin inventory update |

---

## Design Decisions

### 1. Supabase PostgreSQL over Azure SQL
Supabase provides a managed PostgreSQL instance with built-in Auth (JWT/OIDC), which eliminated the need to run a separate identity service. The session pooler endpoint is used to work around IPv6 routing limitations in the CI/CD environment.

### 2. Single HotelService (merged from 3 separate services)
Admin, Search, and Booking were merged into one `HotelService` to simplify deployment complexity while keeping the internal layer separation (separate DbContexts: `CatalogDbContext`, `BookingDbContext`). Cross-service FK constraints are avoided — all cross-service references use plain GUIDs (soft references).

### 3. 15% Discount Strategy
Discount prices are **never cached**. Redis stores base prices only. The discount is computed at the service layer when a valid `Authorization: Bearer` JWT header is detected, keeping the cache provider-agnostic.

### 4. Optimistic Concurrency for Overbooking Prevention
`InventoryBlocks.xmin` is PostgreSQL's built-in `xmin` system column mapped as an EF Core concurrency token. The client reads the `rowVersion` when fetching room details and submits it with the booking request. If another booking changes the row in the meantime, EF Core throws `DbUpdateConcurrencyException` → API returns `409 Conflict`.

### 5. AI Provider Abstraction
The `IAiProvider` interface decouples business logic from the Gemini SDK. Switching to a different AI provider (OpenAI, Anthropic, Azure OpenAI) only requires:
1. A new class implementing `IAiProvider`
2. A DI registration change in `Program.cs`

---

## Assumptions

The following decisions are explicit design assumptions not fully specified in the project definition:

### 1. POST /comments requires JWT authentication
The spec only defines `GET /api/v1/comments/{hotelId}` as documented. `POST /api/v1/comments/{hotelId}` was added as an assumption because:
- Data consistency requires knowing who wrote a review
- Prevents spam/anonymous abuse
- Allows verified-guest enforcement in future iterations

### 2. Booking requires JWT authentication
The spec implies booking is for users, but does not explicitly state it is auth-gated. This was enforced because:
- Anti-spam: anonymous bookings would be trivially abused
- Capacity safety: overbooking resolution requires a user identity
- Owner tracking: `Bookings.UserId` (JWT `sub` claim) enables user booking history

### 3. AI chat history sent from client
The `POST /api/v1/ai/chat` body includes `messages[]` (full conversation history) alongside `contextState` (booking parameters). The client is responsible for maintaining and echoing history each turn — the service is stateless.

### 4. NotificationService nightly cron is externally triggered
The nightly capacity check runs when `POST /api/v1/notifications/capacity-check` is called. An Azure Logic App (or equivalent external scheduler) is configured to call this endpoint nightly. The service itself does not host an internal timer.

---

## Issues Encountered

### Supabase Direct Connection — IPv6 Routing Failure
The direct Supabase connection URL (`db.bmtbhqenqjsyzpoidjyd.supabase.co:5432`) fails in both GitHub Actions CI and on macOS with "No route to host." Root cause: IPv6-only DNS resolution in certain environments. **Resolution:** Use Supabase's session pooler endpoint (`aws-1-eu-central-1.pooler.supabase.com:5432`) which routes over IPv4.

### Redis `SyncTimeout` Blocking CI for 5 Seconds
`StackExchange.Redis` defaults: `ConnectTimeout = 5000ms`, `SyncTimeout = 5000ms`. A disconnected Redis in CI caused `PingAsync()` health checks to block for the full SyncTimeout. **Resolution:** Set `ConnectTimeout = 1000`, `SyncTimeout = 2000`.

### MongoDB `ServerSelectionTimeout` Blocking CI for 30 Seconds
MongoDB driver default `ServerSelectionTimeout` is 30 seconds. Health checks and data seeders triggered full waits on unreachable Atlas in CI. **Resolution:** Set `ServerSelectionTimeout = TimeSpan.FromSeconds(3)`.

### RabbitMQ — CloudAMQP Requires VirtualHost + SSL
CloudAMQP uses a custom virtual host (matches username) and requires SSL on port 5671. The default `ConnectionFactory` without these settings is rejected. **Resolution:** Added `VirtualHost` and `SslOption { Enabled = true, ServerName = host }` to all three usages (Publisher, Consumer, HealthCheck).

### Azure Student Plan — West Europe Unavailable
Azure App Service deployments to West Europe failed for the student subscription. **Resolution:** All services deployed to Italy North region (`italynorth-01.azurewebsites.net`).

### ApiGateway — Duplicate `Content` Item in csproj
`dotnet publish` failed with `NETSDK1022: Duplicate 'Content' items were included` because `ocelot.json` was declared with both `<Content Include>` (implicit SDK behavior) and an explicit `<Content Include>` entry. **Resolution:** Changed to `<Content Update="ocelot.json">`.

---

## Running Locally

### Prerequisites
- .NET 8 SDK
- Node.js 20+
- Docker (optional)

### Backend Services

Each service reads secrets from `appsettings.Development.json` (gitignored). Copy the relevant `appsettings.json` as a starting point and fill in your credentials.

```bash
# Run HotelService
cd backend/HotelService
dotnet run

# Run CommentsService
cd backend/CommentsService
dotnet run

# Run AiAgentService
cd backend/AiAgentService
dotnet run

# Run NotificationService
cd backend/NotificationService
dotnet run

# Run API Gateway
cd backend/ApiGateway
dotnet run
```

### Frontend

```bash
cd frontend
cp .env.example .env   # fill in VITE_GATEWAY_URL, VITE_SUPABASE_URL, VITE_SUPABASE_ANON_KEY
npm install
npm run dev
```

### Run Tests

```bash
dotnet test HotelBookingSystem.slnx
```

---

## Repository Structure

```
HotelBookingSystem/
├── backend/
│   ├── ApiGateway/
│   ├── HotelService/
│   ├── CommentsService/
│   ├── NotificationService/
│   └── AiAgentService/
├── frontend/
├── docs/
│   ├── requirements.md
│   ├── business_process_mapping.md
│   ├── Database-Design-ER-Modeling.md
│   └── project-plan.md
├── .github/
│   └── workflows/
│       └── ci.yml
└── README.md
```

---

*Group 1 — SE 4458 Software Architecture — May 2026*
