# 🧱 Deep-Dive: Enterprise Six-Layer Architecture

This document provides a **high-fidelity map** of the Finance Management Console (FMC) source code. Our solution follows the **Clean Architecture** pattern to ensure maximum decoupling, testability, and future-proof scalability.

<br/>

---

<br/>

## 🏛 The Solution Ecosystem

### 💻 1. **`FMC`** (Blazor Presentation Layer)
The interactive portal for all users, built with **Blazor Server** and **MudBlazor 9**.
  - **📁 Pages/Admin/**: Highly-optimized interfaces like **LoginLogs.razor**, **ManageUsers.razor**, and the **AuditExplorer.razor** (Audit Intelligence Explorer) which utilize specialized `AdminService` calls.
  - **📁 Pages/Admin/Dialogs/**: Forensic drill-down components like **AuditDetailDialog.razor**.
- **📁 Services/Api/**: The **Blazor-to-API Bridge**. These services encapsulate all `HttpClient` calls, ensuring that the UI remains ignorant of the underlying REST protocol.
  - **`AuthService.cs`**: Critical link for platform entry/exit and role-based token persistence.
  - **`AdminService.cs`**: Core forensic and administrative toolset for SuperAdmin workflows.
  - **`ApiFinanceService.cs`**: Gateway for accounts, transactions, and enterprise budgeting data.
  - **`OrganizationApiService.cs`**: Specialized bridge for multi-tenant organizational mapping.
- **📁 Authentication/**
  - **`ApiAuthenticationStateProvider.cs`**: The heart of frontend identity, bridging JWT claims to Blazor's cascading authorization state.
  - **`AuthenticationHeaderHandler.cs`**: A smart interceptor that automatically attaches Bearer tokens to every outgoing API request.
- **📁 wwwroot/**: Contains the **`index.css`** design system and the enterprise-themed assets.

<br/>

### ⚡ 2. **`FMC.Api`** (The Gateway Layer)
A headless **ASP.NET Core REST API** serving as the unified data entry point.
- **📁 Controllers/**
  - **`AuthController.cs`**: Orchestrates identity, including login, logout, and OTP-driven registration.
  - **`OrganizationsController.cs`**: Manages multi-tenant business units and organizational structures.
  - **`UsersController.cs`**: Handles user profile lifecycle and administrative account management.
  - **`AuditController.cs`**: Exposes forensic security logs and system-wide authentication trails.
  - **`AlertsController.cs`**: High-performance gateway for system-wide health and security alerts.
  - **`AccountsController.cs`**: Manages the life-cycle of individual bank accounts and financial entities.
  - **`TransactionsController.cs`**: Specialized endpoint for multi-tenant financial movements and auditing.
  - **`BudgetsController.cs`**: Logic gateway for fiscal limit management and enterprise budgeting.
  - **`DocumentationController.cs`**: The technical delivery engine for these architecture guides.
- **`Program.cs`**: Configures the **JWT Middleware**, **CORS policies**, and the global **Dependency Injection (DI)** container.

<br/>

### 🧠 3. **`FMC.Application`** (The Logic Layer)
The "Core Processor" of the application, containing no UI or database dependencies.
- **📁 Interfaces/**
  - **`IApplicationDbContext.cs`**: Defines the data-access contract.
  - **`IAuditService.cs`** / **`IEmailService.cs`**: Functional blueprints for cross-cutting services.
  - **`ISystemAlertService.cs`**: Contract for system-wide health monitoring and suspicious activity detection.
- **📁 Common/**: Contains global behaviors, audit-request pipelines, and central validation logic.

<br/>

### 🔗 4. **`FMC.Infrastructure`** (The Technical Layer)
The heavy-lifting implementation for external resources.
- **📁 Data/**
  - **`ApplicationDbContext.cs`**: Implementation of EF Core with **Global Tenant Filters** and specialized interceptors for auditable entities.
  - **`ApplicationDbSeeder.cs`**: Boots the system with default roles and the initial SuperAdmin account.
- **📁 Services/**
  - **`IdentityService.cs`**: Complex identity logic using Microsoft Identity to manage claims and security stamps.
  - **`AuditService.cs`**: High-performance logging engine capable of resolving device telemetry and organizational mapping.
  - **`CurrentUserService.cs`**: The heart of the **Multi-Tenant Jailing** system, providing real-time identity and tenant context to all layers.
  - **`EmailService.cs`**: Handles cryptographic OTP delivery via SMTP for secure account verification.
  - **`OrganizationService.cs`**: Heavy-duty business logic for enterprise organizational mapping and lifecycle.
  - **`SystemAlertService.cs`**: Implementation of automated system-wide health and security monitoring.
- **📁 BackgroundServices/**
  - **`HealthMonitorService.cs`**: A persistent background engine for real-time anomalous activity detection.
- **📁 Migrations/**: Auto-generated SQL evolution files tracked by the repository.

<br/>

### 💎 5. **`FMC.Domain`** (The Core Domain)
The absolute center of the system. It contains the business entities and is protected from all external changes.
- **📁 Entities/**: Pure C# objects like **AuditLog**, **Transaction**, **Organization**, and **SystemAlert**. 
- **📁 Common/ITenantEntity.cs**: The contractual interface that drives our multi-tenant "Jailing" technology.

<br/>

### 🌉 6. **`FMC.Shared`** (The Bridge Layer)
A portable class library shared across both the Frontend and the Backend.
- **📁 DTOs/**
  - **`AuthResponseDto.cs`**: Unified tokens and user metadata.
  - **`AuditLogDto.cs`**: The flattened data structure for enterprise reporting.
  - **`SystemAlertDto.cs`**: Standardized DTO for system-wide notifications.
- **📁 Auth/Roles.cs**: Centralized definition of **SuperAdmin**, **CEO**, **Manager**, and **User** role strings.

<br/>

---

<br/>

## 🧭 Navigating Development Tasks (Mobile Ready)

Below is a quick-start guide for finding where to implement changes.

#### **👤 User & Role Management**
- **Action**: Change a User's Role or Permissions.
- **Folder**: `FMC.Infrastructure/Services`
- **Primary File**: `IdentityService.cs`

#### **📊 Auditing & Forensics**
- **Action**: Add a new field to Security Logs or System Alerts.
- **Folder**: `FMC.Domain/Entities`
- **Primary File**: `AuditLog.cs` or `SystemAlert.cs`

#### **🎨 UI Design & Branding**
- **Action**: Update the Login Page or Main Dashboard.
- **Folder**: `FMC/Pages`
- **Primary File**: `Login.razor`

#### **📡 API & Security Boundaries**
- **Action**: Expose a new REST route or modify JWT logic.
- **Folder**: `FMC.Api/Controllers`
- **Primary File**: `AuthController.cs`

#### **💾 Data Persistence**
- **Action**: Update Database Schema or Query Filters.
- **Folder**: `FMC.Infrastructure/Data`
- **Primary File**: `ApplicationDbContext.cs`

#### **🌉 Shared Contracts**
- **Action**: Share new data objects between UI & API.
- **Folder**: `FMC.Shared/DTOs`
- **Primary File**: `[Name]Dto.cs`

<br/>

---

<br/>

## 🏗 Sub-Ecosystems

- [**📜 Project Roadmap**](../project_roadmap.md)
- [**🛡️ RBAC Architecture**](rbac_permissions_architecture.md)
- [**🔁 Authentication Flow**](authentication_flow.md)
- [**📊 Audit Architecture**](security_audit_architecture.md)
