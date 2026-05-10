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
| 3.8  | Dispatch simulated Email/SMS confirmation to user.                                    | Notification Service              | `User Contact Info`, `Booking Details`                                                    |

---

#### BP-04: Comments & Analytics Retrieval

| Step | Action                                                                                            | System Component             | Required Data (Data Flow)                                           |
| :--- | :------------------------------------------------------------------------------------------------ | :--------------------------- | :------------------------------------------------------------------ |
| 4.1  | User requests to view comments for a hotel.                                                       | API Gateway                  | `HotelId`                                                           |
| 4.2  | Query unstructured review documents from NoSQL database.                                          | Comments Service -> NoSQL DB | `HotelId`                                                           |
| 4.3  | Aggregate per-category scores (Cleanliness, Staff, Facilities, Location/Condition, Eco-Friendly). | Comments Service             | `Raw NoSQL Documents`                                               |
| 4.4  | Return paginated comment list and analytics graph data.                                           | Comments Service -> UI       | `JSON Analytics Object` (with `categoryBreakdown` and `comments[]`) |

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
| 6.1  | Trigger nightly scheduled job.                                                    | Cloud Scheduler -> Notification Service           | `Cron Trigger Signal`                                                             |
| 6.2  | Call Hotel Service internal capacity report endpoint.                             | Notification Service -> Hotel Service             | `GET /api/v1/admin/hotels/capacity-report?days=30`                                |
| 6.3  | Hotel Service executes SQL aggregate query and returns low-capacity hotel list.   | Hotel Service -> SQL DB -> Notification Service   | `SQL Aggregate Query (next 30 days, AvailableCount/TotalCount < 0.20)`            |
| 6.4  | Identify hotels where available capacity < 20% of total.                          | Notification Service                              | `Inventory ResultSet (HotelId, HotelName, CapacityRatio, DateRange)`              |
| 6.5  | Dispatch low-capacity warning alert to Admin channels (simulated).                | Notification Service                              | `Admin Contact Info`, `HotelId`, `Alert Message`                                  |

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

> **Note:** All endpoints conform to standard REST conventions, support versioning (`/v1/`) and pagination where applicable, and represent downstream service routes. External client access is routed through the Ocelot API Gateway, which validates JWT tokens before forwarding.

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

#### `GET /api/v1/hotels/{hotelId}/rooms/{roomId}`

- **Description:** Fetches room details including the current `RowVersion` token. The client must call this endpoint before submitting a booking to obtain the `rowVersion` required for optimistic concurrency control.
- **Security:** Public.
  **Response (200 OK):**

```json
{
  "roomId": "room-uuid-123",
  "hotelId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "roomType": "Standard",
  "pricePerNight": 10948.0,
  "availableCount": 3,
  "rowVersion": "AAAAAAAAAAA="
}
```

---

#### `POST /api/v1/bookings`

- **Description:** Creates a reservation. Executes an optimistic concurrency check using the `rowVersion` token to prevent overbooking. On success, decrements capacity in SQL and publishes a `ReservationCreatedEvent` to RabbitMQ.
- **Security:** Authenticated (User). Requires Bearer JWT.
- **Precondition:** Valid User JWT AND `rowVersion` token provided AND `StartDate` < `EndDate` AND `GuestCount` >= 1.
- **Postcondition:** `AvailableCount -= 1` in `InventoryBlocks` (SQL) AND `Booking` record created in `BookingDbContext` AND `ReservationCreatedEvent` published to RabbitMQ.
  **Request Body:**

```json
{
  "hotelId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "roomId": "room-uuid-123",
  "startDate": "2026-06-01T00:00:00Z",
  "endDate": "2026-06-05T00:00:00Z",
  "guestCount": 2,
  "rowVersion": "AAAAAAAAAAA="
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

### 5. AI Agent Service

#### `POST /api/v1/ai/chat`

- **Description:** Stateless natural language endpoint for the AI chat window. Handles the full conversational booking flow: intent parsing → optional clarifying questions → hotel search → 2-step booking confirmation. State is maintained client-side via the `contextState` field echoed back in each request.
- **Security:** Authenticated (User). Requires Bearer JWT.
- **Note:** Real-time messaging is NOT required. Standard HTTP request/response is sufficient.
  **Request Body:**

```json
{
  "sessionId": "sess-abc-123",
  "userMessage": "Find me a hotel in Izmir for next weekend for 2 guests.",
  "contextState": null
}
```

> **Confirmation turn request** (client echoes back `contextState` from previous response):

```json
{
  "sessionId": "sess-abc-123",
  "userMessage": "Yes, book it",
  "contextState": {
    "pendingAction": "BOOK",
    "targetHotelId": "izmir-hotel-1",
    "targetRoomId": "room-uuid-123",
    "startDate": "2026-05-16T00:00:00Z",
    "endDate": "2026-05-18T00:00:00Z",
    "guestCount": 2,
    "rowVersion": "AAAAAAAAAAA="
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
  "reply": "I found 3 great hotels in Izmir for next weekend. The top choice is Swissôtel Büyük Efes at 10,948 TL/night. Would you like me to confirm a reservation there?",
  "requiresConfirmation": true,
  "contextState": {
    "pendingAction": "BOOK",
    "targetHotelId": "izmir-hotel-1",
    "targetRoomId": "room-uuid-123",
    "startDate": "2026-05-16T00:00:00Z",
    "endDate": "2026-05-18T00:00:00Z",
    "guestCount": 2,
    "rowVersion": "AAAAAAAAAAA="
  }
}
```

**Response — Booking Confirmed:**

```json
{
  "reply": "Your reservation at Swissôtel Büyük Efes from May 16–18 is confirmed! Booking ID: booking-guid-456.",
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
  "hotelId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "hotelName": "Hyde Bodrum",
  "roomId": "room-uuid-123",
  "startDate": "2026-06-01T00:00:00Z",
  "endDate": "2026-06-05T00:00:00Z",
  "guestCount": 2,
  "publishedAt": "2026-05-10T14:30:00Z"
}
```

**Consumer Behaviour:**

- On successful processing → send `ACK` to remove message from queue.
- On processing failure → send `NACK` to requeue message for retry.
- Simulated output: Log confirmation message to console (Email/SMS simulation).
