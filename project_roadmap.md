# FMC Project Roadmap & Milestones

This document tracks the evolution of the Finance Management Console (FMC). It summarizes the enterprise milestones achieved today and outlines the strategic roadmap for future development.

---

## ✅ Completed Phases (Today's Achievements)

### Phase 1: Cloud Infrastructure & Data Access
- **Google Cloud SQL Integration**: Established secure connectivity to a remote GCP SQL instance.
- **EF Core Modernization**: Successfully scaffolded and applied migrations for a multi-layered identity schema.
- **Connection Resiliency**: Implemented `IDbContextFactory` to prevent threading issues in Blazor Server environments.

### Phase 2: Identity Security & Audit Governance
- **Extended User Schema**: Injected `FirstName`, `LastName`, `AccountStatus`, and `LoginTelemetry` into the core Identity model.
- **Centralized Audit Logging**: Built a diagnostic engine to track sensitive security events (Logins, Failed OTPs, Deactivations).

### Phase 3: Multi-Factor OTP Engine
- **MailKit SMTP Integration**: Configured a professional email dispatcher for security codes.
- **Secure Verification Logic**: Built an OTP generation and validation service with 60s rate-limiting and 5-attempt brute-force protection.

### Phase 4: Enterprise Authentication UI
- **Interactive Auth Components**: Developed sleek `Login`, `Register`, and `EmailVerification` pages using MudBlazor.
- **Real-Time Caps Lock Detection**: Implemented a custom C# interceptor to warn users about hardware state during password entry.
- **Security Feedback**: Specific error messaging for lockout states, unverified emails, and deactivations.

### Phase 5: UI/UX Polish & Performance
- **Skeleton Loading Screens**: Implemented asynchronous placeholders on the Dashboard to eliminate layout shift.
- **Global Navigation Progress**: Added a top-mounted transition bar reacting to HTTP routing events.
- **Dependency Optimization**: Resolved complex DI conflicts in `Program.cs` to ensure startup stability.

### Phase 6: Enterprise Standardization & Deployment
- **Total Code Documentation**: 100% XML Documentation coverage (`<summary>`, `<param>`, `<returns>`) across all project files.
- **Structural Regions**: Organized the bootstrapper and core services into logical `#region` blocks.
- **GitHub Launch**: Established the official repository at [KyloReb/FinanceManagementConsole](https://github.com/KyloReb/FinanceManagementConsole) with root-level documentation.

---

## 🚀 Upcoming Phases (Strategic Roadmap)

### Phase 7: User Profile & Security Settings
- **Self-Service Security**: Interface for users to update passwords and toggle 2FA.
- **Profile Management**: Ability to update personal details and upload profile avatars.
- **Activity Timeline**: A user-facing view of their recent `AuditLog` security events.

### Phase 8: Advanced Financial Intelligence
- **Interactive Visualizations**: Expand the dashboard with dynamic MudCharts for Category-wise spending and Income-vs-Expense trends.
- **Recurring Transaction Detection**: Logic to automatically flag and project monthly subscriptions and utilities.
- **Budget Alerts**: Push notifications or snackbars when a category reaches 80% or 100% of its budget limit.

### Phase 9: Reporting & Governance 2.0
- **Branded PDF Export**: Upgrade `QuestPDF` logic to include company breading, charts, and summary tables in the generated reports.
- **Bulk Data Operations**: Ability to import transactions via CSV/Excel and bulk-categorize historical data.
- **Multi-Account Reconciliation**: Logic to "clear" transactions against physical bank statements.

### Phase 10: Multi-Tenant & Family Support
- **Shared Access**: Allow users to invite "Collaborators" (e.g., family members) to specific accounts.
- **Permission Boundaries**: Implement Role-Based Access Control (RBAC) at the account level (e.g., Viewer vs. Manager).
- **Currency Agnostic Engine**: Support for multi-currency tracking with automated exchange rate updates.

### Phase 11: Hierarchical "Mother Account" Management
- **Enterprise Ledger Logic**: Implement a "Mother Account" (Master Vault) that acts as the primary liquidity source for sub-accounts.
- **Managed Debit/Credit Workflows**: Allow Managers to allocate funds (Debit) to client sub-accounts and collect funds (Credit) back to the Mother Account.
- **Organization-Based Mapping**: Associate groups of users with a specific Client/Organization ID for isolated balance management.
- **Transaction Sovereignty**: Ensure all inter-account transfers are cryptographically linked in the `AuditLog` for total forensic transparency.
