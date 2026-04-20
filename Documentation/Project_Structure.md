# 🧱 Enterprise Architecture — Finance Management Console (FMC)

This document provides a **high-fidelity technical map** of the FMC source code. The solution follows **Clean Architecture (Onion)** to maximize decoupling, testability, and future scalability across multiple database servers.

> **Last Updated**: 2026-04-20 — Reflects Performance Orchestration, Polly Resilience, and Hangfire integration.

---

## 🏛 The Solution Ecosystem (7 Layers)

### 💻 1. `FMC` — Blazor Presentation Layer
The interactive portal built with **Blazor Server** and **MudBlazor 9**.

| Folder | Purpose |
| :--- | :--- |
| `Pages/Admin/` | Admin interfaces: `LoginLogs.razor`, `ManageUsers.razor`, `AuditExplorer.razor` |
| `Pages/Admin/Dialogs/` | Forensic drill-down components: `AuditDetailDialog.razor` |
| `Components/Dashboard/` | Role-scoped dashboard panels: `ExecutiveAlertPanel`, `WorkflowAlertPanel`, `MakerDashboard` |
| `Components/Pages/` | Core pages: `PendingRequests.razor`, `Transactions.razor`, `Reports.razor`, `Accounts.razor` |
| `Services/Api/` | **Blazor-to-API Bridge** — all `HttpClient` calls live here |

**Key Services:**
- `AuthService.cs` — JWT persistence and role-based entry/exit
- `OrganizationApiService.cs` — Multi-tenant organizational data bridge
- `ApiFinanceService.cs` — Accounts, transactions, and ledger data

---

### ⚡ 2. `FMC.Api` — The Gateway Layer
A headless **ASP.NET Core REST API** serving as the unified data entry point.

| File | Responsibility |
| :--- | :--- |
| `AuthController.cs` | Login, logout, OTP-driven registration |
| `OrganizationsController.cs` | Multi-tenant org lifecycle management |
| `UsersController.cs` | User profile lifecycle and workflow alerts |
| `AlertsController.cs` | System-wide health and security alerts |
| `TransactionsController.cs` | Multi-tenant financial movements |
| `Program.cs` | Configures JWT, Caching, Hangfire, Polly DI container |

**Key Infrastructure configured in `Program.cs`:**
- JWT Bearer Authentication
- EF Core with `EnableRetryOnFailure` (Layer 1 resilience)
- Hangfire SQL Server job queue (2 background workers)
- Polly resilient repository decorator (Layer 2 resilience)
- Hybrid Distributed Cache (Memory in Dev / Redis in Prod)

---

### 🧠 3. `FMC.Application` — The Logic Layer
The "Pure Core" — no UI, no database, no infrastructure dependencies.

| Folder/File | Purpose |
| :--- | :--- |
| `Interfaces/IOrganizationRepository.cs` | Batched data access contract |
| `Interfaces/IBackgroundJobService.cs` | ✨ **NEW** — Abstraction over Hangfire (swap-safe) |
| `Interfaces/IEmailService.cs` | Email delivery contract |
| `Interfaces/IEmailTemplateService.cs` | HTML template generation contract |
| `Interfaces/ILedgerService.cs` | Atomic financial transfer contract |
| `Organizations/Events/` | MediatR domain events: Pending, Approved, WalletAdjusted |

---

### 🔗 4. `FMC.Infrastructure` — The Technical Layer

#### 📁 Data/
- `ApplicationDbContext.cs` — EF Core with Global Tenant Filters and performance indexes on `Transactions` (`OrganizationId`, `Date`, `Status`)

#### 📁 Repositories/
| File | Purpose |
| :--- | :--- |
| `OrganizationRepository.cs` | Primary data access — now includes `GetAllWithStatsAsync` (batched 4-query pattern) |
| `ResilientOrganizationRepository.cs` | ✨ **NEW** — Polly decorator adding retry logic transparently |

#### 📁 Services/
- `OrganizationService.cs` — Now uses `GetAllWithStatsAsync` — reduces 300+ DB calls to 4
- `OrganizationNotificationHandler.cs` — ✨ **Refactored** — now enqueues Hangfire jobs instead of sending emails inline
- `LedgerService.cs`, `AuditService.cs`, `IdentityService.cs`, `EmailService.cs`

#### 📁 BackgroundJobs/ ✨ NEW
| File | Purpose |
| :--- | :--- |
| `HangfireBackgroundJobService.cs` | Hangfire implementation of `IBackgroundJobService` |
| `NotificationJobService.cs` | 3 atomic, retryable email notification jobs |

#### 📁 Resilience/ ✨ NEW
| File | Purpose |
| :--- | :--- |
| `ResiliencePolicies.cs` | Centralized Polly pipeline factory (DB retry, Circuit Breaker) |

#### 📁 BackgroundServices/
- `HealthMonitorService.cs` — Real-time anomalous activity detection (hosted service)

---

### 💎 5. `FMC.Domain` — The Core Domain
Pure C# business entities — zero external dependencies.

| Entity | Key Role |
| :--- | :--- |
| `Transaction.cs` | Pending/Approved/Rejected financial movement |
| `Organization.cs` | Multi-tenant business unit |
| `ApplicationUser.cs` | Extended Identity user with `OrganizationId` and `AccountNumber` |
| `Account.cs` | Wallet/balance holder (org-level or user-level) |
| `AuditLog.cs` | Immutable forensic event record |
| `SystemAlert.cs` | Real-time operational health notification |

---

### 🌉 6. `FMC.Shared` — The Bridge Layer
Portable class library shared between the Blazor UI and the API.

| Folder | Purpose |
| :--- | :--- |
| `DTOs/Organization/` | `OrganizationDto`, `CreateOrganizationDto`, `UpdateOrganizationDto` |
| `DTOs/User/` | `UserDto`, `CreateUserDto`, `UpdateUserDto` |
| `DTOs/Admin/` | `SystemAlertDto`, `AuditLogDto` |
| `Auth/Roles.cs` | Centralized role constants: `SuperAdmin`, `CEO`, `Maker`, `Approver` |
| `Utils/FinanceUtils.cs` | Card masking, formatting helpers |

---

## 🧭 Quick-Start Development Guide

#### 👤 User & Role Management
- **File**: `FMC.Infrastructure/Services/IdentityService.cs`

#### 📊 Auditing & Forensics
- **File**: `FMC.Domain/Entities/AuditLog.cs`

#### 🎨 UI Design & Branding
- **File**: `FMC/Components/Pages/` and `FMC/wwwroot/index.css`

#### 📡 New API Endpoint
- **File**: `FMC.Api/Controllers/`

#### 💾 Database Schema / Query Filters
- **File**: `FMC.Infrastructure/Data/ApplicationDbContext.cs`

#### 📬 Background Job / Email Dispatch
- **File**: `FMC.Infrastructure/BackgroundJobs/NotificationJobService.cs`
- **Enqueue via**: `IBackgroundJobService.Enqueue<NotificationJobService>(...)`

#### 🛡️ Resilience Policies
- **File**: `FMC.Infrastructure/Resilience/ResiliencePolicies.cs`

#### 🌉 Shared DTOs
- **File**: `FMC.Shared/DTOs/[Name]Dto.cs`

---

## 🏗 Sub-Ecosystem Documentation

- [📜 Project Roadmap](../project_roadmap.md)
- [🛡️ RBAC Architecture](rbac_permissions_architecture.md)
- [🔁 Authentication Flow](authentication_flow.md)
- [📊 Audit Architecture](security_audit_architecture.md)
- [🗄️ Distributed Caching](distributed_caching_architecture.md)
- [🔄 Registration & Recovery Workflows](registration_recovery_workflows.md)
- [⚡ Background Job Architecture](background_job_architecture.md)
- [🛡️ Resilience & Performance Architecture](resilience_performance_architecture.md)
