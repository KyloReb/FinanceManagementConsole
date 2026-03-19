# FMC Project Roadmap & Milestones

This document tracks the evolution of the Finance Management Console (FMC). It summarizes the enterprise milestones achieved today and outlines the strategic roadmap for future development.

---

## 📖 Table of Contents
- [✅ Completed Phases](#-completed-phases-todays-achievements)
  - [Phase 1: Cloud Infrastructure](#phase-1)
  - [Phase 2: Identity Security](#phase-2)
  - [Phase 3: Multi-Factor OTP](#phase-3)
  - [Phase 4: Enterprise Auth UI](#phase-4)
  - [Phase 5: UI/UX Polish](#phase-5)
  - [Phase 6: Standardization](#phase-6)
- [🚀 Upcoming Phases](#-upcoming-phases-strategic-roadmap)
  - [Phase 7: Profile & Security](#phase-7)
  - [Phase 8: Financial Intelligence](#phase-8)
  - [Phase 9: Reporting 2.0](#phase-9)
  - [Phase 10: Multi-Tenant](#phase-10)
  - [Phase 11: Hierarchical Management](#phase-11)

---

## ✅ Completed Phases (Today's Achievements)

<details id="phase-1">
<summary><b>Phase 1: Cloud Infrastructure & Data Access</b></summary>

- **Google Cloud SQL Integration**: Established secure connectivity to a remote GCP SQL instance.
- **EF Core Modernization**: Successfully scaffolded and applied migrations for a multi-layered identity schema.
- **Connection Resiliency**: Implemented `IDbContextFactory` to prevent threading issues in Blazor Server environments.
</details>

<details id="phase-2">
<summary><b>Phase 2: Identity Security & Audit Governance</b></summary>

- **Extended User Schema**: Injected `FirstName`, `LastName`, `AccountStatus`, and `LoginTelemetry` into the core Identity model.
- **Centralized Audit Logging**: Built a diagnostic engine to track sensitive security events (Logins, Failed OTPs, Deactivations).
</details>

<details id="phase-3">
<summary><b>Phase 3: Multi-Factor OTP Engine</b></summary>

- **MailKit SMTP Integration**: Configured a professional email dispatcher for security codes.
- **Secure Verification Logic**: Built an OTP generation and validation service with 60s rate-limiting and 5-attempt brute-force protection.
</details>

<details id="phase-4">
<summary><b>Phase 4: Enterprise Authentication UI</b></summary>

- **Interactive Auth Components**: Developed sleek `Login`, `Register`, and `EmailVerification` pages using MudBlazor.
- **Real-Time Caps Lock Detection**: Implemented a custom C# interceptor to warn users about hardware state during password entry.
- **Security Feedback**: Specific error messaging for lockout states, unverified emails, and deactivations.
</details>

<details id="phase-5">
<summary><b>Phase 5: UI/UX Polish & Performance</b></summary>

- **Skeleton Loading Screens**: Implemented asynchronous placeholders on the Dashboard to eliminate layout shift.
- **Global Navigation Progress**: Added a top-mounted transition bar reacting to HTTP routing events.
- **Dependency Optimization**: Resolved complex DI conflicts in `Program.cs` to ensure startup stability.
</details>

<details id="phase-6">
<summary><b>Phase 6: Enterprise Standardization & Deployment</b></summary>

- **Total Code Documentation**: 100% XML Documentation coverage (`<summary>`, `<param>`, `<returns>`) across all project files.
- **Structural Regions**: Organized the bootstrapper and core services into logical `#region` blocks.
- **GitHub Launch**: Established the official repository at [KyloReb/FinanceManagementConsole](https://github.com/KyloReb/FinanceManagementConsole) with root-level documentation.
</details>

---

## 🚀 Upcoming Phases (Strategic Roadmap)

<details id="phase-7">
<summary><b>Phase 7: User Profile & Security Settings</b></summary>

- **Self-Service Security**: Interface for users to update passwords and toggle 2FA.
- **Profile Management**: Ability to update personal details and upload profile avatars.
- **Activity Timeline**: A user-facing view of their recent `AuditLog` security events.
</details>

<details id="phase-8">
<summary><b>Phase 8: Advanced Financial Intelligence</b></summary>

- **Interactive Visualizations**: Expand the dashboard with dynamic MudCharts for Category-wise spending and Income-vs-Expense trends.
- **Recurring Transaction Detection**: Logic to automatically flag and project monthly subscriptions and utilities.
- **Budget Alerts**: Push notifications or snackbars when a category reaches 80% or 100% of its budget limit.
</details>

<details id="phase-9">
<summary><b>Phase 9: Reporting & Governance 2.0</b></summary>

- **Branded PDF Export**: Upgrade `QuestPDF` logic to include company breading, charts, and summary tables in the generated reports.
- **Bulk Data Operations**: Ability to import transactions via CSV/Excel and bulk-categorize historical data.
- **Multi-Account Reconciliation**: Logic to "clear" transactions against physical bank statements.
</details>

<details id="phase-10">
<summary><b>Phase 10: Multi-Tenant & Family Support</b></summary>

- **Shared Access**: Allow users to invite "Collaborators" (e.g., family members) to specific accounts.
- **Permission Boundaries**: Implement Role-Based Access Control (RBAC) at the account level (e.g., Viewer vs. Manager).
- **Currency Agnostic Engine**: Support for multi-currency tracking with automated exchange rate updates.
</details>

<details id="phase-11">
<summary><b>Phase 11: Hierarchical "Mother Account" Management</b></summary>

- **Enterprise Ledger Logic**: Implement a "Mother Account" (Master Vault) that acts as the primary liquidity source for sub-accounts.
- **Authority-Based RBAC**: Define specialized high-privilege roles (e.g., **CEO**, **Manager**) with exclusive permissions to execute ledger operations.
- **Managed Debit/Credit Workflows**: Empower authorized roles to allocate funds (Debit) to client sub-accounts and collect funds (Credit) back to the Mother Account.
- **Organization-Based Mapping**: Associate groups of users with a specific Client/Organization ID for isolated balance management.
- **Transaction Sovereignty**: Ensure all inter-account transfers are cryptographically linked in the `AuditLog` for total forensic transparency.
</details>
