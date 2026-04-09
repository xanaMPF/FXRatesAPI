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

- **Why not DDD (Domain-Driven Design):** DDD is justified when the domain is deep, complex, and well understood — aggregates, value objects, bounded contexts, and ubiquitous language add significant design overhead. For this project the domain is shallow (an exchange rate is a pair of prices) and there is not enough model logic to compensate for that complexity.

- **Alternative considered — Vertical Slice Architecture:** Rather than splitting by technical layer, each feature would live in its own folder containing everything it needs (handler, request, response, validation). This keeps all FX-rate-related code in one place and reduces cross-layer navigation. It would have been a valid choice, but the layered approach was preferred for its simplicity and familiarity.

---

## Currency validation

Currency codes are validated against a hard-coded in-memory `HashSet<string>` of 16 supported currencies. A `HashSet` was chosen over an `enum` because the ISO 4217 standard defines 180 active codes — maintaining an enum of that size and keeping it in sync with the standard would be impractical. The `HashSet` is fast (O(1) lookups) and easy to extend.

Supported currencies: `USD`, `EUR`, `GBP`, `JPY`, `CHF`, `CAD`, `AUD`, `NZD`, `CNY`, `HKD`, `SGD`, `SEK`, `NOK`, `DKK`, `PLN`, `CZK`

Invalid or unsupported codes are rejected early with HTTP 400, before any database or provider call is made.

---

## TimeProvider abstraction

All services that need the current time inject Microsoft's built-in `TimeProvider` (introduced in .NET 8) instead of calling `DateTime.UtcNow` directly. This makes tests deterministic — `FakeTimeProvider` freezes time to an exact value so timestamp assertions are always exact, regardless of when the test runs.

---

## Provider fallback strategy

`ExchangeRateResolver` implements a fallback chain:

1. If the cached DB rate exists and is fresh (within the configured `StaleAfter` TTL, default 15 minutes) → return it immediately, no provider call
2. If the rate is missing or stale → call AlphaVantage
3. If AlphaVantage succeeds → persist and return the new rate
4. If AlphaVantage fails and a stale rate exists in DB → return the stale rate rather than erroring
5. If AlphaVantage fails and there is no DB rate at all → throw `KeyNotFoundException` → 404

This design follows the **Open/Closed Principle**: adding a new provider (e.g. a second fallback source) only requires registering a new `IExchangeRateProvider` implementation — the resolver's core logic does not change. The chain stays clean regardless of how many providers are added.

---

## Error handling middleware

A single `ExceptionHandlingMiddleware` centralises all exception-to-HTTP-status-code mapping. Having it in one place means:

- The mapping is consistent across every endpoint
- Controllers and services throw semantically meaningful exceptions without knowing anything about HTTP
- It can be extended for better observability (structured logging, metrics, correlation IDs) without touching any service

> **Note:** The error message returned when an external service fails currently surfaces internal detail to the caller. A production service should return a generic user-facing message instead (e.g. "An upstream service is temporarily unavailable.").

---

## Custom exceptions

The project defines specific exception types (`ResourceConflictException`, `ExternalServiceException`) rather than using the base `Exception`. This makes log filtering straightforward — searching for `ExternalServiceException` in logs immediately isolates all third-party failures — and keeps the middleware mapping explicit and readable.

---

## AlphaVantage integration

The `AlphaVantageService` fetches rates from the AlphaVantage `CURRENCY_EXCHANGE_RATE` endpoint. Key design choices:

- The HTTP client is registered via `IHttpClientFactory` to avoid socket exhaustion from creating new `HttpClient` instances per request.
- `AlphaVantageExchangeRateMapper` converts the raw JSON response to the domain `ExchangeRate` model and is unit-tested independently using fixed JSON payloads.
- The service is tested with a fake `HttpMessageHandler` so tests never make real network calls.
- The API key is read from configuration and must be supplied via an environment variable — it is never committed to source control.

> **TODO:** In production, the API key should be stored in **Azure Key Vault** (and injected at runtime via managed identity) or supplied as a **CI/CD pipeline secret variable**. It must never live in `appsettings.json`.

---

## Mutation testing

Stryker.NET checks whether the unit tests are actually meaningful. It introduces small changes to the production code (e.g. flipping `>` to `>=`, negating a condition, replacing a return value) and verifies that at least one test fails as a result. If no test catches the change, the mutant "survives" — a signal that the test suite has a gap there.

The current score is not 100%, but running Stryker helped identify and close several gaps iteratively. Configuration lives in `Tests/FxRatesApi.Api.Tests/stryker-config.json`.

> **TODO:** Add unit tests for the remaining gaps:
> - `ExchangeRatesController` — no controller-level tests exist
> - `InMemoryRateEventQueue` — background processing logic is not tested
> - Remaining branches in `ExchangeRateResolver` (persistence toggle, provider chain fallthrough)

---

## CI/CD pipeline

The GitHub Actions workflow (`.github/workflows/ci-cd.yml`) runs on every push and pull request:

1. **build-and-test** — restores, builds, and runs all unit tests.
2. **stryker** — runs mutation testing (informational, does not gate the build unless `break` threshold is set).
3. **deploy** — placeholder step gated on `build-and-test` passing via `needs:`.

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

Beyond serving as documentation, an OpenAPI contract enables a **contract-first** approach: the contract can be written and shared with other teams or clients before development even starts, allowing them to generate typed client SDKs, set up mock servers, or write integration tests against the agreed interface while the API is still being built. This decouples delivery timelines between teams and removes the need for a live server during early integration.

Other consumers can import this contract directly into **Postman**, **SwaggerHub**, or generate a typed client via [NSwag](https://github.com/RicoSuter/NSwag) or [Kiota](https://learn.microsoft.com/en-us/openapi/kiota/).

Key behaviours described in the contract:

- `POST /rates` has upsert semantics — if the pair already exists it is updated; if not, it is created (returns 201 vs 200 accordingly)
- `GET /rates/{from}/{to}` may return a stale rate from the database when the provider is unavailable (fallback behaviour is documented)
- All error responses follow the `{ "message": "..." }` shape

---

## Persistence

SQLite is used via EF Core for simplicity. For production, swap the provider to PostgreSQL or SQL Server by changing the `UseXxx` call in `Program.cs` and the connection string. Unit tests use EF Core's in-memory provider (`UseInMemoryDatabase`) so no database file is required.

No history of exchange rates is stored — only the latest value per pair is kept. This was a deliberate choice given the scope of the problem. Storing history would add value for trend analysis and debugging but was out of scope here.

---

## Next steps / improvements

- **Authentication:** No authentication is implemented. All endpoints are publicly accessible. JWT bearer or API-key auth would be required before any production exposure.
- **API key storage:** Move the AlphaVantage key to Azure Key Vault or a CI/CD pipeline secret. Never store secrets in `appsettings.json`.
- **CorrelationId:** A correlation ID is missing from request logs. A middleware that generates or propagates an `X-Correlation-Id` header and attaches it to every log entry would make it possible to trace a single request end-to-end across distributed logs.
- **Currency coverage:** The 16-currency subset is a simplification. Options to improve: load the full ISO 4217 list from a local file, generate it from the official SIX Group XML during build ([SIX Group ISO 4217 List One](https://www.six-group.com/dam/download/financial-information/data-center/iso-currrency/lists/list-one.xml)), or store supported currencies in a reference-data table managed without code changes.
- **Rate history:** Keeping a history table would enable trend analysis and make it easier to debug unexpected rate changes.
- **Better error messages for provider failures:** The raw exception message from AlphaVantage is currently surfaced to the caller. A generic user-facing message should be returned instead.
- **Unit test coverage:** Add tests for `ExchangeRatesController`, `InMemoryRateEventQueue`, and remaining resolver branches to increase the mutation score.

