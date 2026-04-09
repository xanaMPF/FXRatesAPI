# How to Run

All commands are run from the repository root (`FxRatesApi.Api/`).

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- An AlphaVantage API key (free tier available at https://www.alphavantage.co)

---

## Restore dependencies

```bash
dotnet restore FxRatesApi.Api.sln
```

## Build

```bash
dotnet build FxRatesApi.Api.sln
```

Release mode:

```bash
dotnet build FxRatesApi.Api.sln --configuration Release
```

## Run the API

```bash
dotnet run --project FxRatesApi.Api.csproj
```

The API starts on the ports defined in `Properties/launchSettings.json`.
Swagger UI is available at `/swagger`.

### Set the AlphaVantage API key

The real API key must **never** be committed. Set it via environment variable before running:

```powershell
# PowerShell
$env:ALPHAVANTAGE_API_KEY = "your_real_key_here"
dotnet run --project FxRatesApi.Api.csproj
```

```bash
# bash / zsh
ALPHAVANTAGE_API_KEY=your_real_key_here dotnet run --project FxRatesApi.Api.csproj
```

## Publish (production artefact)

```bash
dotnet publish FxRatesApi.Api.csproj --configuration Release --output ./publish
```

---

## Run unit tests

```bash
dotnet test Tests/FxRatesApi.Api.Tests/FxRatesApi.Api.Tests.csproj
```

With verbose output:

```bash
dotnet test Tests/FxRatesApi.Api.Tests/FxRatesApi.Api.Tests.csproj --verbosity normal
```

---

## Run mutation tests (Stryker.NET)

Install the tool once (globally):

```bash
dotnet tool install --global dotnet-stryker
```

Run from the repository root:

```bash
dotnet-stryker --config-file Tests/FxRatesApi.Api.Tests/stryker-config.json
```

The HTML report is written to `StrykerOutput/<timestamp>/reports/mutation-report.html`.
Open it in a browser to see which mutants survived and where the test gaps are.

---

## EF Core migrations

Apply migrations / create the SQLite database:

```bash
dotnet ef database update --project FxRatesApi.Api.csproj
```

Add a new migration:

```bash
dotnet ef migrations add <MigrationName> --project FxRatesApi.Api.csproj
```
