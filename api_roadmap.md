1: # FMC Enterprise API Roadmap & Architecture
2: 
3: This document defines the strategic roadmap and technical architecture for the Finance Management Console (FMC) Backend API. It aligns with the existing project milestones to move from a monolithic Blazor app to a decoupled, high-available, and secure distributed system.
4: 
5: ---
6: 
7: ## 🏛 Enterprise Architecture Pattern: Clean Architecture
8: 
9: To ensure the FMC is future-proof and "enterprise-level", we will follow a **Clean Architecture (Onion Architecture)** approach. This separates the business logic from the infrastructure and UI.
10: 
11: ### Project Structure:
12: 1.  **`FMC.Core` (Domain Layer)**: Pure C#. Contains Domain Entities, MediatR Command/Query models, and Service Interfaces.
13: 2.  **`FMC.Shared` (Contract Layer)**: Lightweight library containing **DTOs** and validation logic. Shared by both the API and Blazor UI.
14: 3.  **`FMC.Infrastructure` (Data Layer)**: Implements persistence via `ApplicationDbContext`, SQL Migrations, and external integrations (SMTP/OTP).
15: 4.  **`FMC.Api` (Presentation/Backend)**: ASP.NET Core Web API with Controllers, JWT Auth middleware, and Swagger.
16: 5.  **`FMC` (Frontend)**: Refactored Blazor Web App interacting with `FMC.Api` via `HttpClient`.
17: 
18: ---
19: 
20: ## 🧠 Architectural Resolutions
21: 
22: ### I. Shared Library Versioning
23: - **Deployment Flow**: Synchronized atomic deployments via CI/CD pipelines to ensure the Blazor UI always has matching DTOs from `FMC.Shared`.
24: - **API Stability**: Adhere to additive-only changes for DTOs; never rename or delete properties without a version increment (`/api/v2/`).
25: 
26: ### II. Data Access: Implementation of CQRS
27: - **MediatR Integration**: Decouple commands and queries within `FMC.Core`.
28: - **Performance**: Use optimized Dapper/EF Query models for read operations, bypass heavy business logic for high-speed dashboard telemetry.
29: - **Integrity**: Strict EF Core transactional boundaries for "Commands" (Writing data to account ledgers).
30: 
31: ### III. Progressive Testing Strategy
32: - **Contract Security**: Implement JSON Schema validation or Pact to ensure the Blazor client and API remain in sync.
33: - **Resilient Integration**: Use **Testcontainers** for SQL Server to run high-fidelity tests against real database state in the CI pipeline.
34: 
35: ---
36: 
37: ## 🔐 Advanced Authentication Flow (Enterprise Standard)
38: 
39: We will transition from simple Cookies to a session-hardened **JWT + Refresh Token** flow.
40: 
41: ```mermaid
42: sequenceDiagram
43:     participant User as "User (Blazor/Mobile)"
44:     participant API as "FMC.Api (Backend)"
45:     participant DB as "SQL Server (GCP)"
46:     
47:     User->>API: "POST /api/auth/login (Credentials + OTP)"
48:     API->>DB: "Validate User & OTP"
49:     DB-->>API: "Validated"
50:     API->>API: "Generate Access Token (15m) & Refresh Token (7d)"
51:     API-->>User: "Return JWT + HTTP-Only Refresh Cookie"
52:     
53:     Note over User,API: "Ongoing Data Requests (Bearer Token in Header)"
54:     
55:     User->>API: "GET /api/transactions"
56:     API->>API: "Validate JWT Signature/Expiry"
57:     API-->>User: "200 OK (Financial Data)"
58:     
59:     Note over User,API: "Token Expired Flow"
60:     
61:     User->>API: "GET /api/transactions (Expired JWT)"
62:     API-->>User: "401 Unauthorized"
63:     User->>API: "POST /api/auth/refresh (Refresh Cookie)"
64:     API->>DB: "Validate Refresh Token Hash"
65:     API-->>User: "Issue New JWT Access Token"
66: ```
67: 
68: ---
69: 
70: ## 🚀 Tailored API Phases
71: 
72: ### Phase A: Architecture Extraction (Extraction Foundation)
73: *Focus: Isolate the core from the UI components.*
74: 1.  **Library Creation**: Scaffold `FMC.Core`, `FMC.Shared`, and `FMC.Infrastructure`.
75: 2.  **Model Decoupling**: Move entities to `Core` and DTOs to `Shared`.
76: 3.  **DB Context Migration**: Physically move the Entity Framework layer to Infrastructure.
77: 
78: ### Phase B: Secure REST Engine (Security & Access)
79: *Focus: Move Authentication and Authorization to the API.*
80: 1.  **Identity Port**: Shift ASP.NET Identity stores to the API project.
81: 2.  **JWT Implementation**: Configure `JwtBearer` authentication service.
82: 3.  **Endpoint Hardening**: Implement Rate Limiting and Auto-Logging of all high-value transactions.
83: 
84: ### Phase C: Financial Service RESTification (The Core Logic)
85: *Focus: Expose financial services as high-performance REST endpoints.*
86: 1.  **Finance API**: Implement `GET/POST/PUT/DELETE` for Transactions, Accounts, and Budgets.
87: 2.  **DTO Mapping**: Use `AutoMapper` and ensure internal entities are never exposed to the web.
88: 3.  **Error Handling**: Global Exception Middleware returning standardized RFC7807 problem details.
89: 
90: ### Phase D: Governance & Multi-Tenancy (Scale & Security)
91: *Focus: Support for hierarchy and isolation.*
92: 1.  **Tenant Middleware**: Filter queries by `TenantID` (Family/Organization) at the infrastructure level.
93: 2.  **Ledger Integrity**: Cryptographic audit trails for Mother Account transfers.
94: 3.  **Governance API**: SuperAdmin endpoints for system-wide forensic review.
95: 
96: ### Phase E: Advanced Edge & Distributed Caching
97: *Focus: High availability and performance.*
98: 1.  **API Gateway**: Implement **YARP** for request aggregation and rate limiting.
99: 2.  **GCP Memorystore**: Integrate **Redis** for dashboard and session caching.
100: 3.  **Cache Warming**: Background workers to pre-calculate financial trends.
101:  
102: ### Phase F: Predictive Intelligence & AI
103: *Focus: AI-driven financial insights.*
104: 1.  **AI Service Core**: Wrapper for Gemini/LLM integrations.
105: 2.  **Auto-Categorization**: Real-time NLU for transaction labels.
106: 3.  **Anomaly API**: Automated flags for outlier financial behavior.
107: 
108: ---
109: 
110: ## 📡 Observability & DevOps
111: - **Logging**: Centralized logging via **Serilog** to GCP Cloud Logging.
112: - **Monitoring**: Health Checks (`/health`) and OpenTelemetry for request tracing.
113: - **CI/CD**: Multi-project build pipeline using GitHub Actions (Build -> Test -> Deploy).
114: 
115: ---
116: 
117: ## ✅ Final Enterprise Checklist
118: - [ ] **Versioning**: All endpoints prefixed with `/api/v1/`.
119: - [ ] **Documentation**: 100% OpenAPI (Swagger) coverage.
120: - [ ] **Validation**: FluentValidation for all incoming request DTOs.
121: - [ ] **Security**: Strictly enforced CORS and Secure Header policies.
