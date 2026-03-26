# FMC Enterprise API Roadmap & Architecture v2.2

This document defines the strategic roadmap and technical architecture for the Finance Management Console (FMC) Backend API. It aligns with the existing project milestones to move from a monolithic Blazor app to a decoupled, high-available, and secure distributed system.

---

## 🏛 Enterprise Architecture Pattern: Clean Architecture

To ensure the FMC is future-proof and "enterprise-level", we follow a **Clean Architecture (Onion Architecture)** approach. This separates the business logic from the infrastructure and UI.

### Project Structure (6-Layer Architecture):
1.  **`FMC.Domain`**: Pure C# Entities (`Transaction`, `Account`) and Value Objects. No dependencies on any other project.
2.  **`FMC.Application`**: Use Cases and MediatR Command/Query handlers. Orchestrates business logic using Domain entities.
3.  **`FMC.Shared`**: Common **DTOs**, Enums, and Constants. Referenced by both API and UI for contract synchronization.
4.  **`FMC.Infrastructure`**: Persistence (`ApplicationDbContext`), SQL Migrations, and external integrations (SMTP/OTP/Audit).
5.  **`FMC.Api`**: ASP.NET Core Web API with Controllers, JWT Auth middleware, and Swagger.
6.  **`FMC`**: Refactored Blazor Web App interacting with `FMC.Api` via `HttpClient`.

---

## ✅ Completed Architecture Phases

### Phase A: Architecture Extraction (Extraction Foundation)
- [x] **Library Creation**: Scaffolded `FMC.Domain`, `FMC.Application`, `FMC.Shared`, and `FMC.Infrastructure`.
- [x] **Logic Decoupling**: Moved entities to `Domain`, MediatR handlers to `Application`, and DTOs to `Shared`.
- [x] **DB Migration**: Physically moved the Entity Framework layer to Infrastructure.

### Phase B: Secure REST Engine (Security & Access)
- [x] **Identity Port**: Shifted ASP.NET Identity stores and JWT logic to the API project.
- [x] **JWT/Refresh Flow**: Implemented session rotation with secure `HttpOnly` refresh tokens.
- [x] **Role Claims Mapping**: Synchronized Identity roles with JWT claim tokens.

### Phase C: Financial Service RESTification (The Core Logic)
- [x] **Finance API**: Implemented `GET/POST/PUT/DELETE` for Transactions, Accounts, and Budgets.
- [x] **Contract Standardization**: Unified DTOs between the Blazor frontend and ASP.NET Core API.

### Phase D: Governance & Multi-Tenancy (Scale & Security)
- [x] **Tenant Isolation**: Implemented automatic SQL filtering by `TenantID`.
- [x] **Dynamic Context Extraction**: Injected `CurrentUserService` to pull Tenant context from headers/tokens.
- [x] **SuperAdmin Forensic Suite**: Centralized audit log retrieval with filter-ignoring (.IgnoreQueryFilters) visibility.

---

## 🚀 Upcoming API Phases

### Phase E: Edge & Advanced Caching
1.  **GCP Memorystore**: Integrate **Redis** with event-driven cache invalidation patterns.
2.  **API Gateway (YARP)**: Implement a gateway for request aggregation and rate limiting.

### Phase F: Intelligence & Reliability (Observability)
1.  **Exception / Error Monitor**: A dedicated endpoint for SuperAdmins to view server logs remotely.
2.  **Performance Benchmarks**: Document P95 latency for core financial endpoints (SLO tracking).
3.  **OpenTelemetry**: Integration for request tracing and system telemetry.

---

## 📊 Performance Targets (SLIs/SLOs)

| Metric | Target (SLO) | Description |
| :--- | :--- | :--- |
| **P95 Latency (Write)** | < 500ms | Transaction creation/modification. |
| **P95 Latency (Read)** | < 200ms | Dashboard and history queries. |
| **Availability** | 99.9% | Core financial APIs uptime. |
| **Throughput** | 1k req/sec | Per tenant (Auto-scaling at 70% CPU). |

---

## 📡 Observability & DevOps
- **Logging**: Centralized via **Serilog** to GCP Cloud Logging.
- **Monitoring**: Health Checks (`/health`) and OpenTelemetry for request tracing.
- **Disaster Recovery**:
  - **RPO (Recovery Point Objective)**: 15 minutes.
  - **RTO (Recovery Time Objective)**: 1 hour.
  - **Strategy**: Point-in-time recovery for SQL Server; Multi-zone GKE cluster for API.
