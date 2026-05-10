# Project Requirements: Hotel Booking System (Microservices Architecture)

## 1. Project Overview & Definition of Success

- A scalable, service-oriented hotel booking system similar to Hotels.com. The system allows administrators to manage hotel inventory, users to search and book rooms, and includes an AI Agent for conversational interactions.
- **Definition of Success (2.6):** The system successfully allows a user to search for a hotel using cached data, book a vacant room, correctly decrement the database inventory, and asynchronously notify the user without data corruption or race conditions.
- **Definition of Failure (2.6):** A failure occurs if a booking transaction leaves the system in an inconsistent state (e.g., overbooking a room), or if an external service outage crashes the core application. Failures must be caught and return standardized HTTP 4xx/5xx error responses.

## 2. Tech Stack Constraints

- **Backend Framework:** C# .NET Core Web API
- **API Gateway:** Ocelot (or similar .NET Gateway)
- **Authentication (IAM):** Third-party Identity Provider (AWS Cognito, Firebase Auth, or Supabase Auth). _Constraint: Local, custom authentication implementations are strictly forbidden_.
- **Relational Database:** Cloud-hosted SQL (e.g., Azure SQL Server) for transactional data. _Constraint: SQLite is strictly forbidden_.
- **NoSQL Database:** MongoDB or Azure Cosmos DB for unstructured data (Comments).
- **Distributed Cache:** Redis (or in-memory).
- **Message Broker:** RabbitMQ or Azure Message Queues.
- **Scheduler:** Azure App Logic or Google Cloud Scheduler.
- **Containerization:** A `Dockerfile` must be provided in the source code. _Constraint: Do NOT create/upload the actual Docker image file_.

## 3. System Interfaces & External Integrations (1.3 & 3.1)

- **Map Integration (External System):** The UI must integrate an external mapping service (e.g., Google Maps API, Mapbox, or Leaflet/OpenStreetMap) to satisfy the "Haritada goster" requirement.
- **IAM Integration (External System):** Communication with the chosen Identity Provider (e.g., Firebase Auth) will use standard OAuth2/OIDC protocols. The API Gateway will validate JWT Bearer tokens before forwarding requests.
- **Communication Protocols (1.3):** All internal microservice communication will be strictly RESTful (HTTP/HTTPS) utilizing JSON payloads. Asynchronous communication will utilize AMQP (Advanced Message Queuing Protocol) via RabbitMQ.

## 4. Architectural Patterns

These define the high-level structure of the system and are practically required based on the final project document:

- **Microservices Architecture:**
  - _Where to use:_ The entire system backend.
  - _Why:_ The deployment diagram and common requirements explicitly state that the project must be split into separate services. You will have distinct, separate deployments for the Hotel Service, Comments Service, Notification Service, and AI Agent.
- **Database-per-Service Pattern:**
  - _Where to use:_ Data persistence layer.
  - _Why:_ To maintain strict microservice boundaries, each service must manage its own data store and EF Core `DbContext`. Services must never share a database or query another service's tables directly.
- **Event-Driven Architecture (EDA / Pub-Sub):**
  - _Where to use:_ Between the Hotel Service and the Notification Service.
  - _Why:_ The system will utilize the Publish-Subscribe (Pub/Sub) pattern via RabbitMQ to decouple the booking process from notifications. When a user books a room, the Hotel Service acts as the Publisher and sends a "New Reservation" event to RabbitMQ, which the Notification Service then consumes asynchronously.
- **API Gateway Pattern:**
  - _Where to use:_ The single entry point for all front-end clients (React/UI, AI Agent UI, Admin Client).
  - _Why:_ It is a hard requirement. All REST APIs must be reached via an API gateway. This centralizes routing and hides the complexity of your microservices from the client.
- **MVC (Model-View-Controller) / N-Layered:**
  - _Where to use:_ Inside each individual C# microservice.
  - _Why:_ ASP.NET Core natively uses MVC for building RESTful webservices.

## 5. Design Patterns (GoF) & Code Contracts

- **Strategy Pattern:**
  - _Where to use:_ Hotel Search Service (Pricing calculation and Search Sorting Algorithm).
  - _Why:_ Swaps pricing calculation logic based on user authentication status (15% discount for logged-in users) and allows dynamic swapping of sorting rules (e.g., sort by price, rating, distance).
  - **\*Note for README/Documentation:** The Strategy pattern was explicitly chosen for pricing to design for future extensibility. This architecture allows the system to easily add new pricing tiers (e.g., VIP memberships, seasonal discounts) later without modifying the core logic.\*
- **Singleton Pattern:**
  - _Where to use:_ Managing database/broker connections.
  - _Why:_ Manages the Redis `ConnectionMultiplexer` and the RabbitMQ `IConnection` to ensure only one connection pool exists per service instance. _(Note: RabbitMQ `IModel` channels are NOT thread-safe and will be created per-request or properly scoped, not treated as Singletons)._
- **Factory Method Pattern:**
  - _Where to use:_ Inside the Notification Service.
  - _Why:_ Encapsulates the creation logic for different types of alerts (Booking confirmations vs. Low Capacity warnings), allowing the system to easily instantiate the correct notification strategy.
- **Facade Pattern:**
  - _Where to use:_ Inside the AI Agent Service.
  - _Why:_ Hides the complexity of internal REST calls within the AI Agent service.
- **Method Specifications (Design by Contract):**
  - _Where to use:_ Core business logic methods.
  - _Why:_ Explicit Preconditions (e.g., `startDate < endDate`) and Postconditions (e.g., `AvailableRooms -= 1`) must be defined for all critical methods to ensure defensive programming.

## 6. Functional Requirements & I/O Specifications (1.1, 1.2, 1.4, 1.5)

### 6.1 Hotel Admin Service

- **Security Scope:** THIS WILL BE AN AUTHENTICATED SERVICE. Only authorized Admin users can access these endpoints.
- **Inputs:** JSON payload containing `HotelId`, `RoomType`, `StartDate` (DateTime), `EndDate` (DateTime), and `AvailableCount` (Integer).
- **Nice-to-Have Feature:** Image uploading is not necessary but nice-to-have.
- **Outputs:** HTTP 200 OK or HTTP 400 Bad Request. Updates SQL Database inventory table.

### 6.2 Hotel Search Service

- **Security Scope:** Publicly accessible. Users do not need to be logged in to search.
- **Inputs:** URL Query Parameters: `Destination` (String), `StartDate` (DateTime), `EndDate` (DateTime), `GuestCount` (Integer).
- **Outputs:** Paginated JSON Array of Hotel objects. The UI must explicitly include a 'Haritada goster' (Show on map) feature to display the hotels that have been searched.
- **Caching Strategy:** The service implements a **cache-aside pattern**. It must query Redis for hotel availability and details first, falling back to query the SQL database only on a cache miss.
- **Pricing Rule:** Applies a 15% discount algorithm to the output prices if the request header contains a valid user JWT (Client who login to application).

### 6.3 Book Hotel Service

- **Security Scope:** Authenticated Service. Users must be logged in to book.
- **Inputs:** JSON payload: `HotelId`, `RoomId`, `UserId`, `StartDate`, `EndDate`.
- **Concurrency Handling:** To prevent overbooking (race conditions), the booking transaction must utilize Optimistic Concurrency Control (e.g., using a `RowVersion` concurrency token in EF Core) to ensure the room's capacity is validated immediately before committing the decrement.
- **Outputs:** JSON confirmation object. Updates SQL database (decrements capacity). Publishes `ReservationCreatedEvent` (JSON) to RabbitMQ.
- **Payment:** NO transaction data input is required.

### 6.4 Hotel Comments Service

- **Inputs:** `HotelId` (Guid).
- **Outputs:** JSON Array of comment objects and a per-category breakdown graph showing score distributions per service category (e.g., Temizlik, Personel ve servis, İmkân ve özellikler, Çevre dostluğu). Data is retrieved from the NoSQL database.

### 6.5 Notification Service (Dual Responsibility)

This service contains two distinct architectural tasks:

- **Task 1 (Event-Driven Queue Consumer):** Subscribes to RabbitMQ. Inputs: AMQP Message for new reservations. Outputs: Pulls new hotel reservations from the queue and sends them a message about reservation details.
- **Task 2 (Scheduled Cron Job):** A nightly scheduled task. Inputs: Nightly timer trigger. Outputs: Goes over all hotel capacities and notifies hotel administrators when it is below 20% for the next month.

### 6.6 AI Agent Service

- **Inputs:** Natural language text strings from the user via the UI chat window.
- **Interaction Flow:** Must implement a distinct two-step confirmation flow where the agent first presents options, and then explicitly asks the user to confirm (e.g., "Would you like to confirm your reservation at Hotel Roma Plaza...") before the user confirms ("Yes, book it").
- **Outputs:** Structured JSON or text responses offering specific hotel options and asking for booking confirmation.

## 7. General Non-Functional Requirements (2.1 - 2.5)

- **Performance & Response Time (2.1):** The Hotel Search API must return query results in under **500ms** to ensure a smooth UI experience. This dictates the strict necessity of querying the Redis Cache before hitting the SQL database.
- **Timing Considerations (2.2):** The capacity check task must run on a scheduled nightly cron job.
- **Security Level (2.3):** High for Admin and Booking actions. All secure endpoints will enforce HTTPS and require Bearer JWT validation. API Gateway will implement Rate Limiting to prevent DDoS.
- **Reliability & Error Recovery (2.4):**
  - _Circuit Breakers:_ The system will use the `.NET Polly` library. If the primary SQL database is unreachable, the API will fail fast and return a standard 503 Service Unavailable error rather than hanging.
  - _Queue Retries:_ If the Notification service fails to process a RabbitMQ message, it will NACK (Negative Acknowledge) the message, returning it to the queue for a retry.
- **Maintainability (2.5):** The system adheres to Microservices and SOLID principles. Independent deployment pipelines and containerization (Docker) ensure any single service can be updated or replaced without affecting the rest of the application.
- **Versioning & Pagination:** All REST services must be versionable and support pagination when needed.

## 8. Deployment & Deliverables

- **Deployment:** APIs and UI must be hosted on a cloud service (e.g., Azure App Services, AWS, Google Cloud, Vercel).
- **Deliverables Required:**
  - Public Github repository link.
  - A README document containing:
    - Final deployed URLs of the application.
    - Design choices, documented assumptions, and encountered issues.
    - Data models (e.g., an ER diagram).
  - A link to a short presentation video (max 5 minutes).

## 9. Industry-Standard Enhancements (Portfolio Quality)

- **Automated Testing & Quality Assurance:** Use `xUnit` for robust unit testing of business logic (e.g., PricingStrategy) and implement basic smoke tests to verify service availability. E2E UI testing is not required.
- **API Documentation:** Integrate Swagger UI/OpenAPI directly into the **individual microservices** (rather than the API Gateway) to ensure accurate, service-specific contract documentation.
- **Distributed Logging:** Implement structured logging (Serilog) with Correlation IDs to trace a single user's request across the Gateway, Search Service, and Queue.
- **CI/CD Pipeline:** GitHub Actions workflow to build .NET projects, run unit tests, and seamlessly **execute a `docker build`** to verify that the Dockerfiles compile successfully upon every push.
- **Database Operations:** Use EF Core Code-First Migrations (ensuring separate `DbContexts` per service) and implement automated data seeding for test hotels/users.
- **Microservice Health Checks:** Implement `/health` endpoints in all services to verify SQL/Redis connectivity.
