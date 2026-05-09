# ExchangeRateResolver — Interview Questions

Answer each question with a short explanation and any trade-offs you considered.

- How does the fallback chain decide provider order and what trade-offs exist if we change it?
- When a provider fails but a stale DB rate exists, how should we choose between returning stale data or surfacing an error?
- How would you make provider selection and priority configurable at runtime?
- How should concurrent refreshes/upserts for the same pair be handled to avoid lost updates?
- How would you unit- and integration-test the resolver's different branches (cache hit, provider success, provider failure with/without DB fallback)?
- What metrics and logs would you add to monitor provider performance and fallback frequency?
- What service lifetime should `ExchangeRateResolver` have (scoped/singleton/transient) and why?
- How would you implement per-provider throttling or concurrency limits to protect external quotas?
- How would you validate and expose `ExchangeRateLookupOptions` across environments (TTL, persist flag) and guard against bad values?
- When persisting fetched rates, should the resolver implement retries or circuit-breaker logic, and how would you design it?
