# Technical Considerations

## Architecture

The project follows a clean layered architecture:

| Layer | Folder | Responsibility |
|---|---|---|
| Domain | `Domain/` | Core models, constants, exceptions, utilities - no external dependencies |
| Infrastructure | `Infrastructure/` | EF Core persistence, AlphaVantage HTTP client, in-memory event queue |
| Application | `Application/` | Business logic (services, resolver), DTOs, domain events |
| Presentation | `Presentation/` | ASP.NET Core controllers, exception-handling middleware |

Dependencies flow inward: Presentation, Application, Domain. Infrastructure implements interfaces defined in Application/Domain.

- **Why not DDD (Domain-Driven Design):** DDD is justified when the domain is deep, complex, and well understood. Aggregates, value objects, bounded contexts, and ubiquitous language add significant design overhead. For this project the domain is shallow (an exchange rate is a pair of prices) and there is not enough model logic to compensate for that complexity.

- **Alternative considered - Vertical Slice Architecture:** Rather than splitting by technical layer, each feature would live in its own folder containing everything it needs (handler, request, response, validation). This keeps all FX-rate-related code in one place and reduces cross-layer navigation. It would have been a valid choice, but the layered approach was preferred for its simplicity and familiarity.

---

## Currency validation

Currency codes are validated against a hard-coded in-memory `HashSet<string>` of 16 supported currencies. A `HashSet` was chosen over an `enum` because the ISO 4217 standard defines 180 active codes - maintaining an enum of that size and keeping it in sync with the standard would be impractical. The `HashSet` is fast (O(1) lookups) and easy to extend.

Supported currencies: `USD`, `EUR`, `GBP`, `JPY`, `CHF`, `CAD`, `AUD`, `NZD`, `CNY`, `HKD`, `SGD`, `SEK`, `NOK`, `DKK`, `PLN`, `CZK`

Invalid or unsupported codes are rejected early with HTTP 400, before any database or provider call is made.

> **TODO (production approach):** The hard-coded set is a simplification. In a production environment, supported currencies should be dynamic. A dedicated endpoint or an external configuration service would publish the list of valid codes, and the application would load them into a distributed cache such as **Redis** or a local **IMemoryCache** on startup (and refresh periodically). Validation would then query the cache rather than a hard-coded set, allowing operations to add or remove supported currencies without a code deployment.

---

## TimeProvider abstraction

All services that need the current time inject Microsoft's built-in `TimeProvider` (introduced in .NET 8) instead of calling `DateTime.UtcNow` directly. This makes tests deterministic - `FakeTimeProvider` freezes time to an exact value so timestamp assertions are always exact, regardless of when the test runs.

---

## Provider fallback strategy

`ExchangeRateResolver` implements a fallback chain:

1. If the cached DB rate exists and is fresh (within the configured TTL) - return it immediately, no provider call
2. If the rate is missing or stale - call AlphaVantage
3. If AlphaVantage succeeds - persist and return the new rate
4. If AlphaVantage fails and a stale rate exists in DB - return the stale rate rather than erroring
5. If AlphaVantage fails and there is no DB rate at all - throw `KeyNotFoundException`, which the middleware maps to 404

This design follows the **Open/Closed Principle**: adding a new provider (e.g. a second fallback source) only requires registering a new `IExchangeRateProvider` implementation. The resolver's core logic does not change, and the chain stays clean regardless of how many providers are added.

---

## Configurable lookup behaviour (appsettings)

The lookup behaviour is controlled by three settings under `ExchangeRateLookup` in `appsettings.json`:

| Setting | Default | Description |
|---|---|---|
| `UseDatabaseCache` | `true` | When true, the resolver checks the database before calling a provider. Set to false to always fetch live rates. |
| `PersistFetchedRates` | `true` | When true, rates fetched from a provider are saved to the database for future cache hits. Set to false for read-only use. |
| `StaleAfter` | `00:15:00` (15 min) | How long a cached rate is considered fresh. After this TTL expires, the resolver will attempt a provider refresh. |

These settings live in `appsettings.json` (and can be overridden in `appsettings.Development.json` or via environment variables) because they are operational knobs that should be adjustable per environment without a code change or redeployment. For example, a production environment might use a shorter TTL for more up-to-date rates, while a test environment might disable persistence entirely.

---

## Error handling middleware

A single `ExceptionHandlingMiddleware` centralises all exception-to-HTTP-status-code mapping. Having it in one place means:

- The mapping is consistent across every endpoint
- Controllers and services throw semantically meaningful exceptions without knowing anything about HTTP
- It can be extended for better observability (structured logging, metrics, correlation IDs) without touching any service

> **Note:** The error message returned when an external service fails currently surfaces internal detail to the caller. A production service should return a generic user-facing message instead (e.g. "An upstream service is temporarily unavailable.").

---

## Custom exceptions

The project defines specific exception types (`ResourceConflictException`, `ExternalServiceException`) rather than using the base `Exception`. This makes log filtering straightforward - searching for `ExternalServiceException` in logs immediately isolates all third-party failures - and keeps the middleware mapping explicit and readable.

---

## AlphaVantage integration

The `AlphaVantageService` fetches rates from the AlphaVantage `CURRENCY_EXCHANGE_RATE` endpoint. Key design choices:

- The HTTP client is registered via `IHttpClientFactory` to avoid socket exhaustion from creating new `HttpClient` instances per request.
- `AlphaVantageExchangeRateMapper` converts the raw JSON response to the domain `ExchangeRate` model and is unit-tested independently using fixed JSON payloads.
- The service is tested with a fake `HttpMessageHandler` so tests never make real network calls.
- The API key is read from configuration and must be supplied via an environment variable - it is never committed to source control.

> **TODO:** In production, the API key should be stored in **Azure Key Vault** (and injected at runtime via managed identity) or supplied as a **CI/CD pipeline secret variable**. It must never live in `appsettings.json`.

---

## Mutation testing

Stryker.NET checks whether the unit tests are actually meaningful. It introduces small changes to the production code (e.g. flipping `>` to `>=`, negating a condition, replacing a return value) and verifies that at least one test fails as a result. If no test catches the change, the mutant "survives" - a signal that the test suite has a gap there.

The current score is not 100%, but running Stryker helped identify and close several gaps iteratively. Configuration lives in `Tests/FxRatesApi.Api.Tests/stryker-config.json`.

> **TODO:** Add unit tests for the remaining gaps:
> - `ExchangeRatesController` - no controller-level tests exist
> - `InMemoryRateEventQueue` - background processing logic is not tested
> - Remaining branches in `ExchangeRateResolver` (persistence toggle, provider chain fallthrough)

---

## CI/CD pipeline

The GitHub Actions workflow (`.github/workflows/ci-cd.yml`) runs on every push and pull request:

1. **build-and-test** - restores, builds, and runs all unit tests.
2. **stryker** - runs mutation testing (informational, does not gate the build unless `break` threshold is set).
3. **deploy** - placeholder step gated on `build-and-test` passing via `needs:`.

A real deployment to a cloud environment was not set up due to infrastructure constraints. The deploy job uses a dummy step to represent the deployment as recommended by the practical test brief:

```yaml
deploy:
  needs: build-and-test
  runs-on: ubuntu-latest
  steps:
    - name: Simulate deployment
      run: echo "Deploying application... (dummy step)"
```

---

## API contract

A full OpenAPI specification is available at [`docs/openapi.yml`](openapi.yml). It documents all endpoints, request/response shapes, and possible status codes.

**Contract-first vs code-first:**

The contract in this repository was written alongside the implementation (code-first). However, the same file supports a **contract-first** approach: the YAML is written and agreed on before any development starts, then shared with other teams or clients so they can generate typed SDKs, set up mock servers, or write integration tests against the agreed interface while the API is still being built. This decouples delivery timelines between teams entirely.

If a code-first approach is maintained, it is important to automate contract generation so the documentation never drifts from the actual implementation. Tools like NSwag or Swashbuckle can generate the OpenAPI file on every build, and the CI pipeline can publish or diff it automatically whenever an endpoint changes.

Regardless of which approach is chosen, sharing the contract openly - with other internal teams or with external clients who want to call the API directly - provides transparency about the full CRUD surface and removes the need for anyone to read the source code to understand what the API does.

**What each endpoint does:**

| Method | Path | Behaviour |
|---|---|---|
| `GET` | `/rates` | Returns all stored currency pairs. |
| `GET` | `/rates/{from}/{to}` | Returns the rate for a specific pair, fetching from the provider if needed (see fallback strategy). |
| `POST` | `/rates` | **Upsert** - creates the pair if it does not exist (201), or updates it if it already does (200). This makes it safe to call repeatedly without creating duplicates, which is important when ingesting rates from a feed. |
| `PUT` | `/rates/{from}/{to}` | Updates bid, ask, and provider of an existing pair. Returns 404 if the pair does not exist. |
| `DELETE` | `/rates/{from}/{to}` | Removes a currency pair. Returns 204 on success, 404 if not found. |

---

## Persistence

SQLite is used via EF Core for simplicity. For production, swap the provider to PostgreSQL or SQL Server by changing the `UseXxx` call in `Program.cs` and the connection string. Unit tests use EF Core's in-memory provider (`UseInMemoryDatabase`) so no database file is required.

No history of exchange rates is stored - only the latest value per pair is kept. This was a deliberate choice given the scope of the problem. Storing history would add value for trend analysis and debugging but was out of scope here.

---

## Next steps / improvements

- **Authentication:** No authentication is implemented. All endpoints are publicly accessible. JWT bearer or API-key auth would be required before any production exposure.
- **API key storage:** Move the AlphaVantage key to Azure Key Vault or a CI/CD pipeline secret. Never store secrets in `appsettings.json`.
- **CorrelationId:** A correlation ID is missing from request logs. A middleware that generates or propagates an `X-Correlation-Id` header and attaches it to every log entry would make it possible to trace a single request end-to-end across distributed logs.
- **Currency coverage:** The 16-currency subset is a simplification. In production, supported currencies should be loaded dynamically from a cache (Redis or IMemoryCache) fed by a configuration endpoint or the ISO 4217 list ([SIX Group ISO 4217 List One](https://www.six-group.com/dam/download/financial-information/data-center/iso-currrency/lists/list-one.xml)).
- **Bulk upsert endpoint:** Submitting rates one pair at a time is impractical when consuming a source feed that delivers hundreds of pairs at once. A `POST /rates/bulk` endpoint accepting an array of rates would be far more efficient. Like the current `POST /rates`, it should have upsert semantics to remain idempotent and avoid duplicate data.
- **Async processing with Azure Functions:** The current synchronous approach works but ties up the request thread while waiting for AlphaVantage. For bulk ingestion or high-frequency rate updates, an Azure Function triggered by a message queue would be a better fit - it processes work asynchronously, scales independently, and keeps the web API responsive. The upsert logic would need to remain idempotent so repeated messages from the queue do not create duplicate records.
- **Rate history:** Keeping a history table would enable trend analysis and make it easier to debug unexpected rate changes.
- **Better error messages for provider failures:** The raw exception message from AlphaVantage is currently surfaced to the caller. A generic user-facing message should be returned instead.
- **Unit test coverage:** Add tests for `ExchangeRatesController`, `InMemoryRateEventQueue`, and remaining resolver branches to increase the mutation score.
- **Acceptance / end-to-end testing with Reqnroll:** The acceptance criteria written on each GitHub issue (e.g. "GET /rates returns a fresh rate from the DB when not stale") can be translated directly into [Reqnroll](https://reqnroll.net/) (the .NET successor to SpecFlow) feature files using Gherkin syntax. This closes the loop between the board and the test suite: a ticket's acceptance criteria becomes an executable test. These tests would run against a real or containerised instance of the API, verifying the full stack end-to-end. They could be triggered on demand during development or automatically as a gate before publishing to a new environment (e.g. before promoting from staging to production). Because the scenarios are written in plain language, other teams can read and run them without understanding the source code, giving everyone a shared, always-up-to-date view of whether the CRUD is functioning correctly.

