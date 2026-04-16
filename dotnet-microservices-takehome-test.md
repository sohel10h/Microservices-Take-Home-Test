# 🧩 .NET Core Microservices Take‑Home Test

## 🎯 Objective

Build a **simplified microservices‑based system** using **.NET Core** and **event‑driven architecture**.  
This exercise assesses your ability to design clean, maintainable APIs and implement asynchronous communication between services.

Estimated completion time: **5–6 hours**

---

## 🧠 Scenario

You are building a small **Order Processing System** composed of three microservices:

1. **Order Service** – Handles order creation  
2. **Payment Service** – Processes payments  
3. **Notification Service** – Sends notifications when payments succeed

Each service should expose a **minimal REST API** and communicate via **events** (in‑memory event bus or RabbitMQ).

---

## 🧱 Requirements

### 1\. 🛒 Order Service

**Responsibilities**

- Create new orders  
- Publish an event when an order is created

**Endpoints**

| Endpoint | Method | Description |
| :---- | :---- | :---- |
| `/api/orders` | POST | Create a new order |
| `/api/orders` | GET | Get all orders (optional) |

**Event Published**

{

  "orderId": "123",

  "amount": 49.99,

  "customerEmail": "user@example.com"

}

---

### 2\. 💳 Payment Service

**Responsibilities**

- Listen for `OrderCreatedEvent`  
- Simulate payment processing  
- Publish a `PaymentSucceededEvent`

**Endpoints**

| Endpoint | Method | Description |
| :---- | :---- | :---- |
| `/api/payments` | GET | Get all processed payments (optional) |

**Event Consumed** `OrderCreatedEvent`

**Event Published**

{

  "orderId": "123",

  "paymentId": "abc-789",

  "amount": 49.99,

  "timestamp": "2024-06-01T12:30:00Z"

}

---

### 3\. 📧 Notification Service

**Responsibilities**

- Listen for `PaymentSucceededEvent`  
- Simulate sending a notification (e.g., log or console output)

**Endpoints**

| Endpoint | Method | Description |
| :---- | :---- | :---- |
| `/api/notifications` | GET | Get all notifications (optional) |

**Event Consumed** `PaymentSucceededEvent`

---

## 🔁 Event Flow Summary

1. `OrderService` publishes `OrderCreatedEvent` when a new order is created.  
2. `PaymentService` consumes the event, processes payment, and publishes `PaymentSucceededEvent`.  
3. `NotificationService` consumes `PaymentSucceededEvent` and logs or prints a notification.

Use **RabbitMQ** or a **simple in‑memory event bus** for communication.

---

## 🚀 Quick Start Paths

Choose the approach that best fits your skill level and time availability:

### Path A: In-Memory Event Bus (Recommended for 4-5 hours)

- Use a simple in-memory event bus (e.g., `ConcurrentQueue` or `Channel<T>`)  
- Store data in-memory collections (`List<Order>`, `Dictionary<string, Payment>`)  
- No external dependencies required  
- **Best for:** Demonstrating clean architecture and event-driven patterns

### Path B: RabbitMQ \+ Docker (6-7 hours)

- Set up RabbitMQ via Docker Compose  
- Use MassTransit or RabbitMQ.Client for messaging  
- Add optional persistence with EF Core In-Memory or SQL Server  
- **Best for:** Showing production-ready messaging skills

💡 **Recommendation:** Start with Path A to ensure you complete the core requirements within the time limit. Add RabbitMQ/Docker as a bonus if time permits.

---

## 🧱 Architecture Guidelines

Each microservice should follow a **simple layered or clean architecture** approach, separating:

- **API layer** – controllers, endpoints  
- **Application layer** – business logic, DTOs, services  
- **Domain layer** – entities, events  
- **Infrastructure layer** – persistence, messaging

Maintain clear separation of concerns and use **Dependency Injection** throughout.

---

## 💾 Data Storage Guidelines

For this take-home test, you have flexibility in how you store data:

### ✅ Recommended: In-Memory Collections

// Simple and effective for demonstration purposes

private static readonly List\<Order\> \_orders \= new();

private static readonly Dictionary\<string, Payment\> \_payments \= new();

**Pros:** No setup required, focuses on architecture and events  
**Use when:** You want to complete the test efficiently (4-5 hours)

### ✅ Also Good: EF Core In-Memory Database

services.AddDbContext\<OrderContext\>(options \=\>

    options.UseInMemoryDatabase("OrderDb"));

**Pros:** Shows EF Core knowledge, easy setup, realistic patterns  
**Use when:** You're comfortable with EF Core and want to show repository patterns

### 🎁 Bonus: SQL Server with Docker

\# docker-compose.yml

services:

  sqlserver:

    image: mcr.microsoft.com/mssql/server:2022-latest

**Pros:** Production-like setup, demonstrates Docker skills  
**Use when:** You have extra time and want to showcase full-stack capabilities

💡 **Key Point:** We evaluate architecture and event-driven design, not database complexity. Choose the storage approach that lets you focus on clean code and proper event flow.

---

## 🧰 Modern Practices (Required)

- **Swagger/OpenAPI** documentation for each service  
- **Logging** (e.g., Serilog or built‑in logger)  
- **Configuration** via `appsettings.json`  
- **Global error handling middleware**  
- **Unit tests** using xUnit or NUnit

---

## 🧪 Bonus (Optional)

If time permits:

- Add **Dockerfile** or `docker‑compose.yml`  
- Add **retry logic** for failed event delivery  
- Add **API Gateway** (YARP or Ocelot)  
- Add **Health Checks**

---

## 📦 Deliverables

Please submit your solution as a **GitHub repository** containing:

1. Source code for all microservices  
2. Example configuration files (e.g., `appsettings.json`, `docker‑compose.yml` if used)  
3. A top‑level `README.md` including:  
   - How to run the services locally or via Docker  
   - Architecture overview  
   - Event flow description  
   - Design decisions and assumptions  
   - Any known limitations or future improvements

💡 **Tip:** Organize your repository with a clear folder structure, e.g.:

/OrderService

/PaymentService

/NotificationService

/README.md

/docker‑compose.yml (optional)

---

## 🧠 What We'll Evaluate

We will assess your submission based on the following criteria:

| Criteria | Weight | What We Look For |
| :---- | :---- | :---- |
| **Architecture** | 20% | Clean separation of concerns, proper layering, Dependency Injection |
| **Event‑Driven Design** | 20% | Correct event publishing/consumption, decoupled services |
| **Code Quality** | 15% | Readable, maintainable code following SOLID principles |
| **Functionality** | 15% | Working end-to-end flow, all services communicate correctly |
| **Testing** | 10% | Unit tests with meaningful coverage |
| **API Design** | 10% | RESTful conventions, proper status codes, Swagger documentation |
| **Error Handling** | 5% | Validation, graceful error handling, proper logging |
| **Documentation** | 5% | Clear README with setup instructions and design decisions |
| **Bonus Features** | Extra | Docker, RabbitMQ, retry logic, API Gateway, etc. |

### 💡 What "Good" Looks Like

**Architecture:**

- Controllers handle HTTP concerns only  
- Business logic lives in service/application layer  
- Events are in the domain layer  
- Infrastructure (messaging, data access) is abstracted

**Event-Driven Design:**

- Services communicate only via events (no direct API calls)  
- Events are immutable and contain all necessary data  
- Services can run independently  
- Async/await used correctly

**Code Quality:**

- Classes and methods have single responsibility  
- Descriptive names for variables, methods, classes  
- No magic numbers or hardcoded strings  
- Consistent code style

**Testing:**

- Unit tests for core business logic  
- Tests use mocking for dependencies  
- Tests cover both success and error scenarios  
- All tests pass with `dotnet test`

**Documentation:**

- Step-by-step instructions to run locally  
- Architecture/event flow explained  
- Design decisions justified (e.g., why in-memory vs RabbitMQ)

---

## 🕒 Expected Duration

**5–6 hours total**

We understand this is a time investment. Focus on demonstrating your strengths rather than achieving perfection. The core requirements (3 microservices with working event flow and basic tests) are more important than bonus features.

---

## 💡 Notes

- Use **.NET 8 or .NET 9**.  
- RabbitMQ setup is **optional**; an in‑memory event bus is perfectly acceptable.  
- Focus on **clarity, correctness, and maintainability**, not production completeness.  
- Ensure your GitHub repository is **public** or shared for review.  
- If you make trade-offs due to time constraints, document them in your README.

---

## ✅ Submission Instructions

Please create a **public GitHub repository** named  
`dotnet‑microservices‑takehome‑[yourname]`

When complete, share the repository link with us.

Your repository should include:

- Source code for each microservice  
- A clear `README.md` explaining how to run and test the solution  
- Any configuration or Docker files if used

---

## ❓ Questions?

If you have questions about the requirements, please reach out. We want you to succeed\!

Good luck\! 🚀  
