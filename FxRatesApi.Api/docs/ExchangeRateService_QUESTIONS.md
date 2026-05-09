# ExchangeRateService — Recommended Answers
Below are concise recommended answers for each question; use these as a reference when updating implementation or documentation.
- Q: What transaction boundaries should the service enforce for create/update/delete operations to ensure consistency?
	A: Keep each HTTP operation within a single atomic DB transaction. For workflows that must write both the domain entity and an event record, use a transactional outbox (write entity + outbox row in the same DB transaction) so publish is durable and atomic. For multi-step operations spanning many entities, use an explicit `BeginTransactionAsync` and commit/rollback as a unit.
- Q: How should input validation be split between controllers and the service layer?
	A: Validate syntactic rules at the API boundary (DataAnnotations or FluentValidation) to fail fast. Enforce business invariants (Ask >= Bid, supported currencies, provider normalization) inside the service to protect against bypass and ensure consistency. Always map validation failures to RFC7807 `ProblemDetails` (400).
- Q: How should the service communicate upsert results to callers (created vs updated) and why?
	A: Return a typed result (e.g., `ExchangeRateUpsertResult { ExchangeRate Rate; bool Created; }`). The controller should translate that into `201 Created` + `Location` for new resources or `200/204` for updates. Document semantics so clients know when a create occurred versus an update.
- Q: Should event publishing be synchronous or asynchronous relative to the request, and what are the trade-offs?
	A: Prefer asynchronous publishing for responsiveness and resiliency; make delivery durable via a transactional outbox and background worker. Synchronous publishing reduces complexity but increases latency and couples request success to external systems. Use retries, circuit-breaker, and monitoring regardless of choice.
- Q: How would you design idempotency for upsert operations to allow safe retries?
	A: Accept an `Idempotency-Key` header, persist the key and the canonical response (DB or Redis) for a TTL, and check for existing keys before performing the write. Ensure the key is applied before the DB write (or within the same transaction) to prevent races. Define TTLs and behaviour for replayed keys (return stored response or 409/200 depending on policy).
- Q: How would you structure unit tests for the service while mocking `AppDbContext`, resolver, and publisher?
	A: Unit tests: mock `IExchangeRateResolver` and `IRateEventPublisher` (Moq/NSubstitute) and use a lightweight DbContext (SQLite in-memory for realistic EF behavior or a repository abstraction mocked). Assert created vs updated flows, validation errors, exception handling, and event publishing calls. Add integration tests with `WebApplicationFactory` for end-to-end behaviour.
- Q: How should concurrency conflicts be surfaced and handled by the service?
	A: Implement optimistic concurrency (`[Timestamp] byte[] RowVersion`) and expose `ETag`/`If-Match` semantics. Catch `DbUpdateConcurrencyException` and translate to `409 Conflict`, returning the current resource or guidance. Log and emit a metric for conflict frequency.
- Q: Which logs and metrics would you add to observe service-level health and performance?
	A: Track request latency histograms, DB query latency, upsert/create/delete counts, failure/error rates, concurrency conflict count, published-event success/failure, and queue length. Use structured logs with correlation id and include contextual fields (pair, provider, latency).
- Q: Should authorization checks live in the service or controller, and how would you enforce them?
	A: Enforce authentication/authorization at the controller/gateway (policy-based `[Authorize]`) and keep critical business checks in the service (role/claim-dependent logic). Use policy-based authorization and centralize rules where possible to avoid duplication.
- Q: How could the service be extended to support bulk upserts without duplicating business logic?
	A: Add a bulk endpoint (`POST /rates/bulk`) that validates and chunks input, then reuse the single-item upsert logic in a loop or use optimized bulk-merge operations (EF bulk libraries or raw SQL MERGE) inside transactional chunks. Prefer async job processing (202 Accepted + job status) for very large batches, include idempotency keys and partial-failure reporting.
# ExchangeRateService — Interview Questions

Answer each question with a short explanation and any trade-offs you considered.

1) What transaction boundaries should the service enforce for create/update/delete operations to ensure consistency?

2) How should input validation be split between controllers and the service layer?
R: controllers should do the simple versification, check if the dto is not null, for instance. the service layer should do domain validations 


3) How should the service communicate upsert results to callers (created vs updated) and why?
R: Because as it is an upsert, which is not the common logic for the post (post = insert), we should let the user know what was the result of the post (inserted? updated?)

4) Should event publishing be synchronous or asynchronous relative to the request, and what are the trade-offs?
R: Sync: we would only return when the publishing is done. Async: we would do a fire and forget. 
with the sync we would know if there is something that is going on. but then the question would be: shall we return a no success if we just failed to add it to the event queue? for the async, the approach would be 'return as fast as we can to the user'. of course the event publishing could fail. to solve this, we could make an outbox patern: we would , in the same db, create the exchange rate entity and then we would also save there the payload for the event and we would have a background process (workers) running and reading the events and publishing them

5) How would you design idempotency for upsert operations to allow safe retries?
R: we could save somewwhere the idempotency keys (reddis example). if the db already had that idempotency key, i would return the old result (the one saved with idempotency key). if not, we would create it new. 

6) How would you structure unit tests for the service while mocking `AppDbContext`, resolver, and publisher?
- dk

7) How should concurrency conflicts be surfaced and handled by the service?
R: we could throw an conflict exception and transform it in ta 409

8) Which logs and metrics would you add to observe service-level health and performance?
R: we could same time of retrivel of the databse. 


9) Should authorization checks live in the service or controller, and how would you enforce them?
R: It should leave in the controller to start with but in the service we would check the role of the user for instance (or claim) before do any action just to make sure the service doesnt get called by anything that doesnt check it . 

10) How could the service be extended to support bulk upserts without duplicating business logic?
R: we would create a bulk method that would receive a list. for the cases that we had only one, we would only give a List with one item. 