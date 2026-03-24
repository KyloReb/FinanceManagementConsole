# ADR-001: CQRS Pattern for Financial Data

## Status
Accepted

## Context
FMC requires high performance for dashboard reads while maintaining strict transactional integrity for ledger writes (Mother -> Sub Account transfers). A single model for both Read/Write would lead to suboptimal performance and complex locking logic.

## Decision
Implement **Command Query Responsibility Segregation (CQRS)** using **MediatR**. 
- **Commands**: (Write) Handled via EF Core in `FMC.Infrastructure` for ACID compliance.
- **Queries**: (Read) Optimized for speed, using Dapper or EF `AsNoTracking()` to return lean Dto's from `FMC.Shared`.

## Consequences
- **Pros**: Better scalability, optimized read performance, and cleaner separation of concerns.
- **Cons**: Increased complexity due to multiple models and MediatR boilerplate.
