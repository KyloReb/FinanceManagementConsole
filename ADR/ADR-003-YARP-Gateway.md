# ADR-003: YARP Managed API Gateway

## Status
Accepted

## Context
As FMC grows, we need a single entry point to handle cross-cutting concerns (Rate Limiting, Routing, Request Aggregation) without bloating the specialized `FMC.Api`.

## Decision
Use **YARP (Yet Another Reverse Proxy)** by Microsoft.
- It is built on .NET, making it familiar to the core development team.
- Allows for easy request aggregation (e.g., getting user info + balance in one round trip).

## Consequences
- **Pros**: Flexible routing, high performance, and managed entirely in C#.
- **Cons**: Adds another layer in the network path, requiring careful monitoring.
