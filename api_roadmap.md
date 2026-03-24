# FMC Enterprise API Roadmap & Architecture v2.0

This document defines the strategic roadmap and technical architecture for the Finance Management Console (FMC) Backend API. It aligns with the existing project milestones to move from a monolithic Blazor app to a decoupled, high-available, and secure distributed system.

---

## 🏛 Enterprise Architecture Pattern: Clean Architecture

To ensure the FMC is future-proof and "enterprise-level", we follow a **Clean Architecture (Onion Architecture)** approach. This separates the business logic from the infrastructure and UI.

### Project Structure (6-Layer Architecture):
1.  **`FMC.Domain`**: Pure C# Entities (`Transaction`, `Account`) and Value Objects. No dependencies on any other project.
2.  **`FMC.Application`**: Use Cases and MediatR Command/Query handlers. Orchestrates business logic using Domain entities.
3.  **`FMC.Shared`**: Common **DTOs**, Enums, and Constants. Referenced by both API and UI for contract synchronization.
4.  **`FMC.Infrastructure`**: Persistence (`ApplicationDbContext`), SQL Migrations, and external integrations (SMTP/OTP).
5.  **`FMC.Api`**: ASP.NET Core Web API with Controllers, JWT Auth middleware, and Swagger.
6.  **`FMC`**: Refactored Blazor Web App interacting with `FMC.Api` via `HttpClient`.

---

## 🚀 Tailored API Phases

### Phase 0: Foundation Testing & Infrastructure
*Goal: Establish the technical baseline before extraction.*
1.  **Testcontainers Setup**: Integrate SQL Server Testcontainers for high-fidelity integration tests.
2.  **CI/CD Pipeline**: Implement GitHub Actions with security scanning (Snyk/SonarQube) and automated tests.
3.  **Performance Benchmarks**: Establish baseline P95 latency metrics for current monolithic endpoints.

### Phase A: Architecture Extraction (Extraction Foundation)
1.  **Library Creation**: Scaffold `FMC.Domain`, `FMC.Application`, `FMC.Shared`, and `FMC.Infrastructure`.
2.  **Logic Decoupling**: Move entities to `Domain`, MediatR handlers to `Application`, and DTOs to `Shared`.
3.  **DB Migration**: Physically move the Entity Framework layer to Infrastructure.

### Phase B: Secure REST Engine (Security & Access)
1.  **Identity Port**: Shift ASP.NET Identity stores and JWT logic to the API project.
2.  **Rate Limiting**: Implement IP-based and User-based rate limiting on sensitive endpoints.

### Phase C: Financial Service RESTification (The Core Logic)
1.  **Finance API**: Implement `GET/POST/PUT/DELETE` for Transactions, Accounts, and Budgets.
2.  **Contract Testing**: Implement **Pact** flow (Consumer/Provider contract validation).

### Phase D: Governance & Multi-Tenancy (Scale & Security)
1.  **Tenant Isolation**: Implement automatic SQL filtering by `TenantID`.
2.  **Ledger Integrity**: Cryptographic audit trails for Mother Account transfers.

### Phase E: Advanced Edge & Distributed Caching
1.  **API Gateway**: Implement **YARP** for request aggregation and rate limiting.
2.  **GCP Memorystore**: Integrate **Redis** with event-driven cache invalidation.

---

## 📊 Performance Targets (SLIs/SLOs)

| Metric | Target (SLO) | Description |
| :--- | :--- | :--- |
| **P95 Latency (Write)** | < 500ms | Transaction creation/modification. |
| **P95 Latency (Read)** | < 200ms | Dashboard and history queries. |
| **Availability** | 99.9% | Core financial APIs uptime. |
| **Throughput** | 1k req/sec | Per tenant (Auto-scaling at 70% CPU). |

---

## ✅ Success Metrics

| Technical Metrics | Business Metrics |
| :--- | :--- |
| API Error Rate < 0.1% | User-reported latency < 1s |
| Cache Hit Rate > 80% | Zero Security Incidents |
| Deployment Success > 99% | 100% Data Transfer Integrity |

---

## 🔐 Contract Testing Flow (Pact)
1. **Blazor UI (Consumer)**: Defines expectations in Pact JSON file.
2. **CI Pipeline**: Builds pact file and uploads to Pact Broker.
3. **FMC.Api (Provider)**: Verifies functionality against pact file in CI.
4. **Enforcement**: Pact failure blocks deployment to production.

---

## 📡 Observability & DevOps
- **Logging**: Centralized via **Serilog** to GCP Cloud Logging.
- **Monitoring**: Health Checks (`/health`) and OpenTelemetry for request tracing.
- **CI/CD**: Build -> Security Scan -> Test -> Deploy.
