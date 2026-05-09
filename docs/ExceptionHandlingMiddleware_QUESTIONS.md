# ExceptionHandlingMiddleware — Interview Questions

Answer each question with a short explanation and any trade-offs you considered.

- How should the middleware map domain exceptions to HTTP status codes and `ProblemDetails` without leaking internal details?
- How would you handle exceptions that occur after the response has already started (streaming endpoints)?
- How would you ensure exceptions are logged exactly once and correlated with a request ID?
- When would you prefer MVC exception filters or controller-level handling over global middleware?
- How would you test middleware behavior for both expected and unexpected exception types?
- What metrics and alerts should be exposed for error rates and types?
- How should behavior differ between Development and Production environments (e.g., stack traces)?
- How do you ensure consistency between middleware mappings and controller-level error handling?
- For gRPC or WebSocket endpoints, how would error handling differ from HTTP middleware?
- How would you design the middleware to allow easy extension for new exception types or custom handlers?
