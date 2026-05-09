# AlphaVantageService — Interview Questions

Answer each question with a short explanation and any trade-offs you considered.

- How should `HttpClient` and handlers be configured to avoid socket exhaustion and stale DNS entries?
- What retry/backoff and rate-limit strategy would you implement for AlphaVantage's quotas?
- How would you validate and map provider responses to the `ExchangeRate` model and handle unexpected payloads?
- Where should the API key and provider configuration live, and how should rotation be handled securely?
- How would you write unit tests for parsing and error handling without making network calls?
- When should provider errors result in `ExternalServiceException` versus returning `null` to indicate "no rate"?
- Which primary `SocketsHttpHandler` settings would you tune (e.g., `MaxConnectionsPerServer`, `PooledConnectionLifetime`) and why?
- How would you instrument provider-specific metrics and alerts (latency, failure rate, quota exhaustion)?
- Would you add a provider-level cache to reduce calls, and if so, how would invalidation work?
- How should response streams and content be disposed to ensure connections are returned promptly?
