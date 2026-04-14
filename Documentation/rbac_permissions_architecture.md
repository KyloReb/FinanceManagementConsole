# Role-Based Access Control (RBAC) & Permissions

This document outlines the **Authorization Architecture** of the Finance Management Console (FMC), defining how user privileges are enforced, managed, and audited.

---

## 1. Security Hierarchy

FMC uses a high-granularity RBAC model. Permissions are tied to **Roles**, which are then associated with individual user tokens. 

```mermaid
graph TD
    SA[SuperAdmin] -->|Full System Access| CORE[All Operations]
    CEO[CEO] -->|Institutional Governance| FIN[User Creation & Global Logs]
    MAK[Maker] -->|Initiation| TRX[Proposed Adjustments]
    APP[Approver] -->|Validation| COM[Transaction Commitment]
    USR[User] -->|Self-Service| PRV[Personal Identity Records]
    
    MAK -->|Submit| APP
    APP -->|Approve/Reject| CORE
```

---

## 2. Role Definitions & Privilege Matrix

| Role | Responsibility | Data Visibility | Core Permissions |
| :--- | :--- | :--- | :--- |
| **SuperAdmin** | Infrastructure & Global Management | **Global (All Tenants)** | User Provisioning, System Config, Role Management. |
| **CEO** | Institutional Governance | **Tenant Wide** | User Creation, Operational Oversight, Full Forensic Logs. |
| **Maker** | Financial Initiation | **Affiliated Users** | Create Credit/Debit Requests, Personal Ledger Edits. |
| **Approver** | Financial Validation | **Tenant Queue** | Review, Approve, or Reject Maker Requests (Four-Eyes Principle). |
| **User** | Standard Identity | **Self Only** | View Personal Ledger, Identity Verification. |

---

## 3. Enforcement Layers

FMC implements security at three distinct levels to prevent unauthorized access (Broken Function Level Authorization):

### A. Route Authorization (Blazor)
Pages are guarded using the `[Authorize(Roles = ...)]` attribute, ensuring that unauthorized users cannot even render the UI component.
```razor
@page "/admin/manage-users"
@attribute [Authorize(Roles = Roles.SuperAdmin)]
@inject AdminService AdminService
```

### B. Controller Authorization (REST API)
The back-end controllers enforce role checks on every incoming HTTP request. This prevents attackers from bypassing the UI and hitting the API directly.
```csharp
[Authorize(Roles = Roles.SuperAdmin)]
[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase { ... }
```

### C. Data Filter Layer (Entity Framework)
Even if a role permits viewing a record, the **Multi-Tenant Filter** ensures a user only sees data belonging to their own `TenantId`.

---

## 4. Permission Mapping

| Module | SuperAdmin | CEO | Maker | Approver | User |
| :--- | :---: | :---: | :---: | :---: | :---: |
| **User Creation** | ✅ | ✅ | ❌ | ❌ | ❌ |
| **Initiate Adjustment** | ❌ | ❌ | ✅ | ❌ | ❌ |
| **Approve Financials** | ❌ | ❌ | ❌ | ✅ | ❌ |
| **View Audit Logs**| ✅ | ✅ | ❌ | ❌ | ❌ |
| **Personal Profile** | ✅ | ✅ | ✅ | ✅ | ✅ |

---

## 5. Security Guardrails

> [!CAUTION]
> **SuperAdmin Exception**: A `SuperAdmin` should never be deleted. The system must always have at least one root-level account to manage recovery.

> [!IMPORTANT]
> **Role Escalation Protection**: When a SuperAdmin updates a user's role in the `UserDialog`, the change is immediately synchronized to the database. However, the user must **Refresh their Token** (next login or refresh) for the new roles to take effect in the browser.

---

*Document Version 1.1 - Last Refined: 2026-04-13*
