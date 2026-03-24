# FMC Enterprise API Roadmap & Architecture

This document defines the strategic roadmap and technical architecture for the Finance Management Console (FMC) Backend API. It aligns with the existing project milestones to move from a monolithic Blazor app to a decoupled, high-available, and secure distributed system.

---

## 🏛 Enterprise Architecture Pattern: Clean Architecture

To ensure the FMC is future-proof and "enterprise-level", we will follow a **Clean Architecture (Onion Architecture)** approach. This separates the business logic from the infrastructure and UI.

### Project Layering:
1.  **`FMC.Core` (Domain Layer)**:
    - Pure C#. Contains Domain Entities (`Transaction`, `Account`), DTOs, Mapping profiles, and Service Interfaces.
2.  **`FMC.Infrastructure` (Data Layer)**:
    - Implements the Core interfaces. Contains `ApplicationDbContext`, EF Core Migrations, Identity configurations, and external integrations (SMTP/OTP).
3.  **`FMC.Api` (Presentation/Application Layer)**:
    - ASP.NET Core Web API with Controllers. Handles Middleware (Auth, Logging, Rate Limiting) and exposes REST endpoints.
4.  **`FMC.Shared` (DTO Layer)**:
    - Lightweight library containing only DTOs and validation logic, shared between `FMC.Api` and the Blazor UI (`FMC`).

---

## 🧠 Architectural Resolutions

### I. Shared Library Versioning
- **Deployment Flow**: Synchronized atomic deployments via CI/CD pipelines to ensure the Blazor UI always has matching DTOs from `FMC.Shared`.
- **API Stability**: Adhere to additive-only changes for DTOs; never rename or delete properties without a version increment (`/api/v2/`).

### II. Data Access: Implementation of CQRS
- **MediatR Integration**: Decouple commands and queries within `FMC.Core`.
- **Performance**: Use optimized Dapper/EF Query models for read operations, bypass heavy business logic for high-speed dashboard telemetry.
- **Integrity**: Strict EF Core transactional boundaries for "Commands" (Writing data to account ledgers).

### III. Progressive Testing Strategy
- **Contract Security**: Implement JSON Schema validation or Pact to ensure the Blazor client and API remain in sync.
- **Resilient Integration**: Use **Testcontainers** for SQL Server to run high-fidelity tests against real database state in the CI pipeline.

---

## 🔐 Advanced Authentication Flow (Enterprise Standard)

We will transition from simple Cookies to a session-hardened **JWT + Refresh Token** flow.

```mermaid
sequenceDiagram
    participant User as "User (Blazor/Mobile)"
    participant API as "FMC.Api (Backend)"
    participant DB as "SQL Server (GCP)"
    
    User->>API: "POST /api/auth/login (Credentials + OTP)"
    API->>DB: "Validate User & OTP"
    DB-->>API: "Validated"
    API->>API: "Generate Access Token (15m) & Refresh Token (7d)"
    API-->>User: "Return JWT + HTTP-Only Refresh Cookie"
    
    Note over User,API: "Ongoing Data Requests (Bearer Token in Header)"
    
    User->>API: "GET /api/transactions"
    API->>API: "Validate JWT Signature/Expiry"
    API-->>User: "200 OK (Financial Data)"
    
    Note over User,API: "Token Expired Flow"
    
    User->>API: "GET /api/transactions (Expired JWT)"
    API-->>User: "401 Unauthorized"
    User->>API: "POST /api/auth/refresh (Refresh Cookie)"
    API->>DB: "Validate Refresh Token Hash"
    API-->>User: "Issue New JWT Access Token"
```

---

## 🚀 Tailored API Phases

### Phase A: Architecture Extraction (Extraction Foundation)
*Focus: Isolate the core from the UI components.*
1.  **Library Creation**: Scaffold `FMC.Core` and `FMC.Infrastructure` projects.
2.  **Model Decoupling**: Move all database entities to the Core.
3.  **DB Context Migration**: Physically move the Entity Framework layer to Infrastructure.

### Phase B: Secure REST Engine (Security & Access)
*Focus: Move Authentication and Authorization to the API.*
1.  **Identity Port**: Shift ASP.NET Identity stores to the API project.
2.  **JWT Implementation**: Configure `JwtBearer` authentication service.
3.  **Endpoint Hardening**: Implement Rate Limiting (prevent brute force) and Auto-Logging of all high-value transactions.

### Phase C: Financial Service RESTification (The Core Logic)
*Focus: Expose financial services as high-performance REST endpoints.*
1.  **Finance API**: Implement `GET/POST/PUT/DELETE` for Transactions, Accounts, and Budgets.
2.  **DTO Mapping**: Use `AutoMapper` to ensure internal DB schemas are never exposed directly to the web.
3.  **Error Handling**: Global Exception Middleware to return standardized RFC7807 problem details (JSON errors).

### Phase E: Advanced Edge & Distributed Caching
*Focus: High availability and performance optimizations.*
1.  **API Gateway Deployment**: Implement **YARP** (Yet Another Reverse Proxy) for request aggregation and rate limiting.
2.  **GCP Memorystore (Redis)**: Integrate distributed caching for financial summaries and dashboard data.
3.  **Intelligent Invalidation**: Event-driven cache purging using **MediatR Notifications** when ledger state changes.
4.  **Cache Warming**: Automated background workers to pre-calculate spending trends for warm starts.
 
### Phase F: Predictive Intelligence & AI
*Focus: AI-driven financial insights and automation.*
1.  **AI Service Core**: Define the `IAiService` to wrap external LLM calls (e.g., Google Gemini).
2.  **Auto-Categorization**: Real-time transaction categorization using natural language understanding.
3.  **Predictive Forecasting**: AI-generated spending forecasts and balance projections.
4.  **Anomaly Detection API**: Security endpoints to flag suspicious or outlier transactions automatically.
 
---

## ✅ Best Practices Checklist
- [ ] **Versioning**: All endpoints prefixed with `/api/v1/`.
- [ ] **Documentation**: 100% OpenAPI (Swagger) coverage.
- [ ] **Performance**: Response caching for non-sensitive financial summaries.
- [ ] **Validation**: FluentValidation for all incoming request DTOs.
- [ ] **Security**: CORS policies strictly limited to the production frontend domain.
