# Interview Prep — Senior .NET Design

Purpose
-------
Concise prep for a senior-level conversational interview (high-level design + process). Use this to rehearse a short elevator pitch, cover system trade-offs, and prepare questions for the interviewers.

Elevator pitch (2 minutes)
- What the service does: a small API that returns FX rates for currency pairs, using a DB cache and external providers (`ExchangeRateResolver`, `AlphaVantageService`).
- Why it matters: keeps callers fast while protecting quotas of external providers and ensuring availability via a stale-fallback strategy.
- Key design decisions: typed options (`ExchangeRateLookupOptions`), `IExchangeRateProvider` fallback chain, `IHttpClientFactory` to prevent socket exhaustion, mutation testing (Stryker) for test quality.
- One-sentence value: I deliver reliable, observable services that fail safely and are easy to test and operate.

High-level design talking points
- System components: `Presentation` (`ExchangeRatesController`), `Application` (`ExchangeRateService`, `ExchangeRateResolver`), `Domain` (`ExchangeRate`), `Infrastructure` (providers, DB, event queue). Describe responsibilities briefly.
- Data flow: client → controller → service → resolver → provider(s) → optional DB persist → event publish.
- Fallback strategy: DB cache TTL (`StaleAfter`) → provider chain → stale fallback when provider fails → 404 when no data.
- Resilience: `IHttpClientFactory`, handler tuning (pooled lifetimes, `MaxConnectionsPerServer`), timeouts, retries/backoff and circuit-breakers (Polly) for unstable upstreams.
- Concurrency & consistency: optimistic concurrency with `RowVersion` + `ETag` / `If-Match` for updates; translate `DbUpdateConcurrencyException` → 409.
- Scaling: paginate `GET /rates`, background jobs for bulk ingestion, outbox pattern for durable events, Redis cache for hot reads, horizontal read scaling for heavy read traffic.
- Observability: correlation id (`X-Correlation-ID`), structured logs, metrics (latency, error/fallback rates), traces (OpenTelemetry), and health checks.
- Deploy & config: keep operational knobs in `appsettings`/env vars (timeouts, TTLs), store secrets in Key Vault, rotate handlers with `SetHandlerLifetime`.

Test / QA / CI/CD talking points
- Unit tests for small units (mappers, validators); integration tests with `WebApplicationFactory<Program>` for end-to-end behaviour.
- Mutation testing (Stryker) to find untested logic paths — mention current score and next goals.
- Smoke tests in CI to verify health after deploy; contract generation (Swashbuckle/NSwag) to avoid API drift.
- For provider calls: fake `HttpMessageHandler` in unit tests; use test containers or SQLite in-memory for integration tests to capture EF behavior.

Process & people / leadership topics
- Code review: aim for small, thematic PRs and checklist (security, telemetry, tests).
- Mentoring: pair on design, rotate ownership for features/tests, do walkthroughs for new team members.
- Incidents: runbooks, post-mortem culture (blameless), metrics-driven alerts; use correlation id to trace incidents.
- Prioritisation: balance reliability (SLAs, provider quotas) vs features; propose experiments (A/B, canary) for big changes.

Sample concise answers (quick reference)
- Q: How would you avoid provider-induced outages? — A: `IHttpClientFactory` + Polly retries with exponential backoff, circuit-breaker, per-provider concurrency limits, and stale DB fallback; monitor failure rates and add alerts.
- Q: How to handle bulk ingestion? — A: `POST /rates/bulk` → validate & enqueue job → background worker performs chunked DB upserts with idempotency and returns `202 Accepted` + job location.
- Q: How to ensure data correctness on concurrent writes? — A: Add `[Timestamp] byte[] RowVersion`, return `ETag` on GET, require `If-Match` on PUT, map `DbUpdateConcurrencyException` → `409` and instruct client to re-fetch.
- Q: How to measure quality of tests? — A: Unit + integration coverage plus mutation testing (Stryker) to catch gaps; focus on critical branches (fallbacks, error mapping).

Questions to ask interviewers (short list)
- What are the current production pain points or incidents you worry about most? 
- Which parts of the system are hardest to test or maintain today?
- How does the team handle on-call and post-mortems? 
- What are expectations for senior engineers (code ownership, mentoring, architecture)?
- What is the deployment cadence and how much automation exists in CI/CD?

Quick rehearsal tips
- Keep the pitch crisp: problem → design → trade-offs → outcome. Use measurable examples (e.g., TTL of 15m, Stryker score). 
- When answering design questions, prefer 2–3 high-level bullets then one concrete example from this codebase.
