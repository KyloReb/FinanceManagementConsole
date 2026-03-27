# Authentication & User Lifecycle Workflows

This document outlines the enterprise-grade workflows for user registration and password recovery within the **Finance Management Console (FMC)**. These flows prioritize security, forensic auditability, and a premium user experience.

---

## 1. Self-Service User Registration

The registration flow ensures that every new account is tied to a verified email address before access is granted.

### Workflow Steps:
1.  **Front-End Submission**: 
    -   User fills the form on `Register.razor`.
    -   All inputs and navigation links are disabled during the `_isLoading` state to prevent duplicate submissions.
2.  **Back-End Processing (`IdentityService.RegisterAsync`)**:
    -   Validation check for existing Username or Email.
    -   User entity created with `EmailConfirmed = false`.
3.  **OTP & Email Delivery**:
    -   A cryptographically random 6-digit OTP is generated.
    -   The OTP is cached in **Redis** with a 10-minute TTL.
    -   A branded HTML email is dispatched using CID-based image attachments (ensuring high-fidelity logo rendering in Gmail/Outlook).
4.  **Pending Verification**:
    -   The user is redirected to `VerifyEmail.razor`.
    -   Account remains in a "locked" state until the correct OTP is provided.
5.  **Final Activation**:
    -   Upon successful verification, `EmailConfirmed` is set to `true`.
    -   The OTP is explicitly consumed (removed from cache).

---

## 2. Secure Password Recovery (Forgot Password)

FMC implements a "Privacy-First" recovery flow that protects against email enumeration while providing clear guidance to legitimate users.

### Workflow Steps:
1.  **Initiation (`ForgotPassword.razor`)**:
    -   User enters their Username or Email identifier.
2.  **Security Masking & Enumeration Protection**:
    -   The system returns a **Masked Email** (e.g., `d***********@domain.com`) to confirm where the code was sent without revealing the full address.
    -   The API returns a generic success message even if the user does not exist, preventing malicious actors from fishing for valid accounts.
3.  **Rate Limiting (Resend Cooldown)**:
    -   An inline **60-second timer** is triggered in the UI.
    -   The "Resend Code" button is disabled and displays a live countdown to mitigate email spamming.
4.  **Verification & Reset**:
    -   User provides the 6-digit OTP along with their new password.
    -   The UI uses a **Grid System** to maintain perfect alignment and responsiveness on mobile.
    -   The back-end validates the OTP against the Redis cache and applies the reset via `UserManager`.

---

## 3. Administrative User Creation (SuperAdmin)

SuperAdmins can bypass the self-service flow to provision system accounts directly.

### Workflow Steps:
1.  **Dialog Management (`UserDialog.razor`)**:
    -   Accessed via the `Manage Users` dashboard.
    -   Supports complex role assignments (SuperAdmin, CEO, Manager, User).
2.  **UI Hardening**:
    -   All password fields feature a **Visibility Toggle** (Eye Icon) to ensure administrators can verify credentials before finalizing.
    -   Form validation prevents submission of incomplete or duplicate identities.
3.  **Data Persistence**:
    -   Created users are immediately assigned their specific `IdentityRole` claims.
    -   Admin-created accounts can be set to `Active` or `Inactive` toggles instantly.

---

## 4. Security & Audit Specifications

| Feature | Implementation Detail |
| :--- | :--- |
| **OTP Storage** | Redis Distributed Cache (10-minute sliding expiration) |
| **Audit Logging** | Every Login, Registration, and Reset is logged via `RecordAuthEventAsync`. |
| **Forensic Data** | Logs capture Timestamp, IP Address, Event Type, and User-Agent Details. |
| **Mobile Design** | Zero-bleed CSS: OTP boxes use `max-width: 100%` and `box-sizing` for mobile email clients. |
| **Password Visibility** | Adornment-based toggles with `InputType` state switching. |

---

> [!IMPORTANT]
> **Audit Integrity**: All registration events are logged under the "Registration" category in the Security Logs, allowing for immediate identification of account creation activity.
