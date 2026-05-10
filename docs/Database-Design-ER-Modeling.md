# Phase 3: Database Design & Entity-Relationship (ER) Modeling

This document defines the complete data persistence layer for the Hotel Booking System. Adhering to the **Database-per-Service** microservices pattern, data is logically isolated by bounded context. The system uses three distinct storage technologies, each owned exclusively by the service responsible for it.

---

## 1. Storage Architecture Overview

| Storage Type             | Technology                | Owned By                                | Purpose                                                      |
| :----------------------- | :------------------------ | :-------------------------------------- | :----------------------------------------------------------- |
| **Relational SQL**       | Azure SQL Server          | Hotel Service                | Transactional data: hotel inventory, room types, bookings    |
| **NoSQL Document Store** | MongoDB / Azure Cosmos DB | Comments Service             | Unstructured review documents and aggregated category scores |
| **Distributed Cache**    | Redis                     | Hotel Service                | Cached hotel search results with TTL for sub-500ms response  |

> **Deployment Note:** The two SQL contexts (`CatalogDbContext` and `BookingDbContext`) can be implemented as separate schemas within the same managed Azure SQL instance (e.g., `catalog` and `booking` schemas) for cost efficiency, or as physically separate databases. They must **never** share EF Core `DbContext` instances or reference each other with hard FK constraints.

---

## 2. Bounded Contexts & Ownership

- **Hotel Catalog & Inventory Context** _(Hotel Service — `CatalogDbContext`)_: Owns `Hotels`, `RoomTypes`, `InventoryBlocks`. Admin endpoints write; search endpoints read (via Redis cache or direct SQL on cache miss).
- **Booking Context** _(Hotel Service — `BookingDbContext`)_: Owns `Bookings`. References `HotelId` and `RoomTypeId` as soft references — no hard FK constraints cross DbContext boundaries even though both live in the same service.
- **Comments Context** _(Comments Service)_: Owns the NoSQL document collection. Completely isolated; communicates no data to SQL services.

---

## 3. Entity-Relationship (ER) Diagram

> Paste the code block below into a Mermaid viewer (e.g., [mermaid.live](https://mermaid.live) or GitHub's native markdown renderer).

```mermaid
erDiagram

    %% ==========================================
    %% CONTEXT A: Hotel Catalog & Inventory
    %% Owned by: Hotel Service (CatalogDbContext)
    %% ==========================================

    HOTELS ||--o{ ROOM_TYPES : "has"
    ROOM_TYPES ||--o{ INVENTORY_BLOCKS : "allocates"

    HOTELS {
        uniqueidentifier HotelId PK
        nvarchar(100) Name
        nvarchar(100) Destination
        decimal(9_6) Latitude
        decimal(9_6) Longitude
        decimal(3_1) BaseRating
        int TotalReviews
        nvarchar(500) ImageUrl "nullable, nice-to-have"
        bit IsActive
    }

    ROOM_TYPES {
        uniqueidentifier RoomTypeId PK
        uniqueidentifier HotelId FK
        nvarchar(50) TypeName
        int MaxGuests
        decimal(18_2) BasePricePerNight
    }

    INVENTORY_BLOCKS {
        uniqueidentifier InventoryId PK
        uniqueidentifier RoomTypeId FK
        date StartDate
        date EndDate
        int TotalCount
        int AvailableCount
        bit IsAvailable
        timestamp RowVersion
    }

    %% ==========================================
    %% CONTEXT B: Reservations
    %% Owned by: Hotel Service (BookingDbContext)
    %% ==========================================

    BOOKINGS {
        uniqueidentifier BookingId PK
        nvarchar(100) UserId
        uniqueidentifier HotelId
        uniqueidentifier RoomTypeId
        date CheckInDate
        date CheckOutDate
        int GuestCount
        decimal(18_2) TotalAmount
        nvarchar(20) Status
        datetime2 CreatedAt
    }
```

---

## 4. Data Dictionary (SQL Schema Specifications)

### Context A: Hotel Catalog & Inventory (`CatalogDbContext`)

---

#### Table: `Hotels`

The master record for hotel properties. Managed by Hotel Service — admin endpoints write; search endpoints read.

| Column Name   | Data Type          | Constraints                    | Description                                                                                                  |
| :------------ | :----------------- | :----------------------------- | :----------------------------------------------------------------------------------------------------------- |
| `HotelId`     | `UNIQUEIDENTIFIER` | Primary Key, Default `NEWID()` | Unique identifier for the hotel.                                                                             |
| `Name`        | `NVARCHAR(100)`    | Not Null                       | Display name, e.g., "Hyde Bodrum - Yetişkin Oteli".                                                          |
| `Destination` | `NVARCHAR(100)`    | Not Null, **Indexed**          | City/region string used for Hotel Service search endpoint destination filtering, e.g., "Bodrum".              |
| `Latitude`    | `DECIMAL(9,6)`     | Not Null                       | Geographic latitude. Required for "Haritada göster" map feature.                                             |
| `Longitude`   | `DECIMAL(9,6)`     | Not Null                       | Geographic longitude. Required for "Haritada göster" map feature.                                            |
| `BaseRating`   | `DECIMAL(3,1)` | Nullable                      | Denormalized aggregated rating from the Comments Service. Nullable since a new hotel may have no reviews. |
| `TotalReviews` | `INT`          | Not Null, Default `0`         | Denormalized total review count from the Comments Service. Required by the search result UI ("3 yorum"). Seeded with realistic values; updated manually or by a batch sync — never queried live from Comments during search (latency constraint). |
| `ImageUrl`     | `NVARCHAR(500)` | **Nullable** (Nice-to-Have)  | URL of the hotel's primary display image. Optional field — the project spec lists image uploading as a nice-to-have. If implemented, the admin `POST /api/v1/admin/hotels` endpoint accepts this field; the image itself is uploaded to an external storage service (e.g., Azure Blob Storage) and the resulting URL stored here. |
| `IsActive`     | `BIT`          | Not Null, Default `1`         | Soft-delete flag. Inactive hotels do not appear in search results.                                       |

**Indexes:**

- `IX_Hotels_Destination` on `Destination` — accelerates the most common Hotel Service search `WHERE` clause.

---

#### Table: `RoomTypes`

Defines the categories of rooms available within a hotel. Managed by Hotel Service (admin endpoints).

| Column Name         | Data Type          | Constraints                               | Description                                                                                                    |
| :------------------ | :----------------- | :---------------------------------------- | :------------------------------------------------------------------------------------------------------------- |
| `RoomTypeId`        | `UNIQUEIDENTIFIER` | Primary Key, Default `NEWID()`            | Unique identifier for the room category.                                                                       |
| `HotelId`           | `UNIQUEIDENTIFIER` | Foreign Key → `Hotels(HotelId)`, Not Null | Links to the owning hotel.                                                                                     |
| `TypeName`          | `NVARCHAR(50)`     | Not Null                                  | Room category name, e.g., "Standard", "Aile" (Family). Matches the "Oda Tipi" dropdown in the Admin UI mockup. |
| `MaxGuests`         | `INT`              | Not Null, Check `>= 1`                    | Maximum occupancy. Used by the Hotel Service search endpoint to filter results against the `GuestCount` query parameter. |
| `BasePricePerNight` | `DECIMAL(18,2)`    | Not Null, Check `> 0`                     | Price before the 15% JWT discount Strategy is applied.                                                         |

---

#### Table: `InventoryBlocks`

Tracks room availability for specific date ranges. This is the most critical table in the system — it is the target of the Optimistic Concurrency check during booking and the source for the nightly capacity alert cron job.

| Column Name      | Data Type                  | Constraints                                     | Description                                                                                                                                                                        |
| :--------------- | :------------------------- | :---------------------------------------------- | :--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `InventoryId`    | `UNIQUEIDENTIFIER`         | Primary Key, Default `NEWID()`                  | Unique identifier for the availability block.                                                                                                                                      |
| `RoomTypeId`     | `UNIQUEIDENTIFIER`         | Foreign Key → `RoomTypes(RoomTypeId)`, Not Null | Links to the room category this block applies to.                                                                                                                                  |
| `StartDate`      | `DATE`                     | Not Null, **Indexed**                           | Start of the availability window. Maps to the Admin UI "Başlangıç" field.                                                                                                          |
| `EndDate`        | `DATE`                     | Not Null, **Indexed**                           | End of the availability window. Maps to the Admin UI "Bitiş" field. Must satisfy `StartDate < EndDate`.                                                                            |
| `TotalCount`     | `INT`                      | Not Null, Check `>= 0`                          | Total physical rooms of this type for this date range. **Set once on creation** to the value of `AvailableCount` from the admin's "Oda Adedi" input; never modified after that. Exists solely to support the nightly cron ratio: `AvailableCount / TotalCount < 0.20`. |
| `AvailableCount` | `INT`                      | Not Null, Check `>= 0`                          | Currently available rooms. On creation equals `TotalCount`. Decremented atomically during a booking transaction.                                                                   |
| `IsAvailable`    | `BIT`                      | Not Null, Default `1`                           | Admin-set availability flag. Maps to the "Dolu / Boş" radio button in the Admin UI mockup. Only blocks with `IsAvailable = 1` and `AvailableCount > 0` appear in Search results.   |
| `RowVersion`     | `ROWVERSION` / `TIMESTAMP` | Not Null, ConcurrencyToken                      | Auto-incremented binary token managed by SQL Server. Used by EF Core for Optimistic Concurrency. The client must send this value back in the `POST /api/v1/bookings` request body. |

**Indexes:**

- `IX_InventoryBlocks_StartDate_EndDate` on `(StartDate, EndDate)` — accelerates the date-range overlap query used by Search.
- `IX_InventoryBlocks_RoomTypeId` on `RoomTypeId` — accelerates joins from `RoomTypes`.
  **Critical Query (used by Hotel Service — Booking):**

```sql
UPDATE InventoryBlocks
SET AvailableCount = AvailableCount - 1
WHERE InventoryId = @inventoryId
  AND RowVersion = @rowVersion
  AND AvailableCount > 0;
-- If 0 rows affected → EF Core throws DbUpdateConcurrencyException → return HTTP 409 Conflict
```

**Critical Query (used by Nightly Cron Job — BP-06):**

```sql
SELECT h.HotelId, h.Name, ib.StartDate, ib.EndDate,
       ib.TotalCount, ib.AvailableCount,
       CAST(ib.AvailableCount AS FLOAT) / CAST(ib.TotalCount AS FLOAT) AS CapacityRatio
FROM InventoryBlocks ib
JOIN RoomTypes rt ON ib.RoomTypeId = rt.RoomTypeId
JOIN Hotels h ON rt.HotelId = h.HotelId
WHERE ib.StartDate >= CAST(GETUTCDATE() AS DATE)
  AND ib.StartDate <= DATEADD(MONTH, 1, CAST(GETUTCDATE() AS DATE))
  AND ib.TotalCount > 0
  AND (CAST(ib.AvailableCount AS FLOAT) / CAST(ib.TotalCount AS FLOAT)) < 0.20;
```

---

### Context B: Reservations (`BookingDbContext`)

---

#### Table: `Bookings`

Records confirmed user reservations. References Hotel and Room data by ID only — no hard SQL Foreign Key constraints cross service boundaries (Database-per-Service pattern).

| Column Name    | Data Type          | Constraints                      | Description                                                                                                                                                          |
| :------------- | :----------------- | :------------------------------- | :------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `BookingId`    | `UNIQUEIDENTIFIER` | Primary Key, Default `NEWID()`   | Unique identifier for the reservation. Returned to the client as the booking confirmation ID.                                                                        |
| `UserId`       | `NVARCHAR(100)`    | Not Null, **Indexed**            | The `sub` (Subject) claim extracted from the Cognito/Firebase/Supabase JWT. Not a FK — the IAM service owns user identity.                                           |
| `HotelId`      | `UNIQUEIDENTIFIER` | Not Null                         | Soft reference to the hotel. No FK constraint — the Catalog DB is owned by a different service.                                                                      |
| `RoomTypeId`   | `UNIQUEIDENTIFIER` | Not Null                         | Soft reference to the room type booked. No FK constraint.                                                                                                            |
| `CheckInDate`  | `DATE`             | Not Null                         | Start date of the stay.                                                                                                                                              |
| `CheckOutDate` | `DATE`             | Not Null                         | End date of the stay. Must satisfy `CheckInDate < CheckOutDate`.                                                                                                     |
| `GuestCount`   | `INT`              | Not Null, Check `>= 1`           | Number of guests for the reservation.                                                                                                                                |
| `TotalAmount`  | `DECIMAL(18,2)`    | Not Null                         | Total price at time of booking. Reflects the 15% discount if the user was authenticated. Stored so the price is immutable even if `BasePricePerNight` changes later. |
| `Status`       | `NVARCHAR(20)`     | Not Null, Default `'Confirmed'`  | Booking lifecycle state. Valid values: `'Confirmed'`, `'Cancelled'`, `'Completed'`.                                                                                  |
| `CreatedAt`    | `DATETIME2`        | Not Null, Default `GETUTCDATE()` | UTC timestamp of when the booking was created.                                                                                                                       |

**Indexes:**

- `IX_Bookings_UserId` on `UserId` — accelerates lookup of all bookings for a given user.
- `IX_Bookings_HotelId` on `HotelId` — accelerates lookup of all bookings for a given hotel (used by the Notification Service when it needs hotel context from the event payload).

---

## 5. NoSQL Schema: Comments Collection (`CommentsDbContext`)

**Technology:** MongoDB collection or Azure Cosmos DB container.
**Collection Name:** `hotelReviews`
**Partition Key (Cosmos DB):** `/hotelId`

Each document in the collection represents the full review state for one hotel, storing both raw comments and pre-aggregated category scores to avoid expensive recalculation on every read.

### Document Schema

```json
{
  "_id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "hotelId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "totalReviews": 163,
  "overallScore": 9.2,
  "categoryScores": {
    "cleanliness": 9.6,
    "staff": 9.6,
    "facilities": 9.4,
    "locationCondition": 9.6,
    "ecoFriendly": 9.4
  },
  "reviews": [
    {
      "reviewId": "rev-uuid-001",
      "author": "Simge",
      "tripType": "4 gecelik seyahat",
      "rating": 8.0,
      "text": "Great location and very clean.",
      "date": "2025-06-16T00:00:00Z",
      "hotelReply": {
        "repliedBy": "Enver",
        "replyText": "Geri bildiriminiz için teşekkür ederiz.",
        "replyDate": "2025-06-16T00:00:00Z"
      }
    }
  ]
}
```

### Field Descriptions

| Field                              | Type              | Description                                                                                             |
| :--------------------------------- | :---------------- | :------------------------------------------------------------------------------------------------------ |
| `_id` / `hotelId`                  | String (GUID)     | Matches the `HotelId` from the SQL `Hotels` table. Soft reference — no constraint enforced.             |
| `totalReviews`                     | Integer           | Total count of reviews. Used to display "163 doğrulanmış yorum" in the UI.                              |
| `overallScore`                     | Float             | Aggregated overall score, e.g., 9.2.                                                                    |
| `categoryScores`                   | Object            | Pre-aggregated scores for all 5 categories shown in the PDF mockup. Updated when a new review is added. |
| `categoryScores.cleanliness`       | Float             | Temizlik score.                                                                                         |
| `categoryScores.staff`             | Float             | Personel ve servis score.                                                                               |
| `categoryScores.facilities`        | Float             | İmkân ve özellikler score.                                                                              |
| `categoryScores.locationCondition` | Float             | Konaklama yerinin durumu, imkânları ve kolaylıkları score.                                              |
| `categoryScores.ecoFriendly`       | Float             | Çevre dostluğu score.                                                                                   |
| `reviews[]`                        | Array             | Array of individual review sub-documents. Paginated by the Comments Service API.                        |
| `reviews[].reviewId`               | String (GUID)     | Unique ID for each review.                                                                              |
| `reviews[].author`                 | String            | Reviewer's display name.                                                                                |
| `reviews[].tripType`               | String            | E.g., "4 gecelik seyahat".                                                                              |
| `reviews[].rating`                 | Float             | Individual review score out of 10.                                                                      |
| `reviews[].text`                   | String            | Review body text.                                                                                       |
| `reviews[].date`                   | ISO 8601 String   | Date the review was submitted.                                                                          |
| `reviews[].hotelReply`             | Object (Nullable) | Optional hotel management response. Null if the hotel has not replied.                                  |

---

## 6. Redis Cache Schema

**Technology:** Redis (cloud-hosted, e.g., Azure Cache for Redis).
**Pattern:** Cache-Aside (Lazy Loading). The Hotel Service checks Redis before querying SQL.

### Key Structure

| Cache Key Pattern                                         | Value Type  | TTL        | Description                                                                                                                                |
| :-------------------------------------------------------- | :---------- | :--------- | :----------------------------------------------------------------------------------------------------------------------------------------- |
| `search:{destination}:{startDate}:{endDate}:{guestCount}` | JSON String | 15 minutes | Serialized search result set. Key is a composite of all search parameters. Invalidated on TTL expiry.                                      |
| `hotel:detail:{hotelId}`                                  | JSON String | 60 minutes | Serialized hotel detail object including `RoomTypes` and current `InventoryBlocks`. Used by the AI Agent Facade and the hotel detail page. |

### Cached Search Result Value Structure

```json
{
  "cachedAt": "2026-05-10T14:00:00Z",
  "results": [
    {
      "hotelId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "name": "Hyde Bodrum",
      "destination": "Bodrum, Muğla",
      "coordinates": { "lat": 37.034, "lng": 27.43 },
      "basePricePerNight": 10948.0,
      "availableRooms": 2,
      "baseRating": 9.6,
      "totalReviews": 163,
      "roomTypeId": "room-type-uuid-123"
    }
  ]
}
```

> **Note on Pricing:** Base prices (without discount) are stored in Redis. The 15% discount is **never cached** — it is applied at the service layer at response time based on the JWT presence in the request header. This prevents a cached discounted price being served to a non-authenticated user.

> **Note on Cache Invalidation:** When an Admin updates an `InventoryBlock` for a hotel, the `hotel:detail:{hotelId}` key should be explicitly deleted (evicted) from Redis to prevent stale availability data. Search result keys expire naturally via TTL.

---

## 7. Architectural Notes on Data Consistency

### 7.1 Optimistic Concurrency (Overbooking Prevention)

When the Hotel Service processes `POST /api/v1/bookings`:

1. The client sends the `rowVersion` token it received from `GET /api/v1/hotels/{hotelId}/rooms/{roomId}`.
2. EF Core executes: `UPDATE InventoryBlocks SET AvailableCount -= 1 WHERE InventoryId = @id AND RowVersion = @rowVersion AND AvailableCount > 0`.
3. If another user booked the same room milliseconds earlier, SQL Server will have auto-incremented `RowVersion`. The `WHERE` clause will match 0 rows.
4. EF Core detects 0 affected rows and throws `DbUpdateConcurrencyException`.
5. The service catches this exception and returns `HTTP 409 Conflict` to the client.
6. This satisfies the **Definition of Failure** requirement: no booking transaction can leave the system in an inconsistent state (overbooking).

### 7.2 Soft References vs. Hard Foreign Keys

The `Bookings` table stores `HotelId` and `RoomTypeId` as plain `UNIQUEIDENTIFIER` columns with no SQL `FOREIGN KEY` constraint pointing to the `Hotels` or `RoomTypes` tables. This is intentional:

- The `Bookings` table lives in `BookingDbContext`; the `Hotels` table lives in `CatalogDbContext`. Both are owned by the Hotel Service, but they are separate bounded contexts with separate DbContext instances.
- Even within the same service, cross-DbContext SQL Foreign Keys are not added. Each DbContext is independently migratable and independently connectable to a separate database if needed for future scaling.
- Data consistency across the booking and notification boundary is maintained through the event-driven flow: the `ReservationCreatedEvent` carries all necessary data (HotelId, RoomTypeId, dates) so the Notification Service never needs to query HotelService's databases directly.

### 7.3 EF Core Configuration Summary

| DbContext          | Owned By                       | Tables                                   | Migrations Folder                         |
| :----------------- | :----------------------------- | :--------------------------------------- | :---------------------------------------- |
| `CatalogDbContext` | Hotel Service | `Hotels`, `RoomTypes`, `InventoryBlocks` | `/src/HotelService/Migrations/Catalog/`  |
| `BookingDbContext` | Hotel Service | `Bookings`                               | `/src/HotelService/Migrations/Booking/`  |

Each service runs its own `dotnet ef database update` independently. The `RowVersion` column on `InventoryBlocks` must be configured in EF Core as:

```csharp
entity.Property(e => e.RowVersion)
      .IsRowVersion()
      .IsConcurrencyToken();
```

### 7.4 Data Seeding Strategy

To enable meaningful UI and API testing without manual data entry, the following seed data should be applied via EF Core `HasData` or a dedicated `DataSeeder` class on application startup:

| Entity            | Seed Count       | Details                                                                                                                                                                  |
| :---------------- | :--------------- | :----------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `Hotels`          | 5–10             | Mix of cities (Istanbul ×2, Izmir ×1, Bodrum ×2, Antalya ×2). Must include valid `Latitude`/`Longitude` values for the map feature.                                     |
| `RoomTypes`       | 2–3 per hotel    | At least "Standard" and "Family" types per hotel, matching the Admin UI dropdown.                                                                                        |
| `InventoryBlocks` | 3–5 per RoomType | Cover the next 90 days with varied `AvailableCount` values. Include at least one block with `AvailableCount < 20%` of `TotalCount` to verify the nightly cron job alert. |
| `Bookings`        | 3–5              | Sample confirmed bookings with valid `UserId` values from the seeded IAM test users.                                                                                     |
