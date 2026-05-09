# ExchangeRatesController — Recommended Answers

Below are concise recommended answers for each question; use these as a reference when updating the implementation or documentation.

- Q: Explain the trade-offs between using `POST /rates` as an upsert versus using `PUT /rates/{base}/{quote}` for idempotent upsert.
	A: Prefer `PUT /rates/{base}/{quote}` for idempotent upsert because it maps to a resource URL, supports `ETag`/`If-Match` concurrency, and has predictable status semantics. Use `POST` for create or for non-idempotent/bulk endpoints; if `POST` is used as upsert, document the behaviour and return `201 Created` for new resources and `200/204` for updates.

- Q: How would you design DTO validation to mirror database constraints and enforce domain rules such as `Ask >= Bid`?
	A: Validate at the API boundary with DataAnnotations or FluentValidation (e.g., `[StringLength(3)]` for currencies, range/precision checks), enforce `Ask >= Bid` via a custom validator or `IValidatableObject`, and add DB-level protections (column precision with `HasPrecision` and a CHECK constraint) for defence-in-depth. Add unit tests for validators and map failures to ProblemDetails (400).

- Q: How would you structure controller return types and response documentation to make the API clearer for consumers and for Swagger?
	A: Use `ActionResult<T>` for typed responses, annotate actions with `[ProducesResponseType]` for every status shape, use `CreatedAtAction` for creations, and return `ProblemDetails` for errors so Swagger shows accurate schemas and examples.

- Q: How would you map domain exceptions to HTTP status codes and ensure consistent `ProblemDetails` responses across the API?
	A: Centralise mapping in an `IExceptionFilter` or a focused middleware: map `ResourceNotFoundException->404`, `ResourceConflictException->409`, `ExternalServiceException->503`, validation failures->400, other exceptions->500. Always return RFC7807 `ProblemDetails`, log full details server-side with correlation id, and only expose limited details in production.

- Q: What strategy would you use to prevent `GET /rates` from returning too-large payloads or causing performance issues?
	A: Implement pagination (limit/offset or cursor) with sensible defaults and a `maxPageSize` cap, perform DB-side `Skip/Take` projections, allow filtering, and avoid synchronous provider refresh on list requests—use background refresh or provide a separate `?refresh` async job.

- Q: How would you design a bulk upsert endpoint for high-volume ingestion while ensuring idempotency and efficiency?
	A: Expose `POST /rates/bulk` that accepts arrays, validate input, return `202 Accepted` with a job `Location` for async processing. Process in background with idempotency keys, chunked bulk-upsert/merge operations, and clear partial-failure reporting; support an optional synchronous mode only for small batches.

- Q: Describe how you would implement optimistic concurrency (RowVersion/ETag) for updates and how clients should handle conflicts.
	A: Add `[Timestamp] byte[] RowVersion` to the entity, return an `ETag` header (derived from RowVersion) on GET, require `If-Match` on updates, catch `DbUpdateConcurrencyException` and return `409 Conflict` with the current state or guidance. Clients should re-fetch and retry or merge changes.

- Q: When a new resource is created, what headers and status should you return and why (e.g., `201 Created`, `Location`, body)?
	A: Return `201 Created` with a `Location` header pointing to `GET /rates/{base}/{quote}`, include the created resource in the body (or minimal representation), and optionally return an `ETag` header for concurrency.

- Q: What authentication and throttling approach would you recommend to protect external provider quotas and secure the API?
	A: Use OAuth2 (client credentials) for service clients and scoped tokens for permissions; enforce quotas and rate limits at the API gateway or middleware per-client and per-endpoint; apply per-provider concurrency limits and circuit-breakers to avoid exhausting provider quotas.

- Q: How would you implement correlation IDs so requests can be traced through logs and external calls?
	A: Accept `X-Correlation-ID` header and generate one if missing, include it in response headers, push it into the logging scope and outbound HTTP headers, and surface it in `ProblemDetails`/logs for easier tracing and telemetry correlation.

- Q: How should external provider failures be presented to API clients; what should be returned and when would you fall back to cached/stale data?
	A: If a stale DB value exists and is acceptable, return it with an explicit flag (field or header) and normal 200 status, and log a warning; if no cached value exists, return `503 Service Unavailable` with a generic `ProblemDetails` message and optional `Retry-After`. Never return raw upstream error details; instrument metrics and use circuit-breakers and retries.

- Q: Which tests would you prioritise for this controller (unit, integration with `WebApplicationFactory`, smoke tests), and why?
	A: Prioritise integration tests with `WebApplicationFactory` to validate end-to-end behaviour (fallbacks, status codes, persistence); add focused unit tests for validation and exception-to-HTTP mapping; keep smoke tests in CI for quick health checks and use mutation testing to find gaps.

