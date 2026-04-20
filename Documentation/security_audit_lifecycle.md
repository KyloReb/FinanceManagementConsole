# Security, Audit & User Lifecycle

## 1. Audit Log Architecture

Every sensitive system event is written to `AuditLog` via `AuditService`. The table is **append-only** — no Update or Delete methods exist on `AuditService`.

```mermaid
graph LR
    AUTH[Auth Events] --> AS[AuditService]
    FIN[Financial Events] --> AS
    ADMIN[Admin Events] --> AS
    AS --> DB[(AuditLog Table)]
    DB --> AE[Audit Explorer\n/admin/audit]
    DB --> HL[Health Monitor\nSystemAlerts]
```

### Audit Event Taxonomy

| Event | Risk Level | Trigger |
| :--- | :---: | :--- |
| `Login Success` | Low | Valid credentials presented |
| `Login Failed` | High | Invalid credentials (IP + UserAgent captured) |
| `Logout` | Low | Session explicitly terminated |
| `Password Reset` | Medium | OTP-based recovery completed |
| `User Created` | Medium | Admin provisions new account |
| `WALLET_FUNDED` | High | SuperAdmin credits org wallet |
| `TRANSACTION_APPROVED` | High | Approver settles a Maker request |
| `APPROVAL_BLOCKED` | Critical | Four-Eyes or cross-org violation attempt |

> [!WARNING]
> **Immutability**: The AuditLog table has no update or delete pathway. Records are forensic evidence and must be treated as permanent.

---

## 2. User Provisioning

FMC uses **closed-loop provisioning** — no public registration. All accounts are created by a CEO or SuperAdmin.

```mermaid
graph TD
    A[Admin opens User Management] --> B[Fill personnel details\nand select role]
    B --> C[IdentityService.CreateUserAsync]
    C --> D[Account created\nEmailConfirmed = true\nAccountNumber assigned]
    D --> E[Admin hands credentials\nto user via secure channel]
    E --> F[User logs in]
```

---

## 3. Password Recovery (Forgot Password)

```mermaid
sequenceDiagram
    participant User
    participant API
    participant Cache
    participant Email

    User->>API: POST /api/auth/forgot-password (email)
    API->>API: Lookup user (no error if not found — enumeration protection)
    API->>Cache: SET fp_otp_{userId} = 6-digit code (TTL 10min)
    API->>Email: Enqueue via Hangfire → SendOtpEmail
    API-->>User: 200 OK + masked email (d***@domain.com)

    User->>API: POST /api/auth/reset-password (otp, newPassword)
    API->>Cache: GET fp_otp_{userId}
    alt OTP valid
        API->>Cache: DEL fp_otp_{userId}
        API->>API: UserManager.ResetPasswordAsync
        API-->>User: 200 OK
    else OTP expired or invalid
        API-->>User: 400 Bad Request
    end
```

**Rate-Limiting**: A 60-second cooldown timer is enforced in the UI on the "Resend Code" button to prevent email flooding.
