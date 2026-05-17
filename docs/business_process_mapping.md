# Phase 2: Business Process Mapping & API Contracts

This document serves as the foundational blueprint for the Hotel Booking System. Part 1 explicitly maps the business processes, actors, components, and data flows to facilitate strict Data Flow Diagram (DFD) modeling. Part 2 defines the RESTful API contracts that execute these processes.

---

## Part 1: Business Process Mapping (For DFD Modeling)

### 1. Identified Critical Business Processes (High-Level)

| Process ID | Process Name                                 | Description                                                                                                                                               | Primary Actor(s)         | System Components Involved                                                    |
| :--------- | :------------------------------------------- | :-------------------------------------------------------------------------------------------------------------------------------------------------------- | :----------------------- | :---------------------------------------------------------------------------- |
| **BP-01**  | **Hotel Inventory Management**               | Administrators add or update room capacities and availability status for specific date ranges.                                                            | Hotel Admin              | API Gateway, Hotel Service, SQL Database                                |
| **BP-02**  | **Hotel Search & Caching**                   | Users query hotel availability. The system implements a cache-aside pattern and applies dynamic pricing based on auth status.                             | User (Guest / Logged-in) | API Gateway, Hotel Service, Redis Cache, SQL Database                  |
| **BP-03**  | **Room Booking & Event-Driven Notification** | Users reserve a room. The system ensures transaction safety via optimistic concurrency, decrements inventory, and asynchronously triggers a notification. | Authenticated User       | API Gateway, Hotel Service, SQL Database, RabbitMQ, Notification Service |
| **BP-04**  | **Comments & Analytics Retrieval**           | Users view textual reviews and aggregated per-category scores for a specific hotel.                                                                       | User                     | API Gateway, Comments Service, NoSQL Database                           |
| **BP-05**  | **AI Conversational Booking**                | Users interact with an AI to search and book via natural language, with optional clarifying dialogue and a mandatory 2-step confirmation.                 | Authenticated User       | API Gateway, AI Agent Service, Hotel System Facade, Hotel Service      |
| **BP-06**  | **Nightly Capacity Alert**                   | A scheduled cron job evaluates upcoming inventory and alerts admins if capacity drops below 20% for the next month.                                       | System Scheduler (Time)  | Cloud Scheduler, Notification Service, Hotel Service, SQL Database            |
| **BP-07**  | **Queue-Based Reservation Notification**     | The Notification Service consumes new reservation events from RabbitMQ and dispatches confirmation messages to users.                                     | System (Event-Driven)    | RabbitMQ, Notification Service                                                |
| **BP-08**  | **User Registration (Sign-Up)**              | A new user creates an account via the IAM provider (Supabase Auth) using the frontend SDK. On success the user is automatically signed in and receives a JWT, unlocking the 15% member discount. _(Assumption — see BP-08 detail below.)_ | Guest User (Unauthenticated) | React Frontend, Supabase Auth (IAM) |

---

### 2. Detailed Process Breakdown (Data Flow Specifications)

#### BP-01: Hotel Inventory Management

| Step | Action                                                                 | System Component              | Required Data (Data Flow)                                                                   |
| :--- | :--------------------------------------------------------------------- | :---------------------------- | :------------------------------------------------------------------------------------------ |
| 1.1  | Admin requests to add or update room availability.                     | API Gateway (Ocelot)          | `Admin JWT`, `HotelId`, `RoomTypeId`, `StartDate`, `EndDate`, `AvailableCount`, `IsAvailable` |
| 1.2  | Gateway validates JWT (Admin role) and routes request.                 | API Gateway -> Hotel Service  | `Validated Token`, `Inventory Payload`                                                      |
| 1.3  | Validate preconditions (`StartDate < EndDate`, `AvailableCount >= 0`). | Hotel Service                 | `Inventory Payload`                                                                         |
| 1.4  | Update or insert inventory records in the database.                    | Hotel Service -> SQL DB       | `SQL UPDATE / INSERT Command`                                                               |
| 1.5  | Return success or validation error to UI.                              | Hotel Service -> UI           | `HTTP 200 OK` or `HTTP 400 Bad Request`                                                     |

---

#### BP-02: Hotel Search & Caching

| Step | Action                                                     | System Component              | Required Data (Data Flow)                                             |
| :--- | :--------------------------------------------------------- | :---------------------------- | :-------------------------------------------------------------------- |
| 2.1  | User submits search parameters.                            | API Gateway                   | `Destination`, `StartDate`, `EndDate`, `GuestCount`, `JWT (Optional)` |
| 2.2  | Query distributed cache for availability.                  | Hotel Service -> Redis Cache  | `Search Key (Dest + Dates + GuestCount)`                              |
| 2.3  | **If Cache Miss:** Query SQL database for vacant rooms.    | Hotel Service -> SQL DB       | `SQL SELECT WHERE IsAvailable = true AND AvailableCount > 0 AND MaxGuests >= guestCount AND dates overlap` |
| 2.4  | **If Cache Miss:** Populate cache with result set and TTL. | Hotel Service -> Redis Cache  | `Hotel Result Set`                                                    |
| 2.5  | Evaluate User Auth status for Pricing Strategy.            | Hotel Service                 | `Auth Status (JWT present?)`, `Base Prices`                           |
| 2.6  | Apply 15% discount if JWT is valid (Strategy Pattern).     | Hotel Service                 | `Auth Status`, `Base Prices` -> `Discounted Prices`                   |
| 2.7  | Return paginated results (with or without discount).       | Hotel Service -> UI           | `Paginated JSON Hotel List` (includes `coordinates` for map)          |

---

#### BP-03: Room Booking & Event-Driven Notification

| Step | Action                                                                                | System Component                  | Required Data (Data Flow)                                                                 |
| :--- | :------------------------------------------------------------------------------------ | :-------------------------------- | :---------------------------------------------------------------------------------------- |
| 3.1  | User submits booking request (with `RowVersion` fetched from prior hotel detail GET). | API Gateway                       | `User JWT`, `HotelId`, `RoomId`, `StartDate`, `EndDate`, `GuestCount`, `RowVersion Token` |
| 3.2  | Gateway validates JWT and routes to Hotel Service.                                    | API Gateway -> Hotel Service      | `Validated Token`, `Booking Payload`                                                      |
| 3.3  | Execute Optimistic Concurrency check and atomically decrement capacity.               | Hotel Service -> SQL DB           | `SQL UPDATE WHERE RowVersion = X`                                                         |
| 3.4  | **If RowVersion mismatch:** Return 409 Conflict (overbooking prevented).              | Hotel Service -> UI               | `HTTP 409 Conflict Response`                                                              |
| 3.5  | Create and publish `ReservationCreatedEvent` to RabbitMQ.                             | Hotel Service -> RabbitMQ         | `ReservationCreatedEvent (JSON)`                                                          |
| 3.6  | Return immediate booking confirmation to user.                                        | Hotel Service -> UI               | `HTTP 200 OK`, `{ bookingId, status: "Confirmed" }`                                       |
| 3.7  | Notification Service consumes event from queue.                                       | RabbitMQ -> Notification Service  | `ReservationCreatedEvent (JSON)`                                                          |
| 3.8  | Log booking confirmation to console (simulated notification — no actual email/SMS).   | Notification Service              | `UserId`, `BookingId`, `HotelName`, `Dates`                                               |

---

#### BP-04: Comments & Analytics Retrieval

| Step | Action                                                                                            | System Component             | Required Data (Data Flow)                                           |
| :--- | :------------------------------------------------------------------------------------------------ | :--------------------------- | :------------------------------------------------------------------ |
| 4.1  | User requests to view comments for a hotel.                                                       | API Gateway                  | `HotelId`                                                           |
| 4.2  | Query unstructured review documents from NoSQL database.                                          | Comments Service -> NoSQL DB | `HotelId`                                                           |
| 4.3  | Aggregate per-category scores (Cleanliness, Staff, Facilities, Location/Condition, Eco-Friendly). | Comments Service             | `Raw NoSQL Documents`                                               |
| 4.4  | Return paginated comment list and analytics graph data.                                           | Comments Service -> UI       | `JSON Analytics Object` (with `categoryBreakdown` and `comments[]`) |

#### BP-04b: Comment Submission (Assumption)

> **Architectural Decision:** Although the project mock-ups only show the comments display UI, a `POST /api/v1/comments/{hotelId}` endpoint is implemented to populate the NoSQL database and maintain dynamic comment data. Based on the "verified" and stay-duration labels in the mock-ups, only authenticated users may submit reviews.

| Step | Action                                                                             | System Component              | Required Data (Data Flow)                                                    |
| :--- | :--------------------------------------------------------------------------------- | :---------------------------- | :--------------------------------------------------------------------------- |
| 4b.1 | Authenticated user submits a review for a hotel.                                   | API Gateway                   | `User JWT`, `HotelId`, `rating`, `text`, `categoryRatings`, `tripType`       |
| 4b.2 | Gateway validates JWT and routes to Comments Service.                              | API Gateway -> Comments Service | `Validated Token`, `Review Payload`                                        |
| 4b.3 | Comments Service upserts the review document in NoSQL and recalculates aggregates. | Comments Service -> NoSQL DB  | `MongoDB upsert on hotelId document — append to reviews[], update scores`    |
| 4b.4 | Return confirmation to client.                                                     | Comments Service -> UI        | `HTTP 201 Created`, `{ reviewId }`                                           |

---

#### BP-05: AI Conversational Booking

| Step | Action                                                                                                             | System Component                   | Required Data (Data Flow)                                              |
| :--- | :----------------------------------------------------------------------------------------------------------------- | :--------------------------------- | :--------------------------------------------------------------------- |
| 5.1  | User submits natural language prompt.                                                                              | API Gateway                        | `User JWT`, `sessionId`, `Text String`                                 |
| 5.2  | Parse intent and attempt to extract parameters (Destination, Dates, GuestCount).                                   | AI Agent Service                   | `Text String`                                                          |
| 5.3  | **If parameters incomplete:** Return clarifying question to UI (e.g., "Any preferences for rating or amenities?"). | AI Agent Service -> UI             | `AI Dialogue Response` (`requiresConfirmation: false`)                 |
| 5.4  | **If parameters complete:** Execute internal hotel search via Facade.                                              | AI Agent Service -> Hotel Service  | `Internal HTTP GET /api/v1/search/hotels`                              |
| 5.5  | Format search results and present hotel options; ask for booking confirmation.                                     | AI Agent Service -> UI             | `AI Dialogue Response` (`requiresConfirmation: true`, `contextState`)  |
| 5.6  | User confirms selection ("Yes, book it").                                                                          | UI -> AI Agent Service             | `sessionId`, `userMessage: "Yes, book it"`, `contextState` echoed back |
| 5.7  | Execute internal room booking via Facade using stored `contextState`.                                              | AI Agent Service -> Hotel Service  | `Internal HTTP POST /api/v1/bookings`                                  |
| 5.8  | Return final booking confirmation to user.                                                                         | AI Agent Service -> UI             | `"Your reservation is confirmed!"`                                     |

---

#### BP-06: Nightly Capacity Alert (Scheduled Cron)

| Step | Action                                                             | System Component                        | Required Data (Data Flow)                        |
| :--- | :----------------------------------------------------------------- | :-------------------------------------- | :----------------------------------------------- |
| 6.1  | Trigger nightly scheduled job.                                                    | Cloud Scheduler -> Notification Service           | `Cron Trigger Signal (POST /api/v1/notifications/capacity-check)`                 |
| 6.2  | Call Hotel Service internal capacity report endpoint.                             | Notification Service -> Hotel Service             | `GET /api/v1/admin/hotels/capacity-report?days=30`                                |
| 6.3  | Hotel Service executes SQL aggregate query and returns low-capacity hotel list.   | Hotel Service -> SQL DB -> Notification Service   | `SQL Aggregate Query (next 30 days, AvailableCount/TotalCount < 0.20)`            |
| 6.4  | Clear previous run's alert snapshot from NotificationAlerts table.                | Notification Service -> NotificationsDb           | `DELETE FROM "NotificationAlerts"` (bulk — replaces stale snapshot with fresh data) |
| 6.5  | Persist new NotificationAlert row for each low-capacity InventoryBlock found.     | Notification Service -> NotificationsDb           | `INSERT NotificationAlert (HotelId, HotelName, RoomTypeName, AvailableCount, TotalCount, CapacityRatio, StartDate, EndDate)` |
| 6.6  | Admin UI polls GET /api/v1/notifications to retrieve current alert list.          | Admin Browser -> API Gateway -> Notification Service | `GET /gateway/v1/notifications` (paginated, ordered by CreatedAt desc)          |
| 6.7  | Admin views alert cards; marks individual alerts as read.                         | Admin Browser -> API Gateway -> Notification Service | `PATCH /gateway/v1/notifications/{id}/read`; unread badge count updated in UI  |

---

#### BP-08: User Registration / Sign-Up (Assumption)

> **Architectural Decision / Assumption:** The project mock-ups do not include a sign-up screen. However, without self-registration a test user cannot obtain a valid JWT, which means the 15% member discount flow and the authenticated booking flow cannot be exercised end-to-end. A Sign-Up page is therefore implemented in the React frontend. Account creation is handled entirely by the IAM provider (Supabase Auth) using the client-side SDK — **no custom registration endpoint exists in any backend service**. This is consistent with the constraint that local auth implementations are strictly forbidden. This assumption will be documented in the project README.

| Step | Action | System Component | Required Data (Data Flow) |
| :--- | :--------------------------------------------------------------------- | :---------------------------- | :------------------------------------------------------------------------------------------ |
| 8.1  | Guest navigates to the Sign-Up page and submits email + password. | React Frontend | `email`, `password` (min 6 chars), `confirmPassword` |
| 8.2  | Frontend validates inputs client-side (passwords match, length). | React Frontend | `form state` |
| 8.3  | Frontend calls `supabase.auth.signUp({ email, password })` via the Supabase Auth JS SDK. | React Frontend → Supabase Auth | `email`, `password` |
| 8.4a | **If email confirmation is disabled (Supabase project setting):** Supabase returns an active session immediately. Frontend calls `supabase.auth.signIn()` and redirects user to the home page as an authenticated user. | Supabase Auth → React Frontend | `JWT (access_token)`, `Session` |
| 8.4b | **If email confirmation is required (Supabase project setting):** Supabase sends a confirmation email. Frontend displays a "Check your email" screen. User clicks the link, confirms account, then navigates to the Sign-In page. | Supabase Auth → User Email → React Frontend | `Confirmation link` |
| 8.5  | On successful session creation, user JWT is stored in memory and injected as `Authorization: Bearer <token>` on all subsequent API calls via the Axios request interceptor. | React Frontend | `JWT (access_token)` |

---

#### BP-07: Queue-Based Reservation Notification (Event-Driven)

> **Note:** This is architecturally distinct from BP-06. BP-06 is a scheduled pull; BP-07 is an asynchronous event-driven push triggered by new bookings.

| Step | Action                                                                                       | System Component                 | Required Data (Data Flow)                        |
| :--- | :------------------------------------------------------------------------------------------- | :------------------------------- | :----------------------------------------------- |
| 7.1  | Notification Service is subscribed and listening to RabbitMQ queue.                          | Notification Service             | `(Always-on Queue Consumer)`                     |
| 7.2  | New `ReservationCreatedEvent` arrives on the queue (published by Hotel Service).             | RabbitMQ -> Notification Service | `ReservationCreatedEvent (JSON)`                 |
| 7.3  | Deserialize event and extract user and booking details.                                      | Notification Service             | `UserId`, `BookingId`, `HotelId`, `Dates`        |
| 7.4  | Dispatch reservation confirmation message to user (Email/SMS simulation, logged to console). | Notification Service             | `User Contact Info`, `Booking Confirmation Text` |
| 7.5  | ACK message on success; NACK and requeue on failure.                                         | Notification Service -> RabbitMQ | `ACK / NACK Signal`                              |

---

## Part 2: RESTful API Contracts

> **Note:** All endpoints conform to standard REST conventions, support versioning (`/v1/`) and pagination where applicable, and represent downstream service routes. External client access is routed through the Ocelot API Gateway, which validates JWT tokens on protected routes before forwarding. Downstream services also validate the JWT to read user identity claims.

---

### 1. Hotel Service — Admin

#### `POST /api/v1/admin/inventory`

- **Description:** Creates or updates room availability for a given hotel and date range. Sets the room status (vacant/occupied) and available count.
- **Security:** Authenticated (Admin Role). Requires Bearer JWT.
- **Precondition:** `StartDate` < `EndDate` AND `AvailableCount` >= 0.
- **Postcondition:** SQL inventory table updated for the given `HotelId` and date range. On **creation** of a new `InventoryBlock`, `TotalCount` is set to the value of `availableCount` (the admin's "Oda Adedi" input) and remains immutable. On **update** of an existing block, only `AvailableCount` and `IsAvailable` are modified; `TotalCount` is preserved to allow the nightly cron ratio check (`AvailableCount / TotalCount < 0.20`).
  **Request Body:**

```json
{
  "hotelId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "roomTypeId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "startDate": "2026-06-01T00:00:00Z",
  "endDate": "2026-06-15T00:00:00Z",
  "availableCount": 10,
  "isAvailable": true
}
```

**Responses:**

- `200 OK` — Inventory successfully updated.
- `400 Bad Request` — Precondition violated (e.g., `StartDate >= EndDate` or negative count).
- `401 Unauthorized` — Missing or invalid Admin JWT.

---

#### `POST /api/v1/admin/hotels/{hotelId}/roomtypes`

- **Description:** Creates a new room type for a hotel (e.g., "Standard", "Aile"). Required before inventory can be set for that type. Populates the "Oda Tipi" admin dropdown.
- **Security:** Authenticated (Admin Role).
- **Request Body:**

```json
{ "typeName": "Standard", "maxGuests": 2, "basePricePerNight": 8500.00 }
```

**Responses:** `201 Created` — Room type created. `401 Unauthorized`.

---

#### `GET /api/v1/admin/hotels/{hotelId}/roomtypes`

- **Description:** Lists all room types for a hotel. Used by the admin UI to populate the "Oda Tipi" dropdown before submitting inventory.
- **Security:** Authenticated (Admin Role).

---

### 2. Hotel Service — Search

#### `GET /api/v1/search/hotels`

- **Description:** Retrieves available hotels matching the search criteria. Implements Redis cache-aside. Applies a 15% discount to prices if a valid user JWT is present in the `Authorization` header.
- **Security:** Public. JWT is optional (used only for discount pricing).
- **Caching:** Checks Redis first using a composite cache key (`destination + startDate + endDate + guestCount`). Falls back to SQL on cache miss and repopulates cache with TTL.
  **Query Parameters:**

| Parameter     | Type     | Required | Description                    |
| :------------ | :------- | :------- | :----------------------------- |
| `destination` | string   | Yes      | City or region name            |
| `startDate`   | ISO 8601 | Yes      | Check-in date                  |
| `endDate`     | ISO 8601 | Yes      | Check-out date                 |
| `guestCount`  | int      | Yes      | Number of guests               |
| `page`        | int      | No       | Page number (default: 1)       |
| `pageSize`    | int      | No       | Results per page (default: 10) |

**Response (200 OK):**

```json
{
  "page": 1,
  "totalPages": 5,
  "totalRecords": 45,
  "discountApplied": true,
  "data": [
    {
      "hotelId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "name": "Hyde Bodrum",
      "location": "Torba, Bodrum",
      "coordinates": { "lat": 37.034, "lng": 27.43 },
      "pricePerNight": 10948.0,
      "availableRooms": 2,
      "rating": 9.6,
      "totalReviews": 163
    }
  ]
}
```

> **Note:** `coordinates` is required to support the "Haritada göster" (Show on Map) feature in the UI. All results must include `lat`/`lng` values.

**Responses:**

- `200 OK` — Search results returned (may be empty array if no matches).
- `400 Bad Request` — Missing or invalid query parameters.

---

### 3. Hotel Service — Booking

#### `GET /api/v1/hotels/{hotelId}/rooms/{roomTypeId}`

- **Description:** Fetches room details including the current `RowVersion` token and `inventoryId`. The client must call this endpoint before submitting a booking to obtain the `inventoryId` and `rowVersion` required for optimistic concurrency control.
- **Security:** Public.
- **Query Parameters (optional):** `startDate`, `endDate` — when provided, the returned inventory block must fully cover the requested stay dates. Recommended: pass the same dates used in the search.
  **Response (200 OK):**

```json
{
  "roomTypeId": "22222222-0000-0000-0000-000000000001",
  "hotelId": "11111111-0000-0000-0000-000000000001",
  "roomType": "Standard",
  "pricePerNight": 350.0,
  "availableCount": 10,
  "inventoryId": "33333333-0000-0000-0000-000000000001",
  "rowVersion": 3628514
}
```

---

#### `POST /api/v1/bookings`

- **Description:** Creates a reservation. Executes an optimistic concurrency check using the `rowVersion` token to prevent overbooking. On success, decrements capacity in SQL and publishes a `ReservationCreatedEvent` to RabbitMQ.
- **Security:** Authenticated (User). Requires Bearer JWT.
- **Precondition:** Valid User JWT AND `inventoryId` + `rowVersion` token provided (from prior `GET /api/v1/hotels/{hotelId}/rooms/{roomTypeId}`) AND `StartDate` < `EndDate` AND `GuestCount` >= 1 AND requested dates fall within the selected inventory block's date range.
- **Postcondition:** `AvailableCount -= 1` in `InventoryBlocks` (SQL) AND `Booking` record created in `BookingDbContext` AND `ReservationCreatedEvent` published to RabbitMQ.
  **Request Body:**

```json
{
  "hotelId": "11111111-0000-0000-0000-000000000001",
  "roomTypeId": "22222222-0000-0000-0000-000000000001",
  "inventoryId": "33333333-0000-0000-0000-000000000001",
  "startDate": "2026-05-15",
  "endDate": "2026-05-20",
  "guestCount": 2,
  "rowVersion": 3628514
}
```

**Responses:**

- `200 OK` — Booking confirmed.

```json
{ "bookingId": "booking-guid-456", "status": "Confirmed" }
```

- `401 Unauthorized` — Missing or invalid User JWT.
- `409 Conflict` — `RowVersion` mismatch; room capacity changed between read and write. Client should re-fetch room details and retry.

---

### 4. Comments Service

#### `GET /api/v1/comments/{hotelId}`

- **Description:** Fetches aggregated per-category review scores and paginated comment text for a specific hotel from NoSQL storage.
- **Security:** Public.

  **Path Parameters:**

| Parameter | Type | Description                        |
| :-------- | :--- | :--------------------------------- |
| `hotelId` | Guid | The unique identifier of the hotel |

**Query Parameters:**

| Parameter  | Type | Required | Description                           |
| :--------- | :--- | :------- | :------------------------------------ |
| `page`     | int  | No       | Page number for comments (default: 1) |
| `pageSize` | int  | No       | Comments per page (default: 10)       |

**Response (200 OK):**

```json
{
  "hotelId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "totalReviews": 163,
  "overallScore": 9.2,
  "categoryBreakdown": {
    "cleanliness": 9.6,
    "staff": 9.6,
    "facilities": 9.4,
    "locationCondition": 9.6,
    "ecoFriendly": 9.4
  },
  "page": 1,
  "totalPages": 17,
  "comments": [
    {
      "author": "Simge",
      "tripType": "4 gecelik seyahat",
      "text": "Great location and very clean.",
      "rating": 8.0,
      "date": "2025-06-16T00:00:00Z"
    }
  ]
}
```

> **Note:** `categoryBreakdown` maps to the 5 categories shown in the PDF mockup: Temizlik (cleanliness), Personel ve servis (staff), İmkân ve özellikler (facilities), Konaklama yerinin durumu (locationCondition), and Çevre dostluğu (ecoFriendly).

**Responses:**

- `200 OK` — Comments and analytics returned.
- `404 Not Found` — No hotel found for the given `hotelId`.

---

#### `POST /api/v1/comments/{hotelId}` _(Assumption — see BP-04b)_

- **Description:** Submits a new review for a hotel. Upserts the NoSQL document for the given hotel, appends the review to the `reviews[]` array, and recalculates the aggregate `overallScore` and all `categoryScores`. This endpoint is not explicitly shown in the project mock-ups but is implemented to populate the NoSQL database and maintain data consistency. The "verified" and stay-duration labels in the mock-ups confirm that only authenticated users should be allowed to post.
- **Security:** Authenticated (User). Requires Bearer JWT — the IAM service validates the user session before the review is accepted.
- **Precondition:** Valid JWT AND `hotelId` exists AND `rating` is between 1.0 and 10.0 AND `text` is non-empty.
- **Postcondition:** Review appended to `reviews[]` in NoSQL document AND `overallScore` and `categoryScores` recalculated.

**Request Body:**

```json
{
  "rating": 8.5,
  "text": "Great location, very clean rooms.",
  "tripType": "4-night stay",
  "categoryRatings": {
    "cleanliness": 9.0,
    "staff": 8.5,
    "facilities": 8.0,
    "locationCondition": 9.0,
    "ecoFriendly": 8.0
  }
}
```

**Responses:**

- `201 Created` — Review submitted. Returns `{ "reviewId": "rev-uuid-..." }`.
- `401 Unauthorized` — Missing or invalid JWT.
- `404 Not Found` — No hotel document found for the given `hotelId`.

---

### 5. AI Agent Service

#### `POST /api/v1/ai/chat`

- **Description:** Stateless natural language endpoint for the AI chat window. Handles the full conversational booking flow: intent parsing → optional clarifying questions → hotel search → 2-step booking confirmation. State is maintained client-side via the `contextState` field echoed back in each request. Full conversation history is sent via `messages[]` so the backend can pass prior turns directly to the LLM (`Gemini contents[]`), giving the AI memory without server-side session storage.
- **Security:** Authenticated (User). Requires Bearer JWT.
- **Note:** Real-time messaging is NOT required. Standard HTTP request/response is sufficient.
  **Request Body:**

```json
{
  "sessionId": "sess-abc-123",
  "userMessage": "Find me a hotel in Izmir for next weekend for 2 guests.",
  "messages": [],
  "contextState": null
}
```

> **`messages[]` field:** Array of all prior turns in the conversation (role + content). The frontend appends each user/assistant turn to this array and echoes the full history on every request. The backend passes the array directly to the LLM provider (e.g., Gemini `contents[]`) so the model can reason over the full dialogue. On the first message, `messages` is an empty array.

> **Confirmation turn request** (client echoes back full `messages[]` history + `contextState`):

```json
{
  "sessionId": "sess-abc-123",
  "userMessage": "Yes, book it",
  "messages": [
    { "role": "user", "content": "Find me a hotel in Izmir for next weekend for 2 guests." },
    { "role": "assistant", "content": "I found 3 great hotels in Izmir..." }
  ],
  "contextState": {
    "pendingAction": "BOOK",
    "targetHotelId": "11111111-0000-0000-0000-000000000001",
    "targetRoomTypeId": "22222222-0000-0000-0000-000000000001",
    "targetInventoryId": "33333333-0000-0000-0000-000000000001",
    "startDate": "2026-05-16",
    "endDate": "2026-05-18",
    "guestCount": 2,
    "rowVersion": 3628514
  }
}
```

**Response — Clarifying Question (incomplete parameters):**

```json
{
  "reply": "Great! Any preferences for hotel rating, price range, or amenities?",
  "requiresConfirmation": false,
  "contextState": {
    "pendingAction": "CLARIFY",
    "partialParams": {
      "destination": "Izmir",
      "guestCount": 2
    }
  }
}
```

**Response — Hotel Options Presented (confirmation required):**

```json
{
  "reply": "I found 3 great hotels in Izmir for next weekend. The top choice is Swissôtel Büyük Efes Izmir at 3,100 TL/night. Would you like me to confirm a reservation there?",
  "requiresConfirmation": true,
  "contextState": {
    "pendingAction": "BOOK",
    "targetHotelId": "11111111-0000-0000-0000-000000000001",
    "targetRoomTypeId": "22222222-0000-0000-0000-000000000001",
    "targetInventoryId": "33333333-0000-0000-0000-000000000001",
    "startDate": "2026-05-16",
    "endDate": "2026-05-18",
    "guestCount": 2,
    "rowVersion": 3628514
  }
}
```

**Response — Booking Confirmed:**

```json
{
  "reply": "Your reservation at Swissôtel Büyük Efes Izmir from May 16–18 is confirmed! Booking ID: booking-guid-456.",
  "requiresConfirmation": false,
  "contextState": null
}
```

**Responses:**

- `200 OK` — AI response returned (all dialogue turns use 200).
- `401 Unauthorized` — Missing or invalid User JWT.
- `409 Conflict` — Booking step failed due to race condition; AI will inform user and suggest retry.

---

### 6. Internal AMQP Contract (Notification Service — Queue Consumer)

> **Note:** This is not an HTTP endpoint. It documents the message contract consumed by the Notification Service from RabbitMQ, published by the Hotel Service.

**Exchange:** `hotel.reservations`
**Queue:** `reservation.created`
**Routing Key:** `reservation.created`

**Message Payload (`ReservationCreatedEvent`):**

```json
{
  "eventId": "evt-uuid-789",
  "bookingId": "booking-guid-456",
  "userId": "user-uuid-101",
  "hotelId": "11111111-0000-0000-0000-000000000001",
  "hotelName": "The Grand Manhattan Hotel",
  "roomTypeId": "22222222-0000-0000-0000-000000000001",
  "checkInDate": "2026-05-15",
  "checkOutDate": "2026-05-20",
  "guestCount": 2,
  "totalAmount": 1750.00,
  "publishedAt": "2026-05-10T14:30:00Z"
}
```

**Consumer Behaviour:**

- On successful processing → send `ACK` to remove message from queue.
- On processing failure → send `NACK` to requeue message for retry.
- Simulated output: Log confirmation message to console (no actual email/SMS integration).

---

### 7. Hotel Service — User Bookings

#### `GET /api/v1/bookings`

- **Description:** Returns all bookings for the currently authenticated user, ordered by creation date descending.
- **Security:** Authenticated (User JWT). UserId extracted from JWT `sub` claim.

**Response (200 OK):**

```json
[
  {
    "bookingId": "booking-guid-456",
    "hotelId": "11111111-0000-0000-0000-000000000001",
    "roomTypeId": "22222222-0000-0000-0000-000000000001",
    "checkInDate": "2026-05-15",
    "checkOutDate": "2026-05-20",
    "guestCount": 2,
    "totalAmount": 1750.00,
    "status": "Confirmed",
    "createdAt": "2026-05-10T14:30:00Z"
  }
]
```

---

#### `DELETE /api/v1/bookings/{bookingId}`

- **Description:** Cancels a booking. Only the owner of the booking (matched via JWT `sub`) can cancel it.
- **Security:** Authenticated (User JWT).
- **Postcondition:** Booking `Status` updated to `"Cancelled"` in SQL. Inventory is NOT automatically restored (manual admin operation if needed).

**Responses:**
- `200 OK` — Booking cancelled.
- `401 Unauthorized` — Missing or invalid JWT.
- `403 Forbidden` — JWT user does not own this booking.
- `404 Not Found` — Booking not found.

---

### 8. Hotel Service — Admin Extras

#### `DELETE /api/v1/admin/hotels/{hotelId}`

- **Description:** Deletes a hotel (and its room types and inventory blocks) from the catalog.
- **Security:** Authenticated (Admin Role).

**Responses:** `200 OK` — Hotel deleted. `401 Unauthorized`. `404 Not Found`.

---

#### `POST /api/v1/admin/cache/clear`

- **Description:** Clears Redis cache entries matching a pattern. Used after admin inventory or hotel updates to prevent stale search results from being served.
- **Security:** Authenticated (Admin Role).

**Request Body:**
```json
{ "pattern": "v2:search:*" }
```

**Responses:** `200 OK` — Cache entries cleared.

---

### 9. Notification Service

#### `GET /api/v1/notifications`

- **Description:** Returns the current snapshot of low-capacity hotel alerts stored in the `NotificationAlerts` table. Ordered by `CreatedAt` descending (most recent first). Each run of the nightly cron job replaces the previous snapshot — only the latest run's results are stored at any time. The admin panel polls this endpoint every 60 seconds.
- **Security:** Authenticated (Bearer JWT — gateway validates).

**Query Parameters:**

| Parameter | Type | Required | Description |
|:---|:---|:---|:---|
| `page` | int | No | Page number (default: 1) |
| `pageSize` | int | No | Results per page (default: 50) |

**Response (200 OK):**

```json
{
  "page": 1,
  "totalPages": 1,
  "totalRecords": 4,
  "data": [
    {
      "notificationId": "uuid-...",
      "hotelId": "11111111-0000-0000-0000-000000000001",
      "hotelName": "Hyde Bodrum",
      "roomTypeName": "Standard",
      "availableCount": 1,
      "totalCount": 10,
      "capacityRatio": 0.10,
      "startDate": "2026-06-01",
      "endDate": "2026-06-15",
      "createdAt": "2026-05-17T02:00:00Z",
      "isRead": false
    }
  ]
}
```

---

#### `PATCH /api/v1/notifications/{notificationId}/read`

- **Description:** Marks a single notification alert as read. The admin panel calls this when the admin clicks on an alert. The unread badge count in the UI decrements accordingly.
- **Security:** Authenticated (Bearer JWT).

**Responses:**
- `200 OK` — Alert marked as read.
- `404 Not Found` — Alert not found.

---

#### `POST /api/v1/notifications/capacity-check`

- **Description:** Triggers the nightly capacity alert job manually. Intended to be called by the cloud scheduler (Azure App Logic / Google Cloud Scheduler) on a nightly cron schedule. Can also be triggered manually from the Azure portal for testing.
- **Security:** No authentication required (called by internal scheduler).
- **Query Parameters:** `days` (int, optional, default: 30) — number of days ahead to check.

**Responses:**
- `200 OK` — Job ran successfully. Response includes count of alerts saved.
