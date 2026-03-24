# ADR-002: Redis for Distributed Caching

## Status
Accepted

## Context
Deploying to GCP (App Engine/Cloud Run) requires the application to scale horizontally across multiple instances. Standard in-memory caching (`IMemoryCache`) is local to a single instance, leading to "cache misses" and stale data across the cluster.

## Decision
Use **Redis** (via GCP Memorystore) as the distributed caching provider. 
- Implement **Cache-Aside** pattern for financial summaries.
- Use MediatR Notifications to trigger invalidation when a `Command` updates the state.

## Consequences
- **Pros**: Consistent dashboard data across all instances, reduced database load.
- **Cons**: Adds a new infrastructure dependency and potential latency for cache lookups.
