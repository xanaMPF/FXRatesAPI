# AppDbContext — Interview Questions

Answer each question with a short explanation and any trade-offs you considered.

- How would you configure concurrency tokens (`RowVersion`) and decide between optimistic and pessimistic locking?
- What precision/column types should `Bid` and `Ask` use and how do you enforce them in EF Core?
- Which indexes are important for query performance (e.g., `BaseCurrency` + `QuoteCurrency`, `RetrievedAtUtc`)?
- How would you handle migrations and provider-specific differences when switching from SQLite to Postgres/SQL Server?
- How should test environments be configured (in-memory DB vs containerized DB) for reliable tests?
- What EF Core logging and telemetry settings would you enable in development but disable in production?
- How would you evolve the schema if you later add rate history while minimising migration impact?
- What connection resiliency and retry settings should be configured for production databases?
- How should sensitive connection strings and secrets be stored and rotated securely?
- When would you use a repository abstraction versus using `AppDbContext` directly in services?
