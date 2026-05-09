# How to Run

All commands are run from the repository root (`FxRatesApi.Api/`).

## Quick start

1. Open `appsettings.json` and replace the placeholder with your AlphaVantage API key:
   ```json
   "AlphaVantage": {
     "ApiKey": "your_real_key_here"
   }
   ```
   > **Never commit a real key.** For shared or production environments use an environment variable instead (see below).

2. Run the API:
   ```bash
   dotnet run --project FxRatesApi.Api.csproj
   ```

3. Open Swagger UI in your browser:
   ```
   http://localhost:5126/swagger
   ```

---

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

The simplest approach for local development is to put the key directly in `appsettings.json`:

```json
"AlphaVantage": {
  "ApiKey": "your_real_key_here"
}
```

> **Never commit a real key to source control.** For CI/CD or shared environments, set it via an environment variable instead:

```powershell
# PowerShell
$env:ALPHAVANTAGE_API_KEY = "your_real_key_here"
dotnet run --project FxRatesApi.Api.csproj
```

```bash
# bash / zsh
ALPHAVANTAGE_API_KEY=your_real_key_here dotnet run --project FxRatesApi.Api.csproj
```

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
