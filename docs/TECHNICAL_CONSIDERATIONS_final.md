# Technical Considerations

## Architecture

The project follows a clean layered architecture:

| Layer | Folder | Responsibility |
|---|---|---|
| Domain | `Domain/` | Core models, constants, exceptions, utilities — no external dependencies |
| Infrastructure | `Infrastructure/` | EF Core persistence, AlphaVantage HTTP client, in-memory event queue |
| Application | `Application/` | Business logic (services, resolver), DTOs, domain events |
| Presentation | `Presentation/` | ASP.NET Core controllers, exception-handling middleware |

Dependencies flow inward: Presentation → Application → Domain. Infrastructure implements interfaces defined in Application/Domain.

---

## Currency validation

### Why it exists

Invalid currency inputs fail fast with HTTP 400 so we do not waste provider calls, database work, or log noise on obviously bad requests. Validation happens at the boundary (controller/service) before any I/O.

### Current scope

The project validates against a hard-coded in-memory subset of 16 currencies:

`USD`, `EUR`, `GBP`, `JPY`, `CHF`, `CAD`, `AUD`, `NZD`, `CNY`, `HKD`, `SGD`, `SEK`, `NOK`, `DKK`, `PLN`, `CZK`

The lookup uses a `HashSet<string>` so membership checks are O(1).

### Broader coverage

The official ISO 4217 maintenance data currently publishes 180 active codes in List One (as of 2026-01-01). Source:

- SIX Group ISO 4217 List One XML: https://www.six-group.com/dam/download/financial-information/data-center/iso-currrency/lists/list-one.xml

The current subset keeps the implementation simple while still blocking unsupported codes early. Possible follow-up options:

- Load the full ISO 4217 list from a maintained local data file checked into the repo.
- Generate the allowed-code set from the official SIX Group list during build or release.
- Sync the official list on a schedule and cache it locally.
- Store supported currencies in a reference-data table so operations can manage them without code changes.

---

## TimeProvider abstraction

All services that need the current time (`ExchangeRateService`, `ExchangeRateResolver`, `AlphaVantageService`, `AlphaVantageExchangeRateMapper`) inject Microsoft's built-in `TimeProvider` (introduced in .NET 8) instead of calling `DateTime.UtcNow` directly.

**Why:**
- Calling `DateTime.UtcNow` directly makes tests non-deterministic — the "current time" is different on every run.
- `TimeProvider` is an abstraction provided by the framework itself with no third-party dependency.
- In production, `TimeProvider.System` is registered in DI and behaves identically to `DateTime.UtcNow`.
- In tests, `Microsoft.Extensions.TimeProvider.Testing.FakeTimeProvider` allows freezing time to an exact value, making assertions on timestamps exact and repeatable.

---

## AlphaVantage integration

The `AlphaVantageService` fetches rates from the AlphaVantage `CURRENCY_EXCHANGE_RATE` endpoint. Key design choices:

- The HTTP client is registered via `IHttpClientFactory` to avoid socket exhaustion from creating new `HttpClient` instances.
- The real API key is read from configuration (`AlphaVantageOptions.ApiKey`) and must be supplied via an environment variable — it is never committed to source control.
- `AlphaVantageExchangeRateMapper` converts the raw JSON response to the domain `ExchangeRate` model and is independently unit-tested using fixed JSON payloads.
- The service is tested with a fake `HttpMessageHandler` that returns pre-canned responses, so tests never make real network calls.

---

## Mutation testing

[Stryker.NET](https://stryker-mutator.io/docs/stryker-net/introduction/) is used to measure the quality of the test suite by introducing small code mutations (e.g., changing `>` to `>=`, negating conditions, replacing return values) and checking whether the tests catch each change.

Configuration lives in `Tests/FxRatesApi.Api.Tests/stryker-config.json`.

Current thresholds:

| Threshold | Value |
|---|---|
| High | 80 |
| Low | 60 |
| Break (CI gate) | 0 |

The break threshold is currently 0 (no hard gate) to allow incremental improvement. Raise `break` once the score consistently exceeds the low threshold.

---

## CI/CD pipeline

The GitHub Actions workflow (`.github/workflows/ci-cd.yml`) runs on every push and pull request:

1. **build-and-test** — restores, builds, and runs all unit tests.
2. **stryker** — runs mutation testing (informational, does not gate the build unless `break` threshold is set).
3. **deploy** — placeholder step gated on `build-and-test` passing via `needs:`.

---

## Persistence

SQLite is used via EF Core for simplicity during development. The `AppDbContext` is registered with `AddDbContext` and the connection string is read from `appsettings.json`. For production, swap the provider to PostgreSQL or SQL Server by changing the `UseXxx` call in `Program.cs` and the connection string.

Unit tests use EF Core's in-memory provider (`UseInMemoryDatabase`) so no database file is required when running tests.
