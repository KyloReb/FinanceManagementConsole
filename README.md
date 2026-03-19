# Finance Management Console (FMC)

The **Finance Management Console (FMC)** is an enterprise-grade personal finance tracking platform built with **.NET 10** and **MudBlazor**. It provides a robust, beautifully animated interface for managing transactions, aggregating accounts, analyzing budgets, and generating financial reports.

---

## 📖 Table of Contents
- [🏗️ Architecture & Security](#️-architecture--security)
- [🔐 Advanced Authentication Engine](#-advanced-authentication-engine)
- [✨ Key Features](#-key-features)
- [🚀 Getting Started](#-getting-started)
  - [Prerequisites](#prerequisites)
  - [Setup](#setup)

---

## 🏗️ Architecture & Security

The system is rigorously structured using clean, domain-driven boundaries and heavily fortified with standard enterprise security parameters:

| Layer | Location | Responsibility |
|---|---|---|
| **Data Entities** | `FMC/Models` | Core models (`Account`, `Transaction`) adorned with precise XML schemas. |
| **Data Context** | `FMC/Data` | Entity Framework Core context bound to **Remote Google Cloud SQL**. |
| **Business Logic** | `FMC/Services` | Interface-segregated services (`IFinanceService`, `IOtpService`, `IEmailService`). |
| **API Endpoints** | `FMC/Controllers` | Swagger-ready REST controllers providing cookie-based session management. |
| **Presentation** | `FMC/Components` | Interactive Blazor Server views optimized with MudBlazor components. |

---

## 🔐 Advanced Authentication Engine

FMC utilizes a state-of-the-art authentication pipeline wrapping **ASP.NET Core Identity**:
- **Multi-Factor OTP Emailing**: Upon registration, users receive a time-bound, 6-digit cryptographic code dispatched via `MailKit` SMTP to securely verify their email address.
- **Dynamic Rate Limiting**: Intelligent throttling blocks OTP spamming and tracks failed verification attempts to prevent brute-force attacks.
- **Account Lifecycles**: Strict `IsActive` tracking, Lockout on failure policies, and real-time login timestamp monitoring.
- **Audit Compliance**: A centralized `AuditLog` captures sensitive operational events across the system to maintain diagnostic security boundaries.
- **Real-Time Caps Lock Detection**: Custom C# heuristic interceptors instantly warn users when Caps Lock is physically engaged during password entry.

---

## ✨ Key Features

- **Dashboard with Skeleton Loading**: Displays meticulously animated skeleton placeholders while data loads asynchronously over the network, providing near-instant UI feedback.
- **Global Navigation Transitions**: A top-mounted `MudProgressLinear` bar triggers during Blazor HTTP routing events to deliver fluid visual transitions.
- **Report Generation Engine**: Dynamically builds and exports raw `.xlsx` files using `ClosedXML` and beautifully formatted `.pdf` documents using `QuestPDF`.
- **Hybrid Theming Network**: Light and Dark theme selections execute instantly and synchronously persist using both HttpCookies and LocalStorage ensuring zero flash-of-unstyled-content.
- **Enterprise XML Documentation**: 100% of physical implementation files (`.cs`, `.razor`) contain exact C# `<summary>` descriptors and `#region` block partitions.

---

## 🚀 Getting Started

### Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/)
- Standard Google Cloud SQL (or local SQL Server Express)
- SMTP Configuration Credentials (e.g., Gmail App Password)

### Setup

1. **Configure Connections**: Update the `appsettings.json` with your Google Cloud SQL `DefaultConnection` and `SmtpSettings`.
2. **Execute EF Migrations**:
   ```bash
   dotnet ef database update
   ```
3. **Application Bootstrapping**:
   ```bash
   dotnet run
   ```
   > **Note:** The `ApplicationDbSeeder` automatically bootstraps the database roles (`SuperAdmin`, `Admin`, `User`) and provisions an initial root admin account upon the first startup block.
