# InMemoryRateEventQueue — Interview Questions

Answer each question with a short explanation and any trade-offs you considered.

- How would you guarantee reliable delivery and retry semantics for events processed by an in-memory queue?
- What monitoring and visibility would you add to observe queue length, processing rate, and failures?
- How would you detect and handle poison messages to avoid infinite retry loops?
- How would you scale the queue beyond single-process memory (durable queue options)?
- How should the background worker handle graceful shutdown and cancellation to avoid lost events?
- How would you write deterministic tests for the background processing logic?
- Should event publishing be transactional with DB updates, and if so, how would you achieve atomicity?
- How would you enforce ordering guarantees for events related to the same currency pair?
- What batching or back-pressure mechanisms would you implement under high load?
- When durability is required, what persistent queue/backing store would you choose and why?
