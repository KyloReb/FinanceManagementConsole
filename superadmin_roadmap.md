# SuperAdmin Enterprise Roadmap: Finance Management Console (FMC)

## 1. Project Context & Objectives
This roadmap outlines the evolution of the FMC SuperAdmin suite from a basic user management tool to an **Enterprise-Grade Governance and Observability Platform**. The objective is to achieve full accountability, strict multi-tenant isolation, and high-availability monitoring for financial operations.

---

## 2. Current Capabilities (Baseline)
The system currently supports:
- **RBAC Enforcement**: Hard-coded roles (`SuperAdmin`, `User`, `CEO`, `Manager`) that control access to sensitive API endpoints and Blazor pages.
- **User Management (CRUD)**: SuperAdmins can create, update, delete, and synchronize roles for any user across the platform.
- **Security Visibility**: Logging of all Login (Success), Login (Failed), and Logout events, including IP addresses (v4/v6) and User-Agents.
- **Multi-Tenancy Infrastructure**: `TenantId` plumbing is present in the `ApplicationDbContext` and enforced via `SaveChangesAsync` with query filters for isolation.

---

## 3. Enterprise-Level Gap Analysis
A formal review against enterprise standards (SOC2, PCI-DSS, ISO 27001) reveals the following critical gaps:

### **Gap 1: Mutation Audit Trail (Accountability)**
> [!WARNING]
> **Status: HIGH PRIORITY**
> Currently, the system logs **Access (Who entered)** but not **Mutations (What they changed)**. 
> *   **Requirement**: Financial auditors must see a log of every record creation, deletion, or modification (e.g., "Admin X changed Transaction 102 amount from $500 to $5000").

### **Gap 2: SuperAdmin Security Mandates (MFA)**
> [!IMPORTANT]
> **Status: HIGH PRIORITY**
> SuperAdmin accounts have God-level permissions. If a password is leaked, the entire multi-tenant system is compromised. 
> *   **Requirement**: Multi-Factor Authentication (MFA) must be mandated for all accounts with the `SuperAdmin` role.

### **Gap 3: Tenant Isolation Management**
> [!NOTE]
> **Status: MEDIUM PRIORITY**
> While the DB is isolated by `TenantId`, there is no UI to manage the "Tenant (Organization)" entity itself.
> *   **Requirement**: A SuperAdmin should be able to create "Organizations" and assign users to them, rather than relying on manual Guid mapping.

### **Gap 4: Impersonation & Troubleshooting**
> [!TIP]
> **Status: LOW PRIORITY (Operational)**
> Admins often need to "see what the user sees" to debug issues.
> *   **Requirement**: Secure "Impersonate User" flow with a mandatory "impersonation log" to prevent admin abuse.

---

## 4. The 4-Phase Roadmap

### **Phase 1: Deep Accountability (Q1: Forensic Transparency)**
*Focus: Transitioning from access logs to data change logs.*
1. **Mutation Tracking**: Hook the `AuditService` into `SaveChangesAsync` to automatically log entity changes (Created, Updated, Deleted).
2. **Field-Level Diffing**: Specifically log *which* fields were altered (Old Value vs. New Value).
3. **Audit Export Utility**: Allow SuperAdmins to export a period-specific CSV/PDF of all activity for a specific tenant or user.

### **Phase 2: Global Configuration & Tenant Control (Q2: Governance)**
*Focus: Moving configuration out of the code and into the UI.*
1. **Organization Management**: Create a UI to manage Tenants (Companies/Branches). Enable "Suspension" of an entire organization.
2. **Platform Configuration Toggles**: Manage global variables (Session timeouts, maximum login attempts, OTP enablement).
3. **Maintenance Mode**: A SuperAdmin "Kill Switch" that prevents non-admins from accessing the system during critical updates.

### **Phase 3: Security Hardening & Isolation (Q3: Defense-in-Depth)**
*Focus: Protecting the perimeter and sensitive flows.*
4. **MFA Enforcement Flow**: Require an OTP code for every SuperAdmin login, regardless of user settings.
5. **Secure Impersonation Framework**: Implement a "Login As User" feature that allows an Admin to enter a tenant's context temporarily, with its own dedicated audit trail.
6. **API Rate Limiting**: Protection against automated brute-force attacks at the IP level.

### **Phase 4: Observability & Resilience (Q4: Intelligence)**
*Focus: Predicting failure and visualizing scale.*
7. **System Health Command Center**: Real-time charts for API latency, active user sessions, and database storage utilization per tenant.
8. **Exception/Error Monitor**: A dedicated UI page to view the last 50 unhandled server errors, allowing for proactive hotfixing.
9. **Database Maintenance Utility**: UI status for last-successful backup and a "Trigger Manual Backup" button for before complex operations.

---

## 5. Certification Checklist
To achieve compliance certification, ensure the following is true for the SuperAdmin suite:
- [ ] No SuperAdmin action goes unlogged.
- [ ] All audit logs are immutable (cannot be deleted or edited by anyone).
- [ ] Password hashes are salted and use PBKDF2 or equivalent.
- [ ] Tenant data never leaks cross-boundary (verified by automated tests).
