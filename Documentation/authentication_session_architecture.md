# Authentication, Session & Caching Architecture

## 1. Authentication & Session Architecture

FMC uses a **Hybrid JWT + HttpOnly Cookie** strategy. Short-lived JWTs carry identity claims per-request; a long-lived HttpOnly refresh token cookie silently renews sessions.

```mermaid
sequenceDiagram
    participant User
    participant Blazor
    participant API
    participant DB

    User->>Blazor: Enter credentials
    Blazor->>API: POST /api/auth/login
    API->>DB: Verify password hash (UserManager)
    DB-->>API: ApplicationUser record
    API->>API: Generate 60-min JWT (UserId, TenantId, Role)
    API->>API: Generate 7-day Refresh Token (cryptographic GUID)
    API->>DB: Persist Refresh Token to AspNetUsers
    API-->>Blazor: Set-Cookie refreshToken (HttpOnly, Secure)
    API-->>Blazor: 200 OK + JWT (JSON body)
    Blazor->>Blazor: Parse claims → hydrate AuthState
    Blazor-->>User: Redirect to Dashboard
```

### Silent Refresh Lifecycle

When a JWT expires mid-session, the `AuthenticationHeaderHandler` interceptor handles renewal transparently:

```mermaid
sequenceDiagram
    participant Blazor
    participant API

    Blazor->>API: Any authenticated request
    API-->>Blazor: 401 Unauthorized (JWT expired)
    Blazor->>API: POST /api/auth/refresh (browser sends cookie automatically)
    API->>API: Validate refresh token → issue new JWT
    API-->>Blazor: 200 OK + new JWT
    Blazor->>API: Retry original request with new JWT
```

### Security Pillars

| Token | Storage | Lifespan | Purpose |
| :--- | :--- | :--- | :--- |
| **JWT** | App memory | 60 min | Bearer authentication on every API call |
| **Refresh Token** | HttpOnly Cookie | 7 days | Silent session renewal — invisible to JavaScript |

**Hardening Measures:**
- XSS: Tokens never stored in `localStorage`
- CSRF: `SameSite=Lax` cookie policy
- Brute Force: Identity lockout after 5 failed attempts (5-minute lockout)
- Audit: Every login success/failure recorded to `AuditLog` with IP + User-Agent

---

## 2. Distributed Caching

FMC uses `IDistributedCache` — a swap-safe abstraction. The provider differs by environment:

| Environment | Provider | Cost |
| :--- | :--- | :--- |
| Development | `AddDistributedMemoryCache` | Free (in-process RAM) |
| Production | SQL Server or Self-Hosted Redis | Free (company servers) |

**Current Cache Keys:**

| Key Pattern | TTL | Data |
| :--- | :--- | :--- |
| `reg_otp_{email}` | 10 min | Registration OTP |
| `fp_otp_{userId}` | 10 min | Password reset OTP |
| `pwd_otp_{userId}` | 10 min | Password change OTP |

> [!NOTE]
> No PII or plaintext passwords are stored in the cache. Keys use hashed identifiers to prevent cross-tenant collisions.
