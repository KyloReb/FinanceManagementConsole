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
    Note over API: Cryptographic RNG: RandomNumberGenerator.GetInt32
    API->>Cache: SET fp_otp_{userId} = Secure 6-digit code (TTL 10min)
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

### 🔒 4. Session Protection, BFF Cookie Flow & JWT Verification

* **Authentication Storage**: Direct local storage of tokens in the browser is prohibited to prevent XSS-based theft.
* **HttpOnly Session Cookies**: Authenticated sessions are fully mediated by Blazor-managed, server-side HTTP `authToken` cookies configured with `HttpOnly = true`, `Secure = true`, and `SameSite = SameSiteMode.Lax`.
* **Cryptographic Check**: The Custom Middleware in `Program.cs` and the scoped `ApiAuthenticationStateProvider` decode and validate incoming token signatures cryptographically using the synchronized symmetric security key.

```mermaid
sequenceDiagram
    autonumber
    actor Browser as Browser (User)
    participant JS as secureCookieHelper (JS)
    participant BFF as Blazor BFF /api/local-auth
    participant MW as Program.cs Middleware
    participant Auth as ApiAuthenticationStateProvider

    Browser->>JS: Login success — token received from API
    JS->>BFF: fetch POST /api/local-auth/set-token (token, rememberMe)
    Note over BFF: Sets Cookie: authToken<br/>HttpOnly=true, Secure=true, SameSite=Lax
    BFF-->>Browser: 200 OK

    Browser->>MW: Next HTTP Request (cookie auto-attached by browser)
    MW->>MW: Extract authToken from Request.Cookies
    MW->>MW: JwtSecurityTokenHandler.ValidateToken (Issuer, Audience, Signature, Lifetime)
    alt Signature Valid
        MW->>MW: Normalize Claims (Name, Role, Sub, Email)
        MW-->>Browser: context.User populated — request proceeds authenticated
    else Signature Invalid / Token Expired
        MW-->>Browser: context.User = Anonymous — request proceeds unauthenticated
    end

    Browser->>Auth: Blazor SignalR circuit initializes
    Auth->>Auth: Read authToken from HttpContext.Request.Cookies
    Auth->>Auth: ValidateTokenAndGetPrincipal (cryptographic signature check)
    Auth-->>Browser: AuthenticationState resolved (Authenticated or Anonymous)
```

> [!IMPORTANT]
> **XSS Protection**: Since `authToken` is `HttpOnly`, JavaScript (including any injected XSS payloads) has **zero ability** to read or exfiltrate the token via `document.cookie` or `fetch`.

---

### ⏳ 5. Rate-Limiting & Flood Control

* **API-Level Rate Limiting**: A strict backend Fixed Window Rate Limiter policy (`AuthPolicy`) is enforced on all authentication controllers in `FMC.Api`:
  - **Limit**: Max **5 requests per minute**.
  - **Action**: Violators are dropped immediately and receive a `429 Too Many Requests` HTTP status code to block brute force dictionary and automated credentials attacks.
* **UI-Level Cooldown**: A 60-second reactive cooldown timer is enforced directly on the Blazor user interface "Resend Code" button to prevent automated email server flooding.

```mermaid
flowchart TD
    A["Incoming Request\nPOST /api/auth/login\nor /api/auth/forgot-password"] --> B

    subgraph RateLimit ["⏳ Fixed Window Rate Limiter (AuthPolicy)"]
        B{Request count\nin last 60s?}
        B -- "≤ 5 requests" --> C[Allow Request]
        B -- "> 5 requests" --> D["429 Too Many Requests\nRequest dropped immediately"]
    end

    C --> E["AuthController.Login\nor ForgotPassword handler"]
    E --> F{Credentials valid?}
    F -- Yes --> G["200 OK + JWT Token\nRefreshToken in HttpOnly Cookie"]
    F -- No --> H["401 Unauthorized\nAudit event: 'Login Failed' logged\nIP + UserAgent captured"]

    D --> I["Client receives HTTP 429\nNo handler code executed\nNo DB or cache queries"]

    subgraph UILayer ["🖥️ Blazor UI Layer"]
        J["'Resend Code' Button Clicked"] --> K{60s cooldown\nactive?}
        K -- Yes --> L["Button disabled\nCountdown displayed to user"]
        K -- No --> M["OTP resend allowed\nCooldown timer restarts"]
    end
```

> [!WARNING]
> **Scope Restriction**: The `AuthPolicy` rate limiter applies exclusively to `AuthController`. All other API controllers (e.g., `OrganizationsController`, `UsersController`) are protected by JWT bearer authentication and RBAC role policies.
