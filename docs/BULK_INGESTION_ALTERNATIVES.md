# Bulk Ingestion Alternatives

Purpose
-------
This document describes practical approaches for implementing bulk ingestion in the FX rates service, explains the trade-offs for each approach, and gives a recommendation for a safe, testable starting point. The alternatives are written as structured answers rather than checklists so you can read them as decision narratives.

Synchronous POST (small batches only)
------------------------------------
The simplest approach is to keep the existing HTTP POST endpoint and process the incoming batch synchronously inside the request. This works well for small batches that complete quickly: validation, idempotent database upserts, and optional event publishing all occur before returning a response. The main downside is a fragile surface for large or slow work. Long-running batches are vulnerable to client or proxy timeouts, consume request thread-pool resources, and increase the blast radius of any provider or DB slowness. Use this only when batch sizes and processing time are predictably small and when immediate acknowledgement is required.

Entities and roles
------------------
- **Client**: Submits the batch payload to `POST /rates/bulk`.
- **`POST /rates/bulk` controller**: Validates payload and either performs in-request processing or persists job/work items depending on the chosen pattern; in the synchronous variant it executes processing inline.
- **Validation layer / Model binder**: Ensures DTOs are correct and rejects invalid items early.
- **`ExchangeRateService` (domain/service layer)**: Executes idempotent upserts into the `ExchangeRate` table and enforces business rules.
- **App DB (`ExchangeRate` table)**: Stores the final exchange rate rows and enforces uniqueness constraints.
- **(Optional) Event publisher**: If events are published synchronously, this component sends domain events to downstream systems before the response.
- **HTTP Response**: Returns success/failure to the client; long processing affects client latency.

In-process queue consumed by `IHostedService`
--------------------------------------------
An incremental improvement keeps everything in the same process but makes ingestion asynchronous. The controller enqueues work into an in-memory queue (for example a `Channel<T>`), returns an immediate acknowledgement, and a `BackgroundService` (an `IHostedService`) consumes the queue. This avoids client timeouts and keeps operational complexity low because the worker is part of the same application. The critical limitation is durability: if the process restarts or crashes the queued items are lost. This pattern is suitable for low-risk scenarios or development/testing but is not recommended as the only durability mechanism for production bulk processing.

Entities and roles
------------------
- **Client**: Calls `POST /rates/bulk` and receives an immediate acknowledgement.
- **Controller**: Validates request and enqueues items into an in-memory queue (`Channel<T>` or similar).
- **In-memory Queue (`Channel<T>`)**: Holds pending work inside the process memory; no persistence.
- **`BackgroundService` worker**: Consumes the in-memory queue, deserializes items, and calls `ExchangeRateService` to upsert data.
- **`ExchangeRateService`**: Performs idempotent upserts into the `ExchangeRate` table.
- **App DB**: Persists domain objects; durable storage for processed results but not for enqueued items.
- **Health/Restart considerations**: No entity persists the queue; process lifecycle affects durability.

Database-driven job queue with a worker (DB-first)
-------------------------------------------------
A durable, infra-light pattern is to persist a job and one row per work item in the application database and to return `202 Accepted` from the controller. The worker runs as an `IHostedService` and polls the database for `Pending` items, claims them atomically, processes them in bounded chunks, and updates item state to `Done` or `DeadLetter` with metadata such as `AttemptCount`, `LastError` and `NextAttemptAt`. This approach gives durability, visibility (job status and progress), and simple operational control without requiring additional infrastructure. It also makes it straightforward to implement idempotent upserts and to expose an admin UI or API to requeue or inspect failed items. The trade-offs are that it requires careful claim semantics (atomic updates or DB locks) and tuning for chunk size and concurrency to avoid long transactions and lock contention.

Entities and roles
------------------
- **Client**: Submits batch to `POST /rates/bulk` and receives `202 Accepted` with job location.
- **Controller (`POST /rates/bulk`)**: Validates payload and creates a `Job` row and `WorkItem` rows in the App DB within a single transaction; returns job id/location.
- **App DB**: Hosts `Job` table (job lifecycle and metadata), `WorkItem` table (one row per item with `Status`, `AttemptCount`, `NextAttemptAt`, `LastError`), `ExchangeRate` table, and optional `Outbox`/DeadLetter tables.
- **Job row**: Aggregates metadata for the batch (JobId, CreatedAt, Status, counts).
- **WorkItem rows**: Individual units of work to be claimed and processed; contain serialized payload and processing metadata.
- **Processor (`BackgroundService`)**: Polls/claims pending `WorkItem` rows atomically (e.g., `UPDATE ... RETURNING` or `SELECT FOR UPDATE SKIP LOCKED`), deserializes payloads, calls `ExchangeRateService` to perform idempotent upserts, and updates `WorkItem` status (`Done`/`DeadLetter`).
- **`ExchangeRateService`**: Contains the domain logic for upserts and consistency; called by Processor.
- **Outbox table (optional)**: Inserted inside the same transaction as domain writes to guarantee atomic DB+event semantics.
- **Publisher (see Outbox)**: Reads the Outbox and publishes to external brokers; marks outbox entries as published.
- **Admin API / UI**: Inspects Job/WorkItem status, allows requeueing, and shows dead letters.
- **Metrics / Monitoring**: Tracks queue depth, success/failure counts and processing latency.

Broker-driven ingestion (Service Bus / RabbitMQ) with consumer
-------------------------------------------------------------
Using an external message broker decouples producers and consumers and lets the platform handle retries, dead-lettering, and scaling. In this model the POST handler publishes messages (either one message per item or a pointer to a blob with the batch payload) to a queue or topic. A Service Bus-triggered consumer (an Azure Function or a dedicated hosted service) receives messages and performs the upserts. This pattern is ideal for high throughput systems because the broker provides durability, visibility into the queue, and built-in DLQ semantics. Consumers must be written to be idempotent because brokers typically deliver at-least-once, and you should design for eventual consistency. If you need transactional guarantees that the database write and the outward event publish happen together, combine the broker with an outbox (described next).

Entities and roles
------------------
- **Client**: Submits the batch payload to `POST /rates/bulk`.
- **Controller**: Either (A) publishes one message per item to the broker (topic/queue) or (B) uploads a batch payload to blob storage and publishes a single pointer message containing the blob URI.
- **Message Broker (Service Bus / RabbitMQ)**: Durable queue/topic that stores messages, enforces delivery semantics, and manages a Dead-Letter Queue (DLQ).
- **Blob Storage (optional)**: Holds large payloads when the controller sends a pointer instead of inlining the full message.
- **Consumer (Function / Worker)**: Subscribes to the broker; receives messages, deserializes payload or fetches blob, calls `ExchangeRateService` to perform idempotent upserts into App DB, and completes or dead-letters messages based on outcome.
- **App DB**: Stores the resulting `ExchangeRate` rows; consumer writes results here.
- **DLQ / Broker Retry**: Broker provides retry/dlq semantics — failed messages end up in DLQ for manual processing.
- **Outbox (optional)**: If the consumer must publish events after DB writes, combine with outbox to ensure reliable publication.

Outbox pattern (DB-first outbox + publisher)
--------------------------------------------
The outbox pattern addresses the problem of making a database write and the publication of a domain event happen atomically. The processor performs the domain upsert and, in the same DB transaction, inserts an outbox row containing the event payload. After the transaction commits, a publisher component reads the outbox table and sends messages to the broker, marking outbox rows as published. This separation prevents message loss when the process crashes after the DB commit but before publishing. The outbox publisher can be the same process or a separate service/function; separating it improves operational isolation because publishing retries and transient broker issues do not block domain processing. The outbox adds implementation complexity but is the right choice when reliable event delivery is required.

Entities and roles
------------------
- **Client**: Triggers the operation (either via `POST /rates/bulk` or by producing messages).
- **Processor**: Performs the domain write (upsert) and writes an Outbox row within the same DB transaction.
- **App DB (with Outbox table)**: Stores both domain data and the Outbox row atomically.
- **Outbox row**: Contains the serialized domain event, `AttemptCount`, `Status`, and `CreatedAt`.
- **Outbox Publisher (BackgroundService / Function)**: Periodically claims unpublished Outbox rows, publishes them to the broker, and marks them as `Published` with metadata (MessageId/PublishedAt) or moves them to a publisher dead-letter store on repeated failures.
- **Message Broker**: Receives events for downstream consumers.
- **Downstream Consumers**: Subscribe to broker messages; act on published domain events.

Durable / orchestrated approaches (Durable Functions)
----------------------------------------------------

For scenarios requiring complex orchestration (fan-out/fan-in, long-running retries, per-chunk checkpointing), Durable Functions or a comparable orchestrator provide a managed way to express the workflow. An orchestrator can coordinate chunking, parallel processing, and reliable state, and exposes a built-in status endpoint for each orchestration instance. The trade-off is added platform-specific complexity and cost; use orchestration when business logic requires it or when job lifecycle and rich retry semantics are better expressed as stateful workflows.

Entities and roles
------------------
- **Client**: Starts an orchestration (via `POST /rates/bulk` or orchestration start API) and can query orchestration status.
- **Starter API / Controller**: Validates request and starts an orchestration instance with initial input (either full payload or pointer to blob).
- **Orchestration Service (Durable Functions runtime)**: Persists orchestration state, coordinates retries, parallel activity scheduling and provides status endpoints.
- **Activity Functions / Workers**: Execute units of work (chunk processing, upserts, external calls); they write to `App DB` or return results to the orchestrator as needed.
- **Task hubs / Storage backend**: Durable storage (Azure Storage, etc.) that holds orchestration state and checkpoints.
- **App DB**: Stores domain data produced by activity functions; used for idempotent writes.
- **Outbox / Broker (optional)**: If required, activity functions may insert Outbox rows for reliable event publication; a separate publisher publishes to the broker.

Scheduler libraries (Hangfire / Quartz)
-------------------------------------
If you prefer to avoid custom worker code and want a DB-backed scheduler with a UI and retry semantics out of the box, libraries such as Hangfire or Quartz are viable. They persist jobs in a database and handle retries, concurrency and a job dashboard. This is a pragmatic option when you want to minimize custom code and gain immediate operational visibility. The trade-offs are dependency on a third-party runtime and less control over custom claim semantics compared to a purpose-built worker.

Entities and roles
------------------
- **Client**: Either triggers a job through the API or a scheduled job is created externally.
- **Controller / Job enqueuer**: Creates a scheduled job record (or enqueues an immediate job) in the scheduler store.
- **Scheduler Store (Hangfire tables / Quartz store)**: Persists scheduled jobs, retries, and history.
- **Scheduler Server(s)**: Execute jobs according to schedule or on-demand; hosts worker logic that performs upserts via `ExchangeRateService`.
- **Dashboard / Admin UI**: Shows job history, failures, and allows retrying or deleting jobs.
- **App DB**: Destination for domain writes; scheduler jobs call into domain services to persist results.

Comparison and recommendation
-----------------------------
For this repository the best initial choice is the database-driven job queue because it provides durability and visibility without introducing new infra. Implement `POST /rates/bulk` to persist a `Job` row and one `WorkItem` row per input item in a single transaction, then return `202 Accepted` with a job location. Add a `BackgroundService` that claims and processes pending items in configurable chunk sizes, performs idempotent upserts, updates item state, and moves repeatedly failing items to a DeadLetter state after a configured number of attempts. If the system later needs to publish domain events reliably to other systems, add an outbox column/table and a separate publisher service (or Function) that reads outbox rows and publishes them to Service Bus for downstream consumption. If you anticipate cloud-scale or need broker-managed DLQ and sessions, prefer a broker-driven ingestion with Service Bus and an idempotent consumer; combine that with an outbox when you need atomic DB+event semantics.

Operational notes and essential safeguards
----------------------------------------
Implement idempotency at the DB level (unique index on `BaseCurrency` + `QuoteCurrency` or an idempotency key) so retries do not create duplicate data. Claim items atomically so multiple workers can operate concurrently without double-processing; use `SELECT FOR UPDATE SKIP LOCKED` or an atomic UPDATE/RETURNING pattern depending on database capabilities. Use exponential backoff and an `AttemptCount` field for per-item retries and move items to DeadLetter with error details once a retry threshold is exceeded. Expose job and dead-letter visibility through an admin API and emit metrics for queue depth, processing latency, success/failure rates and dead-letter counts. Finally, keep chunk sizes and `MaxDegreeOfParallelism` conservative at first, then tune against real load.

Next steps
----------
If you want, I will scaffold the DB-first implementation in this repository: schema for `Job`, `WorkItem` and `Outbox` tables, a `POST /rates/bulk` controller method that persists work atomically, and two `BackgroundService` skeletons (processor + outbox publisher) with claim/retry logic and metrics hooks so you can run and iterate locally before introducing any external messaging infrastructure.

POST and consumers
------------------
What the `POST /rates/bulk` does
-------------------------------
When the API receives `POST /rates/bulk` it performs the following steps in a single, short-lived transaction: validate the request payload; create a `Job` row (`JobId`, `CreatedAt`, `Status=Pending`); create one `WorkItem` row per input with serialized `Payload`, `Status=Pending`, and `AttemptCount=0`; optionally persist an `IdempotencyKey` if provided. The transaction commits quickly, making ingestion durable. The endpoint then returns `202 Accepted` with `Location: /jobs/{jobId}` and a small response body containing the job id and initial counts so clients can poll or watch job progress.

Consumer 1 — Processor (claims and processes work items)
--------------------------------------------------------
Consumer 1 is the worker that processes `WorkItem` rows and performs domain writes. Typical behaviour:

- Atomically claim a bounded batch of pending `WorkItem`s (for example with an `UPDATE ... RETURNING` or `SELECT FOR UPDATE SKIP LOCKED`).
- For each claimed item: deserialize `Payload`, perform an idempotent upsert into the `ExchangeRate` table (use a unique key or explicit idempotency columns), and — when required — insert an `Outbox` row inside the same DB transaction so the domain write and outbox insert commit together.
- On success mark the `WorkItem` as `Done` and record `ProcessedAt`. On transient failures increment `AttemptCount` and set `NextAttemptAt` for exponential backoff; after exceeding the retry threshold move the item to `DeadLetter` and capture `LastError`.
- Keep transactions small (per-item or small batches), tune chunk sizes and `MaxDegreeOfParallelism`, and emit metrics (processed, failed, in-progress) for observability.

Consumer 2 — Outbox publisher (publishes events to broker)
---------------------------------------------------------
Consumer 2 scans the `Outbox` table for unpublished events and publishes them to the configured broker. Typical behaviour:

- Claim unpublished `Outbox` rows safely and attempt to publish their payloads to the broker (Service Bus, RabbitMQ, etc.).
- On successful publish mark the outbox row as `Published` with `PublishedAt` and broker `MessageId` when available.
- On transient publish failures apply retry/backoff and record `LastError`; after exhausting retries move rows to a publisher dead-letter store for manual intervention.
- The publisher can batch sends and use parallelism to improve throughput, but ensure downstream idempotency or broker deduplication is in place.

Architecture diagram
--------------------
```mermaid
flowchart LR
	Client[POST /rates/bulk]
	Client -->|Create Job + WorkItems (DB)| AppDB[(App DB)]
	AppDB -->|JobId| ClientResponse[202 Accepted + Location]
	AppDB --> Processor[Consumer 1: Processor]
	Processor -->|Upsert rates (idempotent)| AppDB
	Processor -->|Insert outbox row (optional)| Outbox[(Outbox Table)]
	Outbox --> Publisher[Consumer 2: Outbox Publisher]
	Publisher --> Broker[(Message Broker / Service Bus)]
	Broker --> Downstream[Downstream Consumers]
```

Notes
-----
- Splitting responsibilities clarifies operational concerns: the API handles durability and quick acknowledgement; Consumer 1 focuses on domain writes and item-level retries; Consumer 2 focuses on interacting with external systems and broker-level retries.
- This separation allows independent scaling and clearer observability and lifecycle control: you can pause publishers, rerun failed items, or reprocess dead letters without re-running the initial ingestion.

Next steps
----------
If you want, I will scaffold the DB-first implementation in this repository: schema for `Job`, `WorkItem` and `Outbox` tables, a `POST /rates/bulk` controller method that persists work atomically, and two `BackgroundService` skeletons (processor + outbox publisher) with claim/retry logic and metrics hooks so you can run and iterate locally before introducing any external messaging infrastructure.
# Bulk Ingestion Alternatives

Purpose
-------
This document describes practical approaches for implementing bulk ingestion in the FX rates service, explains the trade-offs for each approach, and gives a recommendation for a safe, testable starting point. The alternatives are written as structured answers rather than checklists so you can read them as decision narratives.

Synchronous POST (small batches only)
------------------------------------
The simplest approach is to keep the existing HTTP POST endpoint and process the incoming batch synchronously inside the request. This works well for small batches that complete quickly: validation, idempotent database upserts, and optional event publishing all occur before returning a response. The main downside is a fragile surface for large or slow work. Long-running batches are vulnerable to client or proxy timeouts, consume request thread-pool resources, and increase the blast radius of any provider or DB slowness. Use this only when batch sizes and processing time are predictably small and when immediate acknowledgement is required.

In-process queue consumed by `IHostedService`
--------------------------------------------
An incremental improvement keeps everything in the same process but makes ingestion asynchronous. The controller enqueues work into an in-memory queue (for example a `Channel<T>`), returns an immediate acknowledgement, and a `BackgroundService` (an `IHostedService`) consumes the queue. This avoids client timeouts and keeps operational complexity low because the worker is part of the same application. The critical limitation is durability: if the process restarts or crashes the queued items are lost. This pattern is suitable for low-risk scenarios or development/testing but is not recommended as the only durability mechanism for production bulk processing.

Database-driven job queue with a worker (DB-first)
-------------------------------------------------
A durable, infra-light pattern is to persist a job and one row per work item in the application database and to return `202 Accepted` from the controller. The worker runs as an `IHostedService` and polls the database for `Pending` items, claims them atomically, processes them in bounded chunks, and updates item state to `Done` or `DeadLetter` with metadata such as `AttemptCount`, `LastError` and `NextAttemptAt`. This approach gives durability, visibility (job status and progress), and simple operational control without requiring additional infrastructure. It also makes it straightforward to implement idempotent upserts and to expose an admin UI or API to requeue or inspect failed items. The trade-offs are that it requires careful claim semantics (atomic updates or DB locks) and tuning for chunk size and concurrency to avoid long transactions and lock contention.

Broker-driven ingestion (Service Bus / RabbitMQ) with consumer
-------------------------------------------------------------
Using an external message broker decouples producers and consumers and lets the platform handle retries, dead-lettering, and scaling. In this model the POST handler publishes messages (either one message per item or a pointer to a blob with the batch payload) to a queue or topic. A Service Bus-triggered consumer (an Azure Function or a dedicated hosted service) receives messages and performs the upserts. This pattern is ideal for high throughput systems because the broker provides durability, visibility into the queue, and built-in DLQ semantics. Consumers must be written to be idempotent because brokers typically deliver at-least-once, and you should design for eventual consistency. If you need transactional guarantees that the database write and the outward event publish happen together, combine the broker with an outbox (described next).

Outbox pattern (DB-first outbox + publisher)
--------------------------------------------
The outbox pattern addresses the problem of making a database write and the publication of a domain event happen atomically. The processor performs the domain upsert and, in the same DB transaction, inserts an outbox row containing the event payload. After the transaction commits, a publisher component reads the outbox table and sends messages to the broker, marking outbox rows as published. This separation prevents message loss when the process crashes after the DB commit but before publishing. The outbox publisher can be the same process or a separate service/function; separating it improves operational isolation because publishing retries and transient broker issues do not block domain processing. The outbox adds implementation complexity but is the right choice when reliable event delivery is required.

Durable / orchestrated approaches (Durable Functions)
----------------------------------------------------
For scenarios requiring complex orchestration (fan-out/fan-in, long-running retries, per-chunk checkpointing), Durable Functions or a comparable orchestrator provide a managed way to express the workflow. An orchestrator can coordinate chunking, parallel processing, and reliable state, and exposes a built-in status endpoint for each orchestration instance. The trade-off is added platform-specific complexity and cost; use orchestration when business logic requires it or when job lifecycle and rich retry semantics are better expressed as stateful workflows.

Scheduler libraries (Hangfire / Quartz)
-------------------------------------
If you prefer to avoid custom worker code and want a DB-backed scheduler with a UI and retry semantics out of the box, libraries such as Hangfire or Quartz are viable. They persist jobs in a database and handle retries, concurrency and a job dashboard. This is a pragmatic option when you want to minimize custom code and gain immediate operational visibility. The trade-offs are dependency on a third-party runtime and less control over custom claim semantics compared to a purpose-built worker.

Comparison and recommendation
-----------------------------
For this repository the best initial choice is the database-driven job queue because it provides durability and visibility without introducing new infra. Implement `POST /rates/bulk` to persist a `Job` row and one `WorkItem` row per input item in a single transaction, then return `202 Accepted` with a job location. Add a `BackgroundService` that claims and processes pending items in configurable chunk sizes, performs idempotent upserts, updates item state, and moves repeatedly failing items to a DeadLetter state after a configured number of attempts. If the system later needs to publish domain events reliably to other systems, add an outbox column/table and a separate publisher service (or Function) that reads outbox rows and publishes them to Service Bus for downstream consumption. If you anticipate cloud-scale or need broker-managed DLQ and sessions, prefer a broker-driven ingestion with Service Bus and an idempotent consumer; combine that with an outbox when you need atomic DB+event semantics.

Operational notes and essential safeguards
----------------------------------------
Implement idempotency at the DB level (unique index on `BaseCurrency` + `QuoteCurrency` or an idempotency key) so retries do not create duplicate data. Claim items atomically so multiple workers can operate concurrently without double-processing; use `SELECT FOR UPDATE SKIP LOCKED` or an atomic UPDATE/RETURNING pattern depending on database capabilities. Use exponential backoff and an `AttemptCount` field for per-item retries and move items to DeadLetter with error details once a retry threshold is exceeded. Expose job and dead-letter visibility through an admin API and emit metrics for queue depth, processing latency, success/failure rates and dead-letter counts. Finally, keep chunk sizes and `MaxDegreeOfParallelism` conservative at first, then tune against real load.

Next steps
----------
If you want, I will scaffold the DB-first implementation in this repository: schema for `Job`, `WorkItem` and `Outbox` tables, a `POST /rates/bulk` controller method that persists work atomically, and two `BackgroundService` skeletons (processor + outbox publisher) with claim/retry logic and metrics hooks so you can run and iterate locally before introducing any external messaging infrastructure.
