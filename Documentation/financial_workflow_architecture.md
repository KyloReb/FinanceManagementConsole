# Financial Workflow (Maker-Checker)

FMC enforces a strict **Four-Eyes Principle** for all financial movements. No single user can both initiate and settle a transaction.

```mermaid
sequenceDiagram
    participant Maker
    participant API
    participant DB
    participant Hangfire
    participant Approver
    participant CEO

    Maker->>API: POST /initiate (amount, label, target user)
    API->>DB: INSERT Transaction (Status = Pending)
    API->>Hangfire: Enqueue SendPendingApprovalNotification
    API-->>Maker: 200 OK (instant response)
    Hangfire-->>Approver: Email: "Action Required"
    Hangfire-->>CEO: Email: "Action Required"

    Approver->>API: POST /approve (transactionId)
    API->>API: Validate: Approver ≠ Maker (Four-Eyes)
    API->>API: Validate: Same Organization
    API->>DB: TransferAsync (Org Wallet → User Wallet)
    API->>DB: UPDATE Transaction (Status = Approved)
    API->>Hangfire: Enqueue SendApprovalConfirmation + CapacityCheck
    API-->>Approver: 200 OK
    Hangfire-->>Maker: Email: "Transaction Approved"
    Hangfire-->>CEO: Email: "Transaction Approved"
```

### Transaction States

```mermaid
stateDiagram-v2
    [*] --> Pending: Maker initiates
    Pending --> Approved: Approver commits
    Pending --> Rejected: Approver rejects
    Pending --> Cancelled: Maker withdraws
    Approved --> [*]
    Rejected --> [*]
    Cancelled --> [*]
```
