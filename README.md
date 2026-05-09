# FxRatesApi.Api

ASP.NET Core 8 Web API for querying and managing foreign-exchange rates, backed by SQLite and the AlphaVantage provider.

---

## Submission Checklist

### Part 1 — .NET Core Web API
This repository contains the implementation. Source code: **https://github.com/xanaMPF/FXRatesAPI**

### Part 2 — Automated CI/CD Pipeline
GitHub Actions pipeline is configured in `.github/workflows/ci-cd.yml`. It builds, runs unit tests, runs mutation tests (Stryker), and runs a dummy deploy step on every push to `main`.

Pipeline runs: **https://github.com/xanaMPF/FXRatesAPI/actions**

### Part 3 — Agile Methodologies
User stories broken down into tasks with priorities and estimates on the GitHub Projects board: **https://github.com/users/xanaMPF/projects/3**

---

## Other Submission Requirements

| Requirement | Status |
|---|---|
| Code repository | https://github.com/xanaMPF/FXRatesAPI |
| Real deployment URL | Not deployed — dummy deploy step used in CI/CD pipeline |
| Clean, well-structured code | Developed following Clean Code, SOLID principles and design patterns (see [Technical Considerations](docs/TECHNICAL_CONSIDERATIONS.md)) |
| How to set up and run | [docs/HOW_TO_RUN.md](docs/HOW_TO_RUN.md) |
| Error handling and logging | Global exception handling middleware with structured logging (see [Technical Considerations](docs/TECHNICAL_CONSIDERATIONS.md)) |
| Unit tests | xUnit test suite with 63 tests, including mutation testing via Stryker.NET |
| Self-review / design decisions | [docs/TECHNICAL_CONSIDERATIONS.md](docs/TECHNICAL_CONSIDERATIONS.md) |

---

## Documentation

- [How to run](docs/HOW_TO_RUN.md) — build, run, test, and mutation testing commands
- [Technical considerations](docs/TECHNICAL_CONSIDERATIONS.md) — architecture, design decisions, limitations, and possible improvements
- [OpenAPI spec](docs/openapi.yml) — full API contract